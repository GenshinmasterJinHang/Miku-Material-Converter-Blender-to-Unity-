"""Explicit, deterministic MiGR 1.x/2.x to Miku 2.0 migrations."""

from __future__ import annotations

from typing import Any, Mapping

from .closure_ir import constant_weight
from .contracts import (
    DocumentValidationError,
    canonical_hash,
    make_document,
    stable_uuid,
    validate_document,
)
from .surface_models import build_surface_model_plan


_LEGACY_KIND_TO_MIKU_1 = {
    "migr-target-profile-1.0": "miku-target-profile-1.0",
    "migr-target-profile-2.0": "miku-target-profile-1.0",
    "migr-material-ir-1.0": "miku-material-ir-2.0",
    "migr-material-ir-2.0": "miku-material-ir-2.0",
    "migr-conversion-plan-1.0": "miku-conversion-plan-1.0",
    "migr-conversion-plan-2.0": "miku-conversion-plan-1.0",
    "migr-conversion-manifest-1.0": "miku-conversion-manifest-1.0",
    "migr-conversion-manifest-2.0": "miku-conversion-manifest-1.0",
    "migr-blender-source-map-1.0": "miku-blender-source-map-1.0",
    "migr-bundle-1.0": "miku-bundle-1.0",
    "migr-bundle-2.0": "miku-bundle-1.0",
    "migr-bundle-2.1": "miku-bundle-1.0",
    "migr-bundle-2.2": "miku-bundle-1.0",
    "migr-unity-import-receipt-1.0": "miku-unity-import-receipt-1.0",
    "migr-bake-request-1.0": "miku-bake-request-1.0",
    "migr-bake-result-1.0": "miku-bake-result-1.0",
}


def validate_legacy_document(document: Mapping[str, Any]) -> dict[str, Any]:
    """Validate a frozen MiGR header and canonical hash without writing it."""

    if not isinstance(document, Mapping):
        raise DocumentValidationError(
            "MIKU_DOCUMENT_NOT_OBJECT",
            "Legacy document must be a JSON object",
        )
    required = (
        "documentKind",
        "schemaVersion",
        "toolVersion",
        "id",
        "canonicalHash",
    )
    missing = [key for key in required if key not in document]
    if missing:
        raise DocumentValidationError(
            "MIKU_DOCUMENT_HEADER_MISSING",
            f"Missing legacy header fields: {', '.join(missing)}",
        )
    kind = str(document["documentKind"])
    if kind not in _LEGACY_KIND_TO_MIKU_1:
        raise DocumentValidationError(
            "MIKU_UNKNOWN_SCHEMA",
            f"Unsupported MiGR document kind: {kind}",
        )
    expected_version = kind.rsplit("-", 1)[-1]
    if str(document["schemaVersion"]) != expected_version:
        raise DocumentValidationError(
            "MIKU_SCHEMA_VERSION_MISMATCH",
            f"Expected legacy schemaVersion {expected_version}",
        )
    if "version" in document:
        raise DocumentValidationError(
            "MIKU_LEGACY_VERSION_FIELD",
            "Root version is not a valid MiGR document header",
            "$.version",
        )
    if not isinstance(document["id"], str) or not document["id"]:
        raise DocumentValidationError(
            "MIKU_DOCUMENT_ID_INVALID",
            "Legacy id must be a non-empty string",
        )
    actual = canonical_hash(
        {
            key: value
            for key, value in document.items()
            if key != "canonicalHash"
        }
    )
    if str(document["canonicalHash"]) != actual:
        raise DocumentValidationError(
            "MIKU_CANONICAL_HASH_MISMATCH",
            "Legacy canonicalHash does not match document content",
        )
    return dict(document)


def normalize_legacy_document(
    document: Mapping[str, Any],
) -> dict[str, Any]:
    """Normalize one validated MiGR document into a newly hashed Miku document."""

    source = validate_legacy_document(document)
    kind = str(source["documentKind"])
    if kind.startswith("migr-material-ir-"):
        workflow = source.get("workflow")
        workflow_kind = (
            workflow.get("kind") if isinstance(workflow, Mapping) else ""
        )
        if workflow_kind == "generic_toon":
            raise DocumentValidationError(
                "MIKU_WORKFLOW_RETIRED",
                "The Generic Toon workflow was retired in Miku 2.0",
                "$.workflow.kind",
            )
    if kind == "migr-material-ir-1.0":
        return _migrate_legacy_material_ir_1_0(source)
    payload = {
        key: _normalize_legacy_value(value)
        for key, value in source.items()
        if key
        not in {
            "documentKind",
            "schemaVersion",
            "toolVersion",
            "id",
            "canonicalHash",
        }
    }
    result = make_document(
        _LEGACY_KIND_TO_MIKU_1[kind],
        payload,
        document_id=str(source["id"]),
    )
    if result["documentKind"] == "miku-material-ir-2.0":
        validate_document(result, "miku-material-ir-2.0")
    return result


def migrate_legacy_material_ir(
    document: Mapping[str, Any],
) -> dict[str, Any]:
    """Migrate a frozen MiGR MaterialIR document to Miku MaterialIR 2.0."""

    source = validate_legacy_document(document)
    if not str(source["documentKind"]).startswith("migr-material-ir-"):
        raise DocumentValidationError(
            "MIKU_DOCUMENT_KIND_MISMATCH",
            "Expected a MiGR MaterialIR document",
        )
    workflow = source.get("workflow")
    if isinstance(workflow, Mapping) and workflow.get("kind") == "generic_toon":
        raise DocumentValidationError(
            "MIKU_WORKFLOW_RETIRED",
            "The Generic Toon workflow was retired in Miku 2.0",
            "$.workflow.kind",
        )
    return normalize_legacy_document(source)


def _migrate_legacy_material_ir_1_0(
    document: Mapping[str, Any],
) -> dict[str, Any]:
    """Migrate the proved ordinary Standard PBR subset.

    MiGR 1.0 did not retain closure topology. Reconstructing transparent,
    dielectric, or recursively mixed closures would invent semantics, so those
    documents remain on the frozen v1 compatibility reader.
    """

    source = validate_legacy_document(document)
    if source["documentKind"] != "migr-material-ir-1.0":
        raise DocumentValidationError(
            "MIKU_DOCUMENT_KIND_MISMATCH",
            "Expected migr-material-ir-1.0",
        )
    contract = source.get("surfaceContract")
    contract = contract if isinstance(contract, Mapping) else {}
    model = str(contract.get("model") or "StandardLit")
    render_method = str(contract.get("renderMethod") or "Opaque")
    if model != "StandardLit" or render_method != "Opaque":
        raise DocumentValidationError(
            "MIKU_LEGACY_SURFACE_MIGRATION_UNSAFE",
            (
                "Only ordinary opaque StandardLit v1 documents can be "
                "reconstructed without inventing closure topology"
            ),
            "$.surfaceContract",
        )
    material_id = str(source.get("id") or "") or stable_uuid(
        "miku-material",
        str(source.get("materialKey") or "Material"),
    )
    parameters = _principled_parameters(source.get("channels") or [])
    closure_id = stable_uuid(
        "miku-closure",
        f"v1-standard-pbr:{material_id}",
    )
    local_weight = constant_weight(1.0)
    root = {
        "id": closure_id,
        "kind": "Principled",
        "domain": "SurfaceScattering",
        "operation": "Migration.StandardPBR",
        "sourceNodeId": "",
        "sourceSocketId": "",
        "groupPath": [],
        "sourceSocketOrder": [],
        "localWeight": local_weight,
        "parameters": parameters,
        "distribution": "MULTI_GGX",
    }
    closure_graph = {
        "schema": "miku-closure-1.0",
        "root": root,
        "rootClosureId": closure_id,
        "diagnostics": [],
    }
    term_id = stable_uuid(
        "miku-weighted-closure",
        f"v1-standard-pbr:{material_id}",
    )
    weighted = {
        "schema": "miku-weighted-closures-1.0",
        "terms": [
            {
                "id": term_id,
                "closureId": closure_id,
                "closureKind": "Principled",
                "domain": "SurfaceScattering",
                "parameters": parameters,
                "distribution": "MULTI_GGX",
                "finalWeight": local_weight,
                "weightTrace": [
                    {
                        "operation": "V1StandardPbrMigration",
                        "resultWeightId": local_weight["id"],
                    }
                ],
                "source": {
                    "nodeId": "",
                    "socketId": "",
                    "groupPath": [],
                },
            }
        ],
        "simplifications": [],
        "approximations": [],
    }
    surface_plan = build_surface_model_plan(
        material_id,
        closure_graph,
        weighted,
    )
    surface_plan["rootClosure"] = {"closureId": closure_id}
    payload = {
        key: value
        for key, value in source.items()
        if key
        not in {
            "documentKind",
            "schemaVersion",
            "toolVersion",
            "canonicalHash",
            "id",
            "surfaceContract",
        }
    }
    expressions = list(source.get("expressions") or [])
    payload.update(
        {
            "valueGraph": {
                "schema": "miku-value-graph-1.0",
                "expressions": expressions,
            },
            "expressions": expressions,
            "closureGraph": closure_graph,
            "weightedClosures": weighted,
            "surfaceModelPlan": surface_plan,
            "migration": {
                "fromDocumentKind": "migr-material-ir-1.0",
                "algorithmVersion": "miku-legacy-standard-pbr-1.0",
                "semanticProof": "OpaqueStandardLitChannelProjection",
            },
            "diagnostics": [
                *list(source.get("diagnostics") or []),
                {
                    "severity": "info",
                    "code": "MIKU_LEGACY_STANDARD_PBR_MIGRATED",
                    "translationQuality": "Equivalent",
                    "message": (
                        "Reconstructed one opaque Principled closure from "
                        "the v1 Standard PBR channel contract."
                    ),
                },
            ],
        }
    )
    return make_document(
        "miku-material-ir-2.0",
        payload,
        document_id=material_id,
    )


def migrate_legacy_manifest(
    manifest: Mapping[str, Any],
    *,
    material_ir_v2: Mapping[str, Any],
    conversion_plan_v2: Mapping[str, Any],
) -> dict[str, Any]:
    source = validate_legacy_document(manifest)
    if source["documentKind"] != "migr-conversion-manifest-1.0":
        raise DocumentValidationError(
            "MIKU_DOCUMENT_KIND_MISMATCH",
            "Expected migr-conversion-manifest-1.0",
        )
    validate_document(material_ir_v2, "miku-material-ir-2.0")
    validate_document(conversion_plan_v2, "miku-conversion-plan-1.0")
    payload = {
        key: value
        for key, value in source.items()
        if key
        not in {
            "documentKind",
            "schemaVersion",
            "toolVersion",
            "canonicalHash",
            "id",
            "irHash",
            "planHash",
        }
    }
    payload.update(
        {
            "irHash": material_ir_v2["canonicalHash"],
            "planHash": conversion_plan_v2["canonicalHash"],
            "surfaceModel": material_ir_v2["surfaceModelPlan"],
            "closureGraph": material_ir_v2["closureGraph"],
            "weightedClosures": material_ir_v2["weightedClosures"],
            "migration": {
                "fromDocumentKind": "migr-conversion-manifest-1.0",
                "algorithmVersion": "miku-legacy-standard-pbr-1.0",
            },
        }
    )
    return make_document(
        "miku-conversion-manifest-1.0",
        payload,
        document_id=str(source.get("id") or ""),
    )


def _normalize_legacy_value(value: Any) -> Any:
    if isinstance(value, Mapping):
        result = {}
        for key, item in value.items():
            if (
                key == "schema"
                and isinstance(item, str)
                and item.startswith("migr-")
            ):
                family = item.rsplit("-", 1)[0].replace("migr-", "miku-", 1)
                result[key] = family + "-1.0"
            elif (
                key == "code"
                and isinstance(item, str)
                and item.startswith("MIGR_")
            ):
                result[key] = "MIKU_" + item[len("MIGR_") :]
            else:
                result[key] = _normalize_legacy_value(item)
        return result
    if isinstance(value, list):
        return [_normalize_legacy_value(item) for item in value]
    return value


def _principled_parameters(
    channels: Any,
) -> dict[str, dict[str, Any]]:
    semantic_to_parameter = {
        "BaseColor": "Base Color",
        "Metalness": "Metallic",
        "Roughness": "Roughness",
        "Normal": "Normal",
        "Emission": "Emission Color",
        "Alpha": "Alpha",
        "TransmissionWeight": "Transmission Weight",
        "IOR": "IOR",
    }
    result: dict[str, dict[str, Any]] = {}
    for channel in channels if isinstance(channels, list) else []:
        if not isinstance(channel, Mapping):
            continue
        semantic = str(channel.get("semantic") or "")
        parameter = semantic_to_parameter.get(semantic)
        if not parameter:
            continue
        value = channel.get("value")
        if isinstance(value, Mapping):
            if value.get("kind") == "Constant":
                result[parameter] = {
                    "kind": "Constant",
                    "valueType": str(channel.get("valueType") or "Float"),
                    "value": value.get("value"),
                    "source": {"migration": "v1-channel"},
                }
            else:
                result[parameter] = {
                    "kind": "ValueExpression",
                    "valueType": str(channel.get("valueType") or "Float"),
                    "source": {
                        "expressionId": str(value.get("expressionId") or "")
                    },
                }
            continue
        result[parameter] = {
            "kind": "Constant",
            "valueType": str(channel.get("valueType") or "Float"),
            "value": channel.get("default"),
            "source": {"migration": "v1-channel"},
        }
    defaults = {
        "Base Color": ("Color", [0.8, 0.8, 0.8, 1.0]),
        "Metallic": ("Float", 0.0),
        "Roughness": ("Float", 0.5),
        "Normal": ("Vector3", [0.0, 0.0, 1.0]),
        "Emission Color": ("Color", [0.0, 0.0, 0.0, 1.0]),
        "Alpha": ("Float", 1.0),
        "Transmission Weight": ("Float", 0.0),
        "IOR": ("Float", 1.5),
    }
    for name, (value_type, value) in defaults.items():
        result.setdefault(
            name,
            {
                "kind": "Constant",
                "valueType": value_type,
                "value": value,
                "source": {"migration": "default"},
            },
        )
    return result
