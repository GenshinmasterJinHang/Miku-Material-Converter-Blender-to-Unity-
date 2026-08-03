"""Deterministic semantic-region planner."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Mapping

from .contracts import Fidelity, Route, make_document, stable_uuid
from .semantic import build_material_ir


SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES = frozenset(
    {
        "MIKU_CLOSURE_BACKEND_FEATURE_UNSUPPORTED",
        "MIKU_CLOSURE_PARAMETER_EXPRESSION_UNSUPPORTED",
        "MIKU_CLOSURE_WEIGHT_EXPRESSION_UNSUPPORTED",
        "MIKU_FULL_PBR_BAKE_REQUIRED",
        "MIKU_REQUIRED_CHANNEL_UNRESOLVED",
        "MIKU_REQUIRED_REGION_UNSUPPORTED",
        "MIKU_SOURCE_MESH_FIDELITY_REQUIRED",
        "MIKU_SURFACE_MODEL_UNSUPPORTED",
        "WEIGHT0006",
        "WEIGHT0009",
    }
)


@dataclass(frozen=True)
class TargetProfile:
    unity: str = "6000.4.5f1"
    urp: str = "17.4.0"
    shader_graph: str = "17.4.0"
    color_space: str = "Linear"
    graphics_api: str = "D3D11"
    custom_lit: bool = True

    def to_document(self) -> dict[str, Any]:
        return make_document(
            "miku-target-profile-1.0",
            {
                "target": "Unity6-URP-ShaderGraph",
                "unity": self.unity,
                "urp": self.urp,
                "shaderGraph": self.shader_graph,
                "colorSpace": self.color_space,
                "graphicsApi": self.graphics_api,
                "capabilities": {
                    "standardLit": True,
                    "customLit": self.custom_lit,
                    "forwardAdditionalLights": self.custom_lit,
                    "shadowCaster": True,
                    "depthOnly": True,
                    "alphaClip": True,
                    "alphaBlend": True,
                    "ditheredCoverage": True,
                    "screenRefraction": True,
                    "closureAwareSurfacePlanning": True,
                    "transparentEmission": True,
                    "transparentLitComposite": True,
                    "clearCoat": True,
                    "customMultiLobe": self.custom_lit,
                    "strictFidelity": True,
                },
                "surfaceGenerators": [
                    "OpaquePBR",
                    "CutoutPBR",
                    "TransparentLit",
                    "TransparentEmission",
                    "RefractiveGlass",
                    "CustomMultiLobe",
                ],
                "closureBudget": {
                    "maxLobes": 8,
                    "maxSpecularLobes": 4,
                    "maxTransmissionLobes": 2,
                    "maxRefractionSamples": 2,
                    "maxDistinctNormals": 4,
                    "maxDynamicWeights": 16,
                    "maxEstimatedAlu": 512,
                    "maxTextureSamples": 32,
                },
                "implementationHashes": {
                    "shaderGraphWrapper": "c7c9b5ed0c068208d251d9b8d058a7175d5c1c2930bc6062f05a8e43463dfee6",
                    "clearCoatWrapper": "22f41abb88fd47efcfb57fce41abab77301a7f1db216bbf9c3d640e8ec8c24d6",
                    "alphaBlendWrapper": "8b959dfeb5d1f684b897c074647078789fc6fcd3113ee178374d9aaf86e5f845",
                    "ditheredWrapper": "c51786632f1b712e8e25060357940b5daa127942346d9f093c251df282cb7d2a",
                    "dielectricWrapper": "8694e3bb49c2ce279aff157ce722ec15c4e7793b970e64cb8100cd2c409d67df",
                    "gameToonScreenRim": "86ec8bba65081ee680adfbcefb6f605c1c0b6bb17c4d6a72cd06584386850275",
                    "wuwaEyeBackend": "2ee5788d7e2c62f4f5ce1bf84edfd1af2c6094ac0776b6376e73025f5abd3f97",
                    "generatedSubGraph": "b83e1588103b7ae6ecfaddaed453d7eebbacbce4d08063903c1e8a0db70c0e1c",
                    "runtimeStructuredBackend": "55ae46b385ae4ed8c8c870f3d87c9702bd3f1de3e791f0ba700c0a64c495fbc4",
                    "blenderLightPathRuntime": "3cdccb562f0521bf9b28fa0e91a32a499e1642cc350df72f9ff076d0e0322504",
                    "blenderNoiseRuntime": "f861da2f3fbdaaf998a31914604eb7e4c4ed54e38efb26da78a2359bb1de5669",
                    "sourceMeshGlbWriter": "e23d26499cf91b4617c6e6af9844a68431fcbe73693b25a4c2751df6b3e5ed4c",
                    "surfaceModelRegistry": "80253c7ddfc5a4102adb5170eae4c497cab6ae17c5f796658a4edcb960c08cb3",
                    "multiLobeLighting": "685f019fb73e655ddcc4d9272b6f6c88f11363ebcf836e965791bfaf8a463901",
                },
            },
        )


def default_target_profile() -> dict[str, Any]:
    return TargetProfile().to_document()


def _nested_requires_bake_records(
    value: Any,
    path: tuple[str, ...] = (),
) -> list[tuple[tuple[str, ...], Mapping[str, Any]]]:
    """Return unresolved bake records at any authoritative closure depth."""

    records: list[tuple[tuple[str, ...], Mapping[str, Any]]] = []
    if isinstance(value, Mapping):
        if bool(value.get("requiresBake")):
            records.append((path, value))
        for key, child in value.items():
            records.extend(
                _nested_requires_bake_records(child, (*path, str(key)))
            )
    elif isinstance(value, list):
        for index, child in enumerate(value):
            records.extend(
                _nested_requires_bake_records(child, (*path, str(index)))
            )
    return records


def _nested_expression_ids(value: Any) -> set[str]:
    result: set[str] = set()
    if isinstance(value, Mapping):
        expression_id = str(value.get("expressionId") or "")
        if expression_id:
            result.add(expression_id)
        for child in value.values():
            result.update(_nested_expression_ids(child))
    elif isinstance(value, list):
        for child in value:
            result.update(_nested_expression_ids(child))
    return result


def _reachable_expression_ids(
    expressions: list[Mapping[str, Any]],
    roots: set[str],
) -> set[str]:
    by_id = {
        str(expression.get("id") or ""): expression
        for expression in expressions
        if str(expression.get("id") or "")
    }
    reachable: set[str] = set()
    pending = list(roots)
    while pending:
        expression_id = pending.pop()
        if expression_id in reachable:
            continue
        reachable.add(expression_id)
        expression = by_id.get(expression_id)
        if expression is None:
            continue
        pending.extend(
            sorted(_nested_expression_ids(expression.get("inputs") or {}))
        )
    return reachable


class ConversionPlanner:
    """Plan routes without importing Blender or Unity."""

    def plan(self, ir: Mapping[str, Any], *, target_profile: Mapping[str, Any] | None = None, mode: str = "Auto") -> dict[str, Any]:
        profile = dict(target_profile or default_target_profile())
        regions = list(ir.get("regions") or [])
        diagnostics = list(ir.get("diagnostics") or [])
        workflow_kind = str(
            ((ir.get("workflow") or {}).get("kind") or "")
            if isinstance(ir.get("workflow"), Mapping)
            else ""
        )
        if workflow_kind == "generic_toon":
            raise ValueError("MIKU_WORKFLOW_RETIRED:generic_toon")
        if workflow_kind in {
            "genshin_toon",
            "wuwa_toon",
            "hsr_toon",
            "endfield_toon",
        }:
            if mode != "Auto" and not any(
                str(item.get("code") or "")
                == "MIKU_FIXED_WORKFLOW_CONVERSION_MODE_IGNORED"
                for item in diagnostics
                if isinstance(item, Mapping)
            ):
                diagnostics.append(
                    {
                        "severity": "info",
                        "code": "MIKU_FIXED_WORKFLOW_CONVERSION_MODE_IGNORED",
                        "translationQuality": "Equivalent",
                        "mode": mode,
                        "message": (
                            "Fixed shader workflows always use the Native "
                            "texture-binding route and never schedule baking."
                        ),
                    }
                )
            return make_document(
                "miku-conversion-plan-1.0",
                {
                    "materialKey": ir.get("materialKey", ""),
                    "targetProfile": profile.get("canonicalHash", ""),
                    "mode": mode,
                    "routePolicy": "FixedWorkflowTextureBinding",
                    "surfaceModel": "OpaquePBR",
                    "surfaceBackend": "MikuStaticWorkflowBackend",
                    "surfaceModelPlan": ir.get("surfaceModelPlan") or {},
                    "closureGraph": ir.get("closureGraph") or {},
                    "weightedClosures": ir.get("weightedClosures") or {},
                    "regions": [
                        {
                            "regionId": region.get("id"),
                            "route": Route.NATIVE.value,
                            "fidelity": Fidelity.EQUIVALENT.value,
                            "backend": "MikuStaticWorkflowBackend",
                            "required": True,
                            "sourceRegionId": region.get("sourceRegionId"),
                            "scope": "FixedShaderMaterial",
                        }
                        for region in sorted(
                            regions,
                            key=lambda item: str(item.get("id") or ""),
                        )
                    ],
                    "bakeJobs": [],
                    "parameters": list(ir.get("parameters") or []),
                    "diagnostics": diagnostics,
                    "completionPolicy": {
                        "requireExitCodeZero": True,
                        "requireCompletionMarker": True,
                        "requireArtifactHashes": True,
                    },
                },
            )
        portable_mode = mode in {
            "Auto",
            "NativeOnly",
            "PreferNative",
            "ReusableBakeOnly",
        }
        surface_model_plan = (
            dict(ir.get("surfaceModelPlan") or {})
            if isinstance(ir.get("surfaceModelPlan"), Mapping)
            else {}
        )
        surface_model = str(
            surface_model_plan.get("kind") or "UnsupportedSurface"
        )
        full_pbr_runtime_dependencies = set()
        for expression in ir.get("expressions") or []:
            if not isinstance(expression, Mapping):
                continue
            operation = str(expression.get("op") or "")
            if operation == "Input.ViewDirection":
                full_pbr_runtime_dependencies.add("ViewDirection")
            elif operation.startswith("Input.Camera."):
                full_pbr_runtime_dependencies.add("Camera")
            elif operation.startswith("Input.Time."):
                full_pbr_runtime_dependencies.add("Time")
            elif operation.startswith("Input.LightPath."):
                full_pbr_runtime_dependencies.add("LightPath")
        full_pbr_runtime_dependencies = sorted(full_pbr_runtime_dependencies)
        if mode == "FullPBRBake" and full_pbr_runtime_dependencies:
            diagnostics.append(
                {
                    "severity": "error",
                    "code": "MIKU_RUNTIME_INPUT_UNSUPPORTED",
                    "translationQuality": "Unsupported",
                    "runtimeDependencies": full_pbr_runtime_dependencies,
                    "message": (
                        "Full PBR Bake is source-mesh-bound and cannot encode "
                        "runtime view, camera, light-path, or time data in UV "
                        "textures. Select Portable Hybrid (Prefer Native) to "
                        "preserve supported runtime expressions."
                    ),
                }
            )
            return make_document(
                "miku-conversion-plan-1.0",
                {
                    "materialKey": ir.get("materialKey", ""),
                    "targetProfile": profile.get("canonicalHash", ""),
                    "mode": mode,
                    "routePolicy": "ExplicitSourceMeshFidelity",
                    "surfaceModel": surface_model,
                    "surfaceBackend": "",
                    "surfaceModelPlan": surface_model_plan,
                    "closureGraph": ir.get("closureGraph") or {},
                    "weightedClosures": ir.get("weightedClosures") or {},
                    "regions": [],
                    "bakeJobs": [],
                    "parameters": list(ir.get("parameters") or []),
                    "diagnostics": diagnostics,
                    "completionPolicy": {
                        "requireExitCodeZero": True,
                        "requireCompletionMarker": True,
                        "requireArtifactHashes": True,
                    },
                },
            )
        blocking_errors = [
            item
            for item in diagnostics
            if isinstance(item, Mapping)
            and str(item.get("severity") or "").lower() == "error"
            and str(item.get("code") or "")
            not in SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES
        ]
        full_pbr_semantics = [
            "Alpha",
            "BaseColor",
            "Emission",
            "Metalness",
            "Normal",
            "Roughness",
        ]
        if any(
            isinstance(channel, Mapping)
            and str(channel.get("semantic") or "") == "Height"
            for channel in ir.get("channels") or []
        ):
            full_pbr_semantics.append("Height")
        if mode == "FullPBRBake" and not blocking_errors:
            resolved_codes = sorted(
                {
                    str(item.get("code") or "")
                    for item in diagnostics
                    if isinstance(item, Mapping)
                    and str(item.get("severity") or "").lower() == "error"
                    and str(item.get("code") or "")
                }
            )
            retained_diagnostics = [
                dict(item)
                for item in diagnostics
                if not (
                    isinstance(item, Mapping)
                    and str(item.get("severity") or "").lower() == "error"
                )
            ]
            retained_diagnostics.append(
                {
                    "severity": "info",
                    "code": "MIKU_SOURCE_MESH_FIDELITY_SCHEDULED",
                    "translationQuality": "Baked",
                    "resolvedCodes": resolved_codes,
                    "message": (
                        "Explicit Source Mesh Fidelity scheduled a deterministic "
                        "lighting-independent PBR channel bake."
                    ),
                }
            )
            region_ids = sorted(
                str(region.get("id") or "")
                for region in regions
                if str(region.get("id") or "")
            )
            source_region_id = (
                region_ids[0] if region_ids else str(ir.get("id") or "")
            )
            parameters = []
            for parameter in ir.get("parameters") or []:
                item = dict(parameter)
                item["influences"] = sorted(
                    {str(item.get("semantic") or "")}
                )
                parameters.append(item)
            return make_document(
                "miku-conversion-plan-1.0",
                {
                    "materialKey": ir.get("materialKey", ""),
                    "targetProfile": profile.get("canonicalHash", ""),
                    "mode": mode,
                    "routePolicy": "ExplicitSourceMeshFidelity",
                    "surfaceModel": "OpaquePBR",
                    "surfaceBackend": "OpaquePBRSurfaceModelBackend",
                    "surfaceModelPlan": surface_model_plan,
                    "closureGraph": ir.get("closureGraph") or {},
                    "weightedClosures": ir.get("weightedClosures") or {},
                    "regions": [
                        {
                            "regionId": region.get("id"),
                            "route": Route.FULL_PBR_BAKE.value,
                            "fidelity": Fidelity.BAKED.value,
                            "backend": "BlenderCyclesMeshBakeExecutor",
                            "required": True,
                            "sourceRegionId": region.get("sourceRegionId"),
                            "scope": "Material",
                        }
                        for region in sorted(
                            regions,
                            key=lambda item: str(item.get("id") or ""),
                        )
                    ],
                    "bakeJobs": [
                        {
                            "jobId": (
                                "bake-full-pbr-"
                                + stable_uuid(
                                    "miku-full-pbr-bake",
                                    str(ir.get("id") or ""),
                                )
                            ),
                            "regionId": source_region_id,
                            "route": Route.FULL_PBR_BAKE.value,
                            "scope": "Material",
                            "semantics": full_pbr_semantics,
                            "resolution": 1024,
                            "supersampling": 2,
                            "padding": 16,
                            "samples": 16,
                            "randomSeed": 0,
                            "sourceRegionId": source_region_id,
                            "heightSource": dict(ir.get("heightChannel") or {}),
                            "displacementPolicy": str(
                                ir.get("displacementPolicy") or "FOLLOW_BLENDER"
                            ),
                        }
                    ],
                    "parameters": parameters,
                    "diagnostics": retained_diagnostics,
                    "completionPolicy": {
                        "requireExitCodeZero": True,
                        "requireCompletionMarker": True,
                        "requireArtifactHashes": True,
                    },
                },
            )
        source_mesh_resolvable_errors = [
            item
            for item in diagnostics
            if isinstance(item, Mapping)
            and str(item.get("severity") or "").lower() == "error"
            and str(item.get("code") or "")
            in SOURCE_MESH_RESOLVABLE_DIAGNOSTIC_CODES
        ]
        if (
            mode == "AllowMeshBake"
            and source_mesh_resolvable_errors
            and not blocking_errors
        ):
            diagnostics.insert(
                0,
                {
                    "severity": "error",
                    "code": "MIKU_FULL_PBR_BAKE_REQUIRED",
                    "translationQuality": "Unsupported",
                    "resolvedCodes": sorted(
                        {
                            str(item.get("code") or "")
                            for item in source_mesh_resolvable_errors
                            if str(item.get("code") or "")
                        }
                    ),
                    "message": (
                        "Source Mesh Fidelity cannot safely lower the complete "
                        "static surface. Select Full PBR Bake for this material."
                    ),
                },
            )
        surface_backends = {
            "OpaquePBR": "OpaquePBRSurfaceModelBackend",
            "CutoutPBR": "CutoutPBRSurfaceModelBackend",
            "TransparentLit": "TransparentLitSurfaceModelBackend",
            "TransparentEmission": "TransparentEmissionSurfaceModelBackend",
            "RefractiveGlass": "RefractiveGlassSurfaceModelBackend",
            "CustomMultiLobe": "CustomMultiLobeSurfaceModelBackend",
            "UnsupportedSurface": "",
        }
        if surface_model == "UnsupportedSurface":
            if portable_mode and not blocking_errors:
                diagnostics.insert(
                    0,
                    {
                        "severity": "error",
                        "code": "MIKU_SOURCE_MESH_FIDELITY_REQUIRED",
                        "translationQuality": "Unsupported",
                        "message": (
                            f"{mode} cannot lower this static surface model. "
                            "Use Source Mesh Fidelity for this material."
                        ),
                    },
                )
            diagnostics.append(
                {
                    "severity": "error",
                    "code": "MIKU_SURFACE_MODEL_UNSUPPORTED",
                    "translationQuality": "Unsupported",
                    "message": (
                        "No safe surface backend is available for the "
                        "weighted closure set."
                    ),
                }
            )
        expressions = list(ir.get("expressions") or [])
        has_runtime_expressions = bool(expressions)
        has_runtime_error = any(
            str(item.get("severity") or "").lower() == "error"
            for item in diagnostics
            if isinstance(item, Mapping)
        )
        region_plans = []
        jobs = []
        for region in sorted(regions, key=lambda item: str(item.get("id"))):
            kind = str(region.get("kind") or "OpaqueSemanticRegion")
            dynamic = str(region.get("dynamism") or "Static") == "Runtime"
            source_semantics = region.get("sourceSemantics") or []
            if isinstance(source_semantics, str):
                source_semantics = [source_semantics]
            mesh = str(region.get("coordinateSpace") or "None") in {"Object", "Generated", "World"} or any(
                token in str(item) for item in source_semantics for token in ("Bump", "AmbientOcclusion", "Bevel", "Wireframe")
            )
            if dynamic and has_runtime_error:
                route, fidelity, backend = Route.UNSUPPORTED, None, ""
            elif kind == "RuntimeExpressionRegion":
                route, fidelity, backend = (
                    Route.NATIVE,
                    Fidelity.EXACT,
                    "MikuShaderGraph17_4StructuredSubGraphBackend",
                )
            elif kind == "AnisotropicClosure":
                route, fidelity, backend = Route.NATIVE, Fidelity.APPROXIMATE, "ShaderGraph17_4UrpCustomLitBackend"
            elif kind == "GlassClosure":
                route, fidelity, backend = (
                    Route.NATIVE,
                    Fidelity.APPROXIMATE,
                    "ShaderGraph17_4UrpCustomLitBackend",
                )
            elif kind == "TransparentClosure":
                route, fidelity, backend = (
                    Route.NATIVE,
                    Fidelity.EQUIVALENT,
                    "ShaderGraph17_4UrpBackend",
                )
            elif kind == "SurfaceMix" and dynamic:
                route, fidelity, backend = Route.NATIVE, Fidelity.APPROXIMATE, "ShaderGraph17_4UrpCustomLitBackend"
            elif dynamic:
                route, fidelity, backend = Route.NATIVE, Fidelity.EQUIVALENT, "ShaderGraph17_4UrpBackend"
            elif mesh:
                route, fidelity, backend = Route.MESH_BAKE, Fidelity.BAKED, "BlenderCyclesMeshBakeExecutor"
            elif kind == "OpaqueSemanticRegion":
                # Opaque regions do not yet carry a proof that their coordinate
                # domain is mesh-independent. Treat them as mesh-bound instead
                # of silently advertising a UV bake as reusable.
                route, fidelity, backend = Route.MESH_BAKE, Fidelity.BAKED, "BlenderCyclesMeshBakeExecutor"
            elif kind in {"PrincipledClosure", "EmissionClosure", "SurfaceMix"}:
                route, fidelity, backend = Route.NATIVE, Fidelity.EQUIVALENT, "ShaderGraph17_4UrpBackend"
            else:
                route, fidelity, backend = Route.UNSUPPORTED, None, ""
            if mode == "NativeOnly" and route not in {Route.NATIVE, Route.UNSUPPORTED}:
                route, fidelity, backend = Route.UNSUPPORTED, None, ""
            if mode == "ReusableBakeOnly" and route == Route.NATIVE and not dynamic:
                route, fidelity, backend = Route.REUSABLE_BAKE, Fidelity.BAKED, "BlenderCyclesReusableBakeExecutor"
            result = {
                "regionId": region.get("id"),
                "route": route.value,
                "fidelity": fidelity.value if fidelity else None,
                "backend": backend,
                "required": True,
                "sourceRegionId": region.get("sourceRegionId"),
                "scope": (
                    "ClosureCompatibilityProjection"
                    if kind.endswith("Closure") or kind == "SurfaceMix"
                    else "ValueSubgraph"
                ),
            }
            if result["scope"] == "ClosureCompatibilityProjection":
                result.update(
                    {
                        "route": (
                            Route.UNSUPPORTED.value
                            if surface_model == "UnsupportedSurface"
                            else Route.NATIVE.value
                        ),
                        "fidelity": surface_model_plan.get(
                            "fidelity",
                            Fidelity.EXACT.value,
                        ),
                        "backend": surface_backends.get(surface_model, ""),
                    }
                )
                route = Route(result["route"])
            region_plans.append(result)
            if (
                route in {Route.REUSABLE_BAKE, Route.MESH_BAKE, Route.FULL_PBR_BAKE}
                and has_runtime_expressions
            ):
                # Runtime expressions and static procedural inputs may share a
                # coarse semantic region. Preserve the region for structured
                # lowering; the target-neutral channel proof below schedules
                # only unresolved static semantics with the GPL executor.
                result.update(
                    {
                        "route": Route.NATIVE.value,
                        "fidelity": Fidelity.EQUIVALENT.value,
                        "backend": "MikuShaderGraph17_4StructuredSubGraphBackend",
                    }
                )
                route = Route.NATIVE
            if route in {Route.MESH_BAKE, Route.FULL_PBR_BAKE} and portable_mode:
                result.update(
                    {
                        "route": Route.UNSUPPORTED.value,
                        "fidelity": None,
                        "backend": "",
                    }
                )
                route = Route.UNSUPPORTED
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": "MIKU_SOURCE_MESH_FIDELITY_REQUIRED",
                        "regionId": region.get("id"),
                        "translationQuality": "Unsupported",
                        "message": (
                            f"{mode} cannot emit a mesh-bound Texture2D. "
                            "Use Source Mesh Fidelity for this material."
                        ),
                    }
                )
            if route in {Route.REUSABLE_BAKE, Route.MESH_BAKE, Route.FULL_PBR_BAKE}:
                jobs.append(
                    {
                        "jobId": f"bake-{region.get('id')}",
                        "regionId": region.get("id"),
                        "route": route.value,
                        "resolution": 1024,
                        "supersampling": 2,
                        "padding": 16,
                        "samples": 16,
                        "randomSeed": 0,
                        "sourceRegionId": region.get("sourceRegionId"),
                    }
                )
            if route == Route.UNSUPPORTED:
                diagnostics.append({"severity": "error", "code": "MIKU_REQUIRED_REGION_UNSUPPORTED", "regionId": region.get("id"), "message": f"No safe route for semantic region {kind}."})
        # Standard PBR channels are compatibility projections for a custom
        # multi-lobe surface. Its weighted-closure parameters are the only
        # authoritative consumers, so baking those top-level projections would
        # create unused resources (notably an `_MIKU_IOR` texture).
        authoritative_channels = (
            []
            if surface_model == "CustomMultiLobe"
            else list(ir.get("channels") or [])
        )
        static_channel_semantics = sorted(
            {
                str(channel.get("semantic") or "")
                for channel in authoritative_channels
                if isinstance(channel, Mapping)
                and bool(channel.get("requiresBake"))
                and str(channel.get("semantic") or "")
            }
        )
        if not has_runtime_error and static_channel_semantics:
            if portable_mode:
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": "MIKU_SOURCE_MESH_FIDELITY_REQUIRED",
                        "semantics": static_channel_semantics,
                        "message": (
                            f"{mode} cannot resolve linked channels that "
                            "require a source-mesh bake. Use Source Mesh "
                            "Fidelity for this material."
                        ),
                    }
                )
            else:
                # The channel proof is more specific than coarse closure or
                # opaque-region bake jobs. It covers exactly the linked static
                # Standard PBR outputs and prevents duplicate whole-material
                # executions.
                jobs = [
                    item
                    for item in jobs
                    if str(item.get("scope") or "")
                    not in {"", "Region"}
                ]
                source_region_ids = sorted(
                    {
                        str(channel.get("regionId") or "")
                        for channel in authoritative_channels
                        if isinstance(channel, Mapping)
                        and bool(channel.get("requiresBake"))
                        and str(channel.get("regionId") or "")
                    }
                )
                job_id = stable_uuid(
                    "miku-channel-bake",
                    f"{ir.get('id', '')}:{','.join(static_channel_semantics)}",
                )
                jobs.append(
                    {
                        "jobId": f"bake-channels-{job_id}",
                        "regionId": source_region_ids[0] if source_region_ids else str(ir.get("id") or ""),
                        "route": Route.MESH_BAKE.value,
                        "scope": "Channels",
                        "semantics": static_channel_semantics,
                        "resolution": 1024,
                        "supersampling": 2,
                        "padding": 16,
                        "samples": 16,
                        "randomSeed": 0,
                        "sourceRegionId": source_region_ids[0] if source_region_ids else str(ir.get("id") or ""),
                        "sourceRegionIds": source_region_ids,
                        "heightSource": dict(ir.get("heightChannel") or {}),
                        "displacementPolicy": str(
                            ir.get("displacementPolicy") or "FOLLOW_BLENDER"
                        ),
                    }
                )
                diagnostics.append(
                    {
                        "severity": "info",
                        "code": "MIKU_STATIC_CHANNEL_BAKE_SCHEDULED",
                        "translationQuality": "Baked",
                        "semantics": static_channel_semantics,
                        "message": (
                            "Scheduled only the runtime-independent linked "
                            "channel(s) for mesh-bound baking."
                        ),
                    }
                )
        unresolved_closure_records = _nested_requires_bake_records(
            ir.get("weightedClosures") or {},
            ("weightedClosures",),
        )
        if unresolved_closure_records and not has_runtime_error:
            consumer_paths = [
                ".".join(path) for path, _ in unresolved_closure_records
            ]
            if portable_mode:
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": "MIKU_SOURCE_MESH_FIDELITY_REQUIRED",
                        "consumerPaths": consumer_paths,
                        "message": (
                            f"{mode} cannot resolve active weighted-closure "
                            "parameters that require a source-mesh bake."
                        ),
                    }
                )
            else:
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": "MIKU_CLOSURE_PARAMETER_BAKE_UNRESOLVED",
                        "translationQuality": "Unsupported",
                        "consumerPaths": consumer_paths,
                        "message": (
                            "An active closure parameter still has requiresBake "
                            "after Source Mesh expression compilation."
                        ),
                    }
                )
        expression_islands = sorted(
            (
                expression
                for expression in expressions
                if isinstance(expression, Mapping)
                and str(expression.get("op") or "")
                == "Texture.SampleBaked2D"
            ),
            key=lambda item: str(item.get("id") or ""),
        )
        if surface_model == "CustomMultiLobe":
            closure_roots = _nested_expression_ids(
                ir.get("weightedClosures") or {}
            )
            closure_reachable = _reachable_expression_ids(
                [
                    expression
                    for expression in expressions
                    if isinstance(expression, Mapping)
                ],
                closure_roots,
            )
            expression_islands = [
                expression
                for expression in expression_islands
                if str(expression.get("id") or "") in closure_reachable
            ]
        if expression_islands and not has_runtime_error:
            reusable_expression_islands = [
                expression
                for expression in expression_islands
                if isinstance(expression.get("params"), Mapping)
                and expression["params"].get("meshBindingRequired") is False
                and str(
                    expression["params"].get("coordinateDomain") or ""
                )
                in {"Uniform", "UV0"}
            ]
            mesh_expression_islands = [
                expression
                for expression in expression_islands
                if expression not in reusable_expression_islands
            ]
            reusable_mode = mode in {"PreferNative", "ReusableBakeOnly"}
            if portable_mode and mesh_expression_islands:
                diagnostics.append(
                    {
                        "severity": "error",
                        "code": (
                            "MIKU_PORTABLE_HYBRID_MESH_DEPENDENCY"
                            if mode == "PreferNative"
                            else "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
                        ),
                        "expressionIds": [
                            str(item.get("id") or "")
                            for item in mesh_expression_islands
                        ],
                        "message": (
                            f"{mode} cannot emit mesh-bound expression "
                            "islands. Use Source Mesh Fidelity."
                        ),
                    }
                )
            scheduled_islands = (
                reusable_expression_islands
                if reusable_mode
                else ([] if portable_mode else expression_islands)
            )
            for expression in scheduled_islands:
                    params = (
                        expression.get("params")
                        if isinstance(expression.get("params"), Mapping)
                        else {}
                    )
                    source = (
                        expression.get("source")
                        if isinstance(expression.get("source"), Mapping)
                        else {}
                    )
                    expression_id = str(expression.get("id") or "")
                    jobs.append(
                        {
                            "jobId": f"bake-expression-{expression_id}",
                            "regionId": str(ir.get("id") or ""),
                            "route": (
                                Route.REUSABLE_BAKE.value
                                if expression
                                in reusable_expression_islands
                                else Route.MESH_BAKE.value
                            ),
                            "scope": "ExpressionIsland",
                            "expressionId": expression_id,
                            "resourceId": str(
                                params.get("resourceId") or ""
                            ),
                            "referenceName": str(
                                params.get("referenceName") or ""
                            ),
                            "usage": str(params.get("usage") or "Color"),
                            "channel": str(params.get("channel") or "RGB"),
                            "colorSpace": str(
                                params.get("colorSpace") or "Linear"
                            ),
                            "uvSet": str(params.get("uvSet") or "UV0"),
                            "coordinateDomain": str(
                                params.get("coordinateDomain")
                                or "MeshSurface"
                            ),
                            "meshBindingRequired": bool(
                                params.get("meshBindingRequired", True)
                            ),
                            "sourceNodeId": str(
                                source.get("nodeId") or ""
                            ),
                            "sourceSocketId": str(
                                source.get("socketId") or ""
                            ),
                            "resolution": 1024,
                            "supersampling": 2,
                            "padding": 16,
                            "samples": 16,
                            "randomSeed": 0,
                        }
                    )
            if reusable_expression_islands and reusable_mode:
                diagnostics.append(
                    {
                        "severity": "info",
                        "code": "MIKU_PORTABLE_UV_BAKE_SCHEDULED",
                        "translationQuality": "Baked",
                        "expressionIds": [
                            str(item.get("id") or "")
                            for item in reusable_expression_islands
                        ],
                        "message": (
                            "Scheduled maximal runtime-independent expression "
                            "islands for canonical UV0 baking without source "
                            "mesh binding."
                        ),
                    }
                )
            if not portable_mode and mesh_expression_islands:
                diagnostics.append(
                    {
                        "severity": "info",
                        "code": "MIKU_STATIC_EXPRESSION_ISLAND_BAKE_SCHEDULED",
                        "translationQuality": "Baked",
                        "expressionIds": [
                            str(item.get("id") or "")
                            for item in mesh_expression_islands
                        ],
                        "message": (
                            "Scheduled maximal runtime-independent expression "
                            "islands for mesh-bound baking."
                        ),
                    }
                )
        if has_runtime_expressions and not has_runtime_error:
            diagnostics.append(
                {
                    "severity": "info",
                    "code": "MIKU_RUNTIME_INPUT_PRESERVED",
                    "translationQuality": "Exact",
                    "message": (
                        f"Preserved {len(expressions)} runtime expression(s) "
                        "for native Shader Graph lowering."
                    ),
                }
            )
        parameters = []
        for parameter in ir.get("parameters") or []:
            item = dict(parameter)
            item["influences"] = sorted({str(item.get("semantic") or "")})
            parameters.append(item)
        payload = {
            "materialKey": ir.get("materialKey", ""),
            "targetProfile": profile.get("canonicalHash", ""),
            "mode": mode,
            "routePolicy": "NativeThenReusableThenMesh",
            "surfaceModel": surface_model,
            "surfaceBackend": surface_backends.get(surface_model, ""),
            "surfaceModelPlan": surface_model_plan,
            "closureGraph": ir.get("closureGraph") or {},
            "weightedClosures": ir.get("weightedClosures") or {},
            "regions": region_plans,
            "bakeJobs": jobs,
            "parameters": parameters,
            "diagnostics": diagnostics,
            "completionPolicy": {"requireExitCodeZero": True, "requireCompletionMarker": True, "requireArtifactHashes": True},
        }
        return make_document("miku-conversion-plan-1.0", payload)


def plan_graph(
    graph: Mapping[str, Any],
    *,
    source_blend_id: str = "",
    material_key: str = "",
    mode: str = "Auto",
    fidelity_policy: str = "AllowDeclaredApproximation",
    add_shader_energy_policy: str = "PreserveBlender",
) -> tuple[dict[str, Any], dict[str, Any]]:
    ir = build_material_ir(
        graph,
        source_blend_id=source_blend_id,
        material_key=material_key,
        fidelity_policy=fidelity_policy,
        add_shader_energy_policy=add_shader_energy_policy,
        conversion_mode=mode,
    )
    return ir, ConversionPlanner().plan(ir, mode=mode)
