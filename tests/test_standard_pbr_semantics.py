"""Unit tests for :mod:`miku.standard_pbr_semantics`.

These tests were recovered from the retired B2U test suite and reworked
to use the Miku 1.0 fixture helpers in
:mod:`tests._miku_pbr_test_fixtures`.  They exercise the
socket-aware extraction path that the simplified
``_principled_slots_from_snapshot`` closure pass cannot.
"""

from __future__ import annotations

import unittest

from miku.standard_pbr_semantics import extract_standard_pbr_semantic
from miku.standard_pbr_texture_semantics import (
    infer_standard_pbr_texture_semantic,
)
from tests._miku_pbr_test_fixtures import (
    Image,
    Material,
    Node,
    NodeTree,
    Socket,
    build_graph_dict,
)


def image_node(name, filepath=None):
    image = Image(filepath=filepath or f"//Textures/{name}", name=name)
    return Node(
        name,
        "ShaderNodeTexImage",
        inputs=[Socket("Vector", "VECTOR")],
        outputs=[Socket("Color", "RGBA"), Socket("Alpha", "VALUE")],
        image=image,
    )


def principled_node(inputs=None):
    return Node(
        "Principled",
        "ShaderNodeBsdfPrincipled",
        inputs=inputs
        or [
            Socket("Base Color", "RGBA", [1.0, 1.0, 1.0, 1.0]),
            Socket("Metallic", "VALUE", 0.0),
            Socket("Roughness", "VALUE", 0.5),
            Socket("Alpha", "VALUE", 1.0),
            Socket("Normal", "VECTOR"),
            Socket("Emission Color", "RGBA", [0.0, 0.0, 0.0, 1.0]),
            Socket("Emission Strength", "VALUE", 0.0),
            Socket("Specular IOR Level", "VALUE", 0.5),
        ],
        outputs=[Socket("BSDF", "SHADER")],
    )


def material_output():
    return Node(
        "Output",
        "ShaderNodeOutputMaterial",
        inputs=[Socket("Surface", "SHADER"), Socket("Displacement", "VECTOR")],
        is_active_output=True,
        target="ALL",
    )


def slot(graph, semantic):
    return graph["standardPbrSemantic"]["slots"][semantic]


def run_extractor(material):
    graph = build_graph_dict(material)
    result = extract_standard_pbr_semantic(graph)
    graph["standardPbrSemantic"] = result
    return graph


class StandardPbrSemanticTests(unittest.TestCase):
    def test_standard_pbr_dictionary_covers_common_english_and_chinese_terms(self):
        cases = {
            "brick_diffuse.png": "BaseColor",
            "角色_环境光遮蔽.png": "AmbientOcclusion",
            "metallic_mask.png": "Metalness",
            "roughness.jpg": "Roughness",
            "normalmap.tga": "Normal",
            "高度图.png": "Height",
            "disp.exr": "Displacement",
            "specular_color.png": "Specular",
            "glossiness.png": "Glossiness",
            "emissive.png": "Emission",
            "透明图.png": "Alpha",
        }
        for name, expected in cases.items():
            with self.subTest(name=name):
                self.assertEqual(
                    expected, infer_standard_pbr_texture_semantic(name)["semantic"]
                )

    def test_socket_semantic_overrides_misleading_image_name_for_base_color(self):
        tex = image_node("Concrete_Roughness.png")
        bsdf = principled_node()
        output = material_output()
        tree = NodeTree([tex, bsdf, output])
        tree.link(tex, 0, bsdf, 0)
        tree.link(bsdf, 0, output, 0)

        graph = run_extractor(Material("SocketWins", tree))

        self.assertIn("standardPbrSemantic", graph)
        self.assertIn("BaseColor", graph["standardPbrSemantic"]["slots"])
        self.assertFalse(
            graph["standardPbrSemantic"]["slots"].get("Roughness", {}).get("texture")
        )
        self.assertEqual("sRGB", slot(graph, "BaseColor")["colorSpace"])
        self.assertEqual("RGBA", slot(graph, "BaseColor")["channel"])
        self.assertIn("PrincipledBSDF.Base Color", slot(graph, "BaseColor")["socketPath"])
        self.assertTrue(
            any(
                item["code"] == "standard_pbr_socket_semantic"
                for item in graph["standardPbrSemantic"]["diagnostics"]
            )
        )

    def test_principled_metalness_roughness_and_specular_slots(self):
        metal = image_node("metallic_mask.png")
        rough = image_node("roughness.png")
        spec = image_node("specular.png")
        bsdf = principled_node()
        output = material_output()
        tree = NodeTree([metal, rough, spec, bsdf, output])
        tree.link(metal, 0, bsdf, 1)
        tree.link(rough, 0, bsdf, 2)
        tree.link(spec, 0, bsdf, 7)
        tree.link(bsdf, 0, output, 0)

        graph = run_extractor(Material("PBRMaps", tree))

        self.assertEqual("Linear", slot(graph, "Metalness")["colorSpace"])
        self.assertEqual("R", slot(graph, "Metalness")["channel"])
        self.assertEqual("Linear", slot(graph, "Roughness")["colorSpace"])
        self.assertTrue(slot(graph, "Roughness")["convertToSmoothness"])
        self.assertEqual("Metallic", graph["standardPbrSemantic"]["workflow"])
        self.assertEqual("sRGB", slot(graph, "Specular")["colorSpace"])
        self.assertTrue(
            any(
                item["code"] == "standard_pbr_workflow_conflict"
                for item in graph["standardPbrSemantic"]["diagnostics"]
            )
        )

    def test_normal_and_bump_can_coexist_without_overwrite(self):
        normal_tex = image_node("body_normal.png")
        bump_tex = image_node("body_bump.png")
        normal = Node(
            "Normal Map",
            "ShaderNodeNormalMap",
            inputs=[Socket("Strength", "VALUE", 0.75), Socket("Color", "RGBA")],
            outputs=[Socket("Normal", "VECTOR")],
            space="TANGENT",
        )
        bump = Node(
            "Bump",
            "ShaderNodeBump",
            inputs=[
                Socket("Strength", "VALUE", 0.2),
                Socket("Distance", "VALUE", 1.5),
                Socket("Height", "VALUE"),
            ],
            outputs=[Socket("Normal", "VECTOR")],
        )
        bsdf = principled_node()
        output = material_output()
        tree = NodeTree([normal_tex, bump_tex, normal, bump, bsdf, output])
        tree.link(normal_tex, 0, normal, 1)
        tree.link(normal, 0, bsdf, 4)
        tree.link(bump_tex, 0, bump, 2)
        tree.link(bump, 0, bsdf, 4)
        tree.link(bsdf, 0, output, 0)

        graph = run_extractor(Material("NormalAndBump", tree))

        self.assertIn("Normal", graph["standardPbrSemantic"]["slots"])
        self.assertIn("Bump", graph["standardPbrSemantic"]["slots"])
        self.assertEqual(0.75, slot(graph, "Normal")["strength"])
        self.assertEqual("Tangent", slot(graph, "Normal")["space"])
        self.assertEqual(0.2, slot(graph, "Bump")["strength"])
        self.assertEqual(1.5, slot(graph, "Bump")["distance"])
        self.assertTrue(
            any(
                item["code"] == "standard_pbr_bump_approximation"
                for item in graph["standardPbrSemantic"]["diagnostics"]
            )
        )

    def test_loose_semantic_image_by_name_is_exported_with_lower_confidence(self):
        ao = image_node("wall_AO.png")
        color = image_node("wall_basecolor.png")
        bsdf = principled_node()
        output = material_output()
        tree = NodeTree([ao, color, bsdf, output])
        tree.link(color, 0, bsdf, 0)
        tree.link(bsdf, 0, output, 0)

        graph = run_extractor(Material("LooseAO", tree))

        self.assertIn("AmbientOcclusion", graph["standardPbrSemantic"]["slots"])
        self.assertEqual("loose_name", slot(graph, "AmbientOcclusion")["source"])
        self.assertLess(
            slot(graph, "AmbientOcclusion")["confidence"],
            slot(graph, "BaseColor")["confidence"],
        )
        self.assertTrue(
            any(
                item["code"] == "standard_pbr_loose_semantic_texture"
                for item in graph["standardPbrSemantic"]["diagnostics"]
            )
        )

    def test_separate_color_channel_mapping_beats_file_name(self):
        orm = image_node("packed_ORM.png")
        separate = Node(
            "Separate Color",
            "ShaderNodeSeparateColor",
            inputs=[Socket("Color", "RGBA")],
            outputs=[
                Socket("Red", "VALUE"),
                Socket("Green", "VALUE"),
                Socket("Blue", "VALUE"),
                Socket("Alpha", "VALUE"),
            ],
        )
        bsdf = principled_node()
        output = material_output()
        tree = NodeTree([orm, separate, bsdf, output])
        tree.link(orm, 0, separate, 0)
        tree.link(separate, 1, bsdf, 2)
        tree.link(bsdf, 0, output, 0)

        graph = run_extractor(Material("PackedORM", tree))

        self.assertEqual("G", slot(graph, "Roughness")["channel"])
        self.assertIn("packedTextures", graph["standardPbrSemantic"])
        self.assertIn("ORM", graph["standardPbrSemantic"]["packedTextures"])


if __name__ == "__main__":
    unittest.main()
