"""Blender 5.0-5.2 fixture for Miku runtime expressions."""

from __future__ import annotations

import json
import sys
import tempfile
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender
from miku_blender.versioning import require_blender_capabilities
from miku.planner import ConversionPlanner
from miku.semantic import build_material_ir


require_blender_capabilities(bpy)


def new_material(name: str):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    return material, tree, principled


def assert_dynamic(
    material,
    expected_ops,
    expected_bake_semantics=(),
    *,
    mode="Auto",
    workflow_kind="standard_pbr",
):
    graph = miku_blender.snapshot_material(
        material,
        workflow_kind=workflow_kind,
    )
    ir = build_material_ir(
        graph,
        conversion_mode=mode,
        workflow_kind=workflow_kind,
    )
    plan = ConversionPlanner().plan(ir, mode=mode)
    actual = {item["op"] for item in ir["expressions"]}
    missing = set(expected_ops) - actual
    if missing:
        raise AssertionError(f"missing expressions {sorted(missing)} from {sorted(actual)}")
    baked_semantics = sorted(
        {
            semantic
            for job in plan["bakeJobs"]
            for semantic in job.get("semantics", [])
        }
    )
    if baked_semantics != sorted(expected_bake_semantics):
        raise AssertionError(
            "runtime fixture scheduled unexpected channel bake jobs: "
            f"expected={sorted(expected_bake_semantics)}, "
            f"actual={baked_semantics}, jobs={plan['bakeJobs']}"
        )
    if not any(
        item.get("code") == "MIKU_RUNTIME_INPUT_PRESERVED"
        for item in plan["diagnostics"]
    ):
        raise AssertionError(
            "MIKU_RUNTIME_INPUT_PRESERVED missing: "
            + json.dumps(plan["diagnostics"], ensure_ascii=False)
        )
    return graph, ir


camera_material, camera_tree, camera_surface = new_material("Miku Camera")
camera = camera_tree.nodes.new("ShaderNodeCameraData")
camera_tree.links.new(
    camera.outputs["View Distance"],
    camera_surface.inputs["Roughness"],
)
camera_graph, camera_ir = assert_dynamic(
    camera_material,
    {"Input.Camera.ViewDistance"},
)
camera_snapshot = next(
    item for item in camera_graph["nodes"] if item["op"] == "Input.CameraData"
)
camera_outputs = {item["name"]: item for item in camera_snapshot["outputs"]}
assert camera_outputs["View Vector"]["space"] == "View"
assert camera_outputs["View Vector"]["stage"] == "Fragment"
assert camera_outputs["View Z Depth"]["valueType"] == "Scalar"
assert camera_outputs["View Distance"]["uniformity"] == "Varying"

view_material, view_tree, view_surface = new_material("Miku View")
geometry = view_tree.nodes.new("ShaderNodeNewGeometry")
view_tree.links.new(geometry.outputs["Incoming"], view_surface.inputs["Base Color"])
assert_dynamic(view_material, {"Input.ViewDirection"})

backface_material, backface_tree, backface_surface = new_material(
    "Miku Standard PBR Backfacing"
)
backface_output = next(
    node
    for node in backface_tree.nodes
    if node.bl_idname == "ShaderNodeOutputMaterial"
)
for link in tuple(backface_output.inputs["Surface"].links):
    backface_tree.links.remove(link)
backface_second = backface_tree.nodes.new("ShaderNodeBsdfDiffuse")
backface_mix = backface_tree.nodes.new("ShaderNodeMixShader")
backface_geometry = backface_tree.nodes.new("ShaderNodeNewGeometry")
backface_tree.links.new(
    backface_geometry.outputs["Backfacing"],
    backface_mix.inputs["Fac"],
)
backface_tree.links.new(
    backface_surface.outputs["BSDF"],
    backface_mix.inputs[1],
)
backface_tree.links.new(
    backface_second.outputs["BSDF"],
    backface_mix.inputs[2],
)
backface_tree.links.new(
    backface_mix.outputs["Shader"],
    backface_output.inputs["Surface"],
)
backface_graph, backface_ir = assert_dynamic(
    backface_material,
    {"Input.IsFrontFace", "Math.OneMinus"},
    workflow_kind="standard_pbr",
)
backface_snapshot = next(
    item
    for item in backface_graph["nodes"]
    if item["op"] == "Input.Geometry"
)
backface_output_snapshot = next(
    item
    for item in backface_snapshot["outputs"]
    if item["name"] == "Backfacing"
)
assert backface_output_snapshot["valueType"] == "Scalar"
assert backface_output_snapshot["stage"] == "Fragment"
assert backface_output_snapshot["uniformity"] == "Varying"
assert not any(
    item.get("code")
    in {
        "MIKU_CLOSURE_WEIGHT_EXPRESSION_UNSUPPORTED",
        "MIKU_RUNTIME_INPUT_UNSUPPORTED",
    }
    for item in backface_ir["diagnostics"]
)

fresnel_material, fresnel_tree, fresnel_surface = new_material("Miku Fresnel")
fresnel = fresnel_tree.nodes.new("ShaderNodeFresnel")
fresnel_tree.links.new(fresnel.outputs["Fac"], fresnel_surface.inputs["Roughness"])
assert_dynamic(fresnel_material, {"Math.DielectricFresnel"})

layer_material, layer_tree, layer_surface = new_material("Miku Layer Weight")
layer = layer_tree.nodes.new("ShaderNodeLayerWeight")
layer_tree.links.new(layer.outputs["Facing"], layer_surface.inputs["Roughness"])
assert_dynamic(layer_material, {"Math.LayerWeightFacing"})

group_material, group_tree, group_surface = new_material(
    "Miku Nested Layer Weight"
)
group_tree.nodes.remove(group_surface)
group = bpy.data.node_groups.new("Miku Nested Layer Weight Group", "ShaderNodeTree")
group.interface.new_socket(
    name="Blend",
    in_out="INPUT",
    socket_type="NodeSocketFloat",
)
group.interface.new_socket(
    name="Color A",
    in_out="INPUT",
    socket_type="NodeSocketColor",
)
group.interface.new_socket(
    name="Color B",
    in_out="INPUT",
    socket_type="NodeSocketColor",
)
group.interface.new_socket(
    name="Shader",
    in_out="OUTPUT",
    socket_type="NodeSocketShader",
)
group_input = group.nodes.new("NodeGroupInput")
group_output = group.nodes.new("NodeGroupOutput")
group_layer = group.nodes.new("ShaderNodeLayerWeight")
group_ramp = group.nodes.new("ShaderNodeValToRGB")
group_ramp.color_ramp.interpolation = "EASE"
group_roughness = group.nodes.new("ShaderNodeTexVoronoi")
group_first = group.nodes.new("ShaderNodeBsdfPrincipled")
group_second = group.nodes.new("ShaderNodeBsdfPrincipled")
group_mix = group.nodes.new("ShaderNodeMixShader")
group.links.new(group_input.outputs["Blend"], group_layer.inputs["Blend"])
group.links.new(group_layer.outputs["Facing"], group_ramp.inputs["Fac"])
group.links.new(group_ramp.outputs["Color"], group_mix.inputs["Fac"])
group.links.new(group_input.outputs["Color A"], group_first.inputs["Base Color"])
group.links.new(group_input.outputs["Color B"], group_second.inputs["Base Color"])
group.links.new(
    group_roughness.outputs["Distance"],
    group_first.inputs["Roughness"],
)
group.links.new(
    group_roughness.outputs["Distance"],
    group_second.inputs["Roughness"],
)
group.links.new(group_first.outputs["BSDF"], group_mix.inputs[1])
group.links.new(group_second.outputs["BSDF"], group_mix.inputs[2])
group.links.new(group_mix.outputs["Shader"], group_output.inputs["Shader"])
group_instance = group_tree.nodes.new("ShaderNodeGroup")
group_instance.node_tree = group
group_instance.inputs["Blend"].default_value = 0.85
group_instance.inputs["Color A"].default_value = (0.8, 0.2, 0.1, 1.0)
group_instance.inputs["Color B"].default_value = (0.1, 0.4, 0.8, 1.0)
group_material_output = next(
    node
    for node in group_tree.nodes
    if node.bl_idname == "ShaderNodeOutputMaterial"
)
group_tree.links.new(
    group_instance.outputs["Shader"],
    group_material_output.inputs["Surface"],
)
group_graph, group_ir = assert_dynamic(
    group_material,
    {
        "Input.ViewDirection",
        "Math.LayerWeightFacing",
        "Math.Lerp",
        "Texture.SampleBaked2D",
    },
    mode="AllowMeshBake",
)
assert "Opaque.BlenderNode" not in {
    item["op"] for item in group_graph["nodes"]
}
assert any(
    (item.get("value") or {}).get("kind") == "Expression"
    for item in group_ir["channels"]
)

time_material, time_tree, time_surface = new_material("Miku Time")
time = miku_blender.create_miku_time_node(time_material)
time_tree.links.new(time.outputs["Sine"], time_surface.inputs["Roughness"])
time_graph, time_ir = assert_dynamic(time_material, {"Input.Time.Sine"})
time_snapshot = next(item for item in time_graph["nodes"] if item["op"] == "Input.Time")
assert time_snapshot["params"]["contract"] == "miku_time_v1"
assert time_snapshot["params"]["sourceFps"] > 0
for conversion_mode in (
    "Auto",
    "NativeOnly",
    "PreferNative",
    "ReusableBakeOnly",
    "AllowMeshBake",
    "FullPBRBake",
    "AppearanceSnapshot",
):
    with tempfile.TemporaryDirectory() as output_root:
        try:
            miku_blender.export_material_bundle(
                time_material,
                output_root,
                source_blend_id="runtime-smoke-source",
                persistent_material_id="runtime-smoke-time-" + conversion_mode,
                mode=conversion_mode,
            )
        except RuntimeError as error:
            assert str(error).startswith("MIKU_TIME_INPUT_UNSUPPORTED:"), (
                conversion_mode,
                error,
            )
        else:
            raise AssertionError(
                "reachable time input was exported in " + conversion_mode
            )
        assert not any(Path(output_root).iterdir())

disconnected_time_material, disconnected_time_tree, _ = new_material(
    "Miku Disconnected Time"
)
miku_blender.create_miku_time_node(disconnected_time_material)
with tempfile.TemporaryDirectory() as output_root:
    result = miku_blender.export_material_bundle(
        disconnected_time_material,
        output_root,
        source_blend_id="runtime-smoke-source",
        persistent_material_id="runtime-smoke-disconnected-time",
    )
    assert Path(result["bundlePath"]).is_file()

driver_material, driver_tree, driver_surface = new_material("Miku Driver")
driver = driver_tree.nodes.new("ShaderNodeValue")
curve = driver.outputs[0].driver_add("default_value")
curve.driver.type = "SCRIPTED"
curve.driver.expression = "(frame - 1) / 24"
driver_tree.links.new(driver.outputs[0], driver_surface.inputs["Roughness"])
driver_graph = miku_blender.snapshot_material(driver_material)
driver_snapshot = next(
    item for item in driver_graph["nodes"] if item["op"] == "Input.Value"
)
assert driver_snapshot["outputs"][0]["driver"]["kind"] == "TimeAffine"
driver_ir = build_material_ir(driver_graph, conversion_mode="Auto")
assert any(
    item["op"] == "Input.Time.Frame" for item in driver_ir["expressions"]
)
with tempfile.TemporaryDirectory() as output_root:
    try:
        miku_blender.export_material_bundle(
            driver_material,
            output_root,
            source_blend_id="runtime-smoke-source",
            persistent_material_id="runtime-smoke-driver",
        )
    except RuntimeError as error:
        assert str(error).startswith("MIKU_TIME_INPUT_UNSUPPORTED:")
    else:
        raise AssertionError("frame-driven time input was exported")
    assert not any(Path(output_root).iterdir())

external_material, external_tree, external_surface = new_material(
    "Miku Externalized Driver"
)
external = external_tree.nodes.new("ShaderNodeValue")
external_curve = external.outputs[0].driver_add("default_value")
external_curve.driver.type = "SCRIPTED"
external_curve.driver.expression = "sin(frame)"
external_tree.links.new(
    external.outputs[0],
    external_surface.inputs["Roughness"],
)
external_graph = miku_blender.snapshot_material(external_material)
external_snapshot = next(
    item for item in external_graph["nodes"] if item["op"] == "Input.Value"
)
assert external_snapshot["outputs"][0]["driver"]["kind"] == "Externalized"
assert any(
    item["code"] == "MIKU_TIME_DRIVER_EXTERNALIZED"
    for item in external_graph["diagnostics"]
)

unsafe_material, unsafe_tree, unsafe_surface = new_material(
    "Miku Unsafe Driver"
)
unsafe = unsafe_tree.nodes.new("ShaderNodeValue")
unsafe_curve = unsafe.outputs[0].driver_add("default_value")
unsafe_curve.driver.type = "SCRIPTED"
unsafe_curve.driver.expression = "eval('frame')"
unsafe_tree.links.new(unsafe.outputs[0], unsafe_surface.inputs["Roughness"])
unsafe_graph = miku_blender.snapshot_material(unsafe_material)
unsafe_snapshot = next(
    item for item in unsafe_graph["nodes"] if item["op"] == "Input.Value"
)
assert unsafe_snapshot["outputs"][0]["driver"]["kind"] == "Unsupported"

print(
    "MIKU_RUNTIME_INPUTS_SMOKE_COMPLETE:"
    + json.dumps(
        {
            "cameraExpressions": len(camera_ir["expressions"]),
            "groupExpressions": len(group_ir["expressions"]),
            "timeExpressions": len(time_ir["expressions"]),
            "blenderVersion": list(bpy.app.version),
        },
        sort_keys=True,
    )
)
