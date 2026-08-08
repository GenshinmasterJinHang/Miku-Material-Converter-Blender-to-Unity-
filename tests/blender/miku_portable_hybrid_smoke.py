"""Validate mesh-independent Portable Hybrid baking in Blender 5.0-5.2."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import pathlib
import shutil
import sys
import tempfile

import bpy


REPOSITORY_ROOT = pathlib.Path(__file__).resolve().parents[2]
if str(REPOSITORY_ROOT) not in sys.path:
    sys.path.insert(0, str(REPOSITORY_ROOT))

import miku_blender  # noqa: E402
from miku_blender.versioning import require_blender_capabilities  # noqa: E402


def _load_worker():
    package_root = (
        REPOSITORY_ROOT
        / "extensions"
        / "miku_shader_converter"
        / "bake_worker"
    )
    spec = importlib.util.spec_from_file_location(
        "_miku_portable_hybrid_smoke_worker",
        package_root / "__init__.py",
        submodule_search_locations=[str(package_root)],
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("MIKU_CANONICAL_BAKE_WORKER_LOAD_FAILED")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


def _material():
    material = bpy.data.materials.new("PortableHybridFixture")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    principled = nodes.new("ShaderNodeBsdfPrincipled")
    coordinates = nodes.new("ShaderNodeTexCoord")
    voronoi = nodes.new("ShaderNodeTexVoronoi")
    layer_weight = nodes.new("ShaderNodeLayerWeight")
    mix = nodes.new("ShaderNodeMixRGB")
    mix.inputs[2].default_value = (0.1, 0.4, 0.9, 1.0)
    links = material.node_tree.links
    links.new(coordinates.outputs["UV"], voronoi.inputs["Vector"])
    links.new(layer_weight.outputs["Facing"], mix.inputs["Fac"])
    links.new(voronoi.outputs["Distance"], mix.inputs[1])
    links.new(mix.outputs["Color"], principled.inputs["Base Color"])
    links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material


def _replace_mesh(material, *, cube: bool):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    if cube:
        bpy.ops.mesh.primitive_cube_add()
    else:
        mesh = bpy.data.meshes.new("PortableTriangleMesh")
        mesh.from_pydata(
            [(0.0, 0.0, 0.0), (2.0, 0.0, 0.0), (0.0, 0.5, 0.0)],
            [],
            [(0, 1, 2)],
        )
        obj = bpy.data.objects.new("PortableTriangle", mesh)
        bpy.context.scene.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
    obj = bpy.context.active_object
    obj.data.materials.append(material)
    uv = obj.data.uv_layers.new(name="UVMap")
    for loop in obj.data.loops:
        vertex = obj.data.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = (float(vertex.x) % 1.0, float(vertex.y) % 1.0)


def _file_hashes(root: pathlib.Path):
    return {
        path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(item for item in root.rglob("*") if item.is_file())
    }


def _assert_portable_bundle(root: pathlib.Path):
    files = [item for item in root.rglob("*") if item.is_file()]
    if any(item.suffix.lower() == ".glb" for item in files):
        raise RuntimeError("MIKU_PORTABLE_RESOURCE_MESH_BOUND:SourceMesh")
    for path in files:
        if path.suffix.lower() in {".json", ".mikubundle"}:
            text = path.read_text(encoding="utf-8")
            if '"meshBinding"' in text or '"SourceMesh"' in text:
                raise RuntimeError(
                    "MIKU_PORTABLE_RESOURCE_MESH_BOUND:" + path.name
                )
    caches = list(root.rglob(".miku-reusable-bake-cache.json"))
    if len(caches) != 1:
        raise RuntimeError("MIKU_PORTABLE_CACHE_MISSING")
    cache = json.loads(caches[0].read_text(encoding="utf-8"))
    if cache.get("evaluator") != "BLENDER_CANONICAL_UV0_BAKE":
        raise RuntimeError("MIKU_PORTABLE_CACHE_EVALUATOR_INVALID")
    serialized = json.dumps(cache, sort_keys=True)
    if "targetMeshes" in serialized or "meshFingerprint" in serialized:
        raise RuntimeError("MIKU_PORTABLE_CACHE_MESH_BOUND")


def _assert_cache_reuse(worker, root: pathlib.Path):
    request = next(root.rglob("*.miku-bake-request.json"))
    textures = sorted(root.rglob("*.exr")) + sorted(root.rglob("*.png"))
    before = {
        path: (path.stat().st_mtime_ns, hashlib.sha256(path.read_bytes()).hexdigest())
        for path in textures
    }
    worker.execute_request(str(request), str(request.parent))
    after = {
        path: (path.stat().st_mtime_ns, hashlib.sha256(path.read_bytes()).hexdigest())
        for path in textures
    }
    if before != after:
        raise RuntimeError("MIKU_PORTABLE_CACHE_REUSE_FAILED")


def _assert_worker_rejects_mesh_drift(worker, material, root: pathlib.Path):
    nodes = material.node_tree.nodes
    coordinates = next(node for node in nodes if node.bl_idname == "ShaderNodeTexCoord")
    voronoi = next(node for node in nodes if node.bl_idname == "ShaderNodeTexVoronoi")
    vector = voronoi.inputs["Vector"]
    for link in list(vector.links):
        material.node_tree.links.remove(link)
    material.node_tree.links.new(coordinates.outputs["Generated"], vector)
    request = next(root.rglob("*.miku-bake-request.json"))
    try:
        worker.execute_request(str(request), str(request.parent))
    except RuntimeError as error:
        if "MIKU_PORTABLE_HYBRID_MESH_DEPENDENCY" not in str(error):
            raise
    else:
        raise RuntimeError("MIKU_PORTABLE_WORKER_ACCEPTED_MESH_DRIFT")


def main() -> None:
    require_blender_capabilities(bpy)
    worker = _load_worker()
    worker.register()
    material = _material()
    temporary = pathlib.Path(tempfile.mkdtemp(prefix="miku-portable-hybrid-"))
    first = temporary / "triangle"
    second = temporary / "cube"
    try:
        for output, cube in ((first, False), (second, True)):
            _replace_mesh(material, cube=cube)
            result = miku_blender.export_material_bundle(
                material,
                str(output),
                source_blend_id="portable-hybrid-source",
                persistent_material_id="portable-hybrid-material",
                mode="PreferNative",
            )
            if not pathlib.Path(result["bundlePath"]).is_file():
                raise RuntimeError("MIKU_PORTABLE_BUNDLE_MISSING")
            _assert_portable_bundle(output)
            _assert_cache_reuse(worker, output)
        if _file_hashes(first) != _file_hashes(second):
            raise RuntimeError("MIKU_PORTABLE_BAKE_NOT_DETERMINISTIC")
        _assert_worker_rejects_mesh_drift(worker, material, second)
        print("MIKU_PORTABLE_HYBRID_SMOKE_COMPLETE")
    finally:
        worker.unregister()
        shutil.rmtree(temporary, ignore_errors=True)


if __name__ == "__main__":
    main()
