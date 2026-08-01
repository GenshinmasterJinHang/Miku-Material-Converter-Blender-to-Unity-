# SPDX-FileCopyrightText: 2026 Miku Project Authors
# SPDX-License-Identifier: GPL-2.0-or-later
"""Deterministic GLB export for mesh-bound Miku bake resources.

The writer deliberately exports only the evaluated static meshes that use the
requested material. Geometry is transformed into the source scene's world
space so the generated Unity prefab can use identity child transforms.
"""

from __future__ import annotations

import json
import math
import struct
from pathlib import Path
from typing import Any


_GLTF_FLOAT = 5126
_GLTF_UNSIGNED_INT = 5125


def export_source_mesh_glb(
    context: Any,
    objects: list[Any],
    material: Any,
    output_path: Path,
    source_fingerprints: list[dict[str, Any]],
) -> dict[str, Any]:
    """Write a byte-stable GLB plus renderer bindings for one material."""

    material_objects = sorted(
        (
            obj
            for obj in objects
            if getattr(obj, "type", "") == "MESH"
            and any(
                getattr(slot, "material", None) == material
                for slot in getattr(obj, "material_slots", []) or []
            )
        ),
        key=_object_name,
    )
    if not material_objects:
        raise RuntimeError("MIKU_SOURCE_MESH_MISSING")
    for obj in material_objects:
        if callable(getattr(obj, "find_armature", None)) and obj.find_armature():
            raise RuntimeError(
                f"MIKU_SOURCE_MESH_DEFORM_UNSUPPORTED:{_object_name(obj)}:"
                "armature"
            )

    try:
        depsgraph = context.evaluated_depsgraph_get()
    except Exception as exc:
        raise RuntimeError("MIKU_SOURCE_MESH_EVALUATION_FAILED") from exc

    binary = bytearray()
    buffer_views: list[dict[str, Any]] = []
    accessors: list[dict[str, Any]] = []
    meshes: list[dict[str, Any]] = []
    nodes: list[dict[str, Any]] = []
    materials: list[dict[str, Any]] = []
    renderer_bindings: list[dict[str, Any]] = []
    source_by_name = {
        str(item.get("object") or ""): item
        for item in source_fingerprints
        if isinstance(item, dict)
    }
    total_vertices = 0
    total_indices = 0
    all_have_uv0 = True

    def add_view(payload: bytes, target: int) -> int:
        while len(binary) % 4:
            binary.append(0)
        offset = len(binary)
        binary.extend(payload)
        index = len(buffer_views)
        buffer_views.append(
            {
                "buffer": 0,
                "byteOffset": offset,
                "byteLength": len(payload),
                "target": target,
            }
        )
        return index

    def add_accessor(
        payload: bytes,
        *,
        target: int,
        component_type: int,
        count: int,
        value_type: str,
        minimum: list[float] | None = None,
        maximum: list[float] | None = None,
    ) -> int:
        record: dict[str, Any] = {
            "bufferView": add_view(payload, target),
            "componentType": component_type,
            "count": count,
            "type": value_type,
        }
        if minimum is not None:
            record["min"] = minimum
        if maximum is not None:
            record["max"] = maximum
        index = len(accessors)
        accessors.append(record)
        return index

    try:
        for mesh_index, source_object in enumerate(material_objects):
            evaluated = source_object.evaluated_get(depsgraph)
            mesh = evaluated.to_mesh()
            if mesh is None:
                raise RuntimeError(
                    f"MIKU_SOURCE_MESH_EVALUATION_FAILED:"
                    f"{_object_name(source_object)}"
                )
            try:
                mesh.calc_loop_triangles()
                loop_triangles = list(mesh.loop_triangles)
                if not loop_triangles:
                    raise RuntimeError(
                        f"MIKU_SOURCE_MESH_EMPTY:{_object_name(source_object)}"
                    )
                uv_layers = getattr(mesh, "uv_layers", None)
                uv_layer = None
                if uv_layers is not None:
                    uv_layer = (
                        getattr(uv_layers, "active_render", None)
                        or getattr(uv_layers, "active", None)
                    )
                    if uv_layer is None and len(uv_layers):
                        uv_layer = uv_layers[0]
                if uv_layer is None:
                    all_have_uv0 = False
                    raise RuntimeError(
                        f"MIKU_SOURCE_MESH_UV0_MISSING:"
                        f"{_object_name(source_object)}"
                    )

                world = source_object.matrix_world
                normal_matrix = world.to_3x3().inverted_safe().transposed()
                positions: list[tuple[float, float, float]] = []
                normals: list[tuple[float, float, float]] = []
                uvs: list[tuple[float, float]] = []
                vertex_lookup: dict[tuple[Any, ...], int] = {}
                indices_by_slot: dict[int, list[int]] = {}
                slots = list(
                    getattr(source_object, "material_slots", []) or []
                )

                for triangle in loop_triangles:
                    slot_index = int(getattr(triangle, "material_index", 0))
                    target_indices = indices_by_slot.setdefault(
                        slot_index, []
                    )
                    for loop_index in triangle.loops:
                        loop = mesh.loops[int(loop_index)]
                        vertex_index = int(loop.vertex_index)
                        uv = uv_layer.data[int(loop_index)].uv
                        normal = normal_matrix @ loop.normal
                        normal.normalize()
                        converted_normal = _convert_vector(normal)
                        converted_uv = (
                            _finite_float(uv[0]),
                            _finite_float(1.0 - uv[1]),
                        )
                        key = (
                            vertex_index,
                            *converted_normal,
                            *converted_uv,
                        )
                        exported_index = vertex_lookup.get(key)
                        if exported_index is None:
                            position = world @ mesh.vertices[vertex_index].co
                            exported_index = len(positions)
                            vertex_lookup[key] = exported_index
                            positions.append(_convert_vector(position))
                            normals.append(converted_normal)
                            uvs.append(converted_uv)
                        target_indices.append(exported_index)

                position_accessor = add_accessor(
                    _pack_floats(positions),
                    target=34962,
                    component_type=_GLTF_FLOAT,
                    count=len(positions),
                    value_type="VEC3",
                    minimum=[
                        min(item[axis] for item in positions)
                        for axis in range(3)
                    ],
                    maximum=[
                        max(item[axis] for item in positions)
                        for axis in range(3)
                    ],
                )
                normal_accessor = add_accessor(
                    _pack_floats(normals),
                    target=34962,
                    component_type=_GLTF_FLOAT,
                    count=len(normals),
                    value_type="VEC3",
                )
                uv_accessor = add_accessor(
                    _pack_floats(uvs),
                    target=34962,
                    component_type=_GLTF_FLOAT,
                    count=len(uvs),
                    value_type="VEC2",
                )

                primitives: list[dict[str, Any]] = []
                for slot_index, slot_indices in sorted(
                    indices_by_slot.items()
                ):
                    slot_name = ""
                    if 0 <= slot_index < len(slots):
                        slot_material = getattr(
                            slots[slot_index], "material", None
                        )
                        slot_name = str(
                            getattr(slot_material, "name_full", "")
                            or getattr(slot_material, "name", "")
                        )
                    gltf_material_index = len(materials)
                    materials.append(
                        {
                            "name": (
                                f"{_object_name(source_object)}:"
                                f"{slot_index}:{slot_name}"
                            ),
                            "pbrMetallicRoughness": {
                                "baseColorFactor": [1.0, 1.0, 1.0, 1.0],
                                "metallicFactor": 0.0,
                                "roughnessFactor": 1.0,
                            },
                        }
                    )
                    index_accessor = add_accessor(
                        struct.pack(
                            "<" + "I" * len(slot_indices),
                            *slot_indices,
                        ),
                        target=34963,
                        component_type=_GLTF_UNSIGNED_INT,
                        count=len(slot_indices),
                        value_type="SCALAR",
                        minimum=[min(slot_indices)],
                        maximum=[max(slot_indices)],
                    )
                    primitives.append(
                        {
                            "attributes": {
                                "POSITION": position_accessor,
                                "NORMAL": normal_accessor,
                                "TEXCOORD_0": uv_accessor,
                            },
                            "indices": index_accessor,
                            "material": gltf_material_index,
                            "mode": 4,
                        }
                    )

                object_name = _object_name(source_object)
                meshes.append(
                    {
                        "name": object_name,
                        "primitives": primitives,
                    }
                )
                nodes.append({"name": object_name, "mesh": mesh_index})
                material_slots = [
                    index
                    for index, slot in enumerate(slots)
                    if getattr(slot, "material", None) == material
                ]
                source_fingerprint = source_by_name.get(object_name, {})
                renderer_bindings.append(
                    {
                        "rendererPath": object_name,
                        "sourceObject": object_name,
                        "meshIndex": mesh_index,
                        "materialSlots": material_slots,
                        "meshFingerprint": str(
                            source_fingerprint.get("sha256") or ""
                        ),
                        "sourceVertices": int(
                            source_fingerprint.get("vertices") or 0
                        ),
                        "sourcePolygons": int(
                            source_fingerprint.get("polygons") or 0
                        ),
                        "sourceUv": str(
                            source_fingerprint.get("uv") or ""
                        ),
                        "exportedVertices": len(positions),
                        "exportedIndices": sum(
                            len(item) for item in indices_by_slot.values()
                        ),
                        "hasUv0": True,
                    }
                )
                total_vertices += len(positions)
                total_indices += sum(
                    len(item) for item in indices_by_slot.values()
                )
            finally:
                evaluated.to_mesh_clear()
    except Exception:
        if output_path.exists():
            output_path.unlink()
        raise

    document = {
        "asset": {
            "version": "2.0",
            "generator": "Miku GPL Bake Worker 1.2.0",
        },
        "scene": 0,
        "scenes": [{"nodes": list(range(len(nodes)))}],
        "nodes": nodes,
        "meshes": meshes,
        "materials": materials,
        "accessors": accessors,
        "bufferViews": buffer_views,
        "buffers": [{"byteLength": len(binary)}],
    }
    json_bytes = json.dumps(
        document,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
        allow_nan=False,
    ).encode("utf-8")
    while len(json_bytes) % 4:
        json_bytes += b" "
    while len(binary) % 4:
        binary.append(0)
    total_length = 12 + 8 + len(json_bytes) + 8 + len(binary)
    glb = bytearray()
    glb.extend(struct.pack("<III", 0x46546C67, 2, total_length))
    glb.extend(struct.pack("<II", len(json_bytes), 0x4E4F534A))
    glb.extend(json_bytes)
    glb.extend(struct.pack("<II", len(binary), 0x004E4942))
    glb.extend(binary)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    temporary = output_path.with_suffix(output_path.suffix + ".tmp")
    temporary.write_bytes(bytes(glb))
    temporary.replace(output_path)
    return {
        "meshCount": len(meshes),
        "vertexCount": total_vertices,
        "indexCount": total_indices,
        "hasUv0": all_have_uv0,
        "rendererBindings": renderer_bindings,
    }


def _object_name(obj: Any) -> str:
    return str(
        getattr(obj, "name_full", "")
        or getattr(obj, "name", "")
    )


def _finite_float(value: Any) -> float:
    result = float(value)
    if not math.isfinite(result):
        raise RuntimeError("MIKU_SOURCE_MESH_NUMERIC_INVALID")
    return 0.0 if result == 0.0 else result


def _convert_vector(value: Any) -> tuple[float, float, float]:
    # Blender RH Z-up -> glTF RH Y-up.
    return (
        _finite_float(value[0]),
        _finite_float(value[2]),
        _finite_float(-value[1]),
    )


def _pack_floats(values: list[tuple[float, ...]]) -> bytes:
    flattened = [component for item in values for component in item]
    return struct.pack("<" + "f" * len(flattened), *flattened)
