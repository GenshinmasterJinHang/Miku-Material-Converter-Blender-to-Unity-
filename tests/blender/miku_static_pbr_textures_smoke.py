"""Blender 5.0-5.2 smoke fixture for external PBR image translation."""

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
from miku.semantic import build_material_ir

require_blender_capabilities(bpy)

miku_blender.register()


def make_image(
    root: Path,
    name: str,
    file_format: str,
    color: tuple[float, float, float, float],
    color_space: str,
):
    float_buffer = file_format == "OPEN_EXR"
    image = bpy.data.images.new(
        name,
        width=4,
        height=4,
        alpha=True,
        float_buffer=float_buffer,
    )
    image.pixels = list(color) * 16
    extension = {
        "PNG": ".png",
        "JPEG": ".jpg",
        "OPEN_EXR": ".exr",
    }[file_format]
    path = root / (name + extension)
    image.filepath_raw = str(path)
    image.file_format = file_format
    image.save()
    bpy.data.images.remove(image)
    loaded = bpy.data.images.load(str(path), check_existing=False)
    loaded.colorspace_settings.name = color_space
    return loaded


with tempfile.TemporaryDirectory(prefix="miku-static-pbr-2.2-") as temp:
    root = Path(temp)
    material = bpy.data.materials.new("Miku Static PBR 2.2")
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()

    output = tree.nodes.new("ShaderNodeOutputMaterial")
    principled = tree.nodes.new("ShaderNodeBsdfPrincipled")
    normal_map = tree.nodes.new("ShaderNodeNormalMap")
    displacement = tree.nodes.new("ShaderNodeDisplacement")
    separate = tree.nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    ao_multiply = tree.nodes.new("ShaderNodeMixRGB")
    ao_multiply.blend_type = "MULTIPLY"
    ao_multiply.inputs[0].default_value = 1.0
    displacement.space = "OBJECT"
    normal_map.space = "TANGENT"
    normal_map.inputs["Strength"].default_value = 0.8
    displacement.inputs["Midlevel"].default_value = 0.5
    displacement.inputs["Scale"].default_value = 0.1

    color = tree.nodes.new("ShaderNodeTexImage")
    color.image = make_image(
        root,
        "color",
        "JPEG",
        (0.35, 0.15, 0.05, 1.0),
        "sRGB",
    )
    packed = tree.nodes.new("ShaderNodeTexImage")
    packed.image = make_image(
        root,
        "packed-scalars",
        "PNG",
        (1.0, 0.65, 0.8, 0.6),
        "Non-Color",
    )
    normal = tree.nodes.new("ShaderNodeTexImage")
    normal.image = make_image(
        root,
        "normal",
        "PNG",
        (0.5, 0.5, 1.0, 1.0),
        "Non-Color",
    )
    emission = tree.nodes.new("ShaderNodeTexImage")
    emission.image = make_image(
        root,
        "emission-color",
        "OPEN_EXR",
        (4.0, 0.5, 0.1, 1.0),
        "sRGB",
    )

    tree.links.new(packed.outputs["Color"], separate.inputs["Color"])
    tree.links.new(color.outputs["Color"], ao_multiply.inputs[1])
    tree.links.new(separate.outputs["Blue"], ao_multiply.inputs[2])
    tree.links.new(
        ao_multiply.outputs["Color"],
        principled.inputs["Base Color"],
    )
    tree.links.new(
        separate.outputs["Green"],
        principled.inputs["Roughness"],
    )
    tree.links.new(
        separate.outputs["Red"],
        principled.inputs["Metallic"],
    )
    tree.links.new(normal.outputs["Color"], normal_map.inputs["Color"])
    tree.links.new(normal_map.outputs["Normal"], principled.inputs["Normal"])
    tree.links.new(
        packed.outputs["Alpha"],
        displacement.inputs["Height"],
    )
    tree.links.new(
        packed.outputs["Alpha"],
        principled.inputs["Alpha"],
    )
    tree.links.new(
        emission.outputs["Color"],
        principled.inputs["Emission Color"],
    )
    principled.inputs["Emission Strength"].default_value = 2.0
    tree.links.new(principled.outputs["BSDF"], output.inputs["Surface"])
    tree.links.new(
        displacement.outputs["Displacement"],
        output.inputs["Displacement"],
    )

    if not hasattr(material, "displacement_method"):
        raise RuntimeError("MIKU_BLENDER_DISPLACEMENT_METHOD_API_MISSING")
    material.displacement_method = "BOTH"
    material.surface_render_method = "DITHERED"
    material.miku_normal_convention = "TangentDirectXNegativeY"

    snapshot = miku_blender.snapshot_material(material)
    ao_snapshot = next(
        item for item in snapshot["nodes"]
        if item["id"] == miku_blender._node_stable_id(ao_multiply)
    )
    assert ao_snapshot["params"]["blend_type"] == "MULTIPLY", ao_snapshot
    base_slot = snapshot["standardPbrSemantic"]["slots"]["BaseColor"]
    assert base_slot["source"]["node"] != ao_snapshot["id"], (
        base_slot,
        ao_snapshot,
    )
    for semantic in ("BaseColor", "Metalness", "Roughness", "Normal", "Emission"):
        slot = snapshot["standardPbrSemantic"]["slots"][semantic]
        assert isinstance(slot.get("source"), dict), (semantic, slot)
        assert slot["source"].get("node"), (semantic, slot)
        assert float(slot.get("confidence", 0.0)) > 0.0, (semantic, slot)
    assert (
        float(snapshot["standardPbrSemantic"]["source"]["confidence"]) > 0.0
    ), snapshot["standardPbrSemantic"]
    preview_ir = build_material_ir(
        snapshot,
        source_blend_id="static-pbr-source",
        material_key=material.name,
    )
    preview_samples = [
        item
        for item in preview_ir["expressions"]
        if item["op"] == "Texture.SampleImage2D"
    ]
    assert len({
        item["params"]["resourceId"]
        for item in preview_samples
        if item["params"]["semantic"] == "BaseColor"
    }) == 1, preview_samples

    result = miku_blender.export_material_bundle(
        material,
        str(root / "bundle-output"),
        source_blend_id="static-pbr-source",
        persistent_material_id="static-pbr-material",
        mode="Auto",
    )
    bundle_path = Path(result["bundlePath"])
    bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
    assert bundle["documentKind"] == "miku-bundle-1.0", bundle
    assert len(bundle["resources"]) == 4, bundle["resources"]
    packed_resources = [
        item
        for item in bundle["resources"]
        if item.get("channelBindings")
    ]
    assert len(packed_resources) == 1, packed_resources
    packed_resource = packed_resources[0]
    assert {
        (item["semantic"], item["channel"])
        for item in packed_resource["channelBindings"]
    } == {
        ("Metalness", "R"),
        ("Roughness", "G"),
        ("AmbientOcclusion", "B"),
        ("Height", "A"),
        ("Alpha", "A"),
    }, packed_resource
    assert packed_resource["bindingKey"].startswith(
        "_MIKU_Packed_"
    ), packed_resource
    normal_resource = next(
        item
        for item in bundle["resources"]
        if item["semantic"] == "Normal"
    )
    assert (
        normal_resource["normalConvention"]
        == "TangentDirectXNegativeY"
    ), normal_resource
    assert any(
        item["mediaType"] == "image/jpeg"
        for item in bundle["resources"]
    )
    assert any(
        item["mediaType"] == "image/x-exr"
        for item in bundle["resources"]
    )
    plan = json.loads(
        (bundle_path.parent / bundle["plan"]["relativePath"])
        .read_text(encoding="utf-8")
    )
    assert plan["bakeJobs"] == [], plan["bakeJobs"]
    ir = json.loads(
        (bundle_path.parent / bundle["ir"]["relativePath"])
        .read_text(encoding="utf-8")
    )
    operations = {item["op"] for item in ir["expressions"]}
    assert "Vector.NormalStrength" in operations, operations
    assert "Vector.NormalBlend" in operations, operations
    assert "Vector.Displacement" in operations, operations
    assert "Texture.SampleImage2D" in operations, operations
    assert "Math.Lerp" in operations, operations
    channels = {
        item["semantic"]: item
        for item in ir["channels"]
    }
    assert "AmbientOcclusion" in channels, channels
    assert channels["Displacement"]["stage"] == "Vertex"
    assert ir["surfaceContract"]["renderMethod"] == "Dithered"
    assert any(
        item["code"]
        == "MIKU_HDR_EMISSION_REQUIRES_URP_POST_PROCESSING"
        for item in ir["diagnostics"]
    ), ir["diagnostics"]
    serialized = json.dumps(bundle, ensure_ascii=False)
    assert str(root) not in serialized, serialized

    first_bytes = bundle_path.read_bytes()
    repeated = miku_blender.export_material_bundle(
        material,
        str(root / "bundle-output-repeat"),
        source_blend_id="static-pbr-source",
        persistent_material_id="static-pbr-material",
        mode="Auto",
    )
    assert Path(repeated["bundlePath"]).read_bytes() == first_bytes

    def export_height_mode(
        mode: str,
        policy: str = "FOLLOW_BLENDER",
    ):
        mode_material = bpy.data.materials.new(
            f"Miku Height {mode}"
        )
        mode_material.use_nodes = True
        mode_tree = mode_material.node_tree
        mode_tree.nodes.clear()
        mode_output = mode_tree.nodes.new(
            "ShaderNodeOutputMaterial"
        )
        mode_principled = mode_tree.nodes.new(
            "ShaderNodeBsdfPrincipled"
        )
        mode_height = mode_tree.nodes.new("ShaderNodeTexImage")
        mode_height.image = packed.image
        mode_tree.links.new(
            mode_principled.outputs["BSDF"],
            mode_output.inputs["Surface"],
        )
        if mode == "BUMP":
            mode_normal_image = mode_tree.nodes.new(
                "ShaderNodeTexImage"
            )
            mode_normal_image.image = normal.image
            mode_normal_map = mode_tree.nodes.new(
                "ShaderNodeNormalMap"
            )
            mode_bump = mode_tree.nodes.new("ShaderNodeBump")
            mode_bump.inputs["Strength"].default_value = 0.7
            mode_bump.inputs["Distance"].default_value = 0.2
            mode_tree.links.new(
                mode_normal_image.outputs["Color"],
                mode_normal_map.inputs["Color"],
            )
            mode_tree.links.new(
                mode_normal_map.outputs["Normal"],
                mode_bump.inputs["Normal"],
            )
            mode_tree.links.new(
                mode_height.outputs["Alpha"],
                mode_bump.inputs["Height"],
            )
            mode_tree.links.new(
                mode_bump.outputs["Normal"],
                mode_principled.inputs["Normal"],
            )
        else:
            mode_displacement = mode_tree.nodes.new(
                "ShaderNodeDisplacement"
            )
            mode_displacement.space = "OBJECT"
            mode_tree.links.new(
                mode_height.outputs["Alpha"],
                mode_displacement.inputs["Height"],
            )
            mode_tree.links.new(
                mode_displacement.outputs["Displacement"],
                mode_output.inputs["Displacement"],
            )
        mode_material.displacement_method = mode
        mode_material.miku_normal_convention = (
            "TangentOpenGLPositiveY"
        )
        mode_material.miku_displacement_policy = policy
        policy_suffix = policy.lower().replace("_", "-")
        mode_result = miku_blender.export_material_bundle(
            mode_material,
            str(
                root
                / f"bundle-output-{mode.lower()}-{policy_suffix}"
            ),
            source_blend_id=(
                f"static-pbr-{mode.lower()}-{policy_suffix}-source"
            ),
            persistent_material_id=(
                f"static-pbr-{mode.lower()}-{policy_suffix}-material"
            ),
            mode="Auto",
        )
        mode_bundle_path = Path(mode_result["bundlePath"])
        mode_bundle = json.loads(
            mode_bundle_path.read_text(encoding="utf-8")
        )
        mode_ir = json.loads(
            (
                mode_bundle_path.parent
                / mode_bundle["ir"]["relativePath"]
            ).read_text(encoding="utf-8")
        )
        return mode_bundle, mode_ir

    bump_bundle, bump_ir = export_height_mode("BUMP")
    assert any(
        item["op"] == "Vector.NormalFromHeight"
        for item in bump_ir["expressions"]
    )
    assert not any(
        item["semantic"] == "Displacement"
        and (item.get("value") or {}).get("kind") == "Expression"
        for item in bump_ir["channels"]
    )
    assert next(
        item
        for item in bump_bundle["resources"]
        if item["semantic"] == "Normal"
    )["normalConvention"] == "TangentOpenGLPositiveY"

    _, displacement_ir = export_height_mode("DISPLACEMENT")
    displacement_channel = next(
        item
        for item in displacement_ir["channels"]
        if item["semantic"] == "Displacement"
    )
    assert displacement_channel["stage"] == "Vertex"
    assert (
        displacement_channel["value"]["kind"] == "Expression"
    ), displacement_channel

    _, promoted_ir = export_height_mode("BUMP", "ALWAYS_VERTEX")
    promoted_channels = {
        item["semantic"]: item for item in promoted_ir["channels"]
    }
    assert promoted_ir["displacementPolicy"] == "ALWAYS_VERTEX"
    assert promoted_ir["heightChannel"]["midlevel"] == 0.5
    assert abs(promoted_ir["heightChannel"]["scale"] - 0.14) < 1.0e-6
    assert "Height" in promoted_channels
    assert (
        promoted_channels["Displacement"]["value"]["kind"]
        == "Expression"
    )
    promoted_ops = {
        item["op"] for item in promoted_ir["expressions"]
    }
    assert "Input.MaterialChannel" in promoted_ops

    _, map_only_ir = export_height_mode("BUMP", "MAP_ONLY")
    map_only_channels = {
        item["semantic"]: item for item in map_only_ir["channels"]
    }
    assert "Height" in map_only_channels
    assert not (
        (map_only_channels.get("Displacement", {}).get("value") or {})
        .get("kind") == "Expression"
    )

    opaque_alpha = bpy.data.materials.new(
        "Miku Opaque Linked Alpha"
    )
    opaque_alpha.use_nodes = True
    opaque_tree = opaque_alpha.node_tree
    opaque_tree.nodes.clear()
    opaque_output = opaque_tree.nodes.new(
        "ShaderNodeOutputMaterial"
    )
    opaque_principled = opaque_tree.nodes.new(
        "ShaderNodeBsdfPrincipled"
    )
    opaque_image = opaque_tree.nodes.new("ShaderNodeTexImage")
    opaque_image.image = packed.image
    opaque_tree.links.new(
        opaque_image.outputs["Alpha"],
        opaque_principled.inputs["Alpha"],
    )
    opaque_tree.links.new(
        opaque_principled.outputs["BSDF"],
        opaque_output.inputs["Surface"],
    )
    class OpaqueMaterialProxy:
        name = opaque_alpha.name
        node_tree = opaque_alpha.node_tree
        displacement_method = "BUMP"
        use_backface_culling = False
        miku_normal_convention = "TangentOpenGLPositiveY"

    opaque_snapshot = miku_blender.snapshot_material(
        OpaqueMaterialProxy()
    )
    assert (
        opaque_snapshot["surfaceSemantic"]["renderMethod"]
        == "AlphaBlend"
    ), opaque_snapshot["surfaceSemantic"]
    assert any(
        item["code"] == "MIKU_ALPHA_LINKED_OPAQUE_AUTO_BLEND"
        for item in opaque_snapshot["diagnostics"]
    ), opaque_snapshot["diagnostics"]

    # Regression fixture for the Unity inspector contract: each editable
    # Standard PBR map is backed by its own exported image resource.
    direct_material = bpy.data.materials.new("Miku Direct Standard PBR")
    direct_material.use_nodes = True
    direct_tree = direct_material.node_tree
    direct_tree.nodes.clear()
    direct_output = direct_tree.nodes.new("ShaderNodeOutputMaterial")
    direct_principled = direct_tree.nodes.new("ShaderNodeBsdfPrincipled")
    direct_normal_map = direct_tree.nodes.new("ShaderNodeNormalMap")
    direct_normal_map.space = "TANGENT"
    direct_specs = (
        ("BaseColor", "direct-base", "JPEG", (0.2, 0.4, 0.6, 1.0), "sRGB", "Base Color", "Color"),
        ("Metalness", "direct-metallic", "PNG", (0.8, 0.8, 0.8, 1.0), "Non-Color", "Metallic", "Color"),
        ("Roughness", "direct-roughness", "PNG", (0.35, 0.35, 0.35, 1.0), "Non-Color", "Roughness", "Color"),
        ("Normal", "direct-normal", "PNG", (0.5, 0.5, 1.0, 1.0), "Non-Color", "Color", "Color"),
        ("Emission", "direct-emission", "OPEN_EXR", (2.0, 0.25, 0.05, 1.0), "sRGB", "Emission Color", "Color"),
    )
    direct_images = {}
    for semantic, name, file_format, color_value, color_space, _socket, _output in direct_specs:
        image_node = direct_tree.nodes.new("ShaderNodeTexImage")
        image_node.image = make_image(
            root,
            name,
            file_format,
            color_value,
            color_space,
        )
        direct_images[semantic] = image_node
    direct_tree.links.new(direct_images["BaseColor"].outputs["Color"], direct_principled.inputs["Base Color"])
    direct_tree.links.new(direct_images["Metalness"].outputs["Color"], direct_principled.inputs["Metallic"])
    direct_tree.links.new(direct_images["Roughness"].outputs["Color"], direct_principled.inputs["Roughness"])
    direct_tree.links.new(direct_images["Normal"].outputs["Color"], direct_normal_map.inputs["Color"])
    direct_tree.links.new(direct_normal_map.outputs["Normal"], direct_principled.inputs["Normal"])
    direct_tree.links.new(direct_images["Emission"].outputs["Color"], direct_principled.inputs["Emission Color"])
    direct_tree.links.new(direct_principled.outputs["BSDF"], direct_output.inputs["Surface"])
    direct_material.miku_normal_convention = "TangentOpenGLPositiveY"
    direct_result = miku_blender.export_material_bundle(
        direct_material,
        str(root / "direct-standard-pbr-bundle"),
        source_blend_id="direct-standard-pbr-source",
        persistent_material_id="direct-standard-pbr-material",
        mode="Auto",
    )
    direct_bundle_path = Path(direct_result["bundlePath"])
    direct_bundle = json.loads(direct_bundle_path.read_text(encoding="utf-8"))
    direct_semantics = {
        item["semantic"]
        for item in direct_bundle["resources"]
        if item.get("semantic") in {"BaseColor", "Metalness", "Roughness", "Normal", "Emission"}
    }
    assert direct_semantics == {"BaseColor", "Metalness", "Roughness", "Normal", "Emission"}, direct_bundle["resources"]
    assert {
        item.get("bindingKey")
        for item in direct_bundle["resources"]
        if item.get("semantic") in direct_semantics
    } == direct_semantics, direct_bundle["resources"]

print("MIKU_STATIC_PBR_TEXTURES_SMOKE_OK")
