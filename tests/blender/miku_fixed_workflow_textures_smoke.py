"""Blender 5.2 smoke coverage for tolerant fixed-workflow texture export."""

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


if tuple(bpy.app.version) != (5, 2, 0):
    raise RuntimeError(
        "MIKU_BLENDER_VERSION_MISMATCH:"
        f"expected=(5, 2, 0):got={bpy.app.version}"
    )

miku_blender.register()


def image(
    root: Path,
    name: str,
    color_space: str,
    *,
    file_format: str = "PNG",
):
    value = bpy.data.images.new(name, width=2, height=2, alpha=True)
    value.pixels = [0.25, 0.5, 0.75, 1.0] * 4
    extension = ".tga" if file_format == "TARGA" else ".png"
    path = root / f"{name}{extension}"
    value.filepath_raw = str(path)
    value.file_format = file_format
    value.save()
    value.colorspace_settings.name = color_space
    return value


def material_with_unsupported_graph(name: str):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    emission = tree.nodes.new("ShaderNodeEmission")
    math = tree.nodes.new("ShaderNodeMath")
    light_path = tree.nodes.new("ShaderNodeLightPath")
    math.operation = "MULTIPLY"
    tree.links.new(light_path.outputs["Is Camera Ray"], math.inputs[0])
    tree.links.new(math.outputs[0], emission.inputs["Strength"])
    tree.links.new(emission.outputs[0], output.inputs["Surface"])
    return material


def enabled_input(node, name: str):
    return next(
        socket
        for socket in node.inputs
        if socket.name == name
        and socket.enabled
        and not socket.is_unavailable
    )


def export_bundle(material, root: Path, case_id: str, part: str):
    result = miku_blender.export_material_bundle(
        material,
        str(root / case_id),
        source_blend_id=f"{case_id}-source",
        persistent_material_id=f"{case_id}-material",
        workflow_kind="wuwa_toon",
        workflow_part=part,
        mode="FullPBRBake",
    )
    path = Path(result["bundlePath"])
    bundle = json.loads(path.read_text(encoding="utf-8"))
    bundle["_testBundlePath"] = str(path)
    return bundle


def roles_by_resource(bundle):
    return {
        resource["id"]: {
            binding["role"]
            for binding in resource.get("materialBindings", [])
        }
        for resource in bundle["resources"]
    }


with tempfile.TemporaryDirectory(prefix="miku-fixed-workflow-") as temp:
    root = Path(temp)
    cases = (
        (
            "genshin",
            "genshin_toon",
            (("BaseMap", "sRGB"), ("LightMap", "Non-Color")),
        ),
        (
            "wuwa",
            "wuwa_toon",
            (("BaseMap", "sRGB"), ("NormalMap", "Non-Color")),
        ),
        (
            "hsr",
            "hsr_toon",
            (("BaseMap", "sRGB"), ("LightMap", "Non-Color")),
        ),
    )
    for case_id, workflow, role_specs in cases:
        material = material_with_unsupported_graph(f"Fixed {case_id}")
        tree = material.node_tree
        created = []
        for index, (role, color_space) in enumerate(role_specs):
            node = tree.nodes.new("ShaderNodeTexImage")
            node.image = image(root, f"{case_id}-{index}", color_space)
            created.append(node)
            node.miku_texture_role = role

        first = miku_blender.export_material_bundle(
            material,
            str(root / f"{case_id}-first"),
            source_blend_id=f"{case_id}-source",
            persistent_material_id=f"{case_id}-material",
            workflow_kind=workflow,
            workflow_part="Body",
            mode="FullPBRBake",
        )
        second = miku_blender.export_material_bundle(
            material,
            str(root / f"{case_id}-second"),
            source_blend_id=f"{case_id}-source",
            persistent_material_id=f"{case_id}-material",
            workflow_kind=workflow,
            workflow_part="Body",
            mode="FullPBRBake",
        )
        first_path = Path(first["bundlePath"])
        second_path = Path(second["bundlePath"])
        assert first_path.read_bytes() == second_path.read_bytes()
        bundle = json.loads(first_path.read_text(encoding="utf-8"))
        resources = bundle["resources"]
        roles = {
            binding["role"]
            for resource in resources
            for binding in resource.get("materialBindings", [])
        }
        assert roles == {role for role, _ in role_specs}, (case_id, resources)
        plan = json.loads(
            (first_path.parent / bundle["plan"]["relativePath"]).read_text(
                encoding="utf-8"
            )
        )
        assert plan["routePolicy"] == "FixedWorkflowTextureBinding", plan
        assert plan["bakeJobs"] == [], plan
        assert not any(
            str(item.get("code", "")).startswith("MIKU_CLOSURE_")
            for item in plan["diagnostics"]
        ), plan["diagnostics"]

    voice = bpy.data.materials.new("Voice Active Base")
    voice.use_nodes = True
    voice_tree = voice.node_tree
    voice_tree.nodes.clear()
    voice_output = voice_tree.nodes.new("ShaderNodeOutputMaterial")
    voice_emission = voice_tree.nodes.new("ShaderNodeEmission")
    voice_active_image = image(root, "xing 5", "sRGB")
    voice_active = voice_tree.nodes.new("ShaderNodeTexImage")
    voice_active.image = voice_active_image
    voice_tree.links.new(voice_active.outputs["Color"], voice_emission.inputs["Color"])
    voice_tree.links.new(voice_emission.outputs[0], voice_output.inputs["Surface"])
    voice_inactive_image = image(root, "T_5XingStar_D2", "sRGB")
    voice_inactive = voice_tree.nodes.new("ShaderNodeTexImage")
    voice_inactive.image = voice_inactive_image
    voice_inactive.label = "BaseMap"
    voice_bundle = export_bundle(voice, root, "voice-active-base", "Effect")
    voice_roles = roles_by_resource(voice_bundle)
    assert voice_roles[miku_blender._fixed_image_resource_id(voice_active_image)] == {
        "BaseMap"
    }, voice_roles
    assert "BaseMap" not in voice_roles[
        miku_blender._fixed_image_resource_id(voice_inactive_image)
    ], voice_roles
    voice_plan = json.loads(
        (
            Path(voice_bundle["_testBundlePath"]).parent
            / voice_bundle["plan"]["relativePath"]
        ).read_text(encoding="utf-8")
    )
    assert any(
        item.get("code") == "MIKU_FIXED_TEXTURE_INACTIVE_PRIMARY_IGNORED"
        for item in voice_plan["diagnostics"]
    ), voice_plan["diagnostics"]

    eye = bpy.data.materials.new("Authored Wuwa Eye")
    eye.use_nodes = True
    eye_tree = eye.node_tree
    eye_tree.nodes.clear()
    eye_output = eye_tree.nodes.new("ShaderNodeOutputMaterial")
    eye_emission = eye_tree.nodes.new("ShaderNodeEmission")
    texture_coordinate = eye_tree.nodes.new("ShaderNodeTexCoord")
    base_a_image = image(root, "Eye_D_A", "sRGB")
    base_b_image = image(root, "Eye_D_B", "sRGB")
    base_a = eye_tree.nodes.new("ShaderNodeTexImage")
    base_b = eye_tree.nodes.new("ShaderNodeTexImage")
    base_a.image = base_a_image
    base_b.image = base_b_image
    base_mix = eye_tree.nodes.new("ShaderNodeMix")
    base_mix.data_type = "RGBA"
    enabled_input(base_mix, "Factor").default_value = 1.0
    eye_tree.links.new(base_a.outputs["Color"], enabled_input(base_mix, "A"))
    eye_tree.links.new(base_b.outputs["Color"], enabled_input(base_mix, "B"))

    eye_het_image = image(
        root,
        "T_R2T1FeiBiMd10011Eye_HET",
        "Non-Color",
        file_format="TARGA",
    )
    eye_het = eye_tree.nodes.new("ShaderNodeTexImage")
    eye_het.image = eye_het_image

    upper_image = image(root, "T_Highlight_1", "Non-Color")
    lower_image = image(root, "BottomHighlight_1", "Non-Color")
    upper_group_tree = bpy.data.node_groups.new(
        "Nested Eye Upper Highlight",
        "ShaderNodeTree",
    )
    upper_group_tree.interface.new_socket(
        name="Color",
        in_out="OUTPUT",
        socket_type="NodeSocketColor",
    )
    upper_group_output = upper_group_tree.nodes.new("NodeGroupOutput")
    upper_group_coordinate = upper_group_tree.nodes.new("ShaderNodeTexCoord")
    upper = upper_group_tree.nodes.new("ShaderNodeTexImage")
    lower = eye_tree.nodes.new("ShaderNodeTexImage")
    upper.image = upper_image
    lower.image = lower_image
    upper_mapping = upper_group_tree.nodes.new("ShaderNodeMapping")
    lower_mapping = eye_tree.nodes.new("ShaderNodeMapping")
    upper_mapping.vector_type = "POINT"
    lower_mapping.vector_type = "POINT"
    upper_mapping.inputs["Location"].default_value = (0.13, -0.05, 0.0)
    upper_mapping.inputs["Rotation"].default_value = (0.0, 0.0, 0.0)
    upper_mapping.inputs["Scale"].default_value = (0.68, 1.27, 1.06)
    lower_mapping.inputs["Location"].default_value = (-0.48, -0.27, 0.0)
    lower_mapping.inputs["Rotation"].default_value = (0.0, 0.0, 0.0)
    lower_mapping.inputs["Scale"].default_value = (1.58, 1.61, 1.0)
    upper_group_tree.links.new(
        upper_group_coordinate.outputs["UV"],
        upper_mapping.inputs["Vector"],
    )
    eye_tree.links.new(texture_coordinate.outputs["UV"], lower_mapping.inputs["Vector"])
    upper_group_tree.links.new(
        upper_mapping.outputs["Vector"],
        upper.inputs["Vector"],
    )
    upper_group_tree.links.new(
        upper.outputs["Color"],
        upper_group_output.inputs["Color"],
    )
    eye_tree.links.new(lower_mapping.outputs["Vector"], lower.inputs["Vector"])
    upper_group = eye_tree.nodes.new("ShaderNodeGroup")
    upper_group.node_tree = upper_group_tree
    authored_mix = eye_tree.nodes.new("ShaderNodeMix")
    authored_mix.data_type = "RGBA"
    enabled_input(authored_mix, "Factor").default_value = 0.5
    eye_tree.links.new(
        upper_group.outputs["Color"],
        enabled_input(authored_mix, "A"),
    )
    eye_tree.links.new(lower.outputs["Color"], enabled_input(authored_mix, "B"))

    hdmf_a_image = image(root, "T_HDMF_EM", "Non-Color")
    hdmf_b_image = image(root, "T_HDMF02_EM", "Non-Color")
    hdmf_a = eye_tree.nodes.new("ShaderNodeTexImage")
    hdmf_b = eye_tree.nodes.new("ShaderNodeTexImage")
    hdmf_a.image = hdmf_a_image
    hdmf_b.image = hdmf_b_image
    hdmf_mix = eye_tree.nodes.new("ShaderNodeMix")
    hdmf_mix.data_type = "RGBA"
    enabled_input(hdmf_mix, "Factor").default_value = 1.0
    eye_tree.links.new(hdmf_a.outputs["Color"], enabled_input(hdmf_mix, "A"))
    eye_tree.links.new(hdmf_b.outputs["Color"], enabled_input(hdmf_mix, "B"))

    base_and_hdmf = eye_tree.nodes.new("ShaderNodeMixRGB")
    base_and_hdmf.blend_type = "ADD"
    base_and_hdmf.inputs[0].default_value = 1.0
    final_mix = eye_tree.nodes.new("ShaderNodeMixRGB")
    final_mix.blend_type = "ADD"
    final_mix.inputs[0].default_value = 1.0
    eye_tree.links.new(base_mix.outputs["Result"], base_and_hdmf.inputs[1])
    eye_tree.links.new(hdmf_mix.outputs["Result"], base_and_hdmf.inputs[2])
    eye_tree.links.new(base_and_hdmf.outputs["Color"], final_mix.inputs[1])
    eye_tree.links.new(authored_mix.outputs["Result"], final_mix.inputs[2])
    eye_tree.links.new(final_mix.outputs["Color"], eye_emission.inputs["Color"])
    eye_tree.links.new(eye_emission.outputs[0], eye_output.inputs["Surface"])

    eye_bundle = export_bundle(eye, root, "authored-wuwa-eye", "Eye")
    eye_roles = roles_by_resource(eye_bundle)
    eye_het_resource_id = miku_blender._fixed_image_resource_id(eye_het_image)
    assert eye_roles[eye_het_resource_id] == {"EyeHET"}, eye_roles
    eye_het_resource = next(
        resource
        for resource in eye_bundle["resources"]
        if resource["id"] == eye_het_resource_id
    )
    assert eye_het_resource["relativePath"].endswith(".png"), eye_het_resource
    assert eye_het_resource["mediaType"] == "image/png", eye_het_resource
    assert eye_roles[miku_blender._fixed_image_resource_id(base_b_image)] == {"BaseMap"}
    assert not eye_roles[miku_blender._fixed_image_resource_id(base_a_image)]
    assert eye_roles[miku_blender._fixed_image_resource_id(hdmf_b_image)] == {"EyeHDMF"}
    assert not eye_roles[miku_blender._fixed_image_resource_id(hdmf_a_image)]
    assert eye_roles[miku_blender._fixed_image_resource_id(upper_image)] == {"EyeUpperHighlight"}
    assert eye_roles[miku_blender._fixed_image_resource_id(lower_image)] == {"EyeLowerHighlight"}
    upper_resource = next(
        resource
        for resource in eye_bundle["resources"]
        if resource["id"] == miku_blender._fixed_image_resource_id(upper_image)
    )
    upper_binding = upper_resource["materialBindings"][0]
    assert upper_binding["uvTransform"]["coordinateSpace"] == "UV0"
    assert upper_binding["uvTransform"]["operation"] == "Affine2D"
    assert all(
        abs(left - right) <= 1.0e-6
        for left, right in zip(
            upper_binding["uvTransform"]["matrix"],
            (0.68, 0.0, 0.13, 0.0, 1.27, -0.05),
        )
    ), upper_binding

    bai = bpy.data.materials.new("Wuwa Eye White")
    bai.use_nodes = True
    bai_tree = bai.node_tree
    bai_tree.nodes.clear()
    bai_output = bai_tree.nodes.new("ShaderNodeOutputMaterial")
    bai_emission = bai_tree.nodes.new("ShaderNodeEmission")
    bai_base = bai_tree.nodes.new("ShaderNodeTexImage")
    bai_base.image = image(root, "EyeWhite_D", "sRGB")
    bai_tree.links.new(bai_base.outputs["Color"], bai_emission.inputs["Color"])
    bai_tree.links.new(bai_emission.outputs[0], bai_output.inputs["Surface"])
    eye_mesh = bpy.data.meshes.new("Eye Shared Mesh")
    eye_object = bpy.data.objects.new("Eye Shared Object", eye_mesh)
    bpy.context.scene.collection.objects.link(eye_object)
    eye_mesh.materials.append(bai)
    eye_mesh.materials.append(eye)
    bai_bundle = export_bundle(bai, root, "eye-white-inherits-het", "Eye")
    bai_roles = roles_by_resource(bai_bundle)
    assert bai_roles[eye_het_resource_id] == {"EyeHET"}, bai_roles
    bai_plan = json.loads(
        (
            Path(bai_bundle["_testBundlePath"]).parent
            / bai_bundle["plan"]["relativePath"]
        ).read_text(encoding="utf-8")
    )
    assert any(
        item.get("code") == "MIKU_WUWA_EYE_HET_INHERITED"
        for item in bai_plan["diagnostics"]
    ), bai_plan["diagnostics"]

    second_eye = bpy.data.materials.new("Wuwa Second Eye")
    second_eye.use_nodes = True
    second_tree = second_eye.node_tree
    second_tree.nodes.clear()
    second_output = second_tree.nodes.new("ShaderNodeOutputMaterial")
    second_emission = second_tree.nodes.new("ShaderNodeEmission")
    second_het = second_tree.nodes.new("ShaderNodeTexImage")
    second_het.image = image(
        root,
        "T_R2T1FeiBiMd10011Second_Eye_HET",
        "Non-Color",
    )
    second_tree.links.new(
        second_het.outputs["Color"],
        second_emission.inputs["Color"],
    )
    second_tree.links.new(
        second_emission.outputs[0],
        second_output.inputs["Surface"],
    )
    eye_mesh.materials.append(second_eye)
    ambiguous_bundle = export_bundle(
        bai,
        root,
        "eye-white-ambiguous-het",
        "Eye",
    )
    ambiguous_roles = roles_by_resource(ambiguous_bundle)
    assert not any(
        "EyeHET" in roles for roles in ambiguous_roles.values()
    ), ambiguous_roles
    ambiguous_plan = json.loads(
        (
            Path(ambiguous_bundle["_testBundlePath"]).parent
            / ambiguous_bundle["plan"]["relativePath"]
        ).read_text(encoding="utf-8")
    )
    assert any(
        item.get("code") == "MIKU_FIXED_TEXTURE_ROLE_AMBIGUOUS"
        and item.get("role") == "EyeHET"
        for item in ambiguous_plan["diagnostics"]
    ), ambiguous_plan["diagnostics"]

    stockings = bpy.data.materials.new("ID Stockings")
    stockings.use_nodes = True
    stockings_tree = stockings.node_tree
    stockings_tree.nodes.clear()
    stockings_output = stockings_tree.nodes.new("ShaderNodeOutputMaterial")
    stockings_emission = stockings_tree.nodes.new("ShaderNodeEmission")
    stockings_base = stockings_tree.nodes.new("ShaderNodeTexImage")
    stockings_base.image = image(root, "Down Base", "sRGB")
    stockings_base.miku_texture_role = "BaseMap"
    stockings_tree.links.new(
        stockings_base.outputs["Color"],
        stockings_emission.inputs["Color"],
    )
    stockings_id_image = image(root, "Down_ID", "Non-Color")
    stockings_id = stockings_tree.nodes.new("ShaderNodeTexImage")
    stockings_id.image = stockings_id_image
    stockings_separate = stockings_tree.nodes.new("ShaderNodeSeparateColor")
    greater = stockings_tree.nodes.new("ShaderNodeMath")
    greater.operation = "GREATER_THAN"
    greater.inputs[1].default_value = 0.5
    stockings_tree.links.new(
        stockings_id.outputs["Color"],
        stockings_separate.inputs["Color"],
    )
    stockings_tree.links.new(
        stockings_separate.outputs["Green"],
        greater.inputs[0],
    )
    stockings_tree.links.new(greater.outputs[0], stockings_emission.inputs["Strength"])
    stockings_tree.links.new(
        stockings_emission.outputs[0],
        stockings_output.inputs["Surface"],
    )
    stockings_bundle = export_bundle(
        stockings,
        root,
        "id-stockings",
        "Body",
    )
    stockings_roles = roles_by_resource(stockings_bundle)
    assert stockings_roles[
        miku_blender._fixed_image_resource_id(stockings_id_image)
    ] == {"IDMap", "StockingsMap"}, stockings_roles

print("MIKU_FIXED_WORKFLOW_TEXTURES_SMOKE_OK")
