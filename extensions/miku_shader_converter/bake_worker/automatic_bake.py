# SPDX-FileCopyrightText: 2026 Miku Project Authors
# SPDX-License-Identifier: GPL-2.0-or-later
"""Isolated channel-expression baking for Miku Unity bundles.

This module intentionally lives on the Blender side.  It converts procedural or
otherwise unsupported Principled inputs into ordinary PNG resources while the
original game preset exporters remain untouched. Blender currently exposes the
required bake API through Cycles, but Miku rewires each requested material
channel through an unlit emission surface on temporary data. Cycles materials
are not accepted as a public source dialect.
"""

from __future__ import annotations

import hashlib
import json
import math
import os
import struct
import time

import bpy

try:
    from .io_safety import atomic_write_json
except ImportError:
    # The Blender add-on loads this file through a version-isolated module
    # spec, where Python intentionally does not assign a package context.
    try:
        from io_safety import atomic_write_json
    except (ImportError, ModuleNotFoundError):
        # Load the sibling module explicitly when Blender does not establish a
        # package context for the extension.
        import importlib.util
        from pathlib import Path

        _IO_SPEC = importlib.util.spec_from_file_location(
            "_miku_gpl_worker_io_safety",
            Path(__file__).with_name("io_safety.py"),
        )
        if _IO_SPEC is None or _IO_SPEC.loader is None:
            raise ImportError("Unable to load the Miku io_safety module")
        _IO_MODULE = importlib.util.module_from_spec(_IO_SPEC)
        _IO_SPEC.loader.exec_module(_IO_MODULE)
        atomic_write_json = _IO_MODULE.atomic_write_json


BAKE_SCHEMA = "miku-bake-cache-1.0"
BAKE_ALGORITHM_REVISION = 12
CHANNELS = (
    ("BaseColor", "Base Color", "RGB", "sRGB"),
    ("Metalness", "Metallic", "R", "Linear"),
    ("Roughness", "Roughness", "R", "Linear"),
    ("Normal", "Normal", "RGB", "Linear"),
    ("Emission", "Emission Color", "RGB", "sRGB"),
    ("Alpha", "Alpha", "R", "Linear"),
    ("IOR", "IOR", "R", "Linear"),
)

_COMPLEX_OP_TOKENS = (
    "Texture.Noise",
    "Texture.Voronoi",
    "Texture.Wave",
    "Texture.Musgrave",
    "Texture.Gabor",
    "Texture.Gradient",
    "Texture.Magic",
    "Texture.Brick",
    "Texture.WhiteNoise",
    "Converter.ColorRamp",
    "Converter.RGBCurve",
    "Shader.Mix",
    "Vector.Bump",
    "Vector.Displacement",
)


def graph_requires_bake(graph):
    """Return True only for non-preset graphs that benefit from portability baking."""
    if graph.get("preset") or any(
        graph.get(key) for key in ("genshinToonPreset", "wuwaToonPreset", "hsrToonPreset")
    ):
        return False

    for diagnostic in graph.get("diagnostics", []) or []:
        code = str(diagnostic.get("code", "")).lower()
        if any(token in code for token in ("unsupported", "conditional", "procedural", "displacement", "fallback")):
            return True

    for node in graph.get("nodes", []) or []:
        op = str(node.get("op", ""))
        if any(token in op for token in _COMPLEX_OP_TOKENS):
            return True

    semantic = graph.get("standardPbrSemantic") or {}
    if (semantic.get("source") or {}).get("shader") == "MixedBSDF":
        return True
    for slot in (semantic.get("slots") or {}).values():
        if isinstance(slot, dict) and slot.get("source") not in (None, "", "socket", "socket_default", "loose_name"):
            return True
    return False


def bake_texture_resources(context, objects, material, graph, miku_path, settings):
    """Bake planner-selected procedural nodes to concrete 3D or direction textures.

    This pass intentionally runs before the final mesh/PBR bake.  The generated
    resources keep the source node independently editable in Shader Graph while
    the mesh bake remains the authoritative parity path for material slots.
    """
    routes = [
        dict(item)
        for item in (((graph.get("capabilityReport") or {}).get("textureNodes") or []))
        if isinstance(item, dict)
        and str(item.get("strategy") or "") == "Baked"
        and str(item.get("representation") or "") in {"Texture3D", "DirectionLut"}
    ]
    if not routes:
        return {
            "documentKind": BAKE_SCHEMA,
            "schemaVersion": "1.0",
            "algorithmRevision": BAKE_ALGORITHM_REVISION,
            "status": "not-required",
            "resources": {},
            "nodeOutputs": {},
            "failures": [],
        }

    node_by_id = {item.get("id"): item for item in graph.get("nodes", []) or []}
    used_outputs = {}
    linked_inputs = set()
    for edge in graph.get("edges", []) or []:
        source = edge.get("from") or {}
        target = edge.get("to") or {}
        used_outputs.setdefault(source.get("node"), set()).add(source.get("socket"))
        linked_inputs.add((target.get("node"), _normalized_socket_name(target.get("socket"))))

    volume_resolution = int(getattr(settings, "bake_volume_resolution", "128") or 128)
    volume_resolution = max(16, min(256, volume_resolution))
    direction_width = int(getattr(settings, "bake_direction_width", "2048") or 2048)
    direction_width = max(64, min(4096, direction_width))
    frame = int(getattr(getattr(context, "scene", None), "frame_current", 0) or 0)
    depsgraph = None
    try:
        depsgraph = context.evaluated_depsgraph_get()
    except Exception:
        pass
    runtime_dependencies = {
        "sourceImages": _resource_file_fingerprints(graph),
        "materialNodeTree": _node_tree_fingerprint(getattr(material, "node_tree", None)),
        "objects": [
            _texture_resource_object_dependency(obj, depsgraph)
            for obj in sorted(list(objects or []), key=_object_name)
        ],
    }
    cache_payload = {
        "documentKind": BAKE_SCHEMA,
        "schemaVersion": "1.0",
        "algorithmRevision": BAKE_ALGORITHM_REVISION,
        "graph": _stable_graph_for_cache(graph),
        "routes": routes,
        "volumeResolution": volume_resolution,
        "directionWidth": direction_width,
        "frame": frame,
        "blender": tuple(getattr(getattr(bpy, "app", None), "version", ()) or ()),
        "blenderBuild": str(getattr(getattr(bpy, "app", None), "build_hash", "") or ""),
        "dependencies": runtime_dependencies,
    }
    cache_key = hashlib.sha256(
        json.dumps(cache_payload, ensure_ascii=False, sort_keys=True, default=str, separators=(",", ":")).encode("utf-8")
    ).hexdigest()
    bake_dir = os.path.join(os.path.dirname(miku_path), "Baked")
    cache_path = os.path.join(bake_dir, ".miku-texture-resource-cache.json")
    os.makedirs(bake_dir, exist_ok=True)
    cached = _read_cache(cache_path)
    if (
        cached
        and cached.get("status") in {"completed", "reused"}
        and not cached.get("failures")
        and cached.get("cacheKey") == cache_key
        and _texture_resource_cache_outputs_exist(cached, bake_dir)
    ):
        reused = dict(cached)
        reused["status"] = "reused"
        return reused

    resources = {}
    node_outputs = {}
    failures = []
    for route in routes:
        node_id = route.get("nodeId")
        exported_node = node_by_id.get(node_id)
        if not isinstance(exported_node, dict):
            failures.append(
                {"nodeId": node_id, "message": "The planner route does not reference an exported node."}
            )
            continue
        outputs = sorted(
            socket for socket in (used_outputs.get(node_id) or set())
            if socket and _exported_output(exported_node, socket) is not None
        )
        if not outputs:
            outputs = [
                socket.get("id")
                for socket in exported_node.get("outputs", []) or []
                if socket.get("id")
            ][:1]
        try:
            if route.get("representation") == "Texture3D":
                dimensions = str((exported_node.get("params") or {}).get("dimensions") or "").upper()
                if dimensions == "4D" and (node_id, "w") in linked_inputs:
                    raise RuntimeError(
                        "RequiresRuntimeSupport: a varying W input makes this a four-dimensional "
                        "function and cannot be represented by a static Texture3D."
                    )
                baked = _bake_volume_node(
                    context,
                    material,
                    exported_node,
                    outputs,
                    bake_dir,
                    volume_resolution,
                    route.get("coordinateDomain") or "Generated",
                    objects,
                )
            else:
                baked = _bake_direction_node(
                    context,
                    material,
                    exported_node,
                    outputs,
                    bake_dir,
                    direction_width,
                )
            resources.update(baked["resources"])
            node_outputs[node_id] = baked["outputs"]
        except Exception as exc:
            failures.append(
                {
                    "nodeId": node_id,
                    "op": exported_node.get("op", ""),
                    "representation": route.get("representation", ""),
                    "message": str(exc),
                }
            )

    status = "completed" if not failures else ("partial" if resources else "failed")
    result = {
        "documentKind": BAKE_SCHEMA,
        "schemaVersion": "1.0",
        "algorithmRevision": BAKE_ALGORITHM_REVISION,
        "status": status,
        "cacheKey": cache_key,
        "resources": resources,
        "nodeOutputs": node_outputs,
        "failures": failures,
        "dependencies": {
            "frame": frame,
            "blenderVersion": ".".join(str(item) for item in getattr(bpy.app, "version", ()) or ()),
            "evaluator": "BLENDER_CHANNEL_BAKE",
            "volumeResolution": volume_resolution,
            "directionWidth": direction_width,
            **runtime_dependencies,
        },
    }
    _write_cache(cache_path, result)
    return result


def _texture_resource_object_dependency(obj, depsgraph=None):
    dependency = {
        "object": _object_name(obj),
        "matrix": list(_flatten_matrix(getattr(obj, "matrix_world", None))),
        "mesh": _mesh_fingerprint(obj, depsgraph),
        "particleSystems": [],
    }
    for system in list(getattr(obj, "particle_systems", []) or []):
        settings = getattr(system, "settings", None)
        point_cache = getattr(system, "point_cache", None)
        dependency["particleSystems"].append(
            {
                "name": str(getattr(system, "name", "")),
                "settings": str(
                    getattr(settings, "name_full", "")
                    or getattr(settings, "name", "")
                ),
                "seed": int(getattr(system, "seed", 0) or 0),
                "count": int(getattr(settings, "count", 0) or 0),
                "frameStart": float(getattr(settings, "frame_start", 0.0) or 0.0),
                "frameEnd": float(getattr(settings, "frame_end", 0.0) or 0.0),
                "lifetime": float(getattr(settings, "lifetime", 0.0) or 0.0),
                "cacheFrameStart": int(getattr(point_cache, "frame_start", 0) or 0),
                "cacheFrameEnd": int(getattr(point_cache, "frame_end", 0) or 0),
                "cacheBaked": bool(getattr(point_cache, "is_baked", False)),
            }
        )
    return dependency


def _texture_resource_cache_outputs_exist(cache, bake_dir):
    resources = cache.get("resources") or {}
    if not resources:
        return False
    for resource in resources.values():
        relative = str(resource.get("uri") or "")
        if relative.startswith("Baked/"):
            relative = relative[len("Baked/"):]
        if not relative or not os.path.isfile(os.path.join(bake_dir, relative)):
            return False
    return True


def _exported_output(node, socket_id):
    normalized = _normalized_socket_name(socket_id)
    return next(
        (
            item for item in node.get("outputs", []) or []
            if _normalized_socket_name(item.get("id") or item.get("name")) == normalized
        ),
        None,
    )


def _normalized_socket_name(value):
    return str(value or "").casefold().replace(" ", "").replace("_", "")


def _bake_volume_node(
    context,
    source_material,
    exported_node,
    output_names,
    bake_dir,
    resolution,
    coordinate_domain,
    objects,
):
    columns = max(1, int(math.ceil(math.sqrt(resolution * 2.0))))
    rows = int(math.ceil(resolution / columns))
    domain_min, domain_max = _volume_domain(coordinate_domain, objects)
    resources = {}
    outputs = {}
    for output_name in output_names:
        scratch, owned_trees = _copy_material_for_resource_bake(source_material)
        selection_state = _capture_selection(context)
        image = None
        atlas_object = None
        atlas_mesh = None
        try:
            tree, node = _find_private_source_node(scratch, exported_node.get("source") or {})
            source_socket = _output_by_name(node, output_name)
            if source_socket is None:
                raise RuntimeError(f"Source output '{output_name}' was not found in the private material copy.")
            coordinate_input = (
                "W"
                if str((exported_node.get("params") or {}).get("dimensions") or "").upper() == "1D"
                and _input_by_name(node, "W") is not None
                else "Vector"
            )
            _install_volume_coordinate(tree, node, coordinate_input)
            top_socket = _expose_nested_output(scratch.node_tree, tree, node, source_socket)
            _route_private_output_to_emission(scratch.node_tree, top_socket)

            atlas_object, atlas_mesh = _create_volume_atlas_object(
                context,
                resolution,
                columns,
                rows,
                domain_min,
                domain_max,
                scratch,
            )
            width = resolution * columns
            height = resolution * rows
            image = bpy.data.images.new(
                name=f"__B2U_VOLUME_{exported_node.get('id')}_{output_name}",
                width=width,
                height=height,
                alpha=True,
                float_buffer=True,
            )
            _set_image_color_space(image, "Linear")
            _install_bake_target(scratch, image)
            _select_only(context, [atlas_object])
            context.view_layer.objects.active = atlas_object
            scene = context.scene
            previous_engine = scene.render.engine
            previous_samples = getattr(getattr(scene, "cycles", None), "samples", None)
            try:
                scene.render.engine = "CYCLES"
                if getattr(scene, "cycles", None) is not None:
                    scene.cycles.samples = 1
                outcome = _invoke_bake(
                    context,
                    [atlas_object],
                    {
                        "type": "EMIT",
                        "margin": 0,
                        "use_clear": True,
                        "target": "IMAGE_TEXTURES",
                        "save_mode": "INTERNAL",
                    },
                )
                if "FINISHED" not in set(outcome or []):
                    raise RuntimeError(f"Blender volume bake returned {outcome!r}.")
            finally:
                scene.render.engine = previous_engine
                if previous_samples is not None and getattr(scene, "cycles", None) is not None:
                    scene.cycles.samples = previous_samples

            stem = _safe_stem(
                f"{exported_node.get('id', 'texture')}_{output_name}_volume"
            )
            file_name = stem + ".exr"
            output_path = os.path.join(bake_dir, file_name)
            _save_float_image_atomic(image, output_path)
            resource_id = "miku_volume_" + hashlib.sha1(
                f"{exported_node.get('id')}:{output_name}".encode("utf-8")
            ).hexdigest()[:16]
            resources[resource_id] = {
                "id": resource_id,
                "kind": "volume_atlas",
                "type": "Texture3D",
                "representation": "Texture3D",
                "path": f"Baked/{file_name}",
                "uri": f"Baked/{file_name}",
                "exportFileName": file_name,
                "recommendedColorSpace": "Linear",
                "format": "RGBAHalf",
                "dimensions": {
                    "width": resolution,
                    "height": resolution,
                    "depth": resolution,
                },
                "atlas": {"columns": columns, "rows": rows},
                "wrapMode": "Repeat",
                "filterMode": "Linear",
                "generatedBy": "blender-volume-bake",
            }
            output_info = _exported_output(exported_node, output_name) or {}
            value_type = str(output_info.get("valueType") or "")
            outputs[output_name] = {
                "resource": resource_id,
                "representation": "Texture3D",
                "translationQuality": "Baked",
                "channel": "RGBA" if value_type in {"color4", "float3", "float4"} else "R",
                "coordinateInput": coordinate_input,
                "coordinateDomain": coordinate_domain,
                "domainMin": domain_min,
                "domainMax": domain_max,
            }
            if str((exported_node.get("params") or {}).get("dimensions") or "").upper() == "4D":
                outputs[output_name]["snapshotInputs"] = {
                    "W": float((exported_node.get("params") or {}).get("w") or 0.0),
                    "frame": int(getattr(getattr(context, "scene", None), "frame_current", 0) or 0),
                }
        finally:
            _restore_selection(context, selection_state)
            _remove_resource_bake_object(atlas_object, atlas_mesh)
            if image is not None:
                try:
                    bpy.data.images.remove(image)
                except Exception:
                    pass
            _remove_private_material(scratch, owned_trees)
    return {"resources": resources, "outputs": outputs}


def _volume_domain(coordinate_domain, objects):
    domain = str(coordinate_domain or "Generated")
    if domain in {"Generated", "UV0", "UV1", "Attribute", "Pointiness"}:
        return [0.0, 0.0, 0.0], [1.0, 1.0, 1.0]
    points = []
    for obj in list(objects or [])[:1]:
        for corner in list(getattr(obj, "bound_box", []) or []):
            try:
                point = obj.matrix_world @ corner if domain in {"Position", "World", "AbsoluteWorld"} else corner
                points.append(tuple(float(item) for item in point[:3]))
            except Exception:
                continue
    if not points:
        return [-1.0, -1.0, -1.0], [1.0, 1.0, 1.0]
    minimum = [min(point[index] for point in points) for index in range(3)]
    maximum = [max(point[index] for point in points) for index in range(3)]
    for index in range(3):
        if abs(maximum[index] - minimum[index]) < 1.0e-6:
            maximum[index] = minimum[index] + 1.0
    return minimum, maximum


def _copy_material_for_resource_bake(source):
    material = source.copy()
    material.name = "__B2U_RESOURCE_" + _safe_stem(source.name)
    owned = []
    visited = {}

    def copy_groups(tree):
        if tree is None:
            return
        for node in list(getattr(tree, "nodes", []) or []):
            if getattr(node, "bl_idname", "") != "ShaderNodeGroup" or node.node_tree is None:
                continue
            key = _pointer_key(node.node_tree)
            private = visited.get(key)
            if private is None:
                private = node.node_tree.copy()
                private.name = "__B2U_RESOURCE_GROUP_" + _safe_stem(node.node_tree.name)
                visited[key] = private
                owned.append(private)
                copy_groups(private)
            node.node_tree = private

    copy_groups(material.node_tree)
    return material, owned


def _find_private_source_node(material, source):
    tree = material.node_tree
    for group_name in list(source.get("groupPath") or [])[1:]:
        group = _node_by_name(tree, group_name)
        if group is None or getattr(group, "bl_idname", "") != "ShaderNodeGroup":
            raise RuntimeError(f"Private group '{group_name}' was not found.")
        tree = group.node_tree
    node = _node_by_name(tree, source.get("blenderNodeName"))
    if node is None:
        raise RuntimeError(f"Private source node '{source.get('blenderNodeName', '')}' was not found.")
    return tree, node


def _install_volume_coordinate(tree, node, coordinate_input):
    target = _input_by_name(node, coordinate_input)
    if target is None:
        raise RuntimeError(f"{getattr(node, 'name', 'Texture')} has no {coordinate_input} coordinate input.")
    for link in list(getattr(target, "links", []) or []):
        tree.links.remove(link)
    attribute = tree.nodes.new("ShaderNodeAttribute")
    attribute.name = "__B2U_VOLUME_COORDINATE__"
    attribute.attribute_name = "MikuVolumeCoord"
    coordinate = _output_by_name(attribute, "Color")
    if coordinate_input == "W":
        separate = tree.nodes.new("ShaderNodeSeparateColor")
        separate.name = "__B2U_VOLUME_W__"
        tree.links.new(coordinate, separate.inputs[0])
        coordinate = _output_by_name(separate, "Red")
    tree.links.new(coordinate, target)


def _expose_nested_output(
    root_tree,
    target_tree,
    target_node,
    source_socket,
    *,
    socket_type="NodeSocketColor",
):
    if root_tree == target_tree:
        return source_socket
    chain = []

    def find_chain(tree, path):
        if tree == target_tree:
            return list(path)
        for group in list(getattr(tree, "nodes", []) or []):
            if getattr(group, "bl_idname", "") != "ShaderNodeGroup" or group.node_tree is None:
                continue
            found = find_chain(group.node_tree, path + [group])
            if found is not None:
                return found
        return None

    chain = find_chain(root_tree, [])
    if chain is None:
        raise RuntimeError("The private source node is not reachable from the material root.")
    current_socket = source_socket
    current_tree = target_tree
    for depth, group_node in enumerate(reversed(chain)):
        socket_name = (
            f"__B2U_RESOURCE_{depth}_"
            f"{_safe_stem(getattr(target_node, 'name', 'Node'))}_"
            f"{_safe_stem(getattr(source_socket, 'name', 'Socket'))}"
        )
        group_output = next(
            (
                item for item in list(current_tree.nodes)
                if getattr(item, "bl_idname", "") == "NodeGroupOutput"
                and getattr(item, "is_active_output", True)
            ),
            None,
        )
        if group_output is None:
            group_output = current_tree.nodes.new("NodeGroupOutput")
        group_input = _input_by_name(group_output, socket_name)
        group_output_socket = _output_by_name(group_node, socket_name)
        if group_input is None or group_output_socket is None:
            interface = getattr(current_tree, "interface", None)
            if interface is None or not hasattr(interface, "new_socket"):
                raise RuntimeError("Blender node-group interface API is unavailable.")
            interface.new_socket(
                name=socket_name,
                in_out="OUTPUT",
                socket_type=socket_type,
            )
        if group_input is None or group_output_socket is None:
            current_tree.interface_update(bpy.context)
            group_input = _input_by_name(group_output, socket_name)
            group_output_socket = _output_by_name(group_node, socket_name)
        if group_input is None or group_output_socket is None:
            raise RuntimeError(f"Unable to expose nested output '{socket_name}'.")
        existing = list(getattr(group_input, "links", []) or [])
        if not any(link.from_socket == current_socket for link in existing):
            for link in existing:
                current_tree.links.remove(link)
            current_tree.links.new(current_socket, group_input)
        current_socket = group_output_socket
        current_tree = next(
            (
                tree for tree in [root_tree] + [item.node_tree for item in chain if item.node_tree]
                if group_node in list(getattr(tree, "nodes", []) or [])
            ),
            root_tree,
        )
    return current_socket


def _route_private_output_to_emission(tree, source_socket):
    outputs = [
        node for node in tree.nodes
        if getattr(node, "bl_idname", "") == "ShaderNodeOutputMaterial"
    ]
    output = next(
        (node for node in outputs if getattr(node, "is_active_output", False)),
        outputs[0] if outputs else None,
    )
    if output is None:
        output = tree.nodes.new("ShaderNodeOutputMaterial")
    surface = _input_by_name(output, "Surface")
    for link in list(getattr(surface, "links", []) or []):
        tree.links.remove(link)
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.name = "__B2U_RESOURCE_EMISSION__"
    tree.links.new(source_socket, _input_by_name(emission, "Color"))
    tree.links.new(emission.outputs[0], surface)


def _route_private_output_to_normal(tree, source_socket):
    outputs = [
        node
        for node in tree.nodes
        if getattr(node, "bl_idname", "") == "ShaderNodeOutputMaterial"
    ]
    output = next(
        (node for node in outputs if getattr(node, "is_active_output", False)),
        outputs[0] if outputs else None,
    )
    if output is None:
        output = tree.nodes.new("ShaderNodeOutputMaterial")
    surface = _input_by_name(output, "Surface")
    for link in list(getattr(surface, "links", []) or []):
        tree.links.remove(link)
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    principled.name = "__MIKU_ISLAND_NORMAL__"
    tree.links.new(source_socket, _input_by_name(principled, "Normal"))
    tree.links.new(principled.outputs[0], surface)


def _create_volume_atlas_object(context, resolution, columns, rows, domain_min, domain_max, material):
    vertices = []
    faces = []
    coordinates = []
    uvs = []
    for z_index in range(resolution):
        tile_x = z_index % columns
        tile_y = z_index // columns
        base = len(vertices)
        vertices.extend(
            [
                (float(tile_x), float(tile_y), 0.0),
                (float(tile_x + 1), float(tile_y), 0.0),
                (float(tile_x + 1), float(tile_y + 1), 0.0),
                (float(tile_x), float(tile_y + 1), 0.0),
            ]
        )
        faces.append((base, base + 1, base + 2, base + 3))
        z = domain_min[2] + (domain_max[2] - domain_min[2]) * ((z_index + 0.5) / resolution)
        coordinates.extend(
            [
                (domain_min[0], domain_min[1], z, 1.0),
                (domain_max[0], domain_min[1], z, 1.0),
                (domain_max[0], domain_max[1], z, 1.0),
                (domain_min[0], domain_max[1], z, 1.0),
            ]
        )
        u0, u1 = tile_x / columns, (tile_x + 1) / columns
        v0, v1 = tile_y / rows, (tile_y + 1) / rows
        uvs.extend([(u0, v0), (u1, v0), (u1, v1), (u0, v1)])
    mesh = bpy.data.meshes.new("__B2U_VOLUME_ATLAS_MESH__")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name="UVMap")
    attribute = mesh.color_attributes.new(
        name="MikuVolumeCoord",
        type="FLOAT_COLOR",
        domain="CORNER",
    )
    for loop_index, loop in enumerate(mesh.loops):
        uv_layer.data[loop_index].uv = uvs[loop.vertex_index]
        attribute.data[loop_index].color = coordinates[loop.vertex_index]
    obj = bpy.data.objects.new("__B2U_VOLUME_ATLAS_OBJECT__", mesh)
    context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj, mesh


def _select_only(context, objects):
    for candidate in list(getattr(context.view_layer, "objects", []) or []):
        try:
            candidate.select_set(False)
        except Exception:
            pass
    for obj in objects:
        obj.select_set(True)


def _save_float_image_atomic(image, output_path):
    scene = getattr(getattr(bpy, "context", None), "scene", None)
    settings = getattr(getattr(scene, "render", None), "image_settings", None)
    if settings is None:
        raise RuntimeError("Blender render image settings are unavailable for certified EXR output.")
    previous = {
        "file_format": getattr(settings, "file_format", None),
        "color_mode": getattr(settings, "color_mode", None),
        "color_depth": getattr(settings, "color_depth", None),
        "exr_codec": getattr(settings, "exr_codec", None),
    }
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    temporary = output_path + ".tmp.exr"
    try:
        settings.file_format = "OPEN_EXR"
        settings.color_mode = "RGBA"
        settings.color_depth = "16"
        settings.exr_codec = "ZIP"
        image.save_render(temporary, scene=scene)
        if not os.path.isfile(temporary):
            raise RuntimeError("Blender did not write the certified half-float EXR channel.")
        os.replace(temporary, output_path)
    finally:
        for name, value in previous.items():
            if value is not None:
                try:
                    setattr(settings, name, value)
                except Exception:
                    pass
        if os.path.isfile(temporary):
            try:
                os.remove(temporary)
            except OSError:
                pass


def _save_image_atomic(image, output_path, extension):
    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    temporary = output_path + ".tmp" + extension
    try:
        image.filepath_raw = temporary
        image.save()
        os.replace(temporary, output_path)
    finally:
        if os.path.isfile(temporary):
            try:
                os.remove(temporary)
            except OSError:
                pass


def _remove_resource_bake_object(obj, mesh):
    if obj is not None:
        try:
            bpy.data.objects.remove(obj, do_unlink=True)
        except Exception:
            pass
    if mesh is not None:
        try:
            bpy.data.meshes.remove(mesh)
        except Exception:
            pass


def _remove_private_material(material, owned_trees):
    if material is not None:
        try:
            bpy.data.materials.remove(material)
        except Exception:
            pass
    for tree in reversed(list(owned_trees or [])):
        try:
            bpy.data.node_groups.remove(tree)
        except Exception:
            pass


def _bake_direction_node(context, source_material, exported_node, output_names, bake_dir, width):
    source = exported_node.get("source") or {}
    source_tree = source_material.node_tree
    for group_name in list(source.get("groupPath") or [])[1:]:
        group = _node_by_name(source_tree, group_name)
        if group is None or getattr(group, "bl_idname", "") != "ShaderNodeGroup":
            raise RuntimeError(f"Direction source group '{group_name}' was not found.")
        source_tree = group.node_tree
    source_node = _node_by_name(source_tree, source.get("blenderNodeName"))
    if source_node is None:
        raise RuntimeError(f"Direction source node '{source.get('blenderNodeName', '')}' was not found.")

    scene = bpy.data.scenes.new("__B2U_DIRECTION_SCENE__")
    world = bpy.data.worlds.new("__B2U_DIRECTION_WORLD__")
    camera_data = bpy.data.cameras.new("__B2U_DIRECTION_CAMERA__")
    camera = bpy.data.objects.new("__B2U_DIRECTION_CAMERA__", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    scene.world = world
    world.use_nodes = True
    try:
        tree = world.node_tree
        tree.nodes.clear()
        output = tree.nodes.new("ShaderNodeOutputWorld")
        background = tree.nodes.new("ShaderNodeBackground")
        copied = tree.nodes.new(source_node.bl_idname)
        _copy_direction_node_settings(source_node, copied)
        color = _output_by_name(copied, output_names[0] if output_names else "Color")
        if color is None:
            color = next(iter(copied.outputs), None)
        if color is None:
            raise RuntimeError("The direction texture node has no output.")
        if _input_by_name(copied, "Vector") is not None:
            texcoord = tree.nodes.new("ShaderNodeTexCoord")
            direction = _output_by_name(texcoord, "Normal") or _output_by_name(texcoord, "Generated")
            if direction is not None:
                tree.links.new(direction, _input_by_name(copied, "Vector"))
        tree.links.new(color, _input_by_name(background, "Color"))
        tree.links.new(background.outputs[0], _input_by_name(output, "Surface"))

        camera_data.type = "PANO"
        camera_data.panorama_type = "EQUIRECTANGULAR"
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 1
        scene.render.resolution_x = width
        scene.render.resolution_y = width // 2
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "OPEN_EXR"
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.image_settings.color_depth = "16"
        scene.render.film_transparent = False
        stem = _safe_stem(f"{exported_node.get('id', 'texture')}_direction")
        file_name = stem + ".exr"
        output_path = os.path.join(bake_dir, file_name)
        temporary = output_path + ".tmp.exr"
        scene.render.filepath = temporary
        bpy.ops.render.render(scene=scene.name, write_still=True)
        rendered = temporary if os.path.isfile(temporary) else temporary + ".exr"
        os.replace(rendered, output_path)

        resource_id = "miku_direction_" + hashlib.sha1(
            str(exported_node.get("id") or "").encode("utf-8")
        ).hexdigest()[:16]
        resource = {
            "id": resource_id,
            "kind": "direction_lut",
            "type": "Texture2D",
            "representation": "DirectionLut",
            "path": f"Baked/{file_name}",
            "uri": f"Baked/{file_name}",
            "exportFileName": file_name,
            "recommendedColorSpace": "Linear",
            "format": "RGBAHalf",
            "dimensions": {"width": width, "height": width // 2},
            "projection": "Equirectangular",
            "wrapMode": "Repeat",
            "filterMode": "Linear",
            "generatedBy": "blender-direction-bake",
        }
        outputs = {
            name: {
                "resource": resource_id,
                "representation": "DirectionLut",
                "translationQuality": "Baked",
                "channel": "RGBA" if name in {"Color", "Vector"} else "R",
                "coordinateDomain": "Direction",
            }
            for name in output_names
        }
        return {"resources": {resource_id: resource}, "outputs": outputs}
    finally:
        try:
            bpy.data.objects.remove(camera, do_unlink=True)
        except Exception:
            pass
        try:
            bpy.data.cameras.remove(camera_data)
        except Exception:
            pass
        try:
            bpy.data.worlds.remove(world)
        except Exception:
            pass
        try:
            bpy.data.scenes.remove(scene)
        except Exception:
            pass


def _copy_direction_node_settings(source, target):
    for name in (
        "sky_type",
        "sun_direction",
        "sun_elevation",
        "sun_rotation",
        "sun_disc",
        "sun_size",
        "sun_intensity",
        "altitude",
        "air_density",
        "dust_density",
        "ozone_density",
        "turbidity",
        "ground_albedo",
        "projection",
        "interpolation",
        "mode",
        "filepath",
        "ies",
        "image",
    ):
        if not hasattr(source, name) or not hasattr(target, name):
            continue
        try:
            setattr(target, name, getattr(source, name))
        except Exception:
            pass
    for source_input in list(getattr(source, "inputs", []) or []):
        target_input = _input_by_name(target, getattr(source_input, "name", ""))
        if target_input is None or getattr(source_input, "is_linked", False):
            continue
        try:
            target_input.default_value = source_input.default_value
        except Exception:
            pass


def bake_cache_key(graph, resolution, samples, margin, dependencies=None):
    payload = {
        "documentKind": BAKE_SCHEMA,
        "schemaVersion": "1.0",
        "algorithmRevision": BAKE_ALGORITHM_REVISION,
        "graph": _stable_graph_for_cache(graph),
        "dependencies": dependencies or {},
        "resolution": int(resolution),
        "samples": int(samples),
        "margin": int(margin),
        "blender": list(getattr(getattr(bpy, "app", None), "version", ()) or ()),
    }
    encoded = json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":"), default=str).encode("utf-8")
    return hashlib.sha256(encoded).hexdigest()


def _stable_graph_for_cache(graph):
    """Remove export-only volatility while retaining node parameters and topology."""
    node_names = {}
    normalized_nodes = []
    for node in graph.get("nodes", []) or []:
        source = node.get("source") or {}
        stable_name = "|".join(
            (
                str(node.get("op", "")),
                str(source.get("blenderNodeName", "")),
                "/".join(str(item) for item in source.get("groupPath", []) or []),
            )
        )
        node_names[str(node.get("id", ""))] = stable_name
        normalized_nodes.append(
            {
                "name": stable_name,
                "op": node.get("op", ""),
                "params": node.get("params") or {},
                "inputs": [
                    {
                        "id": item.get("id", ""),
                        "name": item.get("name", ""),
                        "defaultValue": item.get("defaultValue"),
                    }
                    for item in node.get("inputs", []) or []
                ],
                "outputs": [
                    {"id": item.get("id", ""), "name": item.get("name", "")}
                    for item in node.get("outputs", []) or []
                ],
            }
        )
    normalized_nodes.sort(key=lambda item: (item["name"], json.dumps(item, ensure_ascii=False, sort_keys=True, default=str)))

    normalized_edges = []
    for edge in graph.get("edges", []) or []:
        source = edge.get("from") or {}
        target = edge.get("to") or {}
        normalized_edges.append(
            {
                "from": {
                    "node": node_names.get(str(source.get("node", "")), str(source.get("node", ""))),
                    "socket": source.get("socket", ""),
                },
                "to": {
                    "node": node_names.get(str(target.get("node", "")), str(target.get("node", ""))),
                    "socket": target.get("socket", ""),
                },
            }
        )
    normalized_edges.sort(key=lambda item: json.dumps(item, ensure_ascii=False, sort_keys=True, default=str))

    resources = {}
    for key, resource in sorted((graph.get("resources") or {}).items()):
        resources[key] = {item_key: value for item_key, value in resource.items() if item_key not in {"exportPath"}}
    return {
        "version": graph.get("version", ""),
        "material": graph.get("material") or {},
        "nodes": normalized_nodes,
        "edges": normalized_edges,
        "resources": resources,
    }


def _bake_dependencies(context, material_objects, graph, appearance_approximation):
    """Fingerprint every external input that can change baked pixels."""
    depsgraph = None
    try:
        depsgraph = context.evaluated_depsgraph_get()
    except Exception:
        pass

    dependencies = {
        "sourceImages": _resource_file_fingerprints(graph),
        "targetMeshes": [_mesh_fingerprint(obj, depsgraph) for obj in sorted(material_objects, key=_object_name)],
    }
    if appearance_approximation:
        dependencies["appearanceScene"] = _appearance_scene_fingerprint(context, depsgraph)
    return dependencies


def _resource_file_fingerprints(graph):
    result = []
    for resource_id, resource in sorted((graph.get("resources") or {}).items()):
        if not isinstance(resource, dict) or resource.get("kind") != "image":
            continue
        path = resource.get("exportPath") or resource.get("blenderImageFilepath") or ""
        if path and not os.path.isabs(path):
            try:
                path = bpy.path.abspath(path)
            except Exception:
                pass
        item = {
            "resource": resource_id,
            "image": resource.get("blenderImageName") or resource.get("name") or "",
            "uri": resource.get("uri") or resource.get("path") or "",
        }
        if path and os.path.isfile(path):
            item.update({"sha256": _file_sha256(path), "bytes": os.path.getsize(path)})
        else:
            # Missing files are part of the key too.  A later successful export
            # therefore invalidates the cache instead of reusing blank input.
            item["missing"] = True
        result.append(item)
    return result


def _file_sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        while True:
            chunk = handle.read(1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
    return digest.hexdigest()


def _object_name(obj):
    return str(getattr(obj, "name_full", "") or getattr(obj, "name", ""))


def _mesh_fingerprint(obj, depsgraph=None):
    digest = hashlib.sha256()
    _hash_text(digest, _object_name(obj))
    _hash_text(digest, getattr(obj, "type", ""))
    _hash_float_sequence(digest, _flatten_matrix(getattr(obj, "matrix_world", None)))
    evaluated = obj
    temporary_mesh = False
    try:
        if depsgraph is not None and hasattr(obj, "evaluated_get"):
            evaluated = obj.evaluated_get(depsgraph)
        if hasattr(evaluated, "to_mesh"):
            mesh = evaluated.to_mesh()
            temporary_mesh = mesh is not None
        else:
            mesh = getattr(evaluated, "data", None)
        if mesh is None:
            _hash_text(digest, "missing-mesh")
            return {"object": _object_name(obj), "sha256": digest.hexdigest(), "missing": True}

        vertices = list(getattr(mesh, "vertices", []) or [])
        polygons = list(getattr(mesh, "polygons", []) or [])
        loops = list(getattr(mesh, "loops", []) or [])
        _hash_ints(digest, (len(vertices), len(polygons), len(loops)))
        for vertex in vertices:
            _hash_float_sequence(digest, getattr(vertex, "co", ()))
            _hash_float_sequence(digest, getattr(vertex, "normal", ()))
        for polygon in polygons:
            _hash_ints(
                digest,
                (
                    int(getattr(polygon, "material_index", 0)),
                    int(getattr(polygon, "loop_start", 0)),
                    int(getattr(polygon, "loop_total", 0)),
                    int(bool(getattr(polygon, "use_smooth", False))),
                    *tuple(int(value) for value in getattr(polygon, "vertices", ()) or ()),
                ),
            )

        uv_layers = getattr(mesh, "uv_layers", None)
        uv_layer = None
        if uv_layers is not None:
            uv_layer = getattr(uv_layers, "active_render", None) or getattr(uv_layers, "active", None)
            if uv_layer is None and len(uv_layers):
                uv_layer = uv_layers[0]
        _hash_text(digest, getattr(uv_layer, "name", "") if uv_layer is not None else "no-uv")
        if uv_layer is not None:
            uv_data = list(getattr(uv_layer, "data", []) or [])
            _hash_ints(digest, (len(uv_data),))
            for item in uv_data:
                _hash_float_sequence(digest, getattr(item, "uv", ()))
            tangents_ready = False
            try:
                mesh.calc_tangents(uvmap=getattr(uv_layer, "name", ""))
                tangents_ready = True
                for loop in loops:
                    _hash_float_sequence(digest, getattr(loop, "normal", ()))
                    _hash_float_sequence(digest, getattr(loop, "tangent", ()))
                    _hash_float_sequence(digest, (getattr(loop, "bitangent_sign", 0.0),))
            finally:
                if tangents_ready and hasattr(mesh, "free_tangents"):
                    mesh.free_tangents()

        slots = list(getattr(obj, "material_slots", []) or [])
        material_slots = []
        for slot in slots:
            material = getattr(slot, "material", None)
            material_name = (
                getattr(material, "name_full", "")
                or getattr(material, "name", "")
            )
            material_slots.append(str(material_name))
            _hash_text(digest, material_name)
        return {
            "object": _object_name(obj),
            "sha256": digest.hexdigest(),
            "vertices": len(vertices),
            "polygons": len(polygons),
            "indices": sum(
                int(getattr(polygon, "loop_total", 0))
                for polygon in polygons
            ),
            "uv": getattr(uv_layer, "name", "") if uv_layer is not None else "",
            "uvCount": (
                len(list(getattr(uv_layer, "data", []) or []))
                if uv_layer is not None
                else 0
            ),
            "materialSlots": material_slots,
        }
    except Exception as exc:
        # A stable fallback still prevents cross-object cache collisions.  The
        # warning is visible in the sidecar for support diagnostics.
        _hash_text(digest, type(exc).__name__ + ":" + str(exc))
        return {"object": _object_name(obj), "sha256": digest.hexdigest(), "warning": str(exc)}
    finally:
        if temporary_mesh and hasattr(evaluated, "to_mesh_clear"):
            try:
                evaluated.to_mesh_clear()
            except Exception:
                pass


def _appearance_scene_fingerprint(context, depsgraph=None):
    scene = getattr(context, "scene", None)
    digest = hashlib.sha256()
    _hash_ints(digest, (int(getattr(scene, "frame_current", 0)),))
    render = getattr(scene, "render", None)
    _hash_text(digest, getattr(render, "engine", ""))
    _hash_ints(digest, (int(bool(getattr(render, "film_transparent", False))),))

    world = getattr(scene, "world", None)
    if world is not None:
        _hash_text(digest, getattr(world, "name_full", "") or getattr(world, "name", ""))
        _hash_float_sequence(digest, getattr(world, "color", ()))
        _hash_text(digest, _node_tree_fingerprint(getattr(world, "node_tree", None)))

    scene_objects = sorted(list(getattr(scene, "objects", []) or []), key=_object_name)
    for obj in scene_objects:
        _hash_text(digest, _object_name(obj))
        _hash_text(digest, getattr(obj, "type", ""))
        _hash_ints(digest, (int(bool(getattr(obj, "hide_render", False))),))
        _hash_float_sequence(digest, _flatten_matrix(getattr(obj, "matrix_world", None)))
        if getattr(obj, "type", "") == "MESH":
            _hash_text(digest, _mesh_fingerprint(obj, depsgraph).get("sha256", ""))
            for slot in list(getattr(obj, "material_slots", []) or []):
                material = getattr(slot, "material", None)
                _hash_text(digest, _node_tree_fingerprint(getattr(material, "node_tree", None)))
        elif getattr(obj, "type", "") == "LIGHT":
            light = getattr(obj, "data", None)
            _hash_text(digest, getattr(light, "type", ""))
            _hash_float_sequence(digest, getattr(light, "color", ()))
            _hash_float_sequence(
                digest,
                (
                    getattr(light, "energy", 0.0),
                    getattr(light, "power", 0.0),
                    getattr(light, "size", 0.0),
                    getattr(light, "spot_size", 0.0),
                    getattr(light, "spot_blend", 0.0),
                ),
            )
            _hash_text(digest, _node_tree_fingerprint(getattr(light, "node_tree", None)))
    return {
        "sha256": digest.hexdigest(),
        "frame": int(getattr(scene, "frame_current", 0)),
        "objects": len(scene_objects),
    }


def _node_tree_fingerprint(tree):
    if tree is None:
        return ""
    digest = hashlib.sha256()
    nodes = sorted(list(getattr(tree, "nodes", []) or []), key=lambda node: (str(getattr(node, "name", "")), str(getattr(node, "bl_idname", ""))))
    for node in nodes:
        _hash_text(digest, getattr(node, "name", ""))
        _hash_text(digest, getattr(node, "bl_idname", ""))
        _hash_ints(digest, (int(bool(getattr(node, "mute", False))),))
        for socket in list(getattr(node, "inputs", []) or []):
            _hash_text(digest, getattr(socket, "identifier", "") or getattr(socket, "name", ""))
            _hash_json_value(digest, getattr(socket, "default_value", None))
        image = getattr(node, "image", None)
        if image is not None:
            _hash_text(digest, getattr(image, "name_full", "") or getattr(image, "name", ""))
            image_path = getattr(image, "filepath", "") or getattr(image, "filepath_raw", "") or ""
            try:
                image_path = bpy.path.abspath(image_path) if image_path else ""
            except Exception:
                pass
            if image_path and os.path.isfile(image_path):
                _hash_text(digest, _file_sha256(image_path))
    links = []
    for link in list(getattr(tree, "links", []) or []):
        links.append(
            (
                getattr(getattr(link, "from_node", None), "name", ""),
                getattr(getattr(link, "from_socket", None), "identifier", "") or getattr(getattr(link, "from_socket", None), "name", ""),
                getattr(getattr(link, "to_node", None), "name", ""),
                getattr(getattr(link, "to_socket", None), "identifier", "") or getattr(getattr(link, "to_socket", None), "name", ""),
            )
        )
    for item in sorted(links):
        _hash_json_value(digest, item)
    return digest.hexdigest()


def _flatten_matrix(matrix):
    if matrix is None:
        return ()
    try:
        return tuple(float(value) for row in matrix for value in row)
    except Exception:
        try:
            return tuple(float(value) for value in matrix)
        except Exception:
            return ()


def _hash_text(digest, value):
    encoded = str(value or "").encode("utf-8", errors="replace")
    digest.update(struct.pack("<I", len(encoded)))
    digest.update(encoded)


def _hash_ints(digest, values):
    for value in values:
        digest.update(struct.pack("<q", int(value)))


def _hash_float_sequence(digest, values):
    try:
        items = list(values)
    except Exception:
        items = []
    _hash_ints(digest, (len(items),))
    for value in items:
        digest.update(struct.pack("<d", float(value)))


def _hash_json_value(digest, value):
    try:
        if not isinstance(value, (str, int, float, bool, list, tuple, dict, type(None))):
            value = list(value)
    except Exception:
        value = str(value)
    _hash_text(digest, json.dumps(value, ensure_ascii=False, sort_keys=True, default=str, separators=(",", ":")))


def bake_material(
    context,
    objects,
    material,
    graph,
    miku_path,
    settings,
    allow_appearance_approximation=True,
    channel_specs=None,
    channel_scoped=False,
    parity_strategy=None,
):
    """Bake one material and return a serializable result for MIKU injection."""
    channel_specs = tuple(channel_specs or CHANNELS)
    resolution = int(getattr(settings, "bake_resolution", "1024") or 1024)
    samples = max(1, int(getattr(settings, "bake_samples", 16) or 16))
    margin = max(0, int(getattr(settings, "bake_margin", 16) or 16))
    bake_dir = os.path.join(os.path.dirname(miku_path), "Baked")
    os.makedirs(bake_dir, exist_ok=True)
    cache_path = os.path.join(bake_dir, ".miku-bake-cache.json")
    cache_key = bake_cache_key(graph, resolution, samples, margin)

    material_objects = _objects_using_material(objects, material)
    if not material_objects:
        return _failed_result(cache_key, resolution, samples, margin, "No exported mesh uses this material.")
    if not getattr(material, "use_nodes", False) or getattr(material, "node_tree", None) is None:
        return _failed_result(cache_key, resolution, samples, margin, "Material has no node tree to bake.")

    uv_diagnostics = []
    try:
        for obj in material_objects:
            had_uv = len(getattr(getattr(obj, "data", None), "uv_layers", []) or []) > 0
            if not _ensure_uv(context, obj, bool(getattr(settings, "auto_generate_uv", True))):
                return _failed_result(
                    cache_key,
                    resolution,
                    samples,
                    margin,
                    f"Mesh {getattr(obj, 'name', 'Mesh')} has no UV map and automatic UV generation is disabled or failed.",
                )
            if not had_uv:
                uv_diagnostics.append(getattr(obj, "name", "Mesh"))
    except Exception as exc:
        return _failed_result(cache_key, resolution, samples, margin, f"UV preparation failed: {exc}")

    principled, branch_approximation = _find_principled(material)
    semantic_plan = _semantic_closure_plan(material)
    non_bakeable = list((semantic_plan or {}).get("nonBakeableDependencies") or [])
    ignored_closure_dependencies = []
    if semantic_plan is not None:
        requested_semantics = {item[0] for item in channel_specs}
        scoped_dependencies = sorted(
            {
                dependency
                for semantic in requested_semantics
                for dependency in _semantic_channel_dependencies(
                    semantic_plan["root"],
                    semantic,
                )
            }
        )
        if "Normal" in requested_semantics:
            displacement = semantic_plan.get("displacement")
            if displacement is not None:
                scoped_dependencies = sorted(
                    {
                        *scoped_dependencies,
                        *(
                            dependency
                            for socket in (
                                displacement.get("height"),
                                displacement.get("scale"),
                            )
                            for dependency in _non_bakeable_socket_dependencies(
                                socket
                            )
                        ),
                    }
                )
        ignored_closure_dependencies = sorted(
            set(non_bakeable) - set(scoped_dependencies)
        )
        non_bakeable = scoped_dependencies
    if non_bakeable and not allow_appearance_approximation:
        return _skipped_result(
            cache_key,
            resolution,
            samples,
            margin,
            "The material depends on view, camera, light-path, or time data that cannot be encoded in UV textures.",
            non_bakeable,
        )
    if non_bakeable:
        semantic_plan = None
    if semantic_plan is None and not allow_appearance_approximation:
        return _skipped_result(
            cache_key,
            resolution,
            samples,
            margin,
            "The closure graph cannot be lowered to portable Standard PBR channels without baking final lighting.",
            ["UnsupportedClosure"],
        )
    if principled is None and semantic_plan is not None:
        principled = semantic_plan.get("primaryPrincipled")
    # Appearance snapshots remain available only to explicit legacy/forced
    # workflows. Auto Hybrid always requires a lighting-independent plan.
    appearance_approximation = semantic_plan is None and (principled is None or branch_approximation)
    alpha_state = _material_alpha_state(material, principled)
    dependencies = _bake_dependencies(context, material_objects, graph, appearance_approximation)
    dependencies["channelSpecs"] = [list(item) for item in channel_specs]
    dependencies["channelScope"] = "Channels" if channel_scoped else "Material"
    cache_key = bake_cache_key(graph, resolution, samples, margin, dependencies)
    cached = _read_cache(cache_path)
    if cached and cached.get("cacheKey") == cache_key and _cache_outputs_exist(cached, bake_dir):
        cached["status"] = "reused"
        return cached

    safe_name = _safe_stem(getattr(material, "name", "Material"))
    channel_results = {}
    failures = []
    original_engine = getattr(context.scene.render, "engine", "")
    cycles = getattr(context.scene, "cycles", None)
    original_samples = getattr(cycles, "samples", None)
    try:
        context.scene.render.engine = "CYCLES"
        if cycles is not None:
            cycles.samples = samples
        for semantic, socket_name, channel, color_space in channel_specs:
            output_path = os.path.join(bake_dir, f"{safe_name}_{semantic}.png")
            try:
                if appearance_approximation and semantic != "BaseColor":
                    neutral = {
                        "Metalness": (0.0, 0.0, 0.0, 1.0),
                        "Roughness": (0.5, 0.5, 0.5, 1.0),
                        "Normal": (0.5, 0.5, 1.0, 1.0),
                        "Emission": (0.0, 0.0, 0.0, 1.0),
                        "Alpha": (1.0, 1.0, 1.0, 1.0),
                        "IOR": (1.45, 1.45, 1.45, 1.0),
                    }[semantic]
                    result = _write_constant_channel(
                        material,
                        semantic,
                        channel,
                        color_space,
                        neutral,
                        output_path,
                        resolution,
                    )
                else:
                    result = _bake_channel(
                        context,
                        material_objects,
                        material,
                        principled,
                        semantic,
                        socket_name,
                        channel,
                        color_space,
                        output_path,
                        resolution,
                        margin,
                        appearance_approximation=appearance_approximation,
                        semantic_plan=semantic_plan,
                    )
                channel_results[semantic] = result
            except Exception as exc:
                failures.append({"semantic": semantic, "message": str(exc)})
    finally:
        try:
            context.scene.render.engine = original_engine
        except Exception:
            pass
        if cycles is not None and original_samples is not None:
            try:
                cycles.samples = original_samples
            except Exception:
                pass

    requested_semantics = {item[0] for item in channel_specs}
    if channel_scoped:
        required = requested_semantics
    else:
        required = (
            {"BaseColor", "Roughness", "IOR", "Normal"}
            if "IOR" in requested_semantics
            else {"BaseColor", "Metalness", "Roughness"}
        )
    status = "completed" if required.issubset(channel_results) else "failed"
    result = {
        "documentKind": BAKE_SCHEMA,
        "schemaVersion": "1.0",
        "algorithmRevision": BAKE_ALGORITHM_REVISION,
        "status": status,
        "cacheKey": cache_key,
        "resolution": resolution,
        "samples": samples,
        "margin": margin,
        "evaluator": "BLENDER_CHANNEL_BAKE",
        "channels": channel_results,
        "failures": failures,
        "principledBranchApproximation": bool(branch_approximation),
        "appearanceApproximation": bool(appearance_approximation),
        "mixedClosureSemanticBake": bool(
            semantic_plan
            and semantic_plan.get("root", {}).get("kind") in {"mix", "add"}
        ),
        "parityStrategy": (
            parity_strategy
            or ("AppearanceSnapshot" if appearance_approximation else "SemanticPbrChannels")
        ),
        "branches": {
            semantic: {
                "domain": "MeshUV",
                "representation": "Texture2D",
                "meshBindingRequired": True,
                "colorSpace": result.get("colorSpace", "Linear"),
                "normalMap": semantic == "Normal",
            }
            for semantic, result in channel_results.items()
        },
        "closureApproximations": list((semantic_plan or {}).get("approximations") or []),
        "nonBakeableDependencies": non_bakeable,
        "runtimeComposedDependencies": ignored_closure_dependencies,
        "alphaMode": alpha_state["alphaMode"],
        "alphaCutoff": alpha_state["alphaCutoff"],
        "dependencies": dependencies,
        "autoGeneratedUvObjects": sorted(set(uv_diagnostics)),
    }
    if status == "completed":
        _write_cache(cache_path, result)
    return result


def bake_expression_islands(
    context,
    objects,
    material,
    graph,
    jobs,
    miku_path,
    settings,
):
    """Bake maximal static node/socket islands without flattening runtime inputs."""

    resolution = int(getattr(settings, "bake_resolution", "1024") or 1024)
    margin = max(0, int(getattr(settings, "bake_margin", 16) or 16))
    samples = max(1, int(getattr(settings, "bake_samples", 16) or 16))
    bake_dir = os.path.join(os.path.dirname(miku_path), "Baked")
    os.makedirs(bake_dir, exist_ok=True)
    material_objects = _objects_using_material(objects, material)
    if not material_objects:
        return {
            "status": "failed",
            "islands": {},
            "failures": [
                {
                    "code": "MIKU_STATIC_EXPRESSION_ISLAND_BAKE_FAILED",
                    "message": "No exported mesh uses this material.",
                }
            ],
        }
    for obj in material_objects:
        if not _ensure_uv(context, obj, False):
            return {
                "status": "failed",
                "islands": {},
                "failures": [
                    {
                        "code": "MIKU_STATIC_EXPRESSION_ISLAND_BAKE_FAILED",
                        "message": (
                            f"Mesh {getattr(obj, 'name', 'Mesh')} has no UV map."
                        ),
                    }
                ],
            }
    snapshot_nodes = {
        str(item.get("id") or ""): item
        for item in graph.get("nodes", []) or []
        if isinstance(item, dict)
    }
    results = {}
    failures = []
    original_engine = getattr(context.scene.render, "engine", "")
    cycles = getattr(context.scene, "cycles", None)
    original_samples = getattr(cycles, "samples", None)
    try:
        context.scene.render.engine = "CYCLES"
        if cycles is not None:
            cycles.samples = samples
        for job in sorted(jobs, key=lambda item: str(item.get("jobId") or "")):
            expression_id = str(job.get("expressionId") or "")
            source_node_id = str(job.get("sourceNodeId") or "")
            source_socket_id = str(job.get("sourceSocketId") or "")
            usage = str(job.get("usage") or "Color")
            try:
                source_record = snapshot_nodes.get(source_node_id)
                source = (
                    source_record.get("source")
                    if isinstance(source_record, dict)
                    and isinstance(source_record.get("source"), dict)
                    else None
                )
                if source is None:
                    raise RuntimeError(
                        f"Source node {source_node_id!r} is unavailable."
                    )
                scratch, owned_trees = _copy_material_for_resource_bake(
                    material
                )
                image = None
                scratch_images = []
                target_states = []
                assignments = []
                selection_state = _capture_selection(context)
                try:
                    source_tree, source_node = _find_private_source_node(
                        scratch, source
                    )
                    source_socket = _output_by_identifier(
                        source_node, source_socket_id
                    )
                    if source_socket is None:
                        raise RuntimeError(
                            f"Node {source.get('blenderNodeName')!r} has no "
                            f"output {source_socket_id!r}."
                        )
                    root_socket = _expose_nested_output(
                        scratch.node_tree,
                        source_tree,
                        source_node,
                        source_socket,
                        socket_type=(
                            "NodeSocketVector"
                            if usage == "Normal"
                            else (
                                "NodeSocketFloat"
                                if usage == "Scalar"
                                else "NodeSocketColor"
                            )
                        ),
                    )
                    if usage == "Normal":
                        _route_private_output_to_normal(
                            scratch.node_tree, root_socket
                        )
                        bake_type = "NORMAL"
                    else:
                        _route_private_output_to_emission(
                            scratch.node_tree, root_socket
                        )
                        bake_type = "EMIT"
                    float_buffer = usage in {"Color", "Scalar"}
                    suffix = ".exr" if float_buffer else ".png"
                    safe_id = _safe_stem(
                        str(job.get("resourceId") or expression_id)
                    )
                    output_path = os.path.join(
                        bake_dir,
                        f"{_safe_stem(material.name)}_{safe_id}{suffix}",
                    )
                    image = bpy.data.images.new(
                        name=f"Miku_Island_{safe_id}",
                        width=resolution,
                        height=resolution,
                        alpha=True,
                        float_buffer=float_buffer,
                    )
                    image.file_format = (
                        "OPEN_EXR" if float_buffer else "PNG"
                    )
                    _set_image_color_space(image, "Linear")
                    assignments = _assign_private_material(
                        material_objects, material, scratch
                    )
                    for assigned_material in _materials_on_objects(
                        material_objects
                    ):
                        assigned_image = image
                        if assigned_material != scratch:
                            assigned_image = bpy.data.images.new(
                                name=(
                                    f"Miku_Island_Scratch_{safe_id}_"
                                    f"{len(scratch_images)}"
                                ),
                                width=4,
                                height=4,
                                alpha=True,
                                float_buffer=False,
                            )
                            scratch_images.append(assigned_image)
                        target_states.append(
                            _install_bake_target(
                                assigned_material, assigned_image
                            )
                        )
                    _select_only(context, material_objects)
                    kwargs = {
                        "type": bake_type,
                        "margin": margin,
                        "margin_type": "EXTEND",
                        "use_clear": True,
                        "target": "IMAGE_TEXTURES",
                        "save_mode": "INTERNAL",
                    }
                    if bake_type == "NORMAL":
                        kwargs["normal_space"] = "TANGENT"
                    outcome = _invoke_bake(
                        context, material_objects, kwargs
                    )
                    if "FINISHED" not in set(outcome or []):
                        raise RuntimeError(
                            f"Blender bake operator returned {outcome!r}."
                        )
                    if float_buffer:
                        _save_float_image_atomic(image, output_path)
                    else:
                        _save_image_atomic(image, output_path, ".png")
                    if not os.path.isfile(output_path):
                        raise RuntimeError(
                            "Blender did not write the expression island image."
                        )
                    results[expression_id] = {
                        "relativePath": (
                            "Baked/" + os.path.basename(output_path)
                        ),
                        "mediaType": (
                            "image/x-exr" if float_buffer else "image/png"
                        ),
                        "channel": str(job.get("channel") or "RGB"),
                        "colorSpace": "Linear",
                        "channelCount": (
                            1 if usage == "Scalar" else 3
                        ),
                        "componentBytes": 2 if float_buffer else 1,
                        "resourceId": str(job.get("resourceId") or ""),
                        "referenceName": str(
                            job.get("referenceName") or ""
                        ),
                        "usage": usage,
                        "sourceNodeId": source_node_id,
                        "sourceSocketId": source_socket_id,
                    }
                finally:
                    for state in reversed(target_states):
                        _remove_bake_target(state)
                    _restore_private_material_assignments(assignments)
                    _restore_selection(context, selection_state)
                    for scratch_image in scratch_images:
                        try:
                            bpy.data.images.remove(scratch_image)
                        except Exception:
                            pass
                    if image is not None:
                        try:
                            bpy.data.images.remove(image)
                        except Exception:
                            pass
                    _remove_private_material(scratch, owned_trees)
            except Exception as exc:
                failures.append(
                    {
                        "code": "MIKU_STATIC_EXPRESSION_ISLAND_BAKE_FAILED",
                        "expressionId": expression_id,
                        "sourceNodeId": source_node_id,
                        "sourceSocketId": source_socket_id,
                        "message": (
                            f"{source_node_id}:{source_socket_id}: {exc}"
                        ),
                    }
                )
    finally:
        try:
            context.scene.render.engine = original_engine
        except Exception:
            pass
        if cycles is not None and original_samples is not None:
            try:
                cycles.samples = original_samples
            except Exception:
                pass
    return {
        "status": "completed" if not failures else "failed",
        "islands": results,
        "failures": failures,
        "resolution": resolution,
        "samples": samples,
        "margin": margin,
        "dependencies": {
            "targetMeshes": [
                _mesh_fingerprint(obj)
                for obj in sorted(
                    material_objects, key=lambda item: item.name
                )
            ]
        },
    }


def _bake_channel(
    context,
    objects,
    target_material,
    principled,
    semantic,
    socket_name,
    channel,
    color_space,
    output_path,
    resolution,
    margin,
    appearance_approximation=False,
    semantic_plan=None,
):
    floating_output = semantic in {"BaseColor", "Emission"}
    if floating_output:
        output_path = os.path.splitext(output_path)[0] + ".exr"
        color_space = "Linear"
    image = bpy.data.images.new(
        name=f"Miku_Bake_{_safe_stem(target_material.name)}_{semantic}",
        width=resolution,
        height=resolution,
        alpha=True,
        float_buffer=floating_output,
    )
    image.file_format = "OPEN_EXR" if floating_output else "PNG"
    _set_image_color_space(image, color_space)
    scratch_images = []
    node_states = []
    surface_state = None
    working_material = target_material
    working_plan = semantic_plan
    owned_trees = []
    private_assignments = []
    selection_state = _capture_selection(context)
    try:
        if semantic_plan is not None:
            working_material, owned_trees = _copy_material_for_resource_bake(
                target_material
            )
            private_assignments = _assign_private_material(
                objects,
                target_material,
                working_material,
            )
            if not private_assignments:
                raise RuntimeError(
                    "Source Mesh Fidelity requires the source material to be "
                    "assigned to at least one selected mesh object."
                )
            working_plan = _semantic_closure_plan(working_material)
            if working_plan is None:
                raise RuntimeError(
                    "The private material copy no longer has a supported "
                    "surface closure plan."
                )
        materials = _materials_on_objects(objects)
        for material in materials:
            assigned_image = image
            if material != working_material:
                assigned_image = bpy.data.images.new(
                    name=f"Miku_Bake_Scratch_{semantic}_{len(scratch_images)}",
                    width=4,
                    height=4,
                    alpha=True,
                    float_buffer=False,
                )
                scratch_images.append(assigned_image)
            node_states.append(_install_bake_target(material, assigned_image))

        if semantic_plan is not None:
            if semantic == "Normal":
                surface_state = _route_semantic_normal_to_surface(
                    working_material,
                    working_plan,
                )
                bake_type = "NORMAL"
            else:
                surface_state = _route_semantic_channel_to_emission(
                    working_material,
                    working_plan,
                    semantic,
                )
                bake_type = "EMIT"
        elif appearance_approximation:
            bake_type = "COMBINED"
        elif semantic == "Normal":
            bake_type = "NORMAL"
        else:
            socket = _input_by_name(principled, socket_name)
            if socket is None and semantic == "Emission":
                socket = _input_by_name(principled, "Emission")
            if socket is None:
                raise RuntimeError(f"Principled input '{socket_name}' is unavailable in this Blender version.")
            surface_state = _route_socket_to_emission(target_material, principled, socket, semantic)
            bake_type = "EMIT"

        _select_only(context, objects)
        kwargs = {
            "type": bake_type,
            "margin": int(margin),
            "margin_type": "EXTEND",
            "use_clear": True,
            "target": "IMAGE_TEXTURES",
            "save_mode": "INTERNAL",
        }
        if bake_type == "NORMAL":
            kwargs["normal_space"] = "TANGENT"
        outcome = _invoke_bake(context, objects, kwargs)
        if "FINISHED" not in set(outcome or []):
            raise RuntimeError(f"Blender bake operator returned {outcome!r}.")
        if floating_output:
            _save_float_image_atomic(image, output_path)
        else:
            _save_image_atomic(image, output_path, ".png")
        if not os.path.isfile(output_path):
            raise RuntimeError("Blender did not write the baked channel image.")
        return {
            "file": os.path.basename(output_path),
            "relativePath": "Baked/" + os.path.basename(output_path),
            "channel": channel,
            "colorSpace": color_space,
            "bytes": os.path.getsize(output_path),
            "mediaType": "image/x-exr" if floating_output else "image/png",
            "channelCount": 4 if channel == "RGBA" else (3 if channel == "RGB" else 1),
            "componentBytes": 2 if floating_output else 1,
            "decodeScale": 9.0 if semantic == "IOR" else 1.0,
            "decodeBias": 1.0 if semantic == "IOR" else 0.0,
        }
    finally:
        if surface_state is not None:
            _restore_surface_route(surface_state)
        for state in reversed(node_states):
            _remove_bake_target(state)
        _restore_private_material_assignments(private_assignments)
        if working_material is not target_material:
            _remove_private_material(working_material, owned_trees)
        _restore_selection(context, selection_state)
        for scratch in scratch_images:
            try:
                bpy.data.images.remove(scratch)
            except Exception:
                pass
        try:
            bpy.data.images.remove(image)
        except Exception:
            pass


def _write_constant_channel(material, semantic, channel, color_space, color, output_path, resolution):
    floating_output = semantic in {"BaseColor", "Emission"}
    if floating_output:
        output_path = os.path.splitext(output_path)[0] + ".exr"
        color_space = "Linear"
    image = bpy.data.images.new(
        name=f"Miku_Bake_{_safe_stem(material.name)}_{semantic}",
        width=resolution,
        height=resolution,
        alpha=True,
        float_buffer=floating_output,
    )
    try:
        image.generated_color = color
        image.file_format = "OPEN_EXR" if floating_output else "PNG"
        _set_image_color_space(image, color_space)
        if floating_output:
            _save_float_image_atomic(image, output_path)
        else:
            _save_image_atomic(image, output_path, ".png")
        if not os.path.isfile(output_path):
            raise RuntimeError("Blender did not write the neutral baked channel image.")
        return {
            "file": os.path.basename(output_path),
            "relativePath": "Baked/" + os.path.basename(output_path),
            "channel": channel,
            "colorSpace": color_space,
            "bytes": os.path.getsize(output_path),
            "mediaType": "image/x-exr" if floating_output else "image/png",
            "channelCount": 4 if channel == "RGBA" else (3 if channel == "RGB" else 1),
            "componentBytes": 2 if floating_output else 1,
            "neutralFallback": True,
            "decodeScale": 9.0 if semantic == "IOR" else 1.0,
            "decodeBias": 1.0 if semantic == "IOR" else 0.0,
        }
    finally:
        try:
            bpy.data.images.remove(image)
        except Exception:
            pass


def _invoke_bake(context, objects, kwargs):
    temp_override = getattr(context, "temp_override", None)
    if callable(temp_override):
        with temp_override(
            scene=context.scene,
            view_layer=context.view_layer,
            object=objects[0],
            active_object=objects[0],
            selected_objects=list(objects),
            selected_editable_objects=list(objects),
        ):
            return bpy.ops.object.bake(**kwargs)
    return bpy.ops.object.bake(**kwargs)


def _find_principled(material):
    nodes = material.node_tree.nodes
    outputs = [node for node in nodes if getattr(node, "bl_idname", "") == "ShaderNodeOutputMaterial"]
    output = next((node for node in outputs if getattr(node, "is_active_output", False)), outputs[0] if outputs else None)
    if output is None:
        return None, False
    surface = _input_by_name(output, "Surface")
    if surface is None or not getattr(surface, "is_linked", False):
        return None, False
    start = surface.links[0].from_node
    queue = [(start, False)]
    seen = set()
    while queue:
        node, branched = queue.pop(0)
        key = _pointer_key(node)
        if key in seen:
            continue
        seen.add(key)
        if getattr(node, "bl_idname", "") == "ShaderNodeBsdfPrincipled":
            return node, branched
        linked = []
        for socket in getattr(node, "inputs", []) or []:
            linked.extend(link.from_node for link in getattr(socket, "links", []) or [])
        next_branched = branched or len(linked) > 1 or "Mix" in getattr(node, "bl_idname", "")
        queue.extend((item, next_branched) for item in linked)
    return None, False


def _semantic_closure_plan(material):
    """Build a lighting-independent Standard PBR plan for a closure tree."""
    target = _resolve_material_output_target(material, "Surface")
    if target is None:
        return None
    tree, surface = target
    links = list(getattr(surface, "links", []) or [])
    if len(links) != 1:
        return None
    root = _parse_semantic_closure(
        links[0].from_node,
        set(),
        tree=tree,
        output_socket=links[0].from_socket,
    )
    if root is None:
        return None

    leaves = []
    factors = []
    _collect_semantic_closure(root, leaves, factors)
    normals = [_leaf_normal_socket(item) for item in leaves]
    normal_source = None
    normal_default = None
    normal_signature = None
    normal_closure = None
    normal_socket = None
    incompatible_normals = False
    for leaf, socket in zip(leaves, normals):
        signature = _socket_source_signature(socket)
        if normal_signature is None:
            normal_signature = signature
            normal_closure = leaf
            normal_socket = socket
            links = list(getattr(socket, "links", []) or []) if socket is not None else []
            normal_source = links[0].from_socket if len(links) == 1 else None
            candidate_default = getattr(socket, "default_value", None) if socket is not None else None
            normal_default = None if _is_zero_vector(candidate_default) else candidate_default
        elif signature != normal_signature:
            incompatible_normals = True

    approximations = sorted(
        {
            item
            for leaf in leaves
            for item in (
                ["DiffuseToUrpLit"] if leaf["closure"] == "Diffuse" else
                ["AnisotropicToMetallicIsotropic"] if leaf["closure"] == "Anisotropic" else
                ["TranslucentToUrpLit"] if leaf["closure"] == "Translucent" else
                ["SubsurfaceToUrpLit"] if leaf["closure"] == "Subsurface" else
                ["TransparentToAlpha"] if leaf["closure"] == "Transparent" else []
            )
        }
    )
    if _closure_contains_kind(root, "add"):
        approximations.append("AddShaderToClampedPbrChannels")
    semantic_inputs = [
        socket
        for leaf in leaves
        for socket in list(getattr(leaf["node"], "inputs", []) or [])
    ]
    non_bakeable = sorted(
        {
            token
            for socket in [*factors, *semantic_inputs]
            for token in _non_bakeable_socket_dependencies(socket)
        }
    )
    displacement = _semantic_displacement_plan(material)
    return {
        "tree": tree,
        "surface": surface,
        "root": root,
        "normalSource": normal_source,
        "normalDefault": normal_default,
        "normalClosure": normal_closure,
        "normalSocket": normal_socket,
        "mixedNormals": incompatible_normals,
        "displacement": displacement,
        "approximations": approximations,
        "nonBakeableDependencies": sorted(set(non_bakeable)),
        "primaryPrincipled": next((leaf["node"] for leaf in leaves if leaf["closure"] == "Principled"), None),
    }


def _parse_semantic_closure(
    node,
    visiting,
    *,
    tree=None,
    output_socket=None,
):
    key = _pointer_key(node)
    if key in visiting:
        return None
    visiting.add(key)
    try:
        node_type = getattr(node, "bl_idname", "")
        if node_type == "NodeReroute":
            inputs = list(getattr(node, "inputs", []) or [])
            links = (
                list(getattr(inputs[0], "links", []) or [])
                if inputs
                else []
            )
            if len(links) != 1:
                return None
            return _parse_semantic_closure(
                links[0].from_node,
                visiting,
                tree=tree,
                output_socket=links[0].from_socket,
            )
        if node_type == "ShaderNodeGroup":
            group_tree = getattr(node, "node_tree", None)
            if group_tree is None:
                return None
            output_name = str(
                getattr(output_socket, "name", "")
                or getattr(output_socket, "identifier", "")
            )
            group_outputs = [
                item
                for item in list(getattr(group_tree, "nodes", []) or [])
                if getattr(item, "bl_idname", "") == "NodeGroupOutput"
            ]
            group_output = next(
                (
                    item
                    for item in group_outputs
                    if getattr(item, "is_active_output", False)
                ),
                group_outputs[0] if group_outputs else None,
            )
            group_input = (
                _input_by_name(group_output, output_name)
                if group_output is not None
                else None
            )
            links = list(getattr(group_input, "links", []) or [])
            if len(links) != 1:
                return None
            return _parse_semantic_closure(
                links[0].from_node,
                visiting,
                tree=group_tree,
                output_socket=links[0].from_socket,
            )
        leaf_types = {
            "ShaderNodeBsdfPrincipled": "Principled",
            "ShaderNodeBsdfDiffuse": "Diffuse",
            "ShaderNodeBsdfAnisotropic": "Anisotropic",
            "ShaderNodeBsdfGlass": "Glass",
            "ShaderNodeBsdfRefraction": "Refraction",
            "ShaderNodeBsdfTranslucent": "Translucent",
            "ShaderNodeSubsurfaceScattering": "Subsurface",
            "ShaderNodeBsdfTransparent": "Transparent",
            "ShaderNodeEmission": "Emission",
        }
        if node_type in leaf_types:
            return {
                "kind": "leaf",
                "closure": leaf_types[node_type],
                "node": node,
                "tree": tree,
            }
        if node_type not in {"ShaderNodeMixShader", "ShaderNodeAddShader"}:
            return None
        inputs = list(getattr(node, "inputs", []) or [])
        minimum_inputs = 3 if node_type == "ShaderNodeMixShader" else 2
        if len(inputs) < minimum_inputs:
            return None
        branches = inputs[-2:]
        if any(
            len(list(getattr(socket, "links", []) or [])) > 1
            for socket in branches
        ):
            return None
        parsed = []
        for branch in branches:
            links = list(getattr(branch, "links", []) or [])
            parsed.append(
                _parse_semantic_closure(
                    links[0].from_node,
                    visiting,
                    tree=tree,
                    output_socket=links[0].from_socket,
                )
                if links
                else {
                    "kind": "leaf",
                    "closure": "Null",
                    "node": None,
                    "tree": tree,
                }
            )
        left, right = parsed
        if left is None or right is None:
            return None
        if node_type == "ShaderNodeAddShader":
            return {
                "kind": "add",
                "left": left,
                "right": right,
                "node": node,
                "tree": tree,
            }
        return {
            "kind": "mix",
            "factor": inputs[0],
            "left": left,
            "right": right,
            "node": node,
            "tree": tree,
        }
    finally:
        visiting.remove(key)


def _collect_semantic_closure(closure, leaves, factors):
    if closure["kind"] == "leaf":
        leaves.append(closure)
        return
    if closure["kind"] == "mix":
        factors.append(closure["factor"])
    _collect_semantic_closure(closure["left"], leaves, factors)
    _collect_semantic_closure(closure["right"], leaves, factors)


def _closure_contains_kind(closure, kind):
    if closure["kind"] == kind:
        return True
    if closure["kind"] == "leaf":
        return False
    return _closure_contains_kind(
        closure["left"],
        kind,
    ) or _closure_contains_kind(
        closure["right"],
        kind,
    )


def _resolve_material_output_target(material, socket_name):
    tree = getattr(material, "node_tree", None)
    if tree is None:
        return None
    outputs = [node for node in tree.nodes if getattr(node, "bl_idname", "") == "ShaderNodeOutputMaterial"]
    output = next((node for node in outputs if getattr(node, "is_active_output", False)), outputs[0] if outputs else None)
    surface = _input_by_name(output, socket_name) if output is not None else None
    visited = set()
    while surface is not None and len(list(getattr(surface, "links", []) or [])) == 1:
        link = surface.links[0]
        node = link.from_node
        if getattr(node, "bl_idname", "") != "ShaderNodeGroup":
            return tree, surface
        group_tree = getattr(node, "node_tree", None)
        if group_tree is None:
            return None
        key = _pointer_key(group_tree)
        if key in visited:
            return None
        visited.add(key)
        group_outputs = [
            item for item in list(getattr(group_tree, "nodes", []) or [])
            if getattr(item, "bl_idname", "") == "NodeGroupOutput"
        ]
        group_output = next(
            (item for item in group_outputs if getattr(item, "is_active_output", False)),
            group_outputs[0] if group_outputs else None,
        )
        if group_output is None:
            return None
        output_name = getattr(link.from_socket, "name", "") or getattr(link.from_socket, "identifier", "")
        surface = _input_by_name(group_output, output_name)
        tree = group_tree
    return (tree, surface) if surface is not None else None


def _node_by_name(tree, name):
    nodes = getattr(tree, "nodes", None)
    if nodes is None or not name:
        return None
    try:
        return nodes.get(name)
    except Exception:
        return next((node for node in nodes if getattr(node, "name", "") == name), None)


def _output_by_name(node, name):
    outputs = getattr(node, "outputs", None)
    if outputs is None:
        return None
    normalized = str(name or "").casefold().replace(" ", "").replace("_", "")
    try:
        direct = outputs.get(name)
        if direct is not None:
            return direct
    except Exception:
        pass
    for socket in outputs:
        candidates = (
            getattr(socket, "identifier", ""),
            getattr(socket, "name", ""),
        )
        if any(
            str(candidate or "").casefold().replace(" ", "").replace("_", "") == normalized
            for candidate in candidates
        ):
            return socket
    return None


def _leaf_normal_socket(leaf):
    return _input_by_name(leaf["node"], "Normal")


def _socket_source_signature(socket):
    if socket is None:
        return ("implicit_geometry_normal",)
    links = list(getattr(socket, "links", []) or [])
    if len(links) == 1:
        return ("link", _pointer_key(links[0].from_socket))
    value = getattr(socket, "default_value", 0.0)
    try:
        value = tuple(float(item) for item in value)
    except Exception:
        value = (float(value or 0.0),)
    if _is_zero_vector(value):
        return ("implicit_geometry_normal",)
    return ("default",) + value


def _socket_value_signature(socket):
    if socket is None:
        return ("missing",)
    links = list(getattr(socket, "links", []) or [])
    if len(links) == 1:
        return ("link", _pointer_key(links[0].from_socket))
    value = getattr(socket, "default_value", 0.0)
    try:
        return ("default",) + tuple(float(item) for item in value)
    except Exception:
        return ("default", float(value or 0.0))


def _semantic_channel_signature(closure, semantic):
    if closure["kind"] == "leaf":
        if semantic == "Normal":
            return ("normal",) + _socket_source_signature(
                _leaf_normal_socket(closure)
            )
        if closure["closure"] == "Emission" and semantic == "Emission":
            return (
                "emission",
                _socket_value_signature(
                    _input_by_name(closure["node"], "Color")
                ),
                _socket_value_signature(
                    _input_by_name(closure["node"], "Strength")
                ),
            )
        socket, default = _leaf_semantic_value(closure, semantic)
        if socket is not None:
            return ("value",) + _socket_value_signature(socket)
        try:
            default_value = tuple(float(item) for item in default)
        except Exception:
            default_value = (float(default or 0.0),)
        return ("semantic-default",) + default_value
    left = _semantic_channel_signature(closure["left"], semantic)
    right = _semantic_channel_signature(closure["right"], semantic)
    if left == right:
        return left
    if closure["kind"] == "add":
        return ("add", left, right)
    return (
        "mix",
        _socket_value_signature(closure["factor"]),
        left,
        right,
    )


def _semantic_channel_dependencies(closure, semantic):
    if closure["kind"] == "leaf":
        if semantic == "Normal":
            sockets = (_leaf_normal_socket(closure),)
        elif closure["closure"] == "Emission" and semantic == "Emission":
            sockets = (
                _input_by_name(closure["node"], "Color"),
                _input_by_name(closure["node"], "Strength"),
            )
        else:
            socket, _ = _leaf_semantic_value(closure, semantic)
            sockets = (socket,)
        return {
            dependency
            for socket in sockets
            for dependency in _non_bakeable_socket_dependencies(socket)
        }
    left_dependencies = _semantic_channel_dependencies(
        closure["left"],
        semantic,
    )
    right_dependencies = _semantic_channel_dependencies(
        closure["right"],
        semantic,
    )
    dependencies = {*left_dependencies, *right_dependencies}
    if (
        closure["kind"] == "mix"
        and _semantic_channel_signature(
            closure["left"],
            semantic,
        )
        != _semantic_channel_signature(closure["right"], semantic)
    ):
        dependencies.update(
            _non_bakeable_socket_dependencies(closure["factor"])
        )
    return dependencies


def _is_zero_vector(value):
    if value is None:
        return True
    try:
        items = tuple(float(item) for item in value)
    except (TypeError, ValueError):
        return False
    return bool(items) and all(abs(item) <= 1e-12 for item in items[:3])


def _non_bakeable_socket_dependencies(socket):
    dependencies = set()
    queue = [
        (link.from_node, link.from_socket)
        for link in list(getattr(socket, "links", []) or [])
    ]
    seen = set()
    node_dependencies = {
        "ShaderNodeLayerWeight": "ViewDirection",
        "ShaderNodeFresnel": "ViewDirection",
        "ShaderNodeLightPath": "LightPath",
        "ShaderNodeCameraData": "Camera",
    }
    while queue:
        node, output_socket = queue.pop(0)
        key = (
            _pointer_key(node),
            str(
                getattr(output_socket, "identifier", "")
                or getattr(output_socket, "name", "")
            ),
        )
        if key in seen:
            continue
        seen.add(key)
        dependency = node_dependencies.get(getattr(node, "bl_idname", ""))
        if (
            getattr(node, "bl_idname", "") == "ShaderNodeNewGeometry"
            and str(getattr(output_socket, "name", "")) == "Incoming"
        ):
            dependency = "ViewDirection"
        if getattr(node, "bl_idname", "") == "ShaderNodeGroup":
            group = getattr(node, "node_tree", None)
            if (
                group is not None
                and str(getattr(group, "get", lambda *_: "")("miku.semantic", ""))
                == "Input.Time"
            ):
                dependency = "Time"
        if dependency:
            dependencies.add(dependency)
        for input_socket in list(getattr(node, "inputs", []) or []):
            queue.extend(
                (link.from_node, link.from_socket)
                for link in list(getattr(input_socket, "links", []) or [])
            )
    return dependencies


def _semantic_displacement_plan(material):
    target = _resolve_material_output_target(material, "Displacement")
    if target is None:
        return None
    tree, socket = target
    links = list(getattr(socket, "links", []) or [])
    if len(links) != 1:
        return None
    node = links[0].from_node
    if getattr(node, "bl_idname", "") != "ShaderNodeDisplacement":
        return None
    height = _input_by_name(node, "Height")
    scale = _input_by_name(node, "Scale")
    if height is None:
        return None
    return {"tree": tree, "node": node, "height": height, "scale": scale}


def _leaf_semantic_value(leaf, semantic):
    node = leaf["node"]
    closure = leaf["closure"]
    if closure == "Principled":
        names = {
            "BaseColor": ("Base Color",),
            "Metalness": ("Metallic",),
            "Roughness": ("Roughness",),
            "Emission": ("Emission Color", "Emission"),
            "Alpha": ("Alpha",),
            "IOR": ("IOR",),
        }
        for name in names.get(semantic, ()):
            socket = _input_by_name(node, name)
            if socket is not None:
                return socket, None
    elif closure == "Diffuse":
        if semantic == "BaseColor":
            return _input_by_name(node, "Color"), None
        if semantic == "Roughness":
            return _input_by_name(node, "Roughness"), None
        if semantic == "Metalness":
            return None, 0.0
    elif closure in {"Translucent", "Subsurface"}:
        if semantic == "BaseColor":
            return _input_by_name(node, "Color"), None
        if semantic == "Roughness" and closure == "Subsurface":
            return _input_by_name(node, "Roughness"), None
        if semantic == "Metalness":
            return None, 0.0
    elif closure == "Transparent":
        if semantic == "BaseColor":
            return _input_by_name(node, "Color"), None
        if semantic == "Alpha":
            return None, 0.0
        if semantic in {"Metalness", "Roughness"}:
            return None, 0.0
    elif closure == "Anisotropic":
        if semantic == "BaseColor":
            return _input_by_name(node, "Color"), None
        if semantic == "Roughness":
            return _input_by_name(node, "Roughness"), None
        if semantic == "Metalness":
            return None, 1.0
    elif closure in {"Glass", "Refraction"}:
        if semantic == "BaseColor":
            return _input_by_name(node, "Color"), None
        if semantic == "Roughness":
            return _input_by_name(node, "Roughness"), None
        if semantic == "IOR":
            return _input_by_name(node, "IOR"), None
        if semantic == "Metalness":
            return None, 0.0
    elif closure == "Emission":
        if semantic == "Emission":
            return _input_by_name(node, "Color"), None
    if semantic == "Emission":
        return None, (0.0, 0.0, 0.0, 1.0)
    if semantic == "Alpha":
        return None, 1.0
    if semantic == "IOR":
        return None, 1.45
    return None, 0.0


def _connect_semantic_channel(tree, closure, semantic, target, temporary):
    if closure["kind"] == "leaf":
        if closure["closure"] == "Emission" and semantic == "Emission":
            multiply = tree.nodes.new("ShaderNodeMixRGB")
            multiply.name = "__B2U_AUTO_BAKE_EMISSION_STRENGTH__"
            multiply.hide = True
            multiply.blend_type = "MULTIPLY"
            multiply.inputs[0].default_value = 1.0
            temporary.append(multiply)
            _copy_closure_socket_to_input(
                tree,
                closure,
                _input_by_name(closure["node"], "Color"),
                multiply.inputs[1],
                socket_type="NodeSocketColor",
            )
            _copy_closure_socket_to_input(
                tree,
                closure,
                _input_by_name(closure["node"], "Strength"),
                multiply.inputs[2],
                socket_type="NodeSocketFloat",
            )
            tree.links.new(multiply.outputs[0], target)
            return
        socket, default = _leaf_semantic_value(closure, semantic)
        if socket is not None:
            _copy_closure_socket_to_input(
                tree,
                closure,
                socket,
                target,
                socket_type=(
                    "NodeSocketColor"
                    if semantic in {"BaseColor", "Emission"}
                    else "NodeSocketFloat"
                ),
            )
        else:
            target.default_value = _as_color(default)
        return
    if _semantic_channel_signature(
        closure["left"],
        semantic,
    ) == _semantic_channel_signature(closure["right"], semantic):
        _connect_semantic_channel(
            tree,
            closure["left"],
            semantic,
            target,
            temporary,
        )
        return
    mix = tree.nodes.new("ShaderNodeMixRGB")
    mix.name = "__B2U_AUTO_BAKE_CLOSURE_MIX__"
    mix.hide = True
    if closure["kind"] == "add":
        mix.blend_type = "ADD"
        mix.inputs[0].default_value = 1.0
    temporary.append(mix)
    if closure["kind"] == "mix":
        _copy_closure_socket_to_input(
            tree,
            closure,
            closure["factor"],
            mix.inputs[0],
            socket_type="NodeSocketFloat",
        )
    _connect_semantic_channel(tree, closure["left"], semantic, mix.inputs[1], temporary)
    _connect_semantic_channel(tree, closure["right"], semantic, mix.inputs[2], temporary)
    tree.links.new(mix.outputs[0], target)


def _connect_semantic_normal(tree, closure, target, temporary):
    if closure["kind"] == "leaf":
        socket = _leaf_normal_socket(closure)
        links = list(getattr(socket, "links", []) or []) if socket is not None else []
        if len(links) == 1:
            _copy_closure_socket_to_input(
                tree,
                closure,
                socket,
                target,
                socket_type="NodeSocketVector",
            )
            return
        value = getattr(socket, "default_value", None) if socket is not None else None
        if value is not None and not _is_zero_vector(value):
            target.default_value = value
            return
        geometry = tree.nodes.new("ShaderNodeNewGeometry")
        geometry.name = "__B2U_AUTO_BAKE_GEOMETRY_NORMAL__"
        geometry.hide = True
        temporary.append(geometry)
        tree.links.new(_output_by_name(geometry, "Normal"), target)
        return
    if _semantic_channel_signature(
        closure["left"],
        "Normal",
    ) == _semantic_channel_signature(closure["right"], "Normal"):
        _connect_semantic_normal(
            tree,
            closure["left"],
            target,
            temporary,
        )
        return
    mix = tree.nodes.new("ShaderNodeMixRGB")
    mix.name = "__B2U_AUTO_BAKE_CLOSURE_NORMAL_MIX__"
    mix.hide = True
    normalize = tree.nodes.new("ShaderNodeVectorMath")
    normalize.name = "__B2U_AUTO_BAKE_CLOSURE_NORMAL_NORMALIZE__"
    normalize.hide = True
    normalize.operation = "NORMALIZE"
    temporary.extend((mix, normalize))
    if closure["kind"] == "add":
        mix.inputs[0].default_value = 0.5
    else:
        _copy_closure_socket_to_input(
            tree,
            closure,
            closure["factor"],
            mix.inputs[0],
            socket_type="NodeSocketFloat",
        )
    _connect_semantic_normal(tree, closure["left"], mix.inputs[1], temporary)
    _connect_semantic_normal(tree, closure["right"], mix.inputs[2], temporary)
    tree.links.new(mix.outputs[0], normalize.inputs[0])
    tree.links.new(normalize.outputs[0], target)


def _route_semantic_channel_to_emission(material, plan, semantic):
    tree, surface, previous = _detach_surface(material, plan)
    temporary = []
    try:
        emission = tree.nodes.new("ShaderNodeEmission")
        emission.name = "__B2U_AUTO_BAKE_EMISSION__"
        emission.hide = True
        temporary.append(emission)
        _connect_semantic_channel(tree, plan["root"], semantic, _input_by_name(emission, "Color"), temporary)
        strength = _input_by_name(emission, "Strength")
        if strength is not None:
            strength.default_value = 1.0
        tree.links.new(emission.outputs[0], surface)
        return {"tree": tree, "surface": surface, "previous": previous, "temporaryNodes": temporary}
    except Exception:
        _restore_surface_route({"tree": tree, "surface": surface, "previous": previous, "temporaryNodes": temporary})
        raise


def _route_semantic_normal_to_surface(material, plan):
    displacement = plan.get("displacement")
    cross_tree_displacement = displacement is not None and displacement.get("tree") != plan.get("tree")
    tree, surface, previous = _detach_surface(material, None if cross_tree_displacement else plan)
    temporary = []
    try:
        principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
        principled.name = "__B2U_AUTO_BAKE_NORMAL_CLOSURE__"
        principled.hide = True
        temporary.append(principled)
        normal = _input_by_name(principled, "Normal")
        if plan.get("mixedNormals"):
            _connect_semantic_normal(tree, plan["root"], normal, temporary)
        elif displacement is not None:
            bump = tree.nodes.new("ShaderNodeBump")
            bump.name = "__B2U_AUTO_BAKE_DISPLACEMENT_NORMAL__"
            bump.hide = True
            temporary.append(bump)
            _copy_nested_socket_to_input(
                tree,
                displacement.get("tree"),
                displacement.get("node"),
                displacement["height"],
                _input_by_name(bump, "Height"),
                socket_type="NodeSocketFloat",
            )
            scale = displacement.get("scale")
            if scale is not None:
                _copy_nested_socket_to_input(
                    tree,
                    displacement.get("tree"),
                    displacement.get("node"),
                    scale,
                    _input_by_name(bump, "Distance"),
                    socket_type="NodeSocketFloat",
                )
            strength = _input_by_name(bump, "Strength")
            if strength is not None:
                strength.default_value = 1.0
            base_normal = _input_by_name(bump, "Normal")
            if plan.get("normalSource") is not None:
                _copy_closure_socket_to_input(
                    tree,
                    plan.get("normalClosure"),
                    plan.get("normalSocket"),
                    base_normal,
                    socket_type="NodeSocketVector",
                )
            elif base_normal is not None and plan.get("normalDefault") is not None:
                base_normal.default_value = plan["normalDefault"]
            tree.links.new(bump.outputs[0], normal)
        elif plan.get("normalSource") is not None:
            _copy_closure_socket_to_input(
                tree,
                plan.get("normalClosure"),
                plan.get("normalSocket"),
                normal,
                socket_type="NodeSocketVector",
            )
        elif normal is not None and plan.get("normalDefault") is not None:
            normal.default_value = plan["normalDefault"]
        tree.links.new(principled.outputs[0], surface)
        return {"tree": tree, "surface": surface, "previous": previous, "temporaryNodes": temporary}
    except Exception:
        _restore_surface_route({"tree": tree, "surface": surface, "previous": previous, "temporaryNodes": temporary})
        raise


def _detach_surface(material, semantic_plan=None):
    if semantic_plan is not None:
        tree = semantic_plan.get("tree")
        surface = semantic_plan.get("surface")
    else:
        tree = material.node_tree
        outputs = [node for node in tree.nodes if getattr(node, "bl_idname", "") == "ShaderNodeOutputMaterial"]
        output = next((node for node in outputs if getattr(node, "is_active_output", False)), outputs[0] if outputs else None)
        if output is None:
            raise RuntimeError("Material Output node is missing.")
        surface = _input_by_name(output, "Surface")
    if tree is None or surface is None:
        raise RuntimeError("Material or group surface output is missing.")
    previous = [(link.from_socket, link.to_socket) for link in list(surface.links)]
    for link in list(surface.links):
        tree.links.remove(link)
    return tree, surface, previous


def _copy_socket_to_input(tree, source, target):
    if source is None or target is None:
        return
    links = list(getattr(source, "links", []) or [])
    if links:
        tree.links.new(links[0].from_socket, target)
    else:
        value = getattr(source, "default_value", 0.0)
        try:
            target.default_value = value
        except Exception:
            target.default_value = _as_color(value)


def _copy_nested_socket_to_input(
    root_tree,
    source_tree,
    source_node,
    source,
    target,
    *,
    socket_type,
):
    if source is None or target is None:
        return
    links = list(getattr(source, "links", []) or [])
    if links:
        output = links[0].from_socket
        if source_tree is not None and source_tree != root_tree:
            output = _expose_nested_output(
                root_tree,
                source_tree,
                source_node,
                output,
                socket_type=socket_type,
            )
        root_tree.links.new(output, target)
        return
    value = getattr(source, "default_value", 0.0)
    try:
        target.default_value = value
    except Exception:
        target.default_value = _as_color(value)


def _copy_closure_socket_to_input(
    root_tree,
    closure,
    source,
    target,
    *,
    socket_type,
):
    _copy_nested_socket_to_input(
        root_tree,
        closure.get("tree") if closure is not None else None,
        closure.get("node") if closure is not None else None,
        source,
        target,
        socket_type=socket_type,
    )


def _material_alpha_state(material, principled=None):
    """Translate Blender's material/closure alpha intent to the MIKU contract."""
    cutoff = float(getattr(material, "alpha_threshold", 0.5) or 0.5)
    surface_method = str(getattr(material, "surface_render_method", "") or "").upper()
    legacy_method = str(getattr(material, "blend_method", "") or "").upper()

    if legacy_method == "CLIP":
        return {"alphaMode": "Cutout", "alphaCutoff": cutoff}
    if surface_method == "BLENDED" or legacy_method == "BLEND":
        return {"alphaMode": "Transparent", "alphaCutoff": cutoff}

    alpha_evidence = False
    if principled is not None:
        alpha = _input_by_name(principled, "Alpha")
        if alpha is not None:
            alpha_evidence = bool(getattr(alpha, "is_linked", False))
            if not alpha_evidence:
                try:
                    alpha_evidence = float(getattr(alpha, "default_value", 1.0)) < 0.999
                except Exception:
                    pass
    try:
        alpha_evidence = alpha_evidence or float(list(getattr(material, "diffuse_color", (1, 1, 1, 1)))[3]) < 0.999
    except Exception:
        pass
    alpha_evidence = alpha_evidence or _surface_uses_transparent_bsdf(material)

    # Blender 5 uses DITHERED/HASHED as defaults even for opaque materials, so
    # those flags alone are not proof of transparency.  Closure/socket evidence
    # makes the intent explicit.
    if alpha_evidence:
        return {"alphaMode": "Transparent", "alphaCutoff": cutoff}
    return {"alphaMode": "Opaque", "alphaCutoff": cutoff}


def _surface_uses_transparent_bsdf(material):
    tree = getattr(material, "node_tree", None)
    if tree is None:
        return False
    outputs = [node for node in list(getattr(tree, "nodes", []) or []) if getattr(node, "bl_idname", "") == "ShaderNodeOutputMaterial"]
    output = next((node for node in outputs if getattr(node, "is_active_output", False)), outputs[0] if outputs else None)
    if output is None:
        return False
    surface = _input_by_name(output, "Surface")
    starts = [link.from_node for link in list(getattr(surface, "links", []) or [])] if surface is not None else []
    return _upstream_has_node_type(starts, "ShaderNodeBsdfTransparent")


def _upstream_has_node_type(starts, wanted):
    queue = list(starts)
    seen = set()
    while queue:
        node = queue.pop(0)
        key = _pointer_key(node)
        if key in seen:
            continue
        seen.add(key)
        if getattr(node, "bl_idname", "") == wanted:
            return True
        if getattr(node, "bl_idname", "") == "ShaderNodeGroup":
            group_tree = getattr(node, "node_tree", None)
            group_outputs = [
                item
                for item in list(getattr(group_tree, "nodes", []) or [])
                if getattr(item, "bl_idname", "") == "NodeGroupOutput"
            ]
            for group_output in group_outputs:
                for socket in list(getattr(group_output, "inputs", []) or []):
                    queue.extend(link.from_node for link in list(getattr(socket, "links", []) or []))
        for socket in list(getattr(node, "inputs", []) or []):
            queue.extend(link.from_node for link in list(getattr(socket, "links", []) or []))
    return False


def _route_socket_to_emission(material, principled, source_socket, semantic):
    tree, surface, previous = _detach_surface(material)

    emission = tree.nodes.new("ShaderNodeEmission")
    emission.name = "__B2U_AUTO_BAKE_EMISSION__"
    emission.hide = True
    color_input = _input_by_name(emission, "Color")
    strength_input = _input_by_name(emission, "Strength")
    if getattr(source_socket, "is_linked", False):
        tree.links.new(source_socket.links[0].from_socket, color_input)
    else:
        value = getattr(source_socket, "default_value", 0.0)
        color_input.default_value = _as_color(value)

    if semantic == "Emission" and strength_input is not None:
        original_strength = _input_by_name(principled, "Emission Strength")
        if original_strength is not None and getattr(original_strength, "is_linked", False):
            tree.links.new(original_strength.links[0].from_socket, strength_input)
        elif original_strength is not None:
            strength_input.default_value = float(getattr(original_strength, "default_value", 1.0))
        else:
            strength_input.default_value = 1.0
    elif strength_input is not None:
        strength_input.default_value = 1.0
    tree.links.new(emission.outputs[0], surface)
    return {"tree": tree, "surface": surface, "previous": previous, "temporaryNodes": [emission]}


def _restore_surface_route(state):
    tree = state["tree"]
    surface = state["surface"]
    for link in list(surface.links):
        tree.links.remove(link)
    temporary = state.get("temporaryNodes")
    if temporary is None and state.get("emission") is not None:
        temporary = [state["emission"]]
    for node in reversed(list(temporary or [])):
        try:
            tree.nodes.remove(node)
        except Exception:
            pass
    for from_socket, to_socket in state["previous"]:
        try:
            tree.links.new(from_socket, to_socket)
        except Exception:
            pass


def _install_bake_target(material, image):
    if not getattr(material, "use_nodes", False) or getattr(material, "node_tree", None) is None:
        raise RuntimeError(f"Material {getattr(material, 'name', 'Material')} has no node tree.")
    nodes = material.node_tree.nodes
    selected = [(node, bool(getattr(node, "select", False))) for node in nodes]
    active = getattr(nodes, "active", None)
    for node, _ in selected:
        node.select = False
    target = nodes.new("ShaderNodeTexImage")
    target.name = "__B2U_AUTO_BAKE_TARGET__"
    target.image = image
    target.select = True
    nodes.active = target
    return {"nodes": nodes, "target": target, "active": active, "selected": selected}


def _remove_bake_target(state):
    nodes = state["nodes"]
    try:
        nodes.remove(state["target"])
    except Exception:
        pass
    for node, selected in state["selected"]:
        try:
            node.select = selected
        except Exception:
            pass
    try:
        nodes.active = state["active"]
    except Exception:
        pass


def _ensure_uv(context, obj, auto_generate):
    uv_layers = getattr(getattr(obj, "data", None), "uv_layers", None)
    if uv_layers is None:
        return False
    if len(uv_layers):
        return True
    if not auto_generate:
        return False
    state = _capture_selection(context)
    try:
        _select_only(context, [obj])
        if getattr(obj, "mode", "OBJECT") != "OBJECT":
            bpy.ops.object.mode_set(mode="OBJECT")
        uv_layers.new(name="Miku_AutoUV")
        bpy.ops.object.mode_set(mode="EDIT")
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=1.15192, island_margin=0.03)
        bpy.ops.object.mode_set(mode="OBJECT")
        return len(uv_layers) > 0
    finally:
        try:
            if getattr(obj, "mode", "OBJECT") != "OBJECT":
                bpy.ops.object.mode_set(mode="OBJECT")
        except Exception:
            pass
        _restore_selection(context, state)


def _objects_using_material(objects, material):
    result = []
    for obj in objects:
        if getattr(obj, "type", "") != "MESH":
            continue
        if any(getattr(slot, "material", None) == material for slot in getattr(obj, "material_slots", []) or []):
            result.append(obj)
    return result


def _materials_on_objects(objects):
    result = []
    seen = set()
    for obj in objects:
        for slot in getattr(obj, "material_slots", []) or []:
            material = getattr(slot, "material", None)
            if material is None:
                continue
            key = _pointer_key(material)
            if key not in seen:
                seen.add(key)
                result.append(material)
    return result


def _assign_private_material(objects, source, private):
    assignments = []
    for obj in objects:
        for slot in getattr(obj, "material_slots", []) or []:
            if getattr(slot, "material", None) == source:
                assignments.append((slot, source))
                slot.material = private
    return assignments


def _restore_private_material_assignments(assignments):
    for slot, material in reversed(list(assignments or [])):
        try:
            slot.material = material
        except Exception:
            pass


def _capture_selection(context):
    view_layer = getattr(context, "view_layer", None)
    return {
        "selected": list(getattr(context, "selected_objects", []) or []),
        "active": getattr(getattr(view_layer, "objects", None), "active", None),
    }


def _select_only(context, objects):
    view_layer = getattr(context, "view_layer", None)
    pool = list(getattr(view_layer, "objects", []) or [])
    if not pool:
        pool = list(getattr(getattr(context, "scene", None), "objects", []) or [])
    for candidate in pool:
        try:
            candidate.select_set(False)
        except Exception:
            pass
    for obj in objects:
        obj.select_set(True)
    if view_layer is not None and getattr(view_layer, "objects", None) is not None:
        view_layer.objects.active = objects[0]


def _restore_selection(context, state):
    view_layer = getattr(context, "view_layer", None)
    pool = list(getattr(view_layer, "objects", []) or [])
    if not pool:
        pool = list(getattr(getattr(context, "scene", None), "objects", []) or [])
    for candidate in pool:
        try:
            candidate.select_set(False)
        except Exception:
            pass
    for obj in state.get("selected", []):
        try:
            obj.select_set(True)
        except Exception:
            pass
    if view_layer is not None and getattr(view_layer, "objects", None) is not None:
        try:
            view_layer.objects.active = state.get("active")
        except Exception:
            pass


def _input_by_name(node, name):
    inputs = getattr(node, "inputs", None)
    if inputs is None:
        return None
    try:
        return inputs.get(name)
    except Exception:
        return next((socket for socket in inputs if getattr(socket, "name", "") == name), None)


def _output_by_identifier(node, identifier):
    outputs = list(getattr(node, "outputs", []) or [])
    expected = str(identifier or "")
    return next(
        (
            socket
            for socket in outputs
            if str(getattr(socket, "identifier", "") or "") == expected
            or str(getattr(socket, "name", "") or "") == expected
        ),
        None,
    )


def _as_color(value):
    if isinstance(value, (int, float)):
        scalar = float(value)
        return (scalar, scalar, scalar, 1.0)
    try:
        items = list(value)
    except Exception:
        return (0.0, 0.0, 0.0, 1.0)
    while len(items) < 4:
        items.append(1.0)
    return tuple(float(item) for item in items[:4])


def _set_image_color_space(image, color_space):
    choices = ("sRGB",) if color_space == "sRGB" else ("Non-Color", "Raw")
    for choice in choices:
        try:
            image.colorspace_settings.name = choice
            return
        except Exception:
            continue


def _pointer_key(value):
    pointer = getattr(value, "as_pointer", None)
    if callable(pointer):
        try:
            return int(pointer())
        except Exception:
            pass
    return id(value)


def _safe_stem(value):
    text = str(value or "Material").strip()
    for char in '<>:"/\\|?*':
        text = text.replace(char, "_")
    return text or "Material"


def _read_cache(path):
    try:
        with open(path, "r", encoding="utf-8") as handle:
            return json.load(handle)
    except (OSError, ValueError, TypeError):
        return None


def _write_cache(path, result):
    atomic_write_json(path, result, pretty=True, output_root=os.path.dirname(path))


def _cache_outputs_exist(cache, bake_dir):
    channels = cache.get("channels") or {}
    return bool(channels) and all(os.path.isfile(os.path.join(bake_dir, item.get("file", ""))) for item in channels.values())


def _failed_result(cache_key, resolution, samples, margin, message):
    return {
        "documentKind": BAKE_SCHEMA,
        "schemaVersion": "1.0",
        "algorithmRevision": BAKE_ALGORITHM_REVISION,
        "status": "failed",
        "cacheKey": cache_key,
        "resolution": resolution,
        "samples": samples,
        "margin": margin,
        "evaluator": "BLENDER_CHANNEL_BAKE",
        "channels": {},
        "failures": [{"semantic": "Material", "message": message}],
    }


def _skipped_result(cache_key, resolution, samples, margin, message, dependencies):
    return {
        "documentKind": BAKE_SCHEMA,
        "schemaVersion": "1.0",
        "algorithmRevision": BAKE_ALGORITHM_REVISION,
        "status": "skipped",
        "cacheKey": cache_key,
        "resolution": resolution,
        "samples": samples,
        "margin": margin,
        "evaluator": "BLENDER_CHANNEL_BAKE",
        "channels": {},
        "failures": [],
        "skipReason": message,
        "parityStrategy": "SemanticPbrChannels",
        "nonBakeableDependencies": sorted(set(dependencies or [])),
    }
