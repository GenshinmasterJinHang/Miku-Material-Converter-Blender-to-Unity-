"""Strict Miku bundle references, seals, and TOCTOU-safe staging."""

from __future__ import annotations

import hashlib
import math
import os
import re
import shutil
import stat
import unicodedata
from pathlib import Path
from typing import Any, Iterable, Mapping

from .contracts import DocumentValidationError, canonical_hash, validate_document
from .fixed_workflows import FIXED_TEXTURE_ROLES


MAX_RESOURCE_COUNT = 64
MAX_RESOURCE_BYTES = 256 * 1024 * 1024
MAX_BUNDLE_BYTES = 2 * 1024 * 1024 * 1024
MAX_IMAGE_DIMENSION = 4096
MAX_DECODED_IMAGE_BYTES = 512 * 1024 * 1024
_GUID_RE = re.compile(r"^[0-9a-f]{64}$")
_DRIVE_RE = re.compile(r"^[a-zA-Z]:")
_RESERVED_NAMES = {
    "CON",
    "PRN",
    "AUX",
    "NUL",
    *(f"COM{index}" for index in range(1, 10)),
    *(f"LPT{index}" for index in range(1, 10)),
}
_DOCUMENT_MEDIA_TYPES = {"application/json"}
_RESOURCE_MEDIA_TYPES = {
    "image/png",
    "image/jpeg",
    "image/x-exr",
    "model/gltf-binary",
}
_SEMANTICS = {
    "BaseColor",
    "Metalness",
    "Roughness",
    "Normal",
    "Emission",
    "EmissionMask",
    "Alpha",
    "IOR",
    "AmbientOcclusion",
    "Height",
    "ExpressionIsland",
    "FixedWorkflowTexture",
    "SourceMesh",
}
_EXPRESSION_ISLAND_USAGES = {"Color", "Scalar", "Normal"}
_NORMAL_CONVENTIONS = {
    "TangentOpenGLPositiveY",
    "TangentDirectXNegativeY",
}
_PACKABLE_SCALAR_SEMANTICS = {
    "Metalness",
    "Roughness",
    "AmbientOcclusion",
    "Height",
    "Alpha",
    "EmissionMask",
}
_RESOURCE_CHANNELS = {"R", "G", "B", "A", "RGB"}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def normalize_relative_path(value: str) -> str:
    value = unicodedata.normalize("NFC", str(value or "")).replace("\\", "/")
    if not value or value.startswith("/") or _DRIVE_RE.match(value):
        raise DocumentValidationError("MIKU_ARTIFACT_PATH_INVALID", "Artifact path must be relative")
    parts = value.split("/")
    if any(part in {"", ".", ".."} for part in parts):
        raise DocumentValidationError("MIKU_ARTIFACT_PATH_INVALID", "Artifact path is not normalized")
    for part in parts:
        stem = part.rstrip(" .").split(".", 1)[0].upper()
        if part != part.rstrip(" .") or stem in _RESERVED_NAMES:
            raise DocumentValidationError("MIKU_ARTIFACT_PATH_RESERVED", f"Reserved path segment: {part}")
    return "/".join(parts)


def make_file_reference(root: Path, path: Path, *, media_type: str) -> dict[str, Any]:
    root = root.resolve()
    path = path.resolve()
    try:
        relative = path.relative_to(root).as_posix()
    except ValueError as exc:
        raise DocumentValidationError("MIKU_ARTIFACT_PATH_ESCAPE", "Artifact is outside its bundle root") from exc
    size = path.stat().st_size
    return {
        "relativePath": normalize_relative_path(relative),
        "sha256": sha256_file(path),
        "byteLength": size,
        "mediaType": media_type,
    }


def compute_sealed_digest(bundle: Mapping[str, Any]) -> str:
    references = []
    for key in ("ir", "plan", "manifest", "sourceMap"):
        reference = bundle.get(key)
        if isinstance(reference, Mapping):
            references.append(
                {
                    "role": key,
                    "relativePath": reference.get("relativePath", ""),
                    "sha256": reference.get("sha256", ""),
                    "byteLength": reference.get("byteLength", -1),
                }
            )
    for reference in bundle.get("resources", []) or []:
        if isinstance(reference, Mapping):
            references.append(
                {
                    "role": f"resource:{reference.get('id', '')}",
                    "relativePath": reference.get("relativePath", ""),
                    "sha256": reference.get("sha256", ""),
                    "byteLength": reference.get("byteLength", -1),
                }
            )
    payload = {
        "materialKey": bundle.get("materialKey", ""),
        "persistentSourceId": bundle.get("persistentSourceId", ""),
        "persistentMaterialId": bundle.get("persistentMaterialId", ""),
        "targetProfileHash": bundle.get("targetProfileHash", ""),
        "artifacts": sorted(references, key=lambda item: (str(item["role"]), str(item["relativePath"]))),
    }
    return canonical_hash(payload)


def validate_bundle_document(bundle: Mapping[str, Any]) -> dict[str, Any]:
    kind = str(bundle.get("documentKind") or "")
    if kind != "miku-bundle-1.0":
        raise DocumentValidationError(
            "MIKU_UNKNOWN_SCHEMA",
            f"Unsupported bundle kind: {kind or '<missing>'}",
        )
    value = validate_document(bundle, kind)
    for field in ("materialKey", "persistentSourceId", "persistentMaterialId"):
        if not isinstance(value.get(field), str) or not value[field]:
            raise DocumentValidationError("MIKU_BUNDLE_IDENTITY_MISSING", f"{field} is required", f"$.{field}")
    target_profile = str(value.get("targetProfileHash") or "")
    if not _GUID_RE.fullmatch(target_profile) or "pending" in target_profile:
        raise DocumentValidationError("MIKU_TARGET_PROFILE_INVALID", "A verified target profile hash is required")
    document_refs = []
    for field in ("ir", "plan", "manifest", "sourceMap"):
        reference = value.get(field)
        _validate_reference(reference, field, _DOCUMENT_MEDIA_TYPES)
        document_refs.append(reference)
    resources = value.get("resources")
    if not isinstance(resources, list):
        raise DocumentValidationError("MIKU_RESOURCE_LIST_INVALID", "resources must be an array")
    if len(resources) > MAX_RESOURCE_COUNT:
        raise DocumentValidationError("MIKU_RESOURCE_LIMIT", f"At most {MAX_RESOURCE_COUNT} resources are allowed")
    resource_ids = set()
    decoded_bytes = 0
    source_mesh_resources = []
    mesh_bound_textures = []
    for index, reference in enumerate(resources):
        path = f"resources[{index}]"
        _validate_reference(reference, path, _RESOURCE_MEDIA_TYPES)
        resource_id = str(reference.get("id") or "")
        if not resource_id or resource_id in resource_ids:
            raise DocumentValidationError("MIKU_RESOURCE_ID_INVALID", "Resource IDs must be non-empty and unique", path)
        resource_ids.add(resource_id)
        semantic = str(reference.get("semantic") or "")
        if semantic not in _SEMANTICS:
            raise DocumentValidationError("MIKU_RESOURCE_SEMANTIC_INVALID", semantic, path)
        media_type = str(reference.get("mediaType") or "")
        if media_type == "model/gltf-binary":
            if (
                semantic != "SourceMesh"
                or reference.get("kind") != "SourceMesh"
            ):
                raise DocumentValidationError(
                    "MIKU_SOURCE_MESH_RESOURCE_INVALID",
                    "SourceMesh must use the Miku 1.0 SourceMesh contract",
                    path,
                )
            _positive_int(reference.get("meshCount"), "meshCount", path)
            _positive_int(reference.get("vertexCount"), "vertexCount", path)
            _positive_int(reference.get("indexCount"), "indexCount", path)
            if reference.get("hasUv0") is not True:
                raise DocumentValidationError(
                    "MIKU_SOURCE_MESH_UV0_MISSING",
                    "SourceMesh requires UV0",
                    path,
                )
            renderer_bindings = reference.get("rendererBindings")
            if not isinstance(renderer_bindings, list) or not renderer_bindings:
                raise DocumentValidationError(
                    "MIKU_MESH_BINDING_MISMATCH",
                    "SourceMesh rendererBindings are required",
                    path,
                )
            for binding in renderer_bindings:
                if not isinstance(binding, Mapping):
                    raise DocumentValidationError(
                        "MIKU_MESH_BINDING_MISMATCH",
                        "Renderer binding must be an object",
                        path,
                    )
                fingerprint = str(
                    binding.get("meshFingerprint") or ""
                )
                if not _GUID_RE.fullmatch(fingerprint):
                    raise DocumentValidationError(
                        "MIKU_MESH_BINDING_MISMATCH",
                        "Renderer mesh fingerprint is invalid",
                        path,
                    )
            source_mesh_resources.append(reference)
            continue
        if isinstance(reference.get("meshBinding"), Mapping):
            mesh_bound_textures.append(reference)
        if semantic == "ExpressionIsland":
            for field in ("bindingKey", "expressionId"):
                if not isinstance(reference.get(field), str) or not reference[field]:
                    raise DocumentValidationError(
                        "MIKU_EXPRESSION_RESOURCE_INVALID",
                        f"{field} is required for ExpressionIsland resources",
                        path,
                    )
            usage = str(reference.get("usage") or "")
            if usage not in _EXPRESSION_ISLAND_USAGES:
                raise DocumentValidationError(
                    "MIKU_EXPRESSION_RESOURCE_USAGE_INVALID",
                    usage,
                    path,
                )
        if semantic == "FixedWorkflowTexture":
            if not isinstance(reference.get("bindingKey"), str) or not reference[
                "bindingKey"
            ]:
                raise DocumentValidationError(
                    "MIKU_FIXED_TEXTURE_BINDING_KEY_INVALID",
                    "bindingKey is required for fixed-workflow textures",
                    path,
                )
            material_bindings = reference.get("materialBindings", [])
            if not isinstance(material_bindings, list) or len(material_bindings) > 24:
                raise DocumentValidationError(
                    "MIKU_FIXED_TEXTURE_MATERIAL_BINDINGS_INVALID",
                    "materialBindings must contain at most 24 entries",
                    path,
                )
            roles: set[str] = set()
            for binding_index, binding in enumerate(material_bindings):
                binding_path = (
                    f"{path}.materialBindings[{binding_index}]"
                )
                role = (
                    str(binding.get("role") or "")
                    if isinstance(binding, Mapping)
                    else ""
                )
                if role not in FIXED_TEXTURE_ROLES or role in roles:
                    raise DocumentValidationError(
                        "MIKU_FIXED_TEXTURE_ROLE_INVALID",
                        role,
                        binding_path,
                    )
                roles.add(role)
                uv_transform = binding.get("uvTransform")
                if uv_transform is None:
                    continue
                if not isinstance(uv_transform, Mapping):
                    raise DocumentValidationError(
                        "MIKU_FIXED_TEXTURE_UV_TRANSFORM_INVALID",
                        "uvTransform must be an object",
                        binding_path,
                    )
                if (
                    str(uv_transform.get("coordinateSpace") or "") != "UV0"
                    or str(uv_transform.get("operation") or "") != "Affine2D"
                ):
                    raise DocumentValidationError(
                        "MIKU_FIXED_TEXTURE_UV_TRANSFORM_UNSUPPORTED",
                        "Only UV0 Affine2D transforms are supported",
                        binding_path,
                    )
                matrix = uv_transform.get("matrix")
                if (
                    not isinstance(matrix, list)
                    or len(matrix) != 6
                    or any(
                        not isinstance(value, (int, float))
                        or isinstance(value, bool)
                        or not math.isfinite(float(value))
                        for value in matrix
                    )
                ):
                    raise DocumentValidationError(
                        "MIKU_FIXED_TEXTURE_UV_MATRIX_INVALID",
                        "Affine2D matrix must contain six finite numbers",
                        binding_path,
                    )
        color_space = str(reference.get("colorSpace") or "")
        if color_space not in {"sRGB", "Linear"}:
            raise DocumentValidationError("MIKU_RESOURCE_COLOR_SPACE_INVALID", color_space, path)
        channel = str(reference.get("channel") or "")
        if channel not in _RESOURCE_CHANNELS:
            raise DocumentValidationError("MIKU_RESOURCE_CHANNEL_INVALID", channel, path)
        channel_bindings = reference.get("channelBindings")
        if channel_bindings is not None:
            if not isinstance(channel_bindings, list) or not 2 <= len(channel_bindings) <= 24:
                raise DocumentValidationError(
                    "MIKU_CHANNEL_BINDINGS_INVALID",
                    "channelBindings must contain between 2 and 24 entries",
                    path,
                )
            normalized_bindings: set[tuple[str, str]] = set()
            for binding_index, binding in enumerate(channel_bindings):
                binding_path = f"{path}.channelBindings[{binding_index}]"
                if not isinstance(binding, Mapping):
                    raise DocumentValidationError(
                        "MIKU_CHANNEL_BINDING_INVALID",
                        "Channel binding must be an object",
                        binding_path,
                    )
                binding_semantic = str(binding.get("semantic") or "")
                binding_channel = str(binding.get("channel") or "")
                if binding_semantic not in _PACKABLE_SCALAR_SEMANTICS:
                    raise DocumentValidationError(
                        "MIKU_CHANNEL_BINDING_SEMANTIC_INVALID",
                        binding_semantic,
                        binding_path,
                    )
                if binding_channel not in {"R", "G", "B", "A"}:
                    raise DocumentValidationError(
                        "MIKU_CHANNEL_BINDING_CHANNEL_INVALID",
                        binding_channel,
                        binding_path,
                    )
                normalized = (binding_semantic, binding_channel)
                if normalized in normalized_bindings:
                    raise DocumentValidationError(
                        "MIKU_CHANNEL_BINDING_DUPLICATE",
                        f"{binding_semantic}:{binding_channel}",
                        binding_path,
                    )
                normalized_bindings.add(normalized)
            if reference.get("usage") != "Scalar" or color_space != "Linear":
                raise DocumentValidationError(
                    "MIKU_PACKED_RESOURCE_COLOR_SPACE_CONFLICT",
                    "Packed scalar resources require Scalar usage and Linear color space",
                    path,
                )
            if (semantic, channel) not in normalized_bindings:
                raise DocumentValidationError(
                    "MIKU_CHANNEL_BINDING_PRIMARY_MISMATCH",
                    "The primary semantic/channel must also appear in channelBindings",
                    path,
                )
        width = _positive_int(reference.get("width"), "width", path)
        height = _positive_int(reference.get("height"), "height", path)
        if width > MAX_IMAGE_DIMENSION or height > MAX_IMAGE_DIMENSION:
            raise DocumentValidationError("MIKU_RESOURCE_DIMENSION_LIMIT", f"{width}x{height}", path)
        channel_count = _positive_int(reference.get("channelCount", 4), "channelCount", path)
        component_bytes = _positive_int(reference.get("componentBytes", 1), "componentBytes", path)
        pixels = _checked_product(width, height, channel_count, component_bytes)
        decoded_bytes += pixels
        if decoded_bytes > MAX_DECODED_IMAGE_BYTES:
            raise DocumentValidationError("MIKU_RESOURCE_DECODED_LIMIT", "Decoded image budget exceeded")
        is_normal = semantic == "Normal" or reference.get("usage") == "Normal"
        if is_normal and reference.get("normalConvention") not in _NORMAL_CONVENTIONS:
            raise DocumentValidationError(
                "MIKU_NORMAL_CONVENTION_INVALID",
                "Normal resources require a supported tangent-space convention",
                path,
            )
        if "uvSet" in reference and reference.get("uvSet") != "UV0":
            raise DocumentValidationError(
                "MIKU_RESOURCE_UV_SET_INVALID",
                str(reference.get("uvSet")),
                path,
            )
        if "projection" in reference and reference.get("projection") != "FLAT":
            raise DocumentValidationError(
                "MIKU_RESOURCE_PROJECTION_INVALID",
                str(reference.get("projection")),
                path,
            )
        if "interpolation" in reference and reference.get("interpolation") not in {"LINEAR", "CLOSEST"}:
            raise DocumentValidationError(
                "MIKU_RESOURCE_INTERPOLATION_INVALID",
                str(reference.get("interpolation")),
                path,
            )
        if "extension" in reference and reference.get("extension") not in {"REPEAT", "EXTEND"}:
            raise DocumentValidationError(
                "MIKU_RESOURCE_EXTENSION_INVALID",
                str(reference.get("extension")),
                path,
            )
    if (
        len(source_mesh_resources) > 1
        or (mesh_bound_textures and len(source_mesh_resources) != 1)
    ):
        raise DocumentValidationError(
            "MIKU_SOURCE_MESH_RESOURCE_INVALID",
            "Miku Bundle 1.0 permits at most one SourceMesh and requires it for mesh-bound textures",
        )
    if source_mesh_resources:
        source_binding = source_mesh_resources[0].get("meshBinding")
        source_binding_hash = str(
            (source_binding or {}).get("sha256") or ""
        )
        if (
            not _GUID_RE.fullmatch(source_binding_hash)
            or any(
                str((item.get("meshBinding") or {}).get("sha256") or "")
                != source_binding_hash
                for item in mesh_bound_textures
            )
        ):
            raise DocumentValidationError(
                "MIKU_MESH_BINDING_MISMATCH",
                "Texture and SourceMesh fingerprints do not match",
            )
    all_refs = document_refs + resources
    normalized = [normalize_relative_path(str(reference["relativePath"])) for reference in all_refs]
    folded = [unicodedata.normalize("NFC", item).casefold() for item in normalized]
    if len(folded) != len(set(folded)):
        raise DocumentValidationError("MIKU_ARTIFACT_PATH_DUPLICATE", "Artifact paths collide after normalization")
    total = sum(int(reference["byteLength"]) for reference in all_refs)
    if total > MAX_BUNDLE_BYTES:
        raise DocumentValidationError("MIKU_BUNDLE_SIZE_LIMIT", "Bundle artifact byte budget exceeded")
    if str(value.get("sealedDigest") or "") != compute_sealed_digest(value):
        raise DocumentValidationError("MIKU_BUNDLE_SEAL_MISMATCH", "sealedDigest does not match artifact references")
    return value


def validate_portable_hybrid_resources(
    mode: str,
    resources: Iterable[Mapping[str, Any]],
) -> None:
    """Reject source-mesh state from the public PreferNative contract."""

    if str(mode or "") != "PreferNative":
        return
    for index, resource in enumerate(resources):
        if not isinstance(resource, Mapping):
            continue
        if (
            str(resource.get("semantic") or "") == "SourceMesh"
            or str(resource.get("kind") or "") == "SourceMesh"
            or isinstance(resource.get("meshBinding"), Mapping)
            or (
                str(resource.get("semantic") or "") == "ExpressionIsland"
                and (
                    resource.get("meshBindingRequired") is not False
                    or str(resource.get("coordinateDomain") or "")
                    not in {"Uniform", "UV0"}
                )
            )
        ):
            raise DocumentValidationError(
                "MIKU_PORTABLE_RESOURCE_MESH_BOUND",
                "Portable Hybrid output may contain only unbound Uniform/UV0 "
                "expression resources and no SourceMesh or meshBinding",
                f"$.resources[{index}]",
            )


def stage_bundle_artifacts(bundle_path: Path, staging_root: Path) -> tuple[dict[str, Any], Path]:
    """Copy verified source bytes to an external staging root and consume only them."""

    import json

    bundle_path = bundle_path.resolve(strict=True)
    source_root = bundle_path.parent.resolve(strict=True)
    _reject_reparse_ancestors(bundle_path, source_root)
    bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
    bundle = validate_bundle_document(bundle)
    staging_root = staging_root.resolve()
    staging_root.mkdir(parents=True, exist_ok=True)
    transaction_root = staging_root / str(bundle["canonicalHash"])
    if transaction_root.exists():
        shutil.rmtree(transaction_root)
    transaction_root.mkdir(parents=True)
    references: Iterable[Mapping[str, Any]] = [
        bundle[field] for field in ("ir", "plan", "manifest", "sourceMap")
    ]
    references = [*references, *(bundle.get("resources") or [])]
    for reference in references:
        relative = normalize_relative_path(str(reference["relativePath"]))
        source = _secure_source_path(source_root, relative)
        destination = transaction_root / Path(relative)
        destination.parent.mkdir(parents=True, exist_ok=True)
        digest = hashlib.sha256()
        length = 0
        with source.open("rb") as input_stream, destination.open("xb") as output_stream:
            for block in iter(lambda: input_stream.read(1024 * 1024), b""):
                digest.update(block)
                length += len(block)
                if length > int(reference["byteLength"]) or length > MAX_RESOURCE_BYTES:
                    raise DocumentValidationError("MIKU_ARTIFACT_SIZE_MISMATCH", relative)
                output_stream.write(block)
            output_stream.flush()
            os.fsync(output_stream.fileno())
        if length != int(reference["byteLength"]) or digest.hexdigest() != reference["sha256"]:
            raise DocumentValidationError("MIKU_ARTIFACT_HASH_MISMATCH", relative)
        if destination.stat().st_size != length or sha256_file(destination) != reference["sha256"]:
            raise DocumentValidationError("MIKU_STAGED_ARTIFACT_MISMATCH", relative)
    return bundle, transaction_root


def _validate_reference(reference: Any, path: str, media_types: set[str]) -> None:
    if not isinstance(reference, Mapping):
        raise DocumentValidationError("MIKU_ARTIFACT_REFERENCE_INVALID", "Reference must be an object", path)
    normalize_relative_path(str(reference.get("relativePath") or ""))
    digest = str(reference.get("sha256") or "")
    if not _GUID_RE.fullmatch(digest):
        raise DocumentValidationError("MIKU_ARTIFACT_HASH_INVALID", digest, path)
    length = reference.get("byteLength")
    if not isinstance(length, int) or isinstance(length, bool) or length < 0 or length > MAX_RESOURCE_BYTES:
        raise DocumentValidationError("MIKU_ARTIFACT_SIZE_INVALID", str(length), path)
    if str(reference.get("mediaType") or "") not in media_types:
        raise DocumentValidationError("MIKU_ARTIFACT_MEDIA_TYPE_INVALID", str(reference.get("mediaType")), path)


def _positive_int(value: Any, field: str, path: str) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
        raise DocumentValidationError("MIKU_RESOURCE_DIMENSION_INVALID", f"{field}={value}", path)
    return value


def _checked_product(*values: int) -> int:
    result = 1
    for value in values:
        if value > MAX_DECODED_IMAGE_BYTES or result > MAX_DECODED_IMAGE_BYTES // value:
            raise DocumentValidationError("MIKU_RESOURCE_DECODED_LIMIT", "Decoded image size overflow")
        result *= value
    return result


def _secure_source_path(root: Path, relative: str) -> Path:
    candidate = root.joinpath(*relative.split("/"))
    _reject_reparse_ancestors(candidate, root)
    try:
        resolved = candidate.resolve(strict=True)
    except (FileNotFoundError, OSError) as exc:
        raise DocumentValidationError(
            "MIKU_ARTIFACT_MISSING",
            relative,
        ) from exc
    try:
        resolved.relative_to(root)
    except ValueError as exc:
        raise DocumentValidationError("MIKU_ARTIFACT_PATH_ESCAPE", relative) from exc
    if not resolved.is_file():
        raise DocumentValidationError("MIKU_ARTIFACT_MISSING", relative)
    return resolved


def _reject_reparse_ancestors(path: Path, root: Path) -> None:
    root = root.resolve()
    current = path
    while True:
        if current.exists():
            info = current.lstat()
            attributes = getattr(info, "st_file_attributes", 0)
            if stat.S_ISLNK(info.st_mode) or attributes & 0x400:
                raise DocumentValidationError("MIKU_ARTIFACT_REPARSE_POINT", str(current))
        if current == root:
            break
        if root not in current.parents:
            raise DocumentValidationError("MIKU_ARTIFACT_PATH_ESCAPE", str(path))
        current = current.parent
