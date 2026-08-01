from __future__ import annotations

import copy
import unittest

import miku_blender
from miku.contracts import DocumentValidationError, make_document, validate_document
from miku.planner import ConversionPlanner
from miku.runtime_math import overlay
from miku.semantic import build_material_ir


def socket(socket_id: str, default=None, value_type: str = "Scalar") -> dict:
    return {
        "id": socket_id,
        "name": socket_id.replace("_", " "),
        "default": default,
        "valueType": value_type,
    }


def endpoint(node: str, socket_id: str) -> dict[str, str]:
    return {"node": node, "socket": socket_id}


def closure_graph(
    *,
    first: str,
    second: str,
    factor_node: dict | None = None,
    factor_socket: str = "Facing",
    glass: bool = False,
) -> dict:
    nodes = [
        {
            "id": "output",
            "op": "Output.Material",
            "params": {"isActiveOutput": True, "target": "EEVEE"},
        },
        {
            "id": "mix",
            "op": "Shader.Mix",
            "inputs": [
                socket("Factor", 0.5),
                socket("Shader", None, "Closure"),
                socket("Shader_001", None, "Closure"),
            ],
        },
        {
            "id": "transparent",
            "op": "Shader.TransparentBSDF",
            "inputs": [socket("Color", [1.0, 1.0, 1.0, 1.0], "Color")],
        },
        {
            "id": "visible",
            "op": "Shader.GlassBSDF" if glass else "Shader.Emission",
            "inputs": (
                [
                    socket("Color", [0.2, 0.6, 0.9, 1.0], "Color"),
                    socket("Roughness", 0.2),
                    socket("IOR", 1.45),
                    socket("Normal", [0.0, 0.0, 1.0], "Float3"),
                    socket("Weight", 1.0),
                    socket("Thin_Film_Thickness", 0.0),
                ]
                if glass
                else [
                    socket("Color", [0.1, 0.4, 1.0, 1.0], "Color"),
                    socket("Strength", 4.0),
                ]
            ),
        },
    ]
    edges = [
        {"from": endpoint(first, "Closure"), "to": endpoint("mix", "Shader")},
        {
            "from": endpoint(second, "Closure"),
            "to": endpoint("mix", "Shader_001"),
        },
        {
            "from": endpoint("mix", "Closure"),
            "to": endpoint("output", "Surface"),
        },
    ]
    if factor_node:
        nodes.append(factor_node)
        edges.append(
            {
                "from": endpoint(factor_node["id"], factor_socket),
                "to": endpoint("mix", "Factor"),
            }
        )
    slots, surface, diagnostics = miku_blender._principled_slots_from_snapshot(
        nodes,
        edges,
    )
    surface.update(
        {
            "renderMethod": "AlphaBlend",
            "renderFace": "Both",
        }
    )
    return {
        "material": {"name": "TransparentFixture"},
        "workflow": {"kind": "standard_pbr"},
        "nodes": nodes,
        "edges": edges,
        "standardPbrSemantic": {"slots": slots},
        "surfaceSemantic": surface,
        "diagnostics": diagnostics,
    }


class TransparentSurfaceTests(unittest.TestCase):
    def test_transparent_emission_uses_independent_coverage_in_both_orders(self):
        layer = {
            "id": "layer",
            "op": "Input.LayerWeight",
            "inputs": [
                socket("Blend", 0.5),
                socket("Normal", [0.0, 0.0, 0.0], "Float3"),
            ],
            "outputs": [socket("Facing", None)],
        }
        for first, second, expected_a, expected_b in (
            ("transparent", "visible", 0.0, 1.0),
            ("visible", "transparent", 1.0, 0.0),
        ):
            with self.subTest(first=first):
                graph = closure_graph(
                    first=first,
                    second=second,
                    factor_node=copy.deepcopy(layer),
                )
                emission_slot = graph["standardPbrSemantic"]["slots"][
                    "Emission"
                ]
                self.assertNotIn("source", emission_slot)
                self.assertEqual(
                    [0.4, 1.6, 4.0, 4.0],
                    emission_slot["default"],
                )
                ir = build_material_ir(graph)
                channels = {item["semantic"]: item for item in ir["channels"]}
                self.assertFalse(channels["BaseColor"]["required"])
                self.assertTrue(channels["Emission"]["required"])
                self.assertNotIn("requiresBake", channels["Emission"])
                self.assertEqual(
                    [0.4, 1.6, 4.0, 4.0],
                    channels["Emission"]["default"],
                )
                self.assertEqual("Expression", channels["Alpha"]["value"]["kind"])
                alpha_id = channels["Alpha"]["value"]["expressionId"]
                alpha = next(item for item in ir["expressions"] if item["id"] == alpha_id)
                self.assertEqual("Math.Lerp", alpha["op"])
                constants = {
                    item["id"]: item["params"].get("value")
                    for item in ir["expressions"]
                    if item["op"] == "Constant"
                }
                self.assertEqual(expected_a, constants[alpha["inputs"]["A"]["expressionId"]])
                self.assertEqual(expected_b, constants[alpha["inputs"]["B"]["expressionId"]])
                self.assertNotEqual(
                    channels["Emission"].get("value"),
                    channels["Alpha"].get("value"),
                )
                plan = ConversionPlanner().plan(ir)
                self.assertFalse(
                    any(
                        item.get("scope") == "Channels"
                        and "Emission" in item.get("semantics", [])
                        for item in plan["bakeJobs"]
                    ),
                    plan["bakeJobs"],
                )

    def test_facing_transparent_glass_preserves_optical_channels(self):
        layer = {
            "id": "layer",
            "op": "Input.LayerWeight",
            "inputs": [
                socket("Blend", 0.5),
                socket("Normal", [0.0, 0.0, 0.0], "Float3"),
            ],
            "outputs": [socket("Facing", None)],
        }
        ir = build_material_ir(
            closure_graph(
                first="transparent",
                second="visible",
                factor_node=layer,
                glass=True,
            )
        )
        contract = ir["surfaceContract"]
        self.assertEqual("DielectricScreenRefraction", contract["model"])
        self.assertEqual("TransmissionColor", contract["transmissionColorChannel"])
        channels = {item["semantic"]: item for item in ir["channels"]}
        self.assertEqual([0.2, 0.6, 0.9, 1.0], channels["TransmissionColor"]["default"])
        self.assertEqual(1.45, channels["IOR"]["default"])
        self.assertEqual(0.2, channels["Roughness"]["default"])
        validate_document(ir)

    def test_unavailable_glass_weight_uses_one_and_direct_glass_is_native(self):
        nodes = [
            {
                "id": "output",
                "op": "Output.Material",
                "params": {"isActiveOutput": True, "target": "EEVEE"},
            },
            {
                "id": "glass",
                "op": "Shader.GlassBSDF",
                "inputs": [
                    socket("Color", [0.2, 0.6, 0.9, 1.0], "Color"),
                    socket("Roughness", 0.2),
                    socket("IOR", 1.5),
                    socket("Normal", [0.0, 0.0, 1.0], "Float3"),
                    {
                        **socket("Weight", 0.0),
                        "enabled": False,
                        "isUnavailable": True,
                    },
                    socket("Thin_Film_Thickness", 0.0),
                ],
            },
        ]
        edges = [
            {
                "from": endpoint("glass", "Closure"),
                "to": endpoint("output", "Surface"),
            }
        ]
        slots, surface, diagnostics = (
            miku_blender._principled_slots_from_snapshot(nodes, edges)
        )
        self.assertEqual(1.0, slots["TransmissionWeight"]["default"])
        self.assertFalse(
            any(item.get("severity") == "error" for item in diagnostics)
        )
        surface.update(
            {
                "renderMethod": "AlphaBlend",
                "renderFace": "Both",
            }
        )
        ir = build_material_ir(
            {
                "material": {"name": "DirectGlass"},
                "workflow": {"kind": "standard_pbr"},
                "nodes": nodes,
                "edges": edges,
                "standardPbrSemantic": {"slots": slots},
                "surfaceSemantic": surface,
                "diagnostics": diagnostics,
            }
        )
        channels = {item["semantic"]: item for item in ir["channels"]}
        self.assertEqual(1.0, channels["TransmissionWeight"]["default"])
        glass_region = next(
            item for item in ir["regions"] if item["kind"] == "GlassClosure"
        )
        planned = next(
            item
            for item in ConversionPlanner().plan(ir)["regions"]
            if item["regionId"] == glass_region["id"]
        )
        self.assertEqual("Native", planned["route"])
        self.assertEqual("Approximate", planned["fidelity"])

    def test_active_glass_weight_link_is_preserved(self):
        node = {
            "id": "glass",
            "inputs": [
                {
                    **socket("Weight", 0.0),
                    "enabled": True,
                    "isUnavailable": False,
                }
            ],
        }
        source = endpoint("weight", "Value")
        result = miku_blender._snapshot_input(
            node,
            {("glass", "weight"): source},
            ("Weight",),
            1.0,
        )
        self.assertEqual(source, result["source"])

    def test_typed_mix_socket_ignores_inactive_float_variant(self):
        node = {
            "id": "typed-mix",
            "inputs": [
                {
                    **socket("A_Float", 0.0),
                    "name": "A",
                    "enabled": False,
                    "isUnavailable": True,
                },
                {
                    **socket("A_Color", [0.2, 0.4, 0.8, 1.0], "Color"),
                    "name": "A",
                    "enabled": True,
                    "isUnavailable": False,
                },
            ],
        }
        source = endpoint("color-source", "Color")
        result = miku_blender._snapshot_input(
            node,
            {("typed-mix", "acolor"): source},
            ("A",),
            [0.0, 0.0, 0.0, 1.0],
            "Color",
        )
        self.assertEqual(source, result["source"])

    def test_ambiguous_active_typed_socket_fails_explicitly(self):
        node = {
            "id": "ambiguous",
            "inputs": [
                {
                    **socket("A_Color", [0.0, 0.0, 0.0, 1.0], "Color"),
                    "name": "A",
                },
                {
                    **socket("A_RGBA", [1.0, 1.0, 1.0, 1.0], "Color"),
                    "name": "A",
                },
            ],
        }
        with self.assertRaisesRegex(
            RuntimeError,
            "^MIKU_SOCKET_AMBIGUOUS:ambiguous:A:Color$",
        ):
            miku_blender._snapshot_input(
                node,
                {},
                ("A",),
                [0.0, 0.0, 0.0, 1.0],
                "Color",
            )

    def test_principled_emission_color_is_scaled_by_strength(self):
        for strength, expected in (
            (0.0, [0.0, 0.0, 0.0, 0.0]),
            (1.0, [0.2, 0.4, 0.8, 1.0]),
            (12.8, [2.56, 5.12, 10.24, 12.8]),
        ):
            with self.subTest(strength=strength):
                nodes = [
                    {
                        "id": "output",
                        "op": "Output.Material",
                        "params": {
                            "isActiveOutput": True,
                            "target": "EEVEE",
                        },
                    },
                    {
                        "id": "principled",
                        "op": "Shader.PrincipledBSDF",
                        "inputs": [
                            socket(
                                "Emission_Color",
                                [0.2, 0.4, 0.8, 1.0],
                                "Color",
                            ),
                            socket("Emission_Strength", strength),
                        ],
                    },
                ]
                slots, _, diagnostics = (
                    miku_blender._principled_slots_from_snapshot(
                        nodes,
                        [
                            {
                                "from": endpoint(
                                    "principled",
                                    "Closure",
                                ),
                                "to": endpoint("output", "Surface"),
                            }
                        ],
                    )
                )
                self.assertFalse(
                    any(
                        item.get("severity") == "error"
                        for item in diagnostics
                    )
                )
                self.assertEqual(
                    len(expected),
                    len(slots["Emission"]["default"]),
                )
                for actual_component, expected_component in zip(
                    slots["Emission"]["default"],
                    expected,
                ):
                    self.assertAlmostEqual(
                        expected_component,
                        actual_component,
                    )

    def test_principled_dynamic_emission_strength_is_multiplied_once(self):
        nodes = [
            {
                "id": "output",
                "op": "Output.Material",
                "params": {"isActiveOutput": True, "target": "EEVEE"},
            },
            {
                "id": "principled",
                "op": "Shader.PrincipledBSDF",
                "inputs": [
                    socket(
                        "Emission_Color",
                        [0.1, 0.4, 1.0, 1.0],
                        "Color",
                    ),
                    socket("Emission_Strength", 1.0),
                ],
            },
            {
                "id": "time",
                "op": "Input.Time",
                "outputs": [socket("Seconds", None)],
            },
        ]
        edges = [
            {
                "from": endpoint("principled", "Closure"),
                "to": endpoint("output", "Surface"),
            },
            {
                "from": endpoint("time", "Seconds"),
                "to": endpoint("principled", "Emission_Strength"),
            },
        ]
        slots, surface, diagnostics = (
            miku_blender._principled_slots_from_snapshot(nodes, edges)
        )
        ir = build_material_ir(
            {
                "material": {"name": "PrincipledRuntimeEmission"},
                "workflow": {"kind": "standard_pbr"},
                "nodes": nodes,
                "edges": edges,
                "standardPbrSemantic": {"slots": slots},
                "surfaceSemantic": surface,
                "diagnostics": diagnostics,
            }
        )
        emission = next(
            item for item in ir["channels"] if item["semantic"] == "Emission"
        )
        expression = next(
            item
            for item in ir["expressions"]
            if item["id"] == emission["value"]["expressionId"]
        )
        self.assertEqual("Math.Multiply", expression["op"])
        self.assertEqual(
            1,
            sum(
                item["op"] == "Math.Multiply"
                for item in ir["expressions"]
                if item.get("source", {}).get("nodeId")
                == expression.get("source", {}).get("nodeId")
            ),
        )

    def test_linked_runtime_emission_strength_remains_an_expression(self):
        nodes = [
            {
                "id": "output",
                "op": "Output.Material",
                "params": {"isActiveOutput": True, "target": "EEVEE"},
            },
            {
                "id": "mix",
                "op": "Shader.Mix",
                "inputs": [
                    socket("Factor", 0.5),
                    socket("Shader", None, "Closure"),
                    socket("Shader_001", None, "Closure"),
                ],
            },
            {
                "id": "transparent",
                "op": "Shader.TransparentBSDF",
                "inputs": [
                    socket("Color", [1.0, 1.0, 1.0, 1.0], "Color")
                ],
            },
            {
                "id": "emission",
                "op": "Shader.Emission",
                "inputs": [
                    socket("Color", [0.1, 0.4, 1.0, 1.0], "Color"),
                    socket("Strength", 1.0),
                ],
            },
            {
                "id": "time",
                "op": "Input.Time",
                "outputs": [socket("Seconds", None)],
            },
        ]
        edges = [
            {
                "from": endpoint("transparent", "Closure"),
                "to": endpoint("mix", "Shader"),
            },
            {
                "from": endpoint("emission", "Closure"),
                "to": endpoint("mix", "Shader_001"),
            },
            {
                "from": endpoint("mix", "Closure"),
                "to": endpoint("output", "Surface"),
            },
            {
                "from": endpoint("time", "Seconds"),
                "to": endpoint("emission", "Strength"),
            },
        ]
        slots, surface, diagnostics = (
            miku_blender._principled_slots_from_snapshot(nodes, edges)
        )
        surface.update(
            {"renderMethod": "AlphaBlend", "renderFace": "Both"}
        )
        ir = build_material_ir(
            {
                "material": {"name": "RuntimeEmission"},
                "workflow": {"kind": "standard_pbr"},
                "nodes": nodes,
                "edges": edges,
                "standardPbrSemantic": {"slots": slots},
                "surfaceSemantic": surface,
                "diagnostics": diagnostics,
            }
        )
        emission = next(
            item for item in ir["channels"] if item["semantic"] == "Emission"
        )
        self.assertEqual("Expression", emission["value"]["kind"])
        expression = next(
            item
            for item in ir["expressions"]
            if item["id"] == emission["value"]["expressionId"]
        )
        self.assertEqual("Math.Multiply", expression["op"])
        self.assertNotIn("requiresBake", emission)

    def test_required_light_path_keeps_specific_diagnostic(self):
        light_path = {
            "id": "light-path",
            "op": "Input.LightPath",
            "outputs": [socket("Transparent Depth", None)],
        }
        ir = build_material_ir(
            closure_graph(
                first="transparent",
                second="visible",
                factor_node=light_path,
                factor_socket="Transparent Depth",
            )
        )
        error = next(
            item
            for item in ir["diagnostics"]
            if item.get("severity") == "error"
        )
        self.assertEqual("MIKU_LIGHT_PATH_UNSUPPORTED", error["code"])
        self.assertEqual(
            "MIKU_LIGHT_PATH_UNSUPPORTED:Transparent Depth",
            error["message"],
        )

    def test_bump_displacement_is_fragment_normal_from_height(self):
        graph = {
            "material": {"name": "BumpFixture"},
            "workflow": {"kind": "standard_pbr"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
                {
                    "id": "displacement",
                    "op": "Vector.Displacement",
                    "inputs": [
                        socket("Height", 0.0),
                        socket("Midlevel", 0.5),
                        socket("Scale", 1.0),
                    ],
                    "outputs": [socket("Displacement", None, "Float3")],
                },
                {
                    "id": "noise",
                    "op": "Texture.Noise",
                    "outputs": [socket("Fac", None)],
                    "source": {
                        "stableId": "noise",
                        "blenderNodeName": "Noise Texture",
                    },
                },
            ],
            "edges": [
                {
                    "from": endpoint("surface", "Closure"),
                    "to": endpoint("out", "Surface"),
                },
                {
                    "from": endpoint("noise", "Fac"),
                    "to": endpoint("displacement", "Height"),
                },
            ],
            "standardPbrSemantic": {
                "slots": {
                    "Normal": {
                        "default": None,
                        "source": endpoint("displacement", "Displacement"),
                    }
                }
            },
            "surfaceSemantic": {
                "model": "StandardLit",
                "renderMethod": "Opaque",
                "renderFace": "Both",
                "coverageChannel": "Alpha",
                "requiredChannels": ["Normal", "Alpha"],
            },
        }
        ir = build_material_ir(graph)
        normal = next(item for item in ir["channels"] if item["semantic"] == "Normal")
        expression = next(
            item
            for item in ir["expressions"]
            if item["id"] == normal["value"]["expressionId"]
        )
        self.assertEqual("Vector.NormalFromHeight", expression["op"])
        self.assertEqual("Fragment", expression["stage"])
        self.assertTrue(
            any(
                item["op"] == "Texture.Noise.Factor"
                for item in ir["expressions"]
            )
        )

    def test_unknown_surface_schema_and_reference_are_rejected(self):
        ir = build_material_ir(
            closure_graph(first="transparent", second="visible")
        )
        payload = {
            key: copy.deepcopy(value)
            for key, value in ir.items()
            if key
            not in {
                "documentKind",
                "schemaVersion",
                "toolVersion",
                "id",
                "canonicalHash",
            }
        }
        payload["surfaceContract"]["schema"] = "miku-surface-9.0"
        invalid = make_document(
            "miku-material-ir-1.0",
            payload,
            document_id=ir["id"],
        )
        with self.assertRaises(DocumentValidationError) as raised:
            validate_document(invalid)
        self.assertEqual("MIKU_SURFACE_SCHEMA_UNKNOWN", raised.exception.code)

        payload["surfaceContract"]["schema"] = "miku-surface-1.0"
        payload["surfaceContract"]["coverageChannel"] = "Missing"
        invalid = make_document(
            "miku-material-ir-1.0",
            payload,
            document_id=ir["id"],
        )
        with self.assertRaises(DocumentValidationError) as raised:
            validate_document(invalid)
        self.assertEqual(
            "MIKU_SURFACE_CHANNEL_REFERENCE_MISSING",
            raised.exception.code,
        )

    def test_overlay_reference_formula_matches_blender_piecewise_behavior(self):
        self.assertAlmostEqual(0.4, overlay(0.25, 0.8), places=7)
        self.assertAlmostEqual(0.6, overlay(0.75, 0.2), places=7)
        self.assertAlmostEqual(0.325, overlay(0.25, 0.8, 0.5), places=7)
        self.assertAlmostEqual(0.675, overlay(0.75, 0.2, 0.5), places=7)


if __name__ == "__main__":
    unittest.main()
