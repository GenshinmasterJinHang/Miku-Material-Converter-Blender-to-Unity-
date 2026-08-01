"""Closure-domain analysis and target-neutral surface-model planning."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum
from typing import Any, Mapping, Sequence

from .closure_ir import (
    AddShaderEnergyPolicy,
    ClosureBudget,
    ClosureDomain,
    ClosureKind,
    FidelityPolicy,
    expression_is_dynamic,
)
from .contracts import canonical_hash, stable_uuid


class SurfaceModelKind(str, Enum):
    OPAQUE_PBR = "OpaquePBR"
    CUTOUT_PBR = "CutoutPBR"
    TRANSPARENT_LIT = "TransparentLit"
    TRANSPARENT_EMISSION = "TransparentEmission"
    REFRACTIVE_GLASS = "RefractiveGlass"
    CUSTOM_MULTI_LOBE = "CustomMultiLobe"
    UNSUPPORTED_SURFACE = "UnsupportedSurface"


def _terms(weighted_set: Mapping[str, Any]) -> list[Mapping[str, Any]]:
    return [
        item
        for item in weighted_set.get("terms", []) or []
        if isinstance(item, Mapping)
    ]


def _parameter(
    term: Mapping[str, Any],
    *names: str,
) -> Mapping[str, Any] | None:
    normalized = {
        "".join(ch for ch in name.lower() if ch.isalnum())
        for name in names
    }
    for key, value in (term.get("parameters") or {}).items():
        key_normalized = "".join(
            ch for ch in str(key).lower() if ch.isalnum()
        )
        if key_normalized in normalized and isinstance(value, Mapping):
            return value
    return None


def _constant_parameter_value(
    term: Mapping[str, Any],
    *names: str,
) -> Any:
    value = _parameter(term, *names)
    if value is None or value.get("kind") != "Constant":
        return None
    return value.get("value")


@dataclass(frozen=True)
class SurfaceFeatureSet:
    lobe_count: int
    specular_lobe_count: int
    transmission_lobe_count: int
    refraction_lobe_count: int
    emission_term_count: int
    transparent_term_count: int
    distinct_normal_count: int
    dynamic_weight_count: int
    has_alpha_clip: bool
    has_colored_transmittance: bool
    has_view_dependent_weights: bool
    has_volume: bool
    has_holdout: bool
    has_shader_to_rgb_barrier: bool
    is_non_energy_conserving: bool

    def to_document(self) -> dict[str, Any]:
        return {
            "lobeCount": self.lobe_count,
            "specularLobeCount": self.specular_lobe_count,
            "transmissionLobeCount": self.transmission_lobe_count,
            "refractionLobeCount": self.refraction_lobe_count,
            "emissionTermCount": self.emission_term_count,
            "transparentTermCount": self.transparent_term_count,
            "distinctNormalCount": self.distinct_normal_count,
            "dynamicWeightCount": self.dynamic_weight_count,
            "hasAlphaClip": self.has_alpha_clip,
            "hasColoredTransmittance": self.has_colored_transmittance,
            "hasViewDependentWeights": self.has_view_dependent_weights,
            "hasVolume": self.has_volume,
            "hasHoldout": self.has_holdout,
            "hasShaderToRgbBarrier": self.has_shader_to_rgb_barrier,
            "isNonEnergyConserving": self.is_non_energy_conserving,
        }


class SurfaceFeatureAnalyzer:
    def analyze(
        self,
        weighted_set: Mapping[str, Any],
        closure_graph: Mapping[str, Any],
    ) -> SurfaceFeatureSet:
        terms = _terms(weighted_set)
        scattering = [
            term
            for term in terms
            if term.get("domain")
            in {
                ClosureDomain.SURFACE_SCATTERING.value,
                ClosureDomain.SURFACE_TRANSMISSION.value,
            }
        ]
        specular = [
            term
            for term in scattering
            if term.get("closureKind")
            in {
                ClosureKind.GLOSSY.value,
                ClosureKind.METALLIC.value,
                ClosureKind.PRINCIPLED.value,
                ClosureKind.SHEEN.value,
            }
        ]
        transmission = [
            term
            for term in terms
            if term.get("domain")
            == ClosureDomain.SURFACE_TRANSMISSION.value
        ]
        refraction = [
            term
            for term in terms
            if term.get("domain") == ClosureDomain.REFRACTION.value
        ]
        emissions = [
            term
            for term in terms
            if term.get("domain") == ClosureDomain.EMISSION.value
        ]
        transparent = [
            term
            for term in terms
            if term.get("domain")
            == ClosureDomain.TRANSPARENT_PASS_THROUGH.value
        ]
        normal_keys = {
            _surface_normal_identity(_parameter(term, "Normal"))
            for term in [*scattering, *refraction]
        }
        dynamic_weights = [
            term
            for term in terms
            if isinstance(term.get("finalWeight"), Mapping)
            and expression_is_dynamic(term["finalWeight"])
        ]
        has_colored_transmittance = any(
            _is_colored(
                _constant_parameter_value(term, "Color", "Transmission Color")
            )
            for term in transparent
        )
        return SurfaceFeatureSet(
            lobe_count=len(scattering) + len(refraction),
            specular_lobe_count=len(specular),
            transmission_lobe_count=len(transmission),
            refraction_lobe_count=len(refraction),
            emission_term_count=len(emissions),
            transparent_term_count=len(transparent),
            distinct_normal_count=len(normal_keys),
            dynamic_weight_count=len(dynamic_weights),
            has_alpha_clip=False,
            has_colored_transmittance=has_colored_transmittance,
            has_view_dependent_weights=bool(dynamic_weights),
            has_volume=any(
                term.get("domain") == ClosureDomain.VOLUME.value
                for term in terms
            ),
            has_holdout=any(
                term.get("domain") == ClosureDomain.HOLDOUT.value
                for term in terms
            ),
            has_shader_to_rgb_barrier=any(
                term.get("closureKind")
                == ClosureKind.SHADER_TO_RGB_BARRIER.value
                for term in terms
            ),
            is_non_energy_conserving=_contains_add(closure_graph.get("root")),
        )


class StandardLitCompatibilityAnalyzer:
    """Prove the narrow fast path instead of assuming PBR compatibility."""

    def analyze(
        self,
        weighted_set: Mapping[str, Any],
        features: SurfaceFeatureSet,
    ) -> dict[str, Any]:
        terms = _terms(weighted_set)
        surface_terms = [
            term
            for term in terms
            if term.get("domain") == ClosureDomain.SURFACE_SCATTERING.value
        ]
        reasons: list[str] = []
        if len(surface_terms) != 1:
            reasons.append("requires-exactly-one-surface-scattering-term")
        if features.transparent_term_count:
            reasons.append("contains-transparent-pass-through")
        if features.refraction_lobe_count or features.transmission_lobe_count:
            reasons.append("contains-transmission-or-refraction")
        if features.is_non_energy_conserving:
            reasons.append("contains-add-shader")
        if surface_terms:
            term = surface_terms[0]
            if term.get("closureKind") != ClosureKind.PRINCIPLED.value:
                reasons.append("surface-term-is-not-principled")
            if _constant_weight(term) != 1.0:
                reasons.append("principled-root-weight-is-not-one")
            unsupported_parameters = [
                name
                for name in (
                    "Sheen Weight",
                    "Subsurface Weight",
                    "Anisotropic",
                    "Transmission Weight",
                )
                if _parameter_is_nonzero_or_dynamic(term, name)
            ]
            if unsupported_parameters:
                reasons.append(
                    "unsupported-principled-features:"
                    + ",".join(unsupported_parameters)
                )
        return {
            "compatible": not reasons,
            "proofVersion": "miku-standard-lit-proof-1.0",
            "reasons": reasons,
            "termIds": [str(term.get("id") or "") for term in surface_terms],
        }


class ClosureBudgetAnalyzer:
    def evaluate(
        self,
        weighted_set: Mapping[str, Any],
        features: SurfaceFeatureSet,
        budget: ClosureBudget,
    ) -> dict[str, Any]:
        terms = _terms(weighted_set)
        estimated_texture_samples = sum(
            _count_expression_kind(term.get("finalWeight"), "Texture")
            for term in terms
        )
        estimated_alu = (
            features.lobe_count * 48
            + features.emission_term_count * 4
            + features.dynamic_weight_count * 8
            + features.refraction_lobe_count * 32
        )
        violations: list[dict[str, Any]] = []
        checks = (
            ("maxLobes", features.lobe_count, budget.max_lobes),
            (
                "maxSpecularLobes",
                features.specular_lobe_count,
                budget.max_specular_lobes,
            ),
            (
                "maxTransmissionLobes",
                features.transmission_lobe_count,
                budget.max_transmission_lobes,
            ),
            (
                "maxRefractionSamples",
                features.refraction_lobe_count,
                budget.max_refraction_samples,
            ),
            (
                "maxDistinctNormals",
                features.distinct_normal_count,
                budget.max_distinct_normals,
            ),
            (
                "maxDynamicWeights",
                features.dynamic_weight_count,
                budget.max_dynamic_weights,
            ),
            ("maxEstimatedAlu", estimated_alu, budget.max_estimated_alu),
            (
                "maxTextureSamples",
                estimated_texture_samples,
                budget.max_texture_samples,
            ),
        )
        for field, actual, maximum in checks:
            if actual > maximum:
                violations.append(
                    {"field": field, "actual": actual, "maximum": maximum}
                )
        return {
            "budget": budget.to_document(),
            "estimated": {
                "alu": estimated_alu,
                "textureSamples": estimated_texture_samples,
            },
            "withinBudget": not violations,
            "violations": violations,
        }


class ClosureBackendResolver:
    def resolve(
        self,
        *,
        material_id: str,
        closure_graph: Mapping[str, Any],
        weighted_set: Mapping[str, Any],
        fidelity_policy: FidelityPolicy = (
            FidelityPolicy.ALLOW_DECLARED_APPROXIMATION
        ),
        add_energy_policy: AddShaderEnergyPolicy = (
            AddShaderEnergyPolicy.PRESERVE_BLENDER
        ),
        budget: ClosureBudget = ClosureBudget(),
    ) -> dict[str, Any]:
        features = SurfaceFeatureAnalyzer().analyze(
            weighted_set,
            closure_graph,
        )
        compatibility = StandardLitCompatibilityAnalyzer().analyze(
            weighted_set,
            features,
        )
        budget_result = ClosureBudgetAnalyzer().evaluate(
            weighted_set,
            features,
            budget,
        )
        terms = _terms(weighted_set)
        diagnostics: list[dict[str, Any]] = []
        approximations: list[dict[str, Any]] = []
        unsupported_terms = _unsupported_backend_terms(terms)
        if unsupported_terms:
            diagnostics.append(
                {
                    "severity": "error",
                    "code": "MIKU_CLOSURE_BACKEND_FEATURE_UNSUPPORTED",
                    "translationQuality": "Unsupported",
                    "termIds": [
                        item["termId"] for item in unsupported_terms
                    ],
                    "features": unsupported_terms,
                    "message": (
                        "One or more closure features have no faithful "
                        "phase 1-5 backend."
                    ),
                }
            )
        coat_terms = [
            term
            for term in terms
            if term.get("closureKind") == ClosureKind.PRINCIPLED.value
            and _parameter_is_nonzero_or_dynamic(term, "Coat Weight")
        ]
        if coat_terms:
            approximation = {
                "kind": "Urp17ClearCoat",
                "algorithmVersion": "miku-urp17-clear-coat-1",
                "errorBound": (
                    "Blender Principled Coat and URP Complex Lit Clear Coat "
                    "use different BRDF implementations."
                ),
                "originalTermIds": [
                    str(term.get("id") or "") for term in coat_terms
                ],
            }
            approximations.append(approximation)
            diagnostics.append(
                {
                    "severity": (
                        "error"
                        if fidelity_policy == FidelityPolicy.STRICT
                        else "warning"
                    ),
                    "code": "MIKU_COAT_URP_APPROXIMATION",
                    "translationQuality": "Approximate",
                    "termIds": approximation["originalTermIds"],
                    "message": (
                        "Principled Coat is mapped to URP 17.4 Clear Coat "
                        "(Coat Mask and one-minus Coat Roughness)."
                    ),
                }
            )
        realtime_approximation_features: list[dict[str, str]] = []
        for term in terms:
            term_id = str(term.get("id") or "")
            kind = str(term.get("closureKind") or "")
            parameters: tuple[str, ...] = ()
            if kind == ClosureKind.PRINCIPLED.value:
                parameters = (
                    "Sheen Weight",
                    "Subsurface Weight",
                    "Anisotropic",
                    *_unsupported_principled_coat_features(term),
                )
            elif kind == ClosureKind.GLOSSY.value:
                parameters = ("Anisotropy",)
            for parameter in parameters:
                if _parameter_is_nonzero_or_dynamic(term, parameter):
                    realtime_approximation_features.append(
                        {
                            "termId": term_id,
                            "feature": f"{kind}:{parameter}",
                        }
                    )
        surface_terms = [
            term
            for term in terms
            if term.get("domain")
            in {
                ClosureDomain.SURFACE_SCATTERING.value,
                ClosureDomain.SURFACE_TRANSMISSION.value,
                ClosureDomain.REFRACTION.value,
            }
        ]
        normal_keys = {
            _surface_normal_identity(_parameter(term, "Normal"))
            for term in surface_terms
        }
        if len(normal_keys) > 1:
            realtime_approximation_features.extend(
                {
                    "termId": str(term.get("id") or ""),
                    "feature": (
                        f"{str(term.get('closureKind') or '')}:"
                        "per-lobe-normal"
                    ),
                }
                for term in surface_terms
                if not _is_default_surface_normal(
                    _parameter(term, "Normal")
                )
            )
        if realtime_approximation_features:
            approximation = {
                "kind": "EeveeRealtimeSurfaceParameterApproximation",
                "algorithmVersion": "miku-eevee-realtime-surface-1",
                "errorBound": (
                    "URP uses a shared normal, isotropic specular response, "
                    "and its fixed realtime coat/subsurface model."
                ),
                "originalTermIds": sorted(
                    {
                        item["termId"]
                        for item in realtime_approximation_features
                    }
                ),
                "features": realtime_approximation_features,
            }
            approximations.append(approximation)
            diagnostics.append(
                {
                    "severity": (
                        "error"
                        if fidelity_policy == FidelityPolicy.STRICT
                        else "warning"
                    ),
                    "code": "MIKU_EEVEE_SURFACE_PARAMETER_APPROXIMATION",
                    "translationQuality": "Approximate",
                    "features": realtime_approximation_features,
                    "message": (
                        "Unsupported EEVEE lobe details are explicitly "
                        "projected onto the closest URP realtime controls."
                    ),
                }
            )
        optical_terms = [
            term
            for term in terms
            if term.get("domain") == ClosureDomain.REFRACTION.value
        ]
        if len(optical_terms) > 1:
            approximation = {
                "kind": "MultiOpticalLobeToGlossy",
                "algorithmVersion": "miku-multi-optical-glossy-1",
                "errorBound": (
                    "Multiple refraction lobes become weighted realtime "
                    "specular lobes without screen-space transmission."
                ),
                "originalTermIds": [
                    str(term.get("id") or "")
                    for term in optical_terms
                ],
            }
            approximations.append(approximation)
            diagnostics.append(
                {
                    "severity": (
                        "error"
                        if fidelity_policy == FidelityPolicy.STRICT
                        else "warning"
                    ),
                    "code": "MIKU_MULTI_OPTICAL_LOBE_APPROXIMATION",
                    "translationQuality": "Approximate",
                    "termIds": approximation["originalTermIds"],
                    "message": (
                        "Multiple Blender glass/refraction closures are "
                        "preserved as weighted URP glossy lobes."
                    ),
                }
            )
        diffuse_approximation_terms = [
            term
            for term in terms
            if term.get("closureKind")
            in {
                ClosureKind.SUBSURFACE.value,
                ClosureKind.TRANSLUCENT.value,
            }
        ]
        if diffuse_approximation_terms:
            approximation = {
                "kind": "EeveeDiffuseClosureApproximation",
                "algorithmVersion": "miku-eevee-diffuse-closure-1",
                "errorBound": (
                    "URP realtime lighting does not reproduce Blender "
                    "subsurface diffusion or back-facing translucency."
                ),
                "originalTermIds": [
                    str(term.get("id") or "")
                    for term in diffuse_approximation_terms
                ],
            }
            approximations.append(approximation)
            diagnostics.append(
                {
                    "severity": (
                        "error"
                        if fidelity_policy == FidelityPolicy.STRICT
                        else "warning"
                    ),
                    "code": "MIKU_EEVEE_DIFFUSE_CLOSURE_APPROXIMATION",
                    "translationQuality": "Approximate",
                    "termIds": approximation["originalTermIds"],
                    "message": (
                        "Blender Subsurface/Translucent closures are "
                        "preserved as weighted URP diffuse lobes."
                    ),
                }
            )
        if features.is_non_energy_conserving:
            diagnostics.append(
                {
                    "severity": "warning",
                    "code": "WEIGHT0003",
                    "translationQuality": "Exact",
                    "message": (
                        "Add Shader copies parent weight to every branch; "
                        "the result may be non-energy-conserving."
                    ),
                }
            )
        if add_energy_policy != AddShaderEnergyPolicy.PRESERVE_BLENDER:
            approximation = {
                "kind": add_energy_policy.value,
                "algorithmVersion": "miku-add-energy-policy-1",
                "errorBound": "Unbounded for arbitrary closure graphs",
                "originalTermIds": [
                    str(term.get("id") or "") for term in terms
                ],
            }
            approximations.append(approximation)
            diagnostics.append(
                {
                    "severity": (
                        "error"
                        if fidelity_policy == FidelityPolicy.STRICT
                        else "warning"
                    ),
                    "code": "WEIGHT0005",
                    "translationQuality": "Approximate",
                    "message": (
                        f"Applied explicit Add Shader energy policy "
                        f"{add_energy_policy.value}."
                    ),
                }
            )
        kind = (
            SurfaceModelKind.UNSUPPORTED_SURFACE
            if unsupported_terms
            else self._kind(features, compatibility, terms)
        )
        if not budget_result["withinBudget"]:
            diagnostics.append(
                {
                    "severity": "error",
                    "code": "WEIGHT0006",
                    "translationQuality": "Unsupported",
                    "violations": budget_result["violations"],
                    "message": "Closure graph exceeds the configured realtime budget.",
                }
            )
            kind = SurfaceModelKind.UNSUPPORTED_SURFACE
        if (
            fidelity_policy == FidelityPolicy.STRICT
            and approximations
        ):
            kind = SurfaceModelKind.UNSUPPORTED_SURFACE
        if kind == SurfaceModelKind.REFRACTIVE_GLASS:
            approximation = {
                "kind": "SingleSampleScreenSpaceRefraction",
                "algorithmVersion": "miku-glass-low-1",
                "errorBound": "View and scene dependent; no off-screen data",
                "originalTermIds": [
                    str(term.get("id") or "")
                    for term in terms
                    if term.get("domain") == ClosureDomain.REFRACTION.value
                ],
            }
            approximations.append(approximation)
            severity = (
                "error"
                if fidelity_policy == FidelityPolicy.STRICT
                else "warning"
            )
            diagnostics.append(
                {
                    "severity": severity,
                    "code": "MIKU_GLASS_LOW_QUALITY_APPROXIMATION",
                    "translationQuality": "Approximate",
                    "message": (
                        "Low-quality glass uses one screen-color sample and "
                        "reflection-probe Fresnel."
                    ),
                }
            )
            if fidelity_policy == FidelityPolicy.STRICT:
                kind = SurfaceModelKind.UNSUPPORTED_SURFACE
        if kind in {
            SurfaceModelKind.TRANSPARENT_LIT,
            SurfaceModelKind.CUSTOM_MULTI_LOBE,
        }:
            approximation = {
                "kind": "CustomLightingWithoutScreenSpaceAmbientOcclusion",
                "algorithmVersion": "miku-urp17-custom-lighting-1",
                "errorBound": (
                    "Screen-space ambient occlusion contribution is omitted; "
                    "direct lights, shadows, cookies, light probes, and fog "
                    "remain available."
                ),
                "originalTermIds": [
                    str(term.get("id") or "")
                    for term in terms
                    if term.get("domain")
                    in {
                        ClosureDomain.SURFACE_SCATTERING.value,
                        ClosureDomain.SURFACE_TRANSMISSION.value,
                    }
                ],
            }
            approximations.append(approximation)
            diagnostics.append(
                {
                    "severity": (
                        "error"
                        if fidelity_policy == FidelityPolicy.STRICT
                        else "warning"
                    ),
                    "code": "MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE",
                    "translationQuality": "Approximate",
                    "message": (
                        "The URP 17.4 custom multi-lobe path does not consume "
                        "screen-space ambient occlusion. Auto preserves the "
                        "remaining lighting terms; Strict rejects this path."
                    ),
                }
            )
            if fidelity_policy == FidelityPolicy.STRICT:
                kind = SurfaceModelKind.UNSUPPORTED_SURFACE
        render_state = _render_state(kind)
        requirements = _requirements(kind, features)
        composite = (
            _transparent_composite(terms, kind)
            if kind
            in {
                SurfaceModelKind.TRANSPARENT_EMISSION,
                SurfaceModelKind.TRANSPARENT_LIT,
                SurfaceModelKind.REFRACTIVE_GLASS,
            }
            else None
        )
        return {
            "schema": "miku-surface-model-plan-1.0",
            "materialId": material_id,
            "kind": kind.value,
            "features": features.to_document(),
            "rootClosureId": str(closure_graph.get("rootClosureId") or ""),
            "closureLoweringPlan": {
                "weightedTermIds": [
                    str(term.get("id") or "") for term in terms
                ],
                "standardLitCompatibility": compatibility,
                "budget": budget_result,
                "addShaderEnergyPolicy": add_energy_policy.value,
            },
            "channelPlans": [],
            "renderStatePlan": render_state,
            "shaderRequirements": requirements,
            "parameterPlans": [],
            "transparentCompositePlan": composite,
            "fidelity": (
                "Approximate"
                if approximations
                else "Exact"
            ),
            "fidelityPolicy": fidelity_policy.value,
            "approximations": approximations,
            "diagnostics": diagnostics,
        }

    @staticmethod
    def _kind(
        features: SurfaceFeatureSet,
        compatibility: Mapping[str, Any],
        terms: Sequence[Mapping[str, Any]],
    ) -> SurfaceModelKind:
        if (
            features.has_volume
            or features.has_holdout
            or features.has_shader_to_rgb_barrier
            or any(
                term.get("domain") == ClosureDomain.UNSUPPORTED.value
                for term in terms
            )
        ):
            return SurfaceModelKind.UNSUPPORTED_SURFACE
        principled_transmission = [
            term
            for term in terms
            if term.get("closureKind") == ClosureKind.PRINCIPLED.value
            and _parameter_is_nonzero_or_dynamic(
                term,
                "Transmission Weight",
            )
        ]
        optical_count = (
            features.refraction_lobe_count + len(principled_transmission)
        )
        if optical_count:
            if optical_count == 1:
                return SurfaceModelKind.REFRACTIVE_GLASS
            return SurfaceModelKind.CUSTOM_MULTI_LOBE
        if (
            features.transparent_term_count
            and not features.lobe_count
        ):
            return SurfaceModelKind.TRANSPARENT_EMISSION
        if features.transparent_term_count and features.lobe_count:
            return SurfaceModelKind.TRANSPARENT_LIT
        if compatibility.get("compatible"):
            return (
                SurfaceModelKind.CUTOUT_PBR
                if features.has_alpha_clip
                else SurfaceModelKind.OPAQUE_PBR
            )
        if features.lobe_count > 0:
            return SurfaceModelKind.CUSTOM_MULTI_LOBE
        if features.emission_term_count:
            return SurfaceModelKind.OPAQUE_PBR
        return SurfaceModelKind.UNSUPPORTED_SURFACE


def _render_state(kind: SurfaceModelKind) -> dict[str, Any]:
    transparent = kind in {
        SurfaceModelKind.TRANSPARENT_LIT,
        SurfaceModelKind.TRANSPARENT_EMISSION,
        SurfaceModelKind.REFRACTIVE_GLASS,
    }
    cutout = kind == SurfaceModelKind.CUTOUT_PBR
    return {
        "surfaceType": "Transparent" if transparent else "Opaque",
        "blendMode": "Premultiply" if transparent else "Off",
        "sourceBlend": "One",
        "destinationBlend": (
            "OneMinusSrcAlpha" if transparent else "Zero"
        ),
        "zWrite": not transparent,
        "zTest": "LessEqual",
        "cullMode": "Back",
        "renderQueue": "Transparent" if transparent else "Geometry",
        "alphaClip": cutout,
        "alphaClipThreshold": 0.5 if cutout else 0.0,
        "shadowCaster": not transparent or cutout,
        "depthOnly": not transparent or cutout,
    }


def _requirements(
    kind: SurfaceModelKind,
    features: SurfaceFeatureSet,
) -> dict[str, Any]:
    glass = kind == SurfaceModelKind.REFRACTIVE_GLASS
    scene_composite = features.has_colored_transmittance
    return {
        "requiresOpaqueTexture": glass or scene_composite,
        "requiresDepthTexture": False,
        "depthTexturePolicy": "Recommended" if glass else "NotRequired",
        "requiresColorPyramid": False,
        "requiresRendererFeature": False,
        "requiresReflectionProbes": glass
        or kind == SurfaceModelKind.CUSTOM_MULTI_LOBE,
        "requiredPasses": [
            "Forward",
            *([] if kind in {
                SurfaceModelKind.TRANSPARENT_LIT,
                SurfaceModelKind.TRANSPARENT_EMISSION,
                SurfaceModelKind.REFRACTIVE_GLASS,
            } else ["ShadowCaster", "DepthOnly"]),
        ],
        "validatedGraphicsApis": ["D3D11"],
    }


def _transparent_composite(
    terms: Sequence[Mapping[str, Any]],
    kind: SurfaceModelKind,
) -> dict[str, Any]:
    emissions = [
        str(term.get("id") or "")
        for term in terms
        if term.get("domain") == ClosureDomain.EMISSION.value
    ]
    surface = [
        str(term.get("id") or "")
        for term in terms
        if term.get("domain")
        in {
            ClosureDomain.SURFACE_SCATTERING.value,
            ClosureDomain.SURFACE_TRANSMISSION.value,
        }
    ]
    transparent = [
        term
        for term in terms
        if term.get("domain")
        == ClosureDomain.TRANSPARENT_PASS_THROUGH.value
    ]
    colored = any(
        _is_colored(_constant_parameter_value(term, "Color"))
        for term in transparent
    )
    return {
        "schema": "miku-transparent-composite-1.0",
        "additiveRadianceTerms": emissions,
        "surfaceRadianceTerms": surface,
        "directBackgroundTerms": [
            str(term.get("id") or "") for term in transparent
        ],
        "refractedBackgroundTerms": [
            str(term.get("id") or "")
            for term in terms
            if term.get("domain") == ClosureDomain.REFRACTION.value
        ],
        "transmittanceKind": "Colored" if colored else "Scalar",
        "alphaExpression": "1 - scalarTransmittance",
        "premultipliedColorExpression": (
            "surfaceRadiance + additiveRadiance"
        ),
        "blendMode": (
            "SceneColorComposite" if colored else "Premultiply"
        ),
        "premultiplyCount": 1,
        "limitations": (
            ["Colored transmittance requires Scene Color composition"]
            if colored
            else []
        ),
    }


def build_surface_model_plan(
    material_id: str,
    closure_graph: Mapping[str, Any],
    weighted_set: Mapping[str, Any],
    *,
    fidelity_policy: FidelityPolicy = (
        FidelityPolicy.ALLOW_DECLARED_APPROXIMATION
    ),
    add_energy_policy: AddShaderEnergyPolicy = (
        AddShaderEnergyPolicy.PRESERVE_BLENDER
    ),
    budget: ClosureBudget = ClosureBudget(),
) -> dict[str, Any]:
    return ClosureBackendResolver().resolve(
        material_id=material_id,
        closure_graph=closure_graph,
        weighted_set=weighted_set,
        fidelity_policy=fidelity_policy,
        add_energy_policy=add_energy_policy,
        budget=budget,
    )


def _contains_add(root: Any) -> bool:
    if not isinstance(root, Mapping):
        return False
    if root.get("kind") == ClosureKind.ADD.value:
        return True
    return _contains_add(root.get("first")) or _contains_add(root.get("second"))


def _is_colored(value: Any) -> bool:
    if not isinstance(value, Sequence) or isinstance(value, (str, bytes)):
        return False
    if len(value) < 3:
        return False
    components = [float(value[index]) for index in range(3)]
    return max(components) - min(components) > 1.0e-6


def _constant_weight(term: Mapping[str, Any]) -> float | None:
    weight = term.get("finalWeight")
    if not isinstance(weight, Mapping) or weight.get("kind") != "Constant":
        return None
    value = weight.get("value")
    return float(value) if isinstance(value, (int, float)) else None


def _is_zero_or_missing(value: Any) -> bool:
    if value is None:
        return True
    try:
        return abs(float(value)) <= 1.0e-8
    except (TypeError, ValueError):
        return False


def _parameter_is_nonzero_or_dynamic(
    term: Mapping[str, Any],
    name: str,
) -> bool:
    record = _parameter(term, name)
    if record is None:
        return False
    if record.get("kind") != "Constant":
        return True
    return not _is_zero_or_missing(record.get("value"))


def _count_expression_kind(value: Any, kind: str) -> int:
    if not isinstance(value, Mapping):
        return 0
    count = 1 if value.get("kind") == kind else 0
    for nested in value.values():
        if isinstance(nested, Mapping):
            count += _count_expression_kind(nested, kind)
        elif isinstance(nested, Sequence) and not isinstance(
            nested,
            (str, bytes),
        ):
            count += sum(
                _count_expression_kind(item, kind)
                for item in nested
            )
    return count


def _unsupported_backend_terms(
    terms: Sequence[Mapping[str, Any]],
) -> list[dict[str, str]]:
    unsupported: list[dict[str, str]] = []
    supported_kinds = {
        ClosureKind.PRINCIPLED.value,
        ClosureKind.DIFFUSE.value,
        ClosureKind.GLOSSY.value,
        ClosureKind.METALLIC.value,
        ClosureKind.EMISSION.value,
        ClosureKind.TRANSPARENT.value,
        ClosureKind.GLASS.value,
        ClosureKind.REFRACTION.value,
        ClosureKind.SUBSURFACE.value,
        ClosureKind.TRANSLUCENT.value,
    }
    for term in terms:
        term_id = str(term.get("id") or "")
        kind = str(term.get("closureKind") or "")
        if kind not in supported_kinds:
            unsupported.append(
                {"termId": term_id, "feature": f"closure-kind:{kind}"}
            )
            continue
        checks: tuple[str, ...] = ()
        if kind == ClosureKind.GLASS.value:
            checks = ("Thin Film Thickness",)
        for parameter in checks:
            record = _parameter(term, parameter)
            if record is None:
                continue
            value = (
                record.get("value")
                if record.get("kind") == "Constant"
                else None
            )
            if record.get("kind") != "Constant" or not _is_zero_or_missing(value):
                unsupported.append(
                    {
                        "termId": term_id,
                        "feature": f"{kind}:{parameter}",
                    }
                )
    return unsupported


def _unsupported_principled_coat_features(
    term: Mapping[str, Any],
) -> list[str]:
    if not _parameter_is_nonzero_or_dynamic(term, "Coat Weight"):
        return []
    unsupported: list[str] = []
    weight = _parameter(term, "Coat Weight")
    roughness = _parameter(term, "Coat Roughness")
    if not _is_compilable_coat_scalar(weight):
        unsupported.append("Coat Weight")
    if roughness is not None and not _is_compilable_coat_scalar(roughness):
        unsupported.append("Coat Roughness")
    ior = _parameter(term, "Coat IOR")
    if ior is not None and not _is_constant_close(ior, 1.5):
        unsupported.append("Coat IOR")
    tint = _parameter(term, "Coat Tint")
    if tint is not None and not _is_constant_white(tint):
        unsupported.append("Coat Tint")
    normal = _parameter(term, "Coat Normal")
    if normal is not None and not _is_default_surface_normal(normal):
        unsupported.append("Coat Normal")
    return unsupported


def _is_compilable_coat_scalar(record: Mapping[str, Any] | None) -> bool:
    if record is None:
        return True
    kind = str(record.get("kind") or "")
    if kind == "Constant":
        try:
            value = float(record.get("value"))
        except (TypeError, ValueError):
            return False
        return value == value and value not in {float("inf"), float("-inf")}
    return (
        kind == "ValueExpression"
        and bool(record.get("expressionId"))
        and not bool(record.get("requiresBake"))
    )


def _is_constant_close(
    record: Mapping[str, Any],
    expected: float,
    tolerance: float = 1.0e-6,
) -> bool:
    if record.get("kind") != "Constant":
        return False
    try:
        return abs(float(record.get("value")) - expected) <= tolerance
    except (TypeError, ValueError):
        return False


def _is_constant_white(record: Mapping[str, Any]) -> bool:
    if record.get("kind") != "Constant":
        return False
    value = record.get("value")
    if not isinstance(value, Sequence) or isinstance(value, (str, bytes)):
        return False
    if len(value) < 3:
        return False
    try:
        return all(abs(float(value[index]) - 1.0) <= 1.0e-6 for index in range(3))
    except (TypeError, ValueError):
        return False


def _surface_normal_identity(record: Mapping[str, Any] | None) -> str:
    if record is None or _is_default_surface_normal(record):
        return "surface-default"
    return canonical_hash(record)


def _is_default_surface_normal(record: Mapping[str, Any]) -> bool:
    if record.get("kind") != "Constant":
        return False
    value = record.get("value")
    if not isinstance(value, Sequence) or isinstance(value, (str, bytes)):
        return False
    if len(value) < 3:
        return False
    try:
        components = tuple(float(value[index]) for index in range(3))
    except (TypeError, ValueError):
        return False
    return components in {
        (0.0, 0.0, 0.0),
        (0.0, 0.0, 1.0),
    }
