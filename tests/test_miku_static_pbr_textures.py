import unittest

import miku_blender
from miku.semantic import build_material_ir


def _socket(name, default=None, value_type="VALUE"):
    return {
        "id": name,
        "name": name,
        "valueType": value_type,
        "default": default,
    }


def _image(node_id, *, color_space, file_format="PNG"):
    return {
        "id": node_id,
        "op": "Texture.Image",
        "inputs": [_socket("Vector", [0.0, 0.0, 0.0], "VECTOR")],
        "outputs": [
            _socket("Color", [0.0, 0.0, 0.0, 1.0], "RGBA"),
            _socket("Alpha", 1.0),
        ],
        "params": {
            "image": {
                "resourceBaseId": node_id + "-resource",
                "source": "FILE",
                "fileFormat": file_format,
                "width": 4,
                "height": 4,
                "channels": 4,
                "colorSpaceName": color_space,
                "packed": False,
            },
            "projection": "FLAT",
            "interpolation": "LINEAR",
            "extension": "REPEAT",
        },
    }


def _pbr_graph(mode):
    images = [
        _image("color", color_space="sRGB", file_format="JPEG"),
        _image("roughness", color_space="Non-Color"),
        _image("metalness", color_space="Non-Color"),
        _image("normal-image", color_space="Non-Color"),
        _image("height", color_space="Non-Color"),
    ]
    normal_map = {
        "id": "normal-map",
        "op": "Vector.NormalMap",
        "inputs": [
            _socket("Strength", 0.75),
            _socket("Color", [0.5, 0.5, 1.0, 1.0], "RGBA"),
        ],
        "outputs": [_socket("Normal", [0.0, 0.0, 1.0], "VECTOR")],
        "params": {"space": "TANGENT", "uvMap": ""},
    }
    displacement = {
        "id": "displacement",
        "op": "Vector.Displacement",
        "inputs": [
            _socket("Height", 0.0),
            _socket("Midlevel", 0.5),
            _socket("Scale", 0.1),
            _socket("Normal", [0.0, 0.0, 1.0], "VECTOR"),
        ],
        "outputs": [_socket("Displacement", [0.0, 0.0, 0.0], "VECTOR")],
        "params": {"space": "OBJECT"},
    }
    bump = {
        "id": "displacement-bump",
        "op": "Vector.DisplacementBump",
        "inputs": [
            _socket("Height", 0.0),
            _socket("Normal", [0.0, 0.0, 1.0], "VECTOR"),
        ],
        "outputs": [_socket("Normal", [0.0, 0.0, 1.0], "VECTOR")],
        "params": {"midlevel": 0.5, "scale": 0.1},
    }
    principled = {
        "id": "principled",
        "op": "Shader.PrincipledBSDF",
        "inputs": [],
        "outputs": [_socket("BSDF", None, "SHADER")],
    }
    output = {
        "id": "output",
        "op": "Output.Material",
        "inputs": [
            _socket("Surface", None, "SHADER"),
            _socket("Displacement", [0.0, 0.0, 0.0], "VECTOR"),
        ],
        "outputs": [],
        "params": {"isActiveOutput": True, "target": "ALL"},
    }
    normal_source = (
        {"node": "displacement-bump", "socket": "Normal"}
        if mode in {"BUMP", "BOTH"}
        else {"node": "normal-map", "socket": "Normal"}
    )
    slots = {
        "BaseColor": {
            "source": {"node": "color", "socket": "Color"},
            "default": None,
        },
        "Roughness": {
            "source": {"node": "roughness", "socket": "Color"},
            "default": None,
        },
        "Metalness": {
            "source": {"node": "metalness", "socket": "Color"},
            "default": None,
        },
        "Normal": {"source": normal_source, "default": None},
    }
    required = ["BaseColor", "Roughness", "Metalness", "Normal"]
    if mode in {"DISPLACEMENT", "BOTH"}:
        slots["Displacement"] = {
            "source": {"node": "displacement", "socket": "Displacement"},
            "default": None,
        }
        required.append("Displacement")
    return {
        "material": {"name": "StaticPbr"},
        "nodes": [
            output,
            principled,
            *images,
            normal_map,
            displacement,
            bump,
        ],
        "edges": [
            {
                "from": {"node": "principled", "socket": "BSDF"},
                "to": {"node": "output", "socket": "Surface"},
            },
            {
                "from": {"node": "normal-image", "socket": "Color"},
                "to": {"node": "normal-map", "socket": "Color"},
            },
            {
                "from": {"node": "normal-map", "socket": "Normal"},
                "to": {"node": "displacement-bump", "socket": "Normal"},
            },
            {
                "from": {"node": "height", "socket": "Color"},
                "to": {"node": "displacement-bump", "socket": "Height"},
            },
            {
                "from": {"node": "height", "socket": "Color"},
                "to": {"node": "displacement", "socket": "Height"},
            },
        ],
        "standardPbrSemantic": {"slots": slots},
        "surfaceSemantic": {
            "model": "StandardLit",
            "renderMethod": "Opaque",
            "renderFace": "Both",
            "coverageChannel": "Alpha",
            "requiredChannels": required,
        },
        "diagnostics": [],
    }


def _packed_pbr_graph():
    graph = _pbr_graph("DISPLACEMENT")
    packed = _image("packed", color_space="Non-Color")
    separate = {
        "id": "separate",
        "op": "Converter.SeparateColor",
        "inputs": [_socket("Color", [0.0, 0.0, 0.0, 1.0], "RGBA")],
        "outputs": [
            _socket("Red", 0.0),
            _socket("Green", 0.0),
            _socket("Blue", 0.0),
            _socket("Alpha", 0.0),
        ],
        "params": {"mode": "RGB"},
    }
    graph["nodes"].extend([packed, separate])
    graph["edges"].append(
        {
            "from": {"node": "packed", "socket": "Color"},
            "to": {"node": "separate", "socket": "Color"},
        }
    )
    slots = graph["standardPbrSemantic"]["slots"]
    slots["Metalness"] = {
        "source": {"node": "separate", "socket": "Red"},
        "default": None,
    }
    slots["Roughness"] = {
        "source": {"node": "separate", "socket": "Green"},
        "default": None,
    }
    slots["AmbientOcclusion"] = {
        "source": {"node": "separate", "socket": "Blue"},
        "default": None,
    }
    slots["Alpha"] = {
        "source": {"node": "separate", "socket": "Alpha"},
        "default": None,
    }
    graph["surfaceSemantic"]["requiredChannels"].extend(
        ["AmbientOcclusion", "Alpha"]
    )
    return graph


class MikuStaticPbrTextureTests(unittest.TestCase):
    def _ir(self, mode):
        return build_material_ir(
            _pbr_graph(mode),
            source_blend_id="blend",
            material_key="StaticPbr",
        )

    def test_bump_blends_height_with_tangent_normal(self):
        ir = self._ir("BUMP")
        operations = {item["op"] for item in ir["expressions"]}
        self.assertIn("Vector.NormalStrength", operations)
        self.assertIn("Vector.NormalFromHeight", operations)
        self.assertIn("Vector.NormalBlend", operations)
        displacement = next(
            item for item in ir["channels"]
            if item["semantic"] == "Displacement"
        )
        self.assertNotEqual(
            "Expression",
            (displacement.get("value") or {}).get("kind"),
        )

    def test_displacement_uses_vertex_lod_zero_and_object_space(self):
        ir = self._ir("DISPLACEMENT")
        displacement = next(
            item for item in ir["channels"]
            if item["semantic"] == "Displacement"
        )
        self.assertEqual("Vertex", displacement["stage"])
        expression = {
            item["id"]: item for item in ir["expressions"]
        }[displacement["value"]["expressionId"]]
        self.assertEqual("Vector.Displacement", expression["op"])
        height = {
            item["id"]: item for item in ir["expressions"]
        }[expression["inputs"]["Height"]["expressionId"]]
        self.assertEqual("Texture.SampleImage2D", height["op"])
        self.assertEqual("Height", height["params"]["semantic"])
        self.assertEqual("Explicit0", height["params"]["lodMode"])
        self.assertEqual("_MIKU_HeightMap", height["params"]["referenceName"])

    def test_both_keeps_fragment_normal_and_vertex_displacement(self):
        ir = self._ir("BOTH")
        channels = {item["semantic"]: item for item in ir["channels"]}
        self.assertEqual("Fragment", channels["Normal"]["stage"])
        self.assertEqual("Vertex", channels["Displacement"]["stage"])
        self.assertTrue(
            ir["surfaceModelPlan"]["features"]["vertexDisplacement"]
        )
        image_expressions = [
            item for item in ir["expressions"]
            if item["op"] == "Texture.SampleImage2D"
        ]
        self.assertTrue(any(
            item["params"]["semantic"] == "BaseColor"
            and item["params"]["colorSpace"] == "sRGB"
            for item in image_expressions
        ))
        self.assertTrue(all(
            item["params"]["colorSpace"] == "Linear"
            for item in image_expressions
            if item["params"]["semantic"] != "BaseColor"
        ))
        self.assertNotIn("Opaque.BlenderNode", {
            item["op"] for item in ir["expressions"]
        })

    def test_data_texture_in_srgb_is_rejected_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        roughness = next(
            node for node in graph["nodes"] if node["id"] == "roughness"
        )
        roughness["params"]["image"]["colorSpaceName"] = "sRGB"
        ir = build_material_ir(graph, material_key="StaticPbr")
        self.assertTrue(any(
            item["code"] == "MIKU_DATA_TEXTURE_COLOR_SPACE_UNSUPPORTED"
            and item["semantic"] == "Roughness"
            for item in ir["diagnostics"]
        ))

    def test_udim_image_source_is_rejected_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        image = next(
            node for node in graph["nodes"] if node["id"] == "height"
        )
        image["params"]["image"]["source"] = "TILED"
        ir = build_material_ir(graph, material_key="StaticPbr")
        self.assertTrue(any(
            item["code"] == "MIKU_IMAGE_SOURCE_UNSUPPORTED"
            for item in ir["diagnostics"]
        ))

    def test_unsupported_image_format_is_rejected_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        image = next(
            node for node in graph["nodes"] if node["id"] == "height"
        )
        image["params"]["image"]["fileFormat"] = "TIFF"
        ir = build_material_ir(graph, material_key="StaticPbr")
        self.assertTrue(any(
            item["code"] == "MIKU_IMAGE_FORMAT_UNSUPPORTED"
            for item in ir["diagnostics"]
        ))

    def test_world_space_displacement_is_rejected_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        node = next(
            item for item in graph["nodes"]
            if item["id"] == "displacement"
        )
        node["params"]["space"] = "WORLD"
        ir = build_material_ir(graph, material_key="StaticPbr")
        self.assertTrue(any(
            item["code"] == "MIKU_DISPLACEMENT_SPACE_UNSUPPORTED"
            for item in ir["diagnostics"]
        ))

    def test_linked_displacement_normal_is_rejected_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        graph["edges"].extend([
            {
                "from": {
                    "node": "displacement",
                    "socket": "Displacement",
                },
                "to": {"node": "output", "socket": "Displacement"},
            },
            {
                "from": {"node": "normal-map", "socket": "Normal"},
                "to": {"node": "displacement", "socket": "Normal"},
            },
        ])
        _, _, diagnostics = miku_blender._principled_slots_from_snapshot(
            graph["nodes"],
            graph["edges"],
            displacement_method="DISPLACEMENT",
        )
        self.assertTrue(any(
            item["code"] == "MIKU_DISPLACEMENT_NORMAL_INPUT_UNSUPPORTED"
            for item in diagnostics
        ))

    def test_dynamic_displacement_scale_is_rejected_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        graph["edges"].extend([
            {
                "from": {
                    "node": "displacement",
                    "socket": "Displacement",
                },
                "to": {"node": "output", "socket": "Displacement"},
            },
            {
                "from": {"node": "roughness", "socket": "Color"},
                "to": {"node": "displacement", "socket": "Scale"},
            },
        ])
        _, _, diagnostics = miku_blender._principled_slots_from_snapshot(
            graph["nodes"],
            graph["edges"],
            displacement_method="DISPLACEMENT",
        )
        self.assertTrue(any(
            item["code"] == "MIKU_DISPLACEMENT_SCALE_DYNAMIC_UNSUPPORTED"
            for item in diagnostics
        ))

    def test_arbitrary_scalar_channels_share_one_physical_resource(self):
        graph = _packed_pbr_graph()
        principled = next(
            item for item in graph["nodes"] if item["id"] == "principled"
        )
        principled["inputs"] = [
            _socket("Base Color", [0.8, 0.8, 0.8, 1.0], "RGBA")
        ]
        graph["edges"].append(
            {
                "from": {"node": "separate", "socket": "Blue"},
                "to": {"node": "principled", "socket": "Base Color"},
            }
        )
        ir = build_material_ir(
            graph,
            source_blend_id="blend",
            material_key="PackedPbr",
        )
        samples = [
            item for item in ir["expressions"]
            if item["op"] == "Texture.SampleImage2D"
            and (item.get("params") or {}).get("resourceId")
            and (item.get("params") or {}).get("semantic")
            in {"Metalness", "Roughness", "AmbientOcclusion", "Alpha"}
        ]
        self.assertEqual(
            {"R", "G", "B", "A"},
            {(item.get("params") or {})["channel"] for item in samples},
        )
        self.assertEqual(
            1,
            len({
                (item.get("params") or {})["resourceId"]
                for item in samples
            }),
        )
        references = {
            (item.get("params") or {})["referenceName"]
            for item in samples
        }
        self.assertEqual(1, len(references))
        self.assertTrue(next(iter(references)).startswith("_MIKU_Packed_"))
        self.assertTrue(all(
            (item.get("params") or {}).get("packed") is True
            for item in samples
        ))
        base_color_samples = [
            item
            for item in ir["expressions"]
            if item["op"] == "Texture.SampleImage2D"
            and (item.get("params") or {}).get("semantic") == "BaseColor"
        ]
        self.assertEqual(1, len(base_color_samples))
        self.assertEqual(
            "color",
            (base_color_samples[0].get("source") or {}).get("nodeId"),
        )

    def test_directx_normal_convention_is_explicit_not_inferred(self):
        graph = _pbr_graph("BUMP")
        graph["normalConvention"] = "TangentDirectXNegativeY"
        ir = build_material_ir(graph, material_key="DirectXNormal")
        normal_sample = next(
            item for item in ir["expressions"]
            if item["op"] == "Texture.SampleImage2D"
            and (item.get("params") or {}).get("semantic") == "Normal"
        )
        self.assertEqual(
            "TangentDirectXNegativeY",
            normal_sample["params"]["normalConvention"],
        )

    def test_explicit_invert_remains_expression_before_roughness_boundary(self):
        graph = _pbr_graph("DISPLACEMENT")
        invert = {
            "id": "invert",
            "op": "Color.Invert",
            "inputs": [
                _socket("Fac", 1.0),
                _socket("Color", [0.0, 0.0, 0.0, 1.0], "RGBA"),
            ],
            "outputs": [_socket("Color", [0.0, 0.0, 0.0, 1.0], "RGBA")],
        }
        graph["nodes"].append(invert)
        graph["edges"].append(
            {
                "from": {"node": "roughness", "socket": "Color"},
                "to": {"node": "invert", "socket": "Color"},
            }
        )
        graph["standardPbrSemantic"]["slots"]["Roughness"]["source"] = {
            "node": "invert",
            "socket": "Color",
        }
        ir = build_material_ir(graph, material_key="ExplicitInvert")
        operations = [item["op"] for item in ir["expressions"]]
        self.assertEqual(1, operations.count("Math.OneMinus"))
        self.assertIn("Math.Lerp", operations)
        roughness = next(
            item for item in ir["channels"]
            if item["semantic"] == "Roughness"
        )
        self.assertEqual("Expression", roughness["value"]["kind"])

    def test_multiply_mix_is_lowered_without_filename_layout_inference(self):
        graph = _pbr_graph("DISPLACEMENT")
        multiply = {
            "id": "ao-multiply",
            "op": "Color.Mix",
            "inputs": [
                _socket("Factor", 0.5),
                _socket("A", [0.8, 0.7, 0.6, 1.0], "RGBA"),
                _socket("B", [0.25, 0.25, 0.25, 1.0], "RGBA"),
            ],
            "outputs": [_socket("Result", [0.0, 0.0, 0.0, 1.0], "RGBA")],
            "params": {"blend_type": "MULTIPLY", "clampFactor": True},
        }
        graph["nodes"].append(multiply)
        graph["edges"].append(
            {
                "from": {"node": "color", "socket": "Color"},
                "to": {"node": "ao-multiply", "socket": "A"},
            }
        )
        graph["standardPbrSemantic"]["slots"]["BaseColor"]["source"] = {
            "node": "ao-multiply",
            "socket": "Result",
        }
        ir = build_material_ir(graph, material_key="Multiply")
        operations = {item["op"] for item in ir["expressions"]}
        self.assertIn("Math.Multiply", operations)
        self.assertIn("Math.Lerp", operations)

    def test_color_and_scalar_use_of_one_resource_fails_explicitly(self):
        graph = _pbr_graph("DISPLACEMENT")
        shared = next(
            node for node in graph["nodes"] if node["id"] == "color"
        )
        shared["params"]["image"]["colorSpaceName"] = "Non-Color"
        graph["standardPbrSemantic"]["slots"]["Roughness"]["source"] = {
            "node": "color",
            "socket": "Color",
        }
        ir = build_material_ir(graph, material_key="ColorScalarConflict")
        self.assertTrue(any(
            item["code"] == "MIKU_PACKED_TEXTURE_COLOR_SPACE_CONFLICT"
            for item in ir["diagnostics"]
        ))


if __name__ == "__main__":
    unittest.main()
