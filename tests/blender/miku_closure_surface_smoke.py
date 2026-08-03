"""Blender 5.2 programmatic smoke tests for closure-aware MaterialIR 2.0."""

from __future__ import annotations

import json
import sys
import tempfile
import uuid
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

import miku_blender  # noqa: E402
from miku.semantic import build_material_ir  # noqa: E402


def new_material(name: str) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    output = tree.nodes.new("ShaderNodeOutputMaterial")
    output.is_active_output = True
    return material


def output_node(material: bpy.types.Material) -> bpy.types.Node:
    return next(
        node
        for node in material.node_tree.nodes
        if node.bl_idname == "ShaderNodeOutputMaterial"
    )


def mix_material(
    name: str,
    first_type: str,
    second_type: str,
    *,
    dynamic_factor: bool = False,
) -> bpy.types.Material:
    material = new_material(name)
    tree = material.node_tree
    first = tree.nodes.new(first_type)
    second = tree.nodes.new(second_type)
    mix = tree.nodes.new("ShaderNodeMixShader")
    if dynamic_factor:
        layer = tree.nodes.new("ShaderNodeLayerWeight")
        ramp = tree.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.2
        ramp.color_ramp.elements[0].color = (0.02, 0.02, 0.02, 1.0)
        ramp.color_ramp.elements[1].position = 0.8
        ramp.color_ramp.elements[1].color = (0.9, 0.9, 0.9, 1.0)
        tree.links.new(layer.outputs["Facing"], ramp.inputs["Fac"])
        tree.links.new(ramp.outputs["Color"], mix.inputs[0])
    else:
        mix.inputs[0].default_value = 0.35
    tree.links.new(first.outputs[0], mix.inputs[1])
    tree.links.new(second.outputs[0], mix.inputs[2])
    tree.links.new(mix.outputs[0], output_node(material).inputs["Surface"])
    return material


def add_material(name: str) -> bpy.types.Material:
    material = new_material(name)
    tree = material.node_tree
    first = tree.nodes.new("ShaderNodeBsdfDiffuse")
    second = tree.nodes.new("ShaderNodeBsdfDiffuse")
    first.inputs["Color"].default_value = (0.8, 0.12, 0.05, 1.0)
    second.inputs["Color"].default_value = (0.05, 0.2, 0.85, 1.0)
    first.inputs["Roughness"].default_value = 0.7
    second.inputs["Roughness"].default_value = 0.15
    add = tree.nodes.new("ShaderNodeAddShader")
    tree.links.new(first.outputs[0], add.inputs[0])
    tree.links.new(second.outputs[0], add.inputs[1])
    tree.links.new(add.outputs[0], output_node(material).inputs["Surface"])
    return material


def material_ir(
    material: bpy.types.Material,
    *,
    fidelity_policy: str = "AllowDeclaredApproximation",
) -> dict:
    return build_material_ir(
        miku_blender.snapshot_material(material),
        source_blend_id="closure-aware-blender-smoke",
        material_key=material.name,
        fidelity_policy=fidelity_policy,
    )


def assert_no_errors(document: dict) -> None:
    errors = [
        item
        for item in document.get("diagnostics", [])
        if str(item.get("severity") or "").lower() == "error"
    ]
    assert not errors, errors


def main() -> None:
    assert tuple(bpy.app.version) == (5, 2, 0), tuple(bpy.app.version)

    emission = mix_material(
        "MikuTransparentEmission",
        "ShaderNodeBsdfTransparent",
        "ShaderNodeEmission",
        dynamic_factor=True,
    )
    emission_document = material_ir(emission)
    assert emission_document["documentKind"] == "miku-material-ir-1.0"
    assert (
        emission_document["surfaceModelPlan"]["kind"]
        == "TransparentEmission"
    ), emission_document["surfaceModelPlan"]
    assert (
        emission_document["surfaceModelPlan"]["transparentCompositePlan"]
        ["premultiplyCount"]
        == 1
    )
    assert len(emission_document["weightedClosures"]["terms"]) == 2
    dynamic_weights = [
        term["finalWeight"]
        for term in emission_document["weightedClosures"]["terms"]
    ]
    assert any("expressionId" in str(weight) for weight in dynamic_weights)
    root_factor = emission_document["closureGraph"]["root"]["factor"]
    assert (
        root_factor["input"]["conversion"]["conversionKind"]
        == "ColorToFloatLuminance"
    ), root_factor
    assert_no_errors(emission_document)

    lit = mix_material(
        "MikuTransparentLit",
        "ShaderNodeBsdfTransparent",
        "ShaderNodeBsdfDiffuse",
    )
    lit_document = material_ir(lit)
    assert lit_document["surfaceModelPlan"]["kind"] == "TransparentLit"
    assert_no_errors(lit_document)

    multi_lobe = add_material("MikuCustomMultiLobe")
    multi_lobe_document = material_ir(multi_lobe)
    assert (
        multi_lobe_document["surfaceModelPlan"]["kind"]
        == "CustomMultiLobe"
    ), multi_lobe_document["surfaceModelPlan"]
    weights = [
        term["finalWeight"]["value"]
        for term in multi_lobe_document["weightedClosures"]["terms"]
    ]
    assert weights == [1.0, 1.0], weights
    assert any(
        item["code"] == "WEIGHT0003"
        for item in multi_lobe_document["diagnostics"]
    )
    assert_no_errors(multi_lobe_document)

    three_lobe = new_material("MikuThreePrincipledCoating")
    three_tree = three_lobe.node_tree
    principled = [
        three_tree.nodes.new("ShaderNodeBsdfPrincipled")
        for _ in range(3)
    ]
    first_mix = three_tree.nodes.new("ShaderNodeMixShader")
    final_mix = three_tree.nodes.new("ShaderNodeMixShader")
    layer_weight = three_tree.nodes.new("ShaderNodeLayerWeight")
    three_tree.links.new(principled[0].outputs[0], first_mix.inputs[1])
    three_tree.links.new(principled[1].outputs[0], first_mix.inputs[2])
    three_tree.links.new(first_mix.outputs[0], final_mix.inputs[1])
    three_tree.links.new(principled[2].outputs[0], final_mix.inputs[2])
    three_tree.links.new(layer_weight.outputs["Facing"], final_mix.inputs[0])
    three_tree.links.new(
        final_mix.outputs[0],
        output_node(three_lobe).inputs["Surface"],
    )
    three_lobe_document = material_ir(three_lobe)
    assert len(three_lobe_document["weightedClosures"]["terms"]) == 3
    for term in three_lobe_document["weightedClosures"]["terms"]:
        for name in ("Normal", "Coat Normal"):
            parameter = term["parameters"].get(name)
            if parameter and parameter.get("kind") == "Constant":
                assert parameter["value"] == [0.0, 0.0, 1.0], parameter
    assert_no_errors(three_lobe_document)

    blue_emission = mix_material(
        "MikuBlueViewEmission",
        "ShaderNodeBsdfPrincipled",
        "ShaderNodeEmission",
        dynamic_factor=True,
    )
    blue_node = next(
        node
        for node in blue_emission.node_tree.nodes
        if node.bl_idname == "ShaderNodeEmission"
    )
    blue_node.inputs["Color"].default_value = (0.0, 0.303, 1.0, 1.0)
    blue_node.inputs["Strength"].default_value = 11.2
    blue_document = material_ir(blue_emission)
    blue_term = next(
        term
        for term in blue_document["weightedClosures"]["terms"]
        if term["domain"] == "Emission"
    )
    blue_color = blue_term["parameters"]["Color"]["value"]
    assert all(
        abs(actual - expected) < 1e-5
        for actual, expected in zip(blue_color[:3], (0.0, 0.303, 1.0))
    ), blue_term
    assert abs(blue_term["parameters"]["Strength"]["value"] - 11.2) < 1e-5
    assert "expressionId" in str(blue_term["finalWeight"]), blue_term
    assert_no_errors(blue_document)

    shared_normal = new_material("MikuSharedBakedNormalLobes")
    normal_tree = shared_normal.node_tree
    diffuse = normal_tree.nodes.new("ShaderNodeBsdfDiffuse")
    glossy = normal_tree.nodes.new("ShaderNodeBsdfGlossy")
    bump = normal_tree.nodes.new("ShaderNodeBump")
    height = normal_tree.nodes.new("ShaderNodeValue")
    add = normal_tree.nodes.new("ShaderNodeAddShader")
    normal_tree.links.new(height.outputs[0], bump.inputs["Height"])
    normal_tree.links.new(bump.outputs["Normal"], diffuse.inputs["Normal"])
    normal_tree.links.new(bump.outputs["Normal"], glossy.inputs["Normal"])
    normal_tree.links.new(diffuse.outputs[0], add.inputs[0])
    normal_tree.links.new(glossy.outputs[0], add.inputs[1])
    normal_tree.links.new(
        add.outputs[0],
        output_node(shared_normal).inputs["Surface"],
    )
    normal_document = material_ir(shared_normal)
    normal_parameters = [
        term["parameters"]["Normal"]
        for term in normal_document["weightedClosures"]["terms"]
    ]
    assert all(
        item["kind"] == "ValueExpression"
        for item in normal_parameters
    ), normal_parameters
    assert len(
        {item["expressionId"] for item in normal_parameters}
    ) == 1, normal_parameters
    assert_no_errors(normal_document)

    glass = mix_material(
        "MikuStrictGlass",
        "ShaderNodeBsdfTransparent",
        "ShaderNodeBsdfGlass",
    )
    strict_glass = material_ir(
        glass,
        fidelity_policy="Strict",
    )
    assert (
        strict_glass["surfaceModelPlan"]["kind"]
        == "UnsupportedSurface"
    ), strict_glass["surfaceModelPlan"]
    assert any(
        item["code"] == "MIKU_GLASS_LOW_QUALITY_APPROXIMATION"
        and item["severity"] == "error"
        for item in strict_glass["diagnostics"]
    )

    with tempfile.TemporaryDirectory(
        prefix="miku-closure-aware-2.0-"
    ) as output:
        output_root = Path(output)
        mask_path = output_root / "emission-mask.png"
        mask_image = bpy.data.images.new(
            "Miku Emission Mask",
            width=4,
            height=4,
            alpha=True,
        )
        mask_image.pixels = [0.0, 0.0, 0.0, 1.0] * 8 + [
            1.0,
            1.0,
            1.0,
            1.0,
        ] * 8
        mask_image.filepath_raw = str(mask_path)
        mask_image.file_format = "PNG"
        mask_image.save()
        bpy.data.images.remove(mask_image)
        mask_image = bpy.data.images.load(
            str(mask_path),
            check_existing=False,
        )
        mask_image.colorspace_settings.name = "Non-Color"

        masked_emission = mix_material(
            "MikuBlackWhiteEmissionMask",
            "ShaderNodeBsdfPrincipled",
            "ShaderNodeEmission",
        )
        masked_tree = masked_emission.node_tree
        masked_mix = next(
            node
            for node in masked_tree.nodes
            if node.bl_idname == "ShaderNodeMixShader"
        )
        mask_node = masked_tree.nodes.new("ShaderNodeTexImage")
        mask_node.image = mask_image
        masked_tree.links.new(
            mask_node.outputs["Color"],
            masked_mix.inputs[0],
        )
        masked_document = material_ir(masked_emission)
        mask_samples = [
            item
            for item in masked_document["expressions"]
            if item["op"] == "Texture.SampleImage2D"
        ]
        assert len(mask_samples) == 1, mask_samples
        assert (
            mask_samples[0]["params"]["semantic"] == "EmissionMask"
        ), mask_samples
        assert (
            mask_samples[0]["params"]["referenceName"]
            == "_MIKU_EmissionMask"
        ), mask_samples
        assert_no_errors(masked_document)

        additive_emission = new_material("MikuAdditiveEmission")
        additive_tree = additive_emission.node_tree
        additive_principled = additive_tree.nodes.new(
            "ShaderNodeBsdfPrincipled"
        )
        additive_bsdf = additive_tree.nodes.new("ShaderNodeEmission")
        additive = additive_tree.nodes.new("ShaderNodeAddShader")
        additive_tree.links.new(
            additive_principled.outputs[0],
            additive.inputs[0],
        )
        additive_tree.links.new(
            additive_bsdf.outputs[0],
            additive.inputs[1],
        )
        additive_tree.links.new(
            additive.outputs[0],
            output_node(additive_emission).inputs["Surface"],
        )
        additive_document = material_ir(additive_emission)
        assert [
            term["finalWeight"]["value"]
            for term in additive_document["weightedClosures"]["terms"]
        ] == [1.0, 1.0], additive_document["weightedClosures"]
        assert any(
            item["code"] == "WEIGHT0003"
            for item in additive_document["diagnostics"]
        )
        assert_no_errors(additive_document)

        result = miku_blender.export_material_bundle(
            emission,
            output,
            source_blend_id="closure-aware-blender-smoke",
            persistent_material_id=str(
                uuid.uuid5(
                    uuid.NAMESPACE_URL,
                    "miku:closure-aware:transparent-emission",
                )
            ),
            fidelity_policy="AllowDeclaredApproximation",
        )
        bundle_path = Path(result["bundlePath"])
        assert bundle_path.is_file(), result
        bundle = json.loads(bundle_path.read_text(encoding="utf-8"))
        ir_path = bundle_path.parent / bundle["ir"]["relativePath"]
        exported_ir = json.loads(ir_path.read_text(encoding="utf-8"))
        assert exported_ir["documentKind"] == "miku-material-ir-1.0"
        assert result["bundle"]["documentKind"] == "miku-bundle-1.0"
        assert (
            result["manifest"]["documentKind"]
            == "miku-conversion-manifest-1.0"
        )

        masked_result = miku_blender.export_material_bundle(
            masked_emission,
            output,
            source_blend_id="closure-aware-blender-smoke",
            persistent_material_id=str(
                uuid.uuid5(
                    uuid.NAMESPACE_URL,
                    "miku:closure-aware:emission-mask",
                )
            ),
            fidelity_policy="AllowDeclaredApproximation",
        )
        masked_bundle = masked_result["bundle"]
        assert masked_bundle["documentKind"] == "miku-bundle-1.0"
        assert len(masked_bundle["resources"]) == 1
        assert (
            masked_bundle["resources"][0]["semantic"]
            == "EmissionMask"
        ), masked_bundle["resources"]

    print("MIKU_CLOSURE_SURFACE_SMOKE_OK")


if __name__ == "__main__":
    main()
