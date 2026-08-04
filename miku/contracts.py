"""Versioned Miku documents and shared strongly typed vocabulary.

The core deliberately contains no Blender or Unity types.  JSON documents are
canonicalized before hashing so generated assets can be compared byte-for-byte
or by semantic digest without timestamps or unstable dictionary ordering.
"""

from __future__ import annotations

import hashlib
import json
import math
import unicodedata
import uuid
from enum import Enum
from typing import Any, Mapping


class Route(str, Enum):
    NATIVE = "Native"
    REUSABLE_BAKE = "ReusableBake"
    MESH_BAKE = "MeshBake"
    FULL_PBR_BAKE = "FullPBRBake"
    APPEARANCE_SNAPSHOT = "AppearanceSnapshot"
    UNSUPPORTED = "Unsupported"


class Fidelity(str, Enum):
    EXACT = "Exact"
    EQUIVALENT = "Equivalent"
    APPROXIMATE = "Approximate"
    BAKED = "Baked"


class ParameterMutability(str, Enum):
    LIVE = "Live"
    POST_BAKE = "PostBake"
    REBAKE_REQUIRED = "RebakeRequired"
    REBUILD_SHADER_REQUIRED = "RebuildShaderRequired"
    READ_ONLY = "ReadOnly"
    UNSUPPORTED = "Unsupported"


class ParameterScope(str, Enum):
    PER_MATERIAL = "PerMaterial"
    GLOBAL = "Global"
    PER_RENDERER = "PerRenderer"
    EDITOR_ONLY = "EditorOnly"


class ParameterUpdateAction(str, Enum):
    NONE = "None"
    UNITY_ASSET_REBUILD = "UnityAssetRebuild"
    BLENDER_REUSABLE_REBAKE = "BlenderReusableRebake"
    BLENDER_MESH_REBAKE = "BlenderMeshRebake"
    SHADER_GRAPH_REBUILD = "ShaderGraphRebuild"


SUPPORTED_WORKFLOW_KINDS = frozenset(
    {
        "standard_pbr",
        "genshin_toon",
        "wuwa_toon",
        "hsr_toon",
        "endfield_toon",
    }
)
RETIRED_WORKFLOW_KINDS = frozenset({"generic_toon"})
FROZEN_MATERIAL_IR_1_WORKFLOW_KINDS = frozenset(
    {
        "standard_pbr",
        "genshin_toon",
        "wuwa_toon",
        "hsr_toon",
        "generic_toon",
    }
)
# Backward-compatible name for callers that mean currently executable routes.
WORKFLOW_KINDS = SUPPORTED_WORKFLOW_KINDS


DOCUMENT_KINDS = {
    "miku-target-profile-1.0",
    "miku-material-ir-1.0",
    "miku-material-ir-2.0",
    "miku-conversion-plan-1.0",
    "miku-conversion-manifest-1.0",
    "miku-blender-source-map-1.0",
    "miku-bundle-1.0",
    "miku-unity-import-receipt-1.0",
    "miku-bake-request-1.0",
    "miku-bake-request-1.1",
    "miku-bake-request-1.2",
    "miku-bake-result-1.0",
}

_HEADER_KEYS = ("documentKind", "schemaVersion", "toolVersion", "id", "canonicalHash")
TOOL_VERSION = "2.2.9"


class DocumentValidationError(ValueError):
    """A malformed or unknown Miku document."""

    def __init__(self, code: str, message: str, path: str = "$") -> None:
        self.code = code
        self.path = path
        self.message = message
        super().__init__(f"{code} at {path}: {message}")


def _canonical(value: Any) -> Any:
    if isinstance(value, Mapping):
        return {
            unicodedata.normalize("NFC", str(key)): _canonical(value[key])
            for key in sorted(value, key=lambda item: unicodedata.normalize("NFC", str(item)))
        }
    if isinstance(value, (list, tuple)):
        return [_canonical(item) for item in value]
    if isinstance(value, str):
        return unicodedata.normalize("NFC", value)
    if isinstance(value, float):
        if not math.isfinite(value):
            raise DocumentValidationError("MIKU_INVALID_NUMBER", "NaN and Infinity are not allowed")
        # JSON readers such as Newtonsoft.Json do not preserve the sign bit
        # of -0.0. Normalize both zero spellings so hashes are portable.
        if value == 0.0:
            return 0.0
        return value
    if isinstance(value, Enum):
        return value.value
    return value


def canonical_json(value: Any) -> str:
    return json.dumps(
        _canonical(value),
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    )


def canonical_hash(value: Any) -> str:
    return hashlib.sha256(canonical_json(value).encode("utf-8")).hexdigest()


def stable_uuid(namespace: str, name: str) -> str:
    return str(uuid.uuid5(uuid.uuid5(uuid.NAMESPACE_URL, "urn:miku:"), f"{namespace}:{name}"))


def make_document(kind: str, payload: Mapping[str, Any], *, tool_version: str = TOOL_VERSION, document_id: str = "") -> dict[str, Any]:
    if kind not in DOCUMENT_KINDS:
        raise DocumentValidationError("MIKU_UNKNOWN_DOCUMENT_KIND", f"Unsupported document kind: {kind}")
    document = dict(payload)
    document.update(
        {
            "documentKind": kind,
            "schemaVersion": kind.rsplit("-", 1)[-1],
            "toolVersion": tool_version,
            "id": document_id or stable_uuid(kind, canonical_json(payload)),
        }
    )
    document["canonicalHash"] = canonical_hash({key: value for key, value in document.items() if key != "canonicalHash"})
    return document


def validate_document(document: Mapping[str, Any], expected_kind: str | None = None) -> dict[str, Any]:
    if not isinstance(document, Mapping):
        raise DocumentValidationError("MIKU_DOCUMENT_NOT_OBJECT", "Document must be a JSON object")
    missing = [key for key in _HEADER_KEYS if key not in document]
    if missing:
        raise DocumentValidationError("MIKU_DOCUMENT_HEADER_MISSING", f"Missing header fields: {', '.join(missing)}")
    kind = str(document["documentKind"])
    if kind not in DOCUMENT_KINDS:
        raise DocumentValidationError("MIKU_UNKNOWN_SCHEMA", f"Unknown document kind: {kind}")
    if expected_kind and kind != expected_kind:
        raise DocumentValidationError("MIKU_DOCUMENT_KIND_MISMATCH", f"Expected {expected_kind}, got {kind}")
    if "version" in document:
        raise DocumentValidationError(
            "MIKU_LEGACY_VERSION_FIELD",
            "Root version is retired; documentKind and schemaVersion are authoritative",
            "$.version",
        )
    expected_version = kind.rsplit("-", 1)[-1]
    if str(document["schemaVersion"]) != expected_version:
        raise DocumentValidationError("MIKU_SCHEMA_VERSION_MISMATCH", f"Expected {expected_version}")
    if not isinstance(document["id"], str) or not document["id"]:
        raise DocumentValidationError("MIKU_DOCUMENT_ID_INVALID", "id must be a non-empty string")
    actual = canonical_hash({key: value for key, value in document.items() if key != "canonicalHash"})
    if str(document["canonicalHash"]) != actual:
        raise DocumentValidationError("MIKU_CANONICAL_HASH_MISMATCH", "canonicalHash does not match document content")
    if kind in {"miku-material-ir-1.0", "miku-material-ir-2.0"}:
        workflow = document.get("workflow")
        workflow_kind = workflow.get("kind") if isinstance(workflow, Mapping) else None
        allowed_workflows = (
            FROZEN_MATERIAL_IR_1_WORKFLOW_KINDS
            if kind == "miku-material-ir-1.0"
            else SUPPORTED_WORKFLOW_KINDS
        )
        if workflow_kind not in allowed_workflows:
            raise DocumentValidationError(
                "MIKU_WORKFLOW_INVALID",
                "workflow.kind must name one supported Miku workflow",
                "$.workflow.kind",
            )
        workflow_part = workflow.get("part") if isinstance(workflow, Mapping) else None
        if workflow_kind in {
            "genshin_toon",
            "wuwa_toon",
            "hsr_toon",
            "endfield_toon",
        }:
            if workflow_kind == "endfield_toon":
                allowed_parts = {
                    "Body",
                    "Skin",
                    "Hair",
                    "Face",
                    "Eye",
                    "Mouth",
                    "Overlay",
                    "Effect",
                    "HairShadow",
                }
            elif workflow_kind == "wuwa_toon":
                allowed_parts = {"Body", "Hair", "Face", "Eye", "Effect"}
            else:
                allowed_parts = {"Body", "Hair", "Face", "Eye"}
            if workflow_part not in allowed_parts:
                raise DocumentValidationError(
                    "MIKU_WORKFLOW_PART_INVALID",
                    "Game workflows require one supported workflow part",
                    "$.workflow.part",
                )
        elif workflow_part is not None:
            raise DocumentValidationError(
                "MIKU_WORKFLOW_PART_NOT_APPLICABLE",
                "workflow.part is only valid for game workflows",
                "$.workflow.part",
            )
        _validate_surface_contract(document)
        _validate_closure_aware_material(document)
        _validate_material_expressions(document)
    return dict(document)


def _validate_closure_aware_material(document: Mapping[str, Any]) -> None:
    closure_graph = document.get("closureGraph")
    if not isinstance(closure_graph, Mapping):
        raise DocumentValidationError(
            "MIKU_CLOSURE_GRAPH_MISSING",
            "Miku MaterialIR 1.0 requires closureGraph",
            "$.closureGraph",
        )
    if closure_graph.get("schema") != "miku-closure-1.0":
        raise DocumentValidationError(
            "MIKU_CLOSURE_SCHEMA_UNKNOWN",
            str(closure_graph.get("schema") or "<missing>"),
            "$.closureGraph.schema",
        )
    root = closure_graph.get("root")
    if not isinstance(root, Mapping):
        raise DocumentValidationError(
            "MIKU_CLOSURE_ROOT_MISSING",
            "closureGraph.root must be an object",
            "$.closureGraph.root",
        )
    root_id = str(root.get("id") or "")
    if not root_id or str(closure_graph.get("rootClosureId") or "") != root_id:
        raise DocumentValidationError(
            "MIKU_CLOSURE_ROOT_ID_MISMATCH",
            "rootClosureId must reference closureGraph.root.id",
            "$.closureGraph.rootClosureId",
        )
    _validate_closure_node(root, "$.closureGraph.root", set())

    weighted = document.get("weightedClosures")
    if not isinstance(weighted, Mapping):
        raise DocumentValidationError(
            "MIKU_WEIGHTED_CLOSURES_MISSING",
            "Miku MaterialIR 1.0 requires weightedClosures",
            "$.weightedClosures",
        )
    if weighted.get("schema") != "miku-weighted-closures-1.0":
        raise DocumentValidationError(
            "MIKU_WEIGHTED_CLOSURE_SCHEMA_UNKNOWN",
            str(weighted.get("schema") or "<missing>"),
            "$.weightedClosures.schema",
        )
    term_ids: set[str] = set()
    for index, term in enumerate(weighted.get("terms") or []):
        path = f"$.weightedClosures.terms[{index}]"
        if not isinstance(term, Mapping):
            raise DocumentValidationError(
                "MIKU_WEIGHTED_CLOSURE_TERM_INVALID",
                "term must be an object",
                path,
            )
        term_id = str(term.get("id") or "")
        if not term_id:
            raise DocumentValidationError(
                "MIKU_WEIGHTED_CLOSURE_TERM_ID_INVALID",
                "term id must be non-empty",
                f"{path}.id",
            )
        if term_id in term_ids:
            raise DocumentValidationError(
                "MIKU_WEIGHTED_CLOSURE_TERM_ID_DUPLICATE",
                term_id,
                f"{path}.id",
            )
        term_ids.add(term_id)
        final_weight = term.get("finalWeight")
        if not isinstance(final_weight, Mapping):
            raise DocumentValidationError(
                "MIKU_WEIGHT_EXPRESSION_MISSING",
                "term requires finalWeight",
                f"{path}.finalWeight",
            )
        _validate_weight_expression(
            final_weight,
            f"{path}.finalWeight",
            set(),
        )

    surface_plan = document.get("surfaceModelPlan")
    if not isinstance(surface_plan, Mapping):
        raise DocumentValidationError(
            "MIKU_SURFACE_MODEL_PLAN_MISSING",
            "Miku MaterialIR 1.0 requires surfaceModelPlan",
            "$.surfaceModelPlan",
        )
    if surface_plan.get("schema") != "miku-surface-model-plan-1.0":
        raise DocumentValidationError(
            "MIKU_SURFACE_MODEL_PLAN_SCHEMA_UNKNOWN",
            str(surface_plan.get("schema") or "<missing>"),
            "$.surfaceModelPlan.schema",
        )
    allowed_models = {
        "OpaquePBR",
        "CutoutPBR",
        "TransparentLit",
        "TransparentEmission",
        "RefractiveGlass",
        "CustomMultiLobe",
        "UnsupportedSurface",
    }
    if surface_plan.get("kind") not in allowed_models:
        raise DocumentValidationError(
            "MIKU_SURFACE_MODEL_KIND_INVALID",
            str(surface_plan.get("kind") or "<missing>"),
            "$.surfaceModelPlan.kind",
        )
    plan_term_ids = set(
        str(item)
        for item in (
            (surface_plan.get("closureLoweringPlan") or {}).get(
                "weightedTermIds"
            )
            or []
        )
    )
    if plan_term_ids - term_ids:
        raise DocumentValidationError(
            "MIKU_SURFACE_PLAN_TERM_REFERENCE_MISSING",
            ", ".join(sorted(plan_term_ids - term_ids)),
            "$.surfaceModelPlan.closureLoweringPlan.weightedTermIds",
        )


def _validate_closure_node(
    closure: Mapping[str, Any],
    path: str,
    visiting: set[str],
) -> None:
    closure_id = str(closure.get("id") or "")
    if not closure_id:
        raise DocumentValidationError(
            "MIKU_CLOSURE_ID_INVALID",
            "closure id must be non-empty",
            f"{path}.id",
        )
    if closure_id in visiting:
        raise DocumentValidationError(
            "MIKU_CLOSURE_GRAPH_CYCLE",
            closure_id,
            path,
        )
    kind = str(closure.get("kind") or "")
    if kind not in {
        "Null",
        "Principled",
        "Diffuse",
        "Glossy",
        "Metallic",
        "Emission",
        "Transparent",
        "Glass",
        "Refraction",
        "Translucent",
        "SubsurfaceScattering",
        "Sheen",
        "Volume",
        "Holdout",
        "Mix",
        "Add",
        "ShaderToRgbBarrier",
        "Unsupported",
    }:
        raise DocumentValidationError(
            "MIKU_CLOSURE_KIND_INVALID",
            kind or "<missing>",
            f"{path}.kind",
        )
    if kind == "Null":
        return
    if kind not in {"Mix", "Add"}:
        local_weight = closure.get("localWeight")
        if not isinstance(local_weight, Mapping):
            raise DocumentValidationError(
                "MIKU_CLOSURE_LOCAL_WEIGHT_MISSING",
                "leaf closure requires localWeight",
                f"{path}.localWeight",
            )
        _validate_weight_expression(
            local_weight,
            f"{path}.localWeight",
            set(),
        )
        return
    nested_visiting = {*visiting, closure_id}
    for key in ("first", "second"):
        child = closure.get(key)
        if not isinstance(child, Mapping):
            raise DocumentValidationError(
                "MIKU_CLOSURE_CHILD_MISSING",
                key,
                f"{path}.{key}",
            )
        _validate_closure_node(child, f"{path}.{key}", nested_visiting)
    if kind == "Mix":
        factor = closure.get("factor")
        if not isinstance(factor, Mapping):
            raise DocumentValidationError(
                "MIKU_MIX_FACTOR_MISSING",
                "Mix closure requires factor",
                f"{path}.factor",
            )
        _validate_weight_expression(factor, f"{path}.factor", set())
        conversion = closure.get("factorConversion")
        if not isinstance(conversion, Mapping):
            raise DocumentValidationError(
                "MIKU_MIX_FACTOR_CONVERSION_MISSING",
                "Mix closure requires factorConversion",
                f"{path}.factorConversion",
            )


def _validate_weight_expression(
    expression: Mapping[str, Any],
    path: str,
    visiting: set[str],
) -> None:
    expression_id = str(expression.get("id") or "")
    if not expression_id:
        raise DocumentValidationError(
            "MIKU_WEIGHT_EXPRESSION_ID_INVALID",
            "weight expression id must be non-empty",
            f"{path}.id",
        )
    if expression_id in visiting:
        raise DocumentValidationError(
            "MIKU_WEIGHT_EXPRESSION_CYCLE",
            expression_id,
            path,
        )
    kind = str(expression.get("kind") or "")
    allowed = {
        "Constant",
        "Parameter",
        "Texture",
        "Math",
        "ViewDependent",
        "LayerWeight",
        "Fresnel",
        "Add",
        "Multiply",
        "OneMinus",
        "Clamp",
        "ImplicitConversion",
    }
    if kind not in allowed:
        raise DocumentValidationError(
            "MIKU_WEIGHT_EXPRESSION_KIND_INVALID",
            kind or "<missing>",
            f"{path}.kind",
        )
    nested_visiting = {*visiting, expression_id}
    if kind in {"OneMinus", "Clamp", "ImplicitConversion"}:
        child = expression.get("input")
        if not isinstance(child, Mapping):
            raise DocumentValidationError(
                "MIKU_WEIGHT_EXPRESSION_INPUT_MISSING",
                kind,
                f"{path}.input",
            )
        _validate_weight_expression(child, f"{path}.input", nested_visiting)
    if kind in {"Add", "Multiply"}:
        children = expression.get("inputs")
        if not isinstance(children, list) or not children:
            raise DocumentValidationError(
                "MIKU_WEIGHT_EXPRESSION_INPUTS_INVALID",
                kind,
                f"{path}.inputs",
            )
        for index, child in enumerate(children):
            if not isinstance(child, Mapping):
                raise DocumentValidationError(
                    "MIKU_WEIGHT_EXPRESSION_INPUT_INVALID",
                    kind,
                    f"{path}.inputs[{index}]",
                )
            _validate_weight_expression(
                child,
                f"{path}.inputs[{index}]",
                nested_visiting,
            )


def _validate_surface_contract(document: Mapping[str, Any]) -> None:
    contract = document.get("surfaceContract")
    if contract is None:
        return
    if not isinstance(contract, Mapping):
        raise DocumentValidationError(
            "MIKU_SURFACE_CONTRACT_INVALID",
            "surfaceContract must be an object",
            "$.surfaceContract",
        )
    allowed = {
        "schema",
        "model",
        "renderMethod",
        "renderFace",
        "coverageChannel",
        "transmissionColorChannel",
        "transmissionWeightChannel",
        "iorChannel",
        "thicknessChannel",
        "roughnessChannel",
        "normalChannel",
    }
    unknown = sorted(set(contract) - allowed)
    if unknown:
        raise DocumentValidationError(
            "MIKU_SURFACE_CONTRACT_FIELD_UNKNOWN",
            ", ".join(unknown),
            "$.surfaceContract",
        )
    if contract.get("schema") != "miku-surface-1.0":
        raise DocumentValidationError(
            "MIKU_SURFACE_SCHEMA_UNKNOWN",
            str(contract.get("schema") or "<missing>"),
            "$.surfaceContract.schema",
        )
    enum_fields = {
        "model": {"StandardLit", "DielectricScreenRefraction"},
        "renderMethod": {"Opaque", "AlphaBlend", "Dithered"},
        "renderFace": {"Front", "Back", "Both"},
    }
    for field, values in enum_fields.items():
        if contract.get(field) not in values:
            raise DocumentValidationError(
                "MIKU_SURFACE_CONTRACT_INVALID",
                f"{field} must be one of {', '.join(sorted(values))}",
                f"$.surfaceContract.{field}",
            )
    channels = {
        str(channel.get("semantic") or ""): channel
        for channel in document.get("channels", []) or []
        if isinstance(channel, Mapping) and str(channel.get("semantic") or "")
    }
    reference_fields = ["coverageChannel"]
    if contract.get("model") == "DielectricScreenRefraction":
        reference_fields.extend(
            [
                "transmissionColorChannel",
                "transmissionWeightChannel",
                "iorChannel",
                "thicknessChannel",
                "roughnessChannel",
                "normalChannel",
            ]
        )
    for field in reference_fields:
        semantic = str(contract.get(field) or "")
        if not semantic or semantic not in channels:
            raise DocumentValidationError(
                "MIKU_SURFACE_CHANNEL_REFERENCE_MISSING",
                semantic or "<empty>",
                f"$.surfaceContract.{field}",
            )
    if str(channels[str(contract["coverageChannel"])].get("valueType") or "") != "Scalar":
        raise DocumentValidationError(
            "MIKU_SURFACE_COVERAGE_TYPE_INVALID",
            "coverageChannel must reference a Scalar channel",
            "$.surfaceContract.coverageChannel",
        )


def _validate_material_expressions(document: Mapping[str, Any]) -> None:
    expressions = document.get("expressions") or []
    if not isinstance(expressions, list):
        raise DocumentValidationError(
            "MIKU_EXPRESSION_SET_INVALID",
            "expressions must be an array",
            "$.expressions",
        )
    by_id: dict[str, Mapping[str, Any]] = {}
    for index, expression in enumerate(expressions):
        if not isinstance(expression, Mapping):
            raise DocumentValidationError(
                "MIKU_EXPRESSION_INVALID",
                "expression must be an object",
                f"$.expressions[{index}]",
            )
        expression_id = str(expression.get("id") or "")
        if not expression_id:
            raise DocumentValidationError(
                "MIKU_EXPRESSION_ID_INVALID",
                "expression id must be non-empty",
                f"$.expressions[{index}].id",
            )
        if expression_id in by_id:
            raise DocumentValidationError(
                "MIKU_EXPRESSION_ID_DUPLICATE",
                expression_id,
                f"$.expressions[{index}].id",
            )
        by_id[expression_id] = expression
        op = str(expression.get("op") or "")
        stage = str(expression.get("stage") or "")
        if (
            op.startswith("Input.Camera.")
            or op == "Vector.NormalFromHeight"
        ) and stage != "Fragment":
            raise DocumentValidationError(
                "shader_stage_conflict",
                f"{op} is fragment-only",
                f"$.expressions[{index}].stage",
            )

    references: dict[str, list[str]] = {}
    for expression_id, expression in by_id.items():
        inputs = expression.get("inputs") or {}
        if not isinstance(inputs, Mapping):
            raise DocumentValidationError(
                "MIKU_EXPRESSION_INPUTS_INVALID",
                "inputs must be an object",
            )
        references[expression_id] = []
        for name, reference in inputs.items():
            reference_id = (
                str(reference.get("expressionId") or "")
                if isinstance(reference, Mapping)
                else ""
            )
            if reference_id not in by_id:
                raise DocumentValidationError(
                    "MIKU_EXPRESSION_REFERENCE_MISSING",
                    f"{expression_id}.{name} references {reference_id or '<empty>'}",
                )
            references[expression_id].append(reference_id)

    visiting: set[str] = set()
    visited: set[str] = set()

    def visit(expression_id: str) -> None:
        if expression_id in visiting:
            raise DocumentValidationError(
                "MIKU_EXPRESSION_CYCLE",
                expression_id,
            )
        if expression_id in visited:
            return
        visiting.add(expression_id)
        for reference_id in references.get(expression_id, ()):
            visit(reference_id)
        visiting.remove(expression_id)
        visited.add(expression_id)

    for expression_id in sorted(by_id):
        visit(expression_id)

    for index, channel in enumerate(document.get("channels") or []):
        if not isinstance(channel, Mapping):
            continue
        value = channel.get("value")
        if not isinstance(value, Mapping) or value.get("kind") != "Expression":
            continue
        expression_id = str(value.get("expressionId") or "")
        if expression_id not in by_id:
            raise DocumentValidationError(
                "MIKU_EXPRESSION_REFERENCE_MISSING",
                expression_id,
                f"$.channels[{index}].value.expressionId",
            )
        if str(channel.get("stage") or "Fragment") != "Vertex":
            continue
        pending = [expression_id]
        seen: set[str] = set()
        while pending:
            current = pending.pop()
            if current in seen:
                continue
            seen.add(current)
            if str(by_id[current].get("stage") or "") == "Fragment":
                raise DocumentValidationError(
                    "shader_stage_conflict",
                    "A fragment-only expression cannot feed a vertex channel",
                    f"$.channels[{index}]",
                )
            pending.extend(references.get(current, ()))
