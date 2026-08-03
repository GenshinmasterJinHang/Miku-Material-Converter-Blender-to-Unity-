from __future__ import annotations

import copy
import math
import unittest

import miku_blender
from miku.contracts import DocumentValidationError, make_document, validate_document
from miku.planner import ConversionPlanner
from miku.runtime_math import (
    dielectric_fresnel,
    hue_saturation_value,
    layer_weight,
    two_element_bspline,
)
from miku.semantic import build_material_ir, build_source_map
from miku.time_driver import TimeDriverError, parse_affine_frame


def source(node: str, socket: str) -> dict[str, str]:
    return {"node": node, "socket": socket}


def base_graph(
    dynamic_node: dict,
    dynamic_socket: str,
    *,
    semantic: str = "BaseColor",
) -> dict:
    return {
        "material": {"name": "RuntimeFixture"},
        "workflow": {"kind": "standard_pbr"},
        "nodes": [
            {"id": "out", "op": "Output.Material"},
            {
                "id": "surface",
                "op": "Shader.PrincipledBSDF",
                "inputs": [],
            },
            dynamic_node,
        ],
        "edges": [
            {
                "from": source(dynamic_node["id"], dynamic_socket),
                "to": source("surface", semantic),
            },
            {
                "from": source("surface", "Closure"),
                "to": source("out", "Surface"),
            },
        ],
        "standardPbrSemantic": {
            "slots": {
                semantic: {
                    "default": None,
                    "source": source(dynamic_node["id"], dynamic_socket),
                }
            }
        },
    }


def portable_uv_voronoi_graph(*, explicit_uv: bool = True) -> dict:
    graph = {
        "material": {"name": "PortableUvVoronoi"},
        "workflow": {"kind": "standard_pbr"},
        "nodes": [
            {"id": "out", "op": "Output.Material"},
            {
                "id": "surface",
                "op": "Shader.PrincipledBSDF",
                "inputs": [],
                "outputs": [{"id": "Closure", "valueType": "Closure"}],
            },
            {
                "id": "uv",
                "op": "Input.TextureCoordinate",
                "outputs": [
                    {
                        "id": "UV",
                        "name": "UV",
                        "valueType": "Float3",
                        "space": "UV0",
                    }
                ],
            },
            {
                "id": "voronoi",
                "op": "Texture.Voronoi",
                "inputs": [{"id": "Vector", "valueType": "Float3"}],
                "outputs": [{"id": "Color", "valueType": "Color"}],
                "source": {
                    "stableId": "voronoi",
                    "blenderNodeName": "Voronoi Texture",
                    "groupPath": ["Material"],
                },
            },
        ],
        "edges": [
            {
                "from": source("voronoi", "Color"),
                "to": source("surface", "BaseColor"),
            },
            {
                "from": source("surface", "Closure"),
                "to": source("out", "Surface"),
            },
        ],
        "standardPbrSemantic": {
            "slots": {
                "BaseColor": {
                    "default": None,
                    "source": source("voronoi", "Color"),
                }
            }
        },
    }
    if explicit_uv:
        graph["edges"].insert(
            0,
            {
                "from": source("uv", "UV"),
                "to": source("voronoi", "Vector"),
            },
        )
    return graph


def bump_normal_graph(*, nested: bool = False, runtime_height: bool = False) -> dict:
    surface_inputs = [
        {
            "id": "Base Color",
            "name": "Base Color",
            "valueType": "Color",
            "default": [0.8, 0.8, 0.8, 1.0],
        },
        {
            "id": "Metallic",
            "name": "Metallic",
            "valueType": "Scalar",
            "default": 0.0,
        },
        {
            "id": "Roughness",
            "name": "Roughness",
            "valueType": "Scalar",
            "default": 0.5,
        },
        {
            "id": "Normal",
            "name": "Normal",
            "valueType": "Vector3",
            "default": [0.0, 0.0, 0.0],
        },
        {
            "id": "Alpha",
            "name": "Alpha",
            "valueType": "Scalar",
            "default": 1.0,
        },
    ]

    def bump_node(node_id: str) -> dict:
        return {
            "id": node_id,
            "op": "Vector.Bump",
            "params": {"invert": False},
            "inputs": [
                {"id": "Strength", "valueType": "Scalar", "default": 0.2},
                {"id": "Distance", "valueType": "Scalar", "default": 1.0},
                {"id": "Filter Width", "valueType": "Scalar", "default": 1.0},
                {"id": "Height", "valueType": "Scalar", "default": 1.0},
                {
                    "id": "Normal",
                    "valueType": "Vector3",
                    "default": [0.0, 0.0, 0.0],
                },
            ],
            "outputs": [
                {"id": "Normal", "valueType": "Vector3", "space": "Tangent"}
            ],
            "source": {
                "stableId": node_id,
                "blenderNodeName": node_id,
                "groupPath": ["Material", "Group"],
            },
        }

    nodes = [
        {"id": "out", "op": "Output.Material"},
        {
            "id": "surface",
            "op": "Shader.PrincipledBSDF",
            "inputs": surface_inputs,
            "outputs": [{"id": "Closure", "valueType": "Closure"}],
        },
        bump_node("bump"),
    ]
    edges = [
        {"from": source("bump", "Normal"), "to": source("surface", "Normal")},
        {"from": source("surface", "Closure"), "to": source("out", "Surface")},
    ]
    if nested:
        nodes.append(bump_node("inner-bump"))
        nodes.append(
            {
                "id": "height",
                "op": "Texture.Noise",
                "outputs": [{"id": "Fac", "valueType": "Scalar"}],
                "source": {
                    "stableId": "height",
                    "blenderNodeName": "Noise Texture",
                    "groupPath": ["Material", "Group"],
                },
            }
        )
        edges.extend(
            [
                {
                    "from": source("height", "Fac"),
                    "to": source("inner-bump", "Height"),
                },
                {
                    "from": source("inner-bump", "Normal"),
                    "to": source("bump", "Normal"),
                },
            ]
        )
    elif runtime_height:
        nodes.append(
            {
                "id": "time",
                "op": "Input.Time",
                "params": {"contract": "miku_time_v1"},
                "outputs": [{"id": "Seconds", "valueType": "Scalar"}],
            }
        )
        edges.append(
            {
                "from": source("time", "Seconds"),
                "to": source("bump", "Height"),
            }
        )
    else:
        nodes.append(
            {
                "id": "height",
                "op": "Texture.Noise",
                "outputs": [{"id": "Fac", "valueType": "Scalar"}],
                "source": {
                    "stableId": "height",
                    "blenderNodeName": "Noise Texture",
                    "groupPath": ["Material", "Group"],
                },
            }
        )
        edges.append(
            {"from": source("height", "Fac"), "to": source("bump", "Height")}
        )
    return {
        "material": {"name": "BumpNormalFixture"},
        "workflow": {"kind": "standard_pbr"},
        "nodes": nodes,
        "edges": edges,
        "standardPbrSemantic": {
            "slots": {
                "BaseColor": {"default": [0.8, 0.8, 0.8, 1.0]},
                "Metalness": {"default": 0.0},
                "Roughness": {"default": 0.5},
                "Normal": {
                    "default": None,
                    "source": source("bump", "Normal"),
                },
                "Alpha": {"default": 1.0},
            }
        },
        "surfaceSemantic": {
            "model": "StandardLit",
            "renderMethod": "Opaque",
            "renderFace": "Front",
            "requiredChannels": [
                "BaseColor",
                "Metalness",
                "Roughness",
                "Normal",
                "Alpha",
            ],
        },
    }


class RuntimeExpressionTests(unittest.TestCase):
    def test_unlinked_zero_normal_is_canonicalized_without_touching_real_normals(self):
        graph = {
            "material": {"name": "NormalDefaults"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
            ],
            "edges": [
                {
                    "from": source("surface", "Closure"),
                    "to": source("out", "Surface"),
                }
            ],
            "standardPbrSemantic": {
                "slots": {
                    "BaseColor": {
                        "default": [0.8, 0.8, 0.8, 1.0],
                        "source": None,
                    },
                    "Metalness": {"default": 0.0, "source": None},
                    "Roughness": {"default": 0.5, "source": None},
                    "Normal": {
                        "default": [0.0, 0.0, 0.0],
                        "source": None,
                    },
                    "Alpha": {"default": 1.0, "source": None},
                }
            },
        }

        normalized = build_material_ir(graph)
        normal = next(
            item
            for item in normalized["channels"]
            if item["semantic"] == "Normal"
        )
        self.assertEqual([0.0, 0.0, 1.0], normal["default"])
        finalized = miku_blender._apply_channel_values(normalized, [])
        self.assertEqual(
            "miku-material-ir-2.0",
            finalized["documentKind"],
        )
        finalized_normal = next(
            item
            for item in finalized["channels"]
            if item["semantic"] == "Normal"
        )
        self.assertEqual(
            {
                "kind": "Constant",
                "value": [0.0, 0.0, 1.0],
            },
            finalized_normal["value"],
        )

        nonzero_graph = copy.deepcopy(graph)
        nonzero_graph["standardPbrSemantic"]["slots"]["Normal"]["default"] = [
            0.25,
            -0.5,
            0.75,
        ]
        nonzero = build_material_ir(nonzero_graph)
        nonzero_normal = next(
            item
            for item in nonzero["channels"]
            if item["semantic"] == "Normal"
        )
        self.assertEqual([0.25, -0.5, 0.75], nonzero_normal["default"])

        linked_graph = base_graph(
            {
                "id": "geometry",
                "op": "Input.Geometry",
                "outputs": [
                    {
                        "id": "Normal",
                        "name": "Normal",
                        "valueType": "Float3",
                        "space": "World",
                        "stage": "Fragment",
                        "uniformity": "Varying",
                    }
                ],
            },
            "Normal",
            semantic="Normal",
        )
        linked_graph["standardPbrSemantic"]["slots"]["Normal"]["default"] = [
            0.0,
            0.0,
            0.0,
        ]
        linked = build_material_ir(
            linked_graph,
            conversion_mode="AllowMeshBake",
        )
        linked_normal = next(
            item
            for item in linked["channels"]
            if item["semantic"] == "Normal"
        )
        self.assertEqual([0.0, 0.0, 0.0], linked_normal["default"])
        self.assertTrue(linked_normal["requiresBake"])

    def test_baked_resource_validation_is_deferred_until_worker_merge(self):
        ir = build_material_ir(bump_normal_graph())
        baked_expression = {
            "id": "deferred-baked-resource",
            "op": "Texture.SampleBaked2D",
            "valueType": "Scalar",
            "space": "UV0",
            "stage": "Fragment",
            "uniformity": "Varying",
            "inputs": {},
            "params": {"resourceId": "missing-until-worker-merge"},
            "source": {"nodeId": "wire", "socketId": "Fac"},
        }
        ir["expressions"].append(baked_expression)
        ir["valueGraph"]["expressions"].append(
            copy.deepcopy(baked_expression)
        )
        deferred = miku_blender._apply_channel_values(
            ir,
            [],
            validate_baked_resources=False,
        )
        self.assertTrue(
            any(
                item["op"] == "Texture.SampleBaked2D"
                for item in deferred["expressions"]
            )
        )
        with self.assertRaisesRegex(
            RuntimeError,
            "MIKU_STATIC_EXPRESSION_ISLAND_BAKE_FAILED",
        ):
            miku_blender._apply_channel_values(ir, [])

    def test_only_active_surface_output_is_reachable(self):
        graph = {
            "material": {"name": "ActiveOutput"},
            "nodes": [
                {
                    "id": "active",
                    "op": "Output.Material",
                    "params": {"isActiveOutput": True, "target": "EEVEE"},
                },
                {
                    "id": "inactive",
                    "op": "Output.Material",
                    "params": {"isActiveOutput": False, "target": "ALL"},
                },
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
                {"id": "unused", "op": "Texture.Noise"},
            ],
            "edges": [
                {
                    "from": source("surface", "Closure"),
                    "to": source("active", "Surface"),
                },
                {
                    "from": source("unused", "Fac"),
                    "to": source("inactive", "Surface"),
                },
            ],
            "standardPbrSemantic": {"slots": {}},
        }
        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        self.assertNotIn(
            "Texture.Noise",
            {
                semantic
                for region in ir["regions"]
                for semantic in region.get("sourceSemantics", [])
            },
        )
        self.assertEqual([], ConversionPlanner().plan(ir)["bakeJobs"])

    def test_geometry_incoming_becomes_world_view_direction_without_bake(self):
        graph = base_graph(
            {
                "id": "geometry",
                "op": "Input.Geometry",
                "outputs": [
                    {
                        "id": "Incoming",
                        "name": "Incoming",
                        "valueType": "Float3",
                        "space": "World",
                        "stage": "Fragment",
                        "uniformity": "Varying",
                    }
                ],
            },
            "Incoming",
        )
        ir = build_material_ir(graph)
        self.assertEqual("Input.ViewDirection", ir["expressions"][0]["op"])
        self.assertEqual("World", ir["expressions"][0]["space"])
        self.assertEqual(
            "Expression",
            next(item for item in ir["channels"] if item["semantic"] == "BaseColor")[
                "value"
            ]["kind"],
        )
        plan = ConversionPlanner().plan(ir, mode="AllowMeshBake")
        self.assertEqual([], plan["bakeJobs"])
        self.assertTrue(
            any(
                item.get("code") == "MIKU_RUNTIME_INPUT_PRESERVED"
                for item in plan["diagnostics"]
            )
        )

    def test_geometry_backfacing_generic_workflow_is_retired(self):
        graph = base_graph(
            {
                "id": "geometry",
                "op": "Input.Geometry",
                "outputs": [
                    {
                        "id": "Backfacing",
                        "name": "Backfacing",
                        "valueType": "Float",
                        "space": "None",
                        "stage": "Fragment",
                        "uniformity": "Varying",
                    }
                ],
            },
            "Backfacing",
        )
        with self.assertRaisesRegex(ValueError, r"MIKU_WORKFLOW_RETIRED:generic_toon"):
            build_material_ir(graph, workflow_kind="generic_toon")

    def test_static_linked_channel_is_baked_without_flattening_runtime_channel(self):
        graph = {
            "material": {"name": "RuntimeAndStaticFixture"},
            "workflow": {"kind": "standard_pbr"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF", "inputs": []},
                {
                    "id": "geometry",
                    "op": "Input.Geometry",
                    "outputs": [
                        {
                            "id": "Incoming",
                            "name": "Incoming",
                            "valueType": "Float3",
                        }
                    ],
                },
                {
                    "id": "voronoi",
                    "op": "Texture.Voronoi",
                    "outputs": [
                        {
                            "id": "Distance",
                            "name": "Distance",
                            "valueType": "Scalar",
                        }
                    ],
                    "source": {
                        "stableId": "voronoi",
                        "blenderNodeName": "Voronoi Texture",
                        "groupPath": ["Material"],
                    },
                },
            ],
            "edges": [
                {
                    "from": source("geometry", "Incoming"),
                    "to": source("surface", "BaseColor"),
                },
                {
                    "from": source("voronoi", "Distance"),
                    "to": source("surface", "Roughness"),
                },
                {
                    "from": source("surface", "Closure"),
                    "to": source("out", "Surface"),
                },
            ],
            "standardPbrSemantic": {
                "slots": {
                    "BaseColor": {
                        "default": None,
                        "source": source("geometry", "Incoming"),
                    },
                    "Roughness": {
                        "default": None,
                        "source": source("voronoi", "Distance"),
                    },
                }
            },
        }
        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        channels = {item["semantic"]: item for item in ir["channels"]}
        self.assertEqual(
            "Expression",
            channels["BaseColor"]["value"]["kind"],
        )
        self.assertEqual(
            "Expression",
            channels["Roughness"]["value"]["kind"],
        )
        self.assertNotIn("requiresBake", channels["Roughness"])

        plan = ConversionPlanner().plan(
            ir,
            mode="AllowMeshBake",
        )
        self.assertEqual(1, len(plan["bakeJobs"]))
        self.assertEqual("ExpressionIsland", plan["bakeJobs"][0]["scope"])
        self.assertEqual("voronoi", plan["bakeJobs"][0]["sourceNodeId"])

    def test_prefer_native_schedules_uv0_island_without_mesh_binding(self):
        ir = build_material_ir(
            portable_uv_voronoi_graph(),
            conversion_mode="PreferNative",
        )
        baked = [
            item
            for item in ir["expressions"]
            if item["op"] == "Texture.SampleBaked2D"
        ]
        self.assertEqual(1, len(baked))
        self.assertEqual("UV0", baked[0]["params"]["coordinateDomain"])
        self.assertFalse(baked[0]["params"]["meshBindingRequired"])

        plan = ConversionPlanner().plan(ir, mode="PreferNative")
        self.assertFalse(
            any(item.get("severity") == "error" for item in plan["diagnostics"]),
            plan["diagnostics"],
        )
        self.assertEqual(1, len(plan["bakeJobs"]))
        job = plan["bakeJobs"][0]
        self.assertEqual("ReusableBake", job["route"])
        self.assertEqual("UV0", job["coordinateDomain"])
        self.assertFalse(job["meshBindingRequired"])
        self.assertTrue(
            any(
                item.get("code") == "MIKU_PORTABLE_UV_BAKE_SCHEDULED"
                for item in plan["diagnostics"]
            )
        )

    def test_prefer_native_extracts_uv0_island_from_runtime_mix(self):
        graph = portable_uv_voronoi_graph()
        graph["nodes"].extend(
            [
                {
                    "id": "mix",
                    "op": "Color.Mix",
                    "params": {
                        "blend_type": "MIX",
                        "semantic": "BaseColor",
                    },
                    "inputs": [
                        {"id": "Factor", "valueType": "Scalar"},
                        {"id": "A", "valueType": "Color"},
                        {"id": "B", "valueType": "Color"},
                    ],
                    "outputs": [{"id": "Result", "valueType": "Color"}],
                },
                {
                    "id": "layer",
                    "op": "Input.LayerWeight",
                    "inputs": [{"id": "Blend", "default": 0.5}],
                    "outputs": [{"id": "Facing", "valueType": "Scalar"}],
                },
            ]
        )
        graph["edges"] = [
            edge
            for edge in graph["edges"]
            if edge["to"] != source("surface", "BaseColor")
        ]
        graph["edges"].extend(
            [
                {"from": source("voronoi", "Color"), "to": source("mix", "A")},
                {"from": source("layer", "Facing"), "to": source("mix", "Factor")},
                {"from": source("mix", "Result"), "to": source("surface", "BaseColor")},
            ]
        )
        graph["standardPbrSemantic"]["slots"]["BaseColor"]["source"] = (
            source("mix", "Result")
        )

        ir = build_material_ir(graph, conversion_mode="PreferNative")
        samples = [
            item
            for item in ir["expressions"]
            if item["op"] == "Texture.SampleBaked2D"
        ]
        self.assertEqual(1, len(samples))
        self.assertEqual("UV0", samples[0]["params"]["coordinateDomain"])
        self.assertTrue(
            any(item["op"] == "Math.LayerWeightFacing" for item in ir["expressions"])
        )
        plan = ConversionPlanner().plan(ir, mode="PreferNative")
        self.assertEqual("ReusableBake", plan["bakeJobs"][0]["route"])

    def test_prefer_native_rejects_generated_texture_island(self):
        ir = build_material_ir(
            portable_uv_voronoi_graph(explicit_uv=False),
            conversion_mode="PreferNative",
        )
        self.assertTrue(
            any(
                item.get("code")
                == "MIKU_PORTABLE_HYBRID_MESH_DEPENDENCY"
                for item in ir["diagnostics"]
            ),
            ir["diagnostics"],
        )
        plan = ConversionPlanner().plan(ir, mode="PreferNative")
        self.assertEqual([], plan["bakeJobs"])

    def test_full_pbr_rejects_view_direction_before_worker(self):
        graph = base_graph(
            {
                "id": "geometry",
                "op": "Input.Geometry",
                "outputs": [
                    {
                        "id": "Incoming",
                        "name": "Incoming",
                        "valueType": "Float3",
                        "space": "World",
                    }
                ],
            },
            "Incoming",
        )
        ir = build_material_ir(graph, conversion_mode="FullPBRBake")
        plan = ConversionPlanner().plan(ir, mode="FullPBRBake")
        self.assertEqual([], plan["bakeJobs"])
        diagnostic = next(
            item
            for item in plan["diagnostics"]
            if item.get("code") == "MIKU_RUNTIME_INPUT_UNSUPPORTED"
        )
        self.assertEqual(["ViewDirection"], diagnostic["runtimeDependencies"])
        self.assertIn("Portable Hybrid", diagnostic["message"])

    def test_camera_outputs_keep_type_space_and_fragment_stage(self):
        expected = {
            "View Vector": ("Input.Camera.ViewVector", "Float3", "View"),
            "View Z Depth": ("Input.Camera.ViewZDepth", "Scalar", "View"),
            "View Distance": ("Input.Camera.ViewDistance", "Scalar", "None"),
        }
        for socket_name, (op, value_type, space) in expected.items():
            with self.subTest(socket=socket_name):
                graph = base_graph(
                    {
                        "id": "camera",
                        "op": "Input.CameraData",
                        "outputs": [
                            {
                                "id": socket_name,
                                "name": socket_name,
                                "valueType": value_type,
                            }
                        ],
                    },
                    socket_name,
                )
                ir = build_material_ir(graph)
                expression = next(
                    item for item in ir["expressions"] if item["op"] == op
                )
                self.assertEqual(value_type, expression["valueType"])
                self.assertEqual(space, expression["space"])
                self.assertEqual("Fragment", expression["stage"])

    def test_miku_time_contract_has_four_distinct_outputs(self):
        expected = {
            "Seconds": "Input.Time.Seconds",
            "Frame": "Input.Time.Frame",
            "Sine": "Input.Time.Sine",
            "Cosine": "Input.Time.Cosine",
        }
        for socket_name, op in expected.items():
            with self.subTest(socket=socket_name):
                graph = base_graph(
                    {
                        "id": "time",
                        "op": "Input.Time",
                        "params": {
                            "contract": "miku_time_v1",
                            "sourceFps": 24.0,
                            "frameStart": 1,
                        },
                        "outputs": [
                            {
                                "id": socket_name,
                                "name": socket_name,
                                "valueType": "Scalar",
                            }
                        ],
                    },
                    socket_name,
                    semantic="Roughness",
                )
                ir = build_material_ir(graph)
                expression = next(
                    item for item in ir["expressions"] if item["op"] == op
                )
                self.assertEqual("miku_time_v1", expression["params"]["contract"])
                self.assertEqual("Both", expression["stage"])
                self.assertEqual("Uniform", expression["uniformity"])

    def test_unmarked_legacy_time_keeps_existing_slot_contract(self):
        graph = base_graph(
            {
                "id": "legacy-time",
                "op": "Input.Time",
                "params": {},
                "outputs": [
                    {
                        "id": "Delta Time",
                        "name": "Delta Time",
                        "valueType": "Scalar",
                    }
                ],
            },
            "Delta Time",
            semantic="Roughness",
        )
        expression = next(
            item
            for item in build_material_ir(graph)["expressions"]
            if item["op"].startswith("Input.Time.")
        )
        self.assertEqual("Input.Time.LegacyDelta", expression["op"])

    def test_fresnel_and_layer_weight_are_not_shader_graph_power_shortcuts(self):
        fresnel_graph = base_graph(
            {
                "id": "fresnel",
                "op": "Input.Fresnel",
                "inputs": [
                    {
                        "id": "IOR",
                        "name": "IOR",
                        "valueType": "Scalar",
                        "default": 1.45,
                    },
                    {
                        "id": "Normal",
                        "name": "Normal",
                        "valueType": "Float3",
                        "default": [0.0, 0.0, 0.0],
                    },
                ],
                "outputs": [{"id": "Fac", "name": "Fac", "valueType": "Scalar"}],
            },
            "Fac",
            semantic="Roughness",
        )
        fresnel = build_material_ir(fresnel_graph)
        physical = next(
            item
            for item in fresnel["expressions"]
            if item["op"] == "Math.DielectricFresnel"
        )
        self.assertCountEqual(
            ["IOR", "Normal", "ViewDirection", "IsFrontFace"],
            physical["inputs"],
        )

        outputs = {}
        for socket_name in ("Fresnel", "Facing"):
            graph = base_graph(
                {
                    "id": "layer",
                    "op": "Input.LayerWeight",
                    "inputs": [
                        {
                            "id": "Blend",
                            "name": "Blend",
                            "valueType": "Scalar",
                            "default": 0.5,
                        },
                        {
                            "id": "Normal",
                            "name": "Normal",
                            "valueType": "Float3",
                            "default": [0.0, 0.0, 0.0],
                        },
                    ],
                    "outputs": [
                        {
                            "id": socket_name,
                            "name": socket_name,
                            "valueType": "Scalar",
                        }
                    ],
                },
                socket_name,
                semantic="Roughness",
            )
            ir = build_material_ir(graph)
            outputs[socket_name] = {
                item["op"]
                for item in ir["expressions"]
                if item["op"].startswith("Math.LayerWeight")
            }.pop()
        self.assertEqual("Math.LayerWeightFresnel", outputs["Fresnel"])
        self.assertEqual("Math.LayerWeightFacing", outputs["Facing"])

    def test_camera_expression_cannot_feed_vertex_stage(self):
        ir = build_material_ir(
            base_graph(
                {
                    "id": "camera",
                    "op": "Input.CameraData",
                    "outputs": [
                        {
                            "id": "View Distance",
                            "name": "View Distance",
                            "valueType": "Scalar",
                        }
                    ],
                },
                "View Distance",
            )
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
        payload["channels"][0]["stage"] = "Vertex"
        invalid = make_document(
            "miku-material-ir-1.0",
            payload,
            document_id=ir["id"],
        )
        with self.assertRaises(DocumentValidationError) as raised:
            validate_document(invalid)
        self.assertEqual("shader_stage_conflict", raised.exception.code)

    def test_repeated_generation_keeps_expression_ids_and_bytes_stable(self):
        graph = base_graph(
            {
                "id": "camera",
                "op": "Input.CameraData",
                "outputs": [
                    {
                        "id": "View Distance",
                        "name": "View Distance",
                        "valueType": "Scalar",
                    }
                ],
            },
            "View Distance",
        )
        self.assertEqual(build_material_ir(graph), build_material_ir(graph))
        source_map = build_source_map(graph, material_key="RuntimeFixture")
        self.assertTrue(source_map["expressionBindings"])
        self.assertEqual(
            "camera",
            source_map["expressionBindings"][0]["nodeId"],
        )
        self.assertEqual(
            "View Distance",
            source_map["expressionBindings"][0]["socketId"],
        )

    def test_dynamic_mix_extracts_maximal_static_expression_island(self):
        graph = {
            "material": {"name": "StaticIsland"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
                {
                    "id": "mix",
                    "op": "Color.Mix",
                    "params": {"blend_type": "MIX", "semantic": "BaseColor"},
                    "inputs": [
                        {"id": "Factor", "valueType": "Scalar", "default": 0.5},
                        {"id": "A", "valueType": "Color", "default": [0, 0, 0, 1]},
                        {"id": "B", "valueType": "Color", "default": [1, 1, 1, 1]},
                    ],
                    "outputs": [{"id": "Result", "valueType": "Color"}],
                },
                {
                    "id": "layer",
                    "op": "Input.LayerWeight",
                    "inputs": [{"id": "Blend", "default": 0.5}],
                    "outputs": [{"id": "Facing", "valueType": "Scalar"}],
                },
                {
                    "id": "noise",
                    "op": "Texture.Noise",
                    "outputs": [{"id": "Color", "valueType": "Color"}],
                    "source": {
                        "stableId": "noise",
                        "blenderNodeName": "Noise Texture",
                        "groupPath": ["Material"],
                    },
                },
            ],
            "edges": [
                {"from": source("layer", "Facing"), "to": source("mix", "Factor")},
                {"from": source("noise", "Color"), "to": source("mix", "A")},
                {"from": source("mix", "Result"), "to": source("surface", "Base Color")},
                {"from": source("surface", "Closure"), "to": source("out", "Surface")},
            ],
            "standardPbrSemantic": {
                "slots": {
                    "BaseColor": {
                        "default": None,
                        "source": source("mix", "Result"),
                    }
                }
            },
        }
        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        samples = [
            item
            for item in ir["expressions"]
            if item["op"] == "Texture.SampleBaked2D"
        ]
        self.assertEqual(1, len(samples))
        self.assertEqual("Color", samples[0]["params"]["usage"])
        jobs = ConversionPlanner().plan(
            ir,
            mode="AllowMeshBake",
        )["bakeJobs"]
        self.assertEqual(1, len(jobs))
        self.assertEqual("ExpressionIsland", jobs[0]["scope"])
        self.assertEqual("noise", jobs[0]["sourceNodeId"])

    def test_runtime_material_bakes_static_channel_as_isolated_endpoint(self):
        graph = {
            "material": {"name": "RuntimeWithStaticChannel"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
                {
                    "id": "layer",
                    "op": "Input.LayerWeight",
                    "inputs": [{"id": "Blend", "default": 0.5}],
                    "outputs": [{"id": "Facing", "valueType": "Scalar"}],
                },
                {
                    "id": "noise",
                    "op": "Texture.Noise",
                    "outputs": [{"id": "Color", "valueType": "Color"}],
                    "source": {
                        "stableId": "noise",
                        "blenderNodeName": "Noise Texture",
                        "groupPath": ["Material"],
                    },
                },
            ],
            "edges": [
                {
                    "from": source("noise", "Color"),
                    "to": source("surface", "Base Color"),
                },
                {
                    "from": source("layer", "Facing"),
                    "to": source("surface", "Roughness"),
                },
                {
                    "from": source("surface", "Closure"),
                    "to": source("out", "Surface"),
                },
            ],
            "standardPbrSemantic": {
                "slots": {
                    "BaseColor": {
                        "default": None,
                        "source": source("noise", "Color"),
                    },
                    "Roughness": {
                        "default": None,
                        "source": source("layer", "Facing"),
                    },
                }
            },
        }

        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        base_color = next(
            item for item in ir["channels"]
            if item["semantic"] == "BaseColor"
        )
        jobs = ConversionPlanner().plan(
            ir,
            mode="AllowMeshBake",
        )["bakeJobs"]

        self.assertEqual("Expression", base_color["value"]["kind"])
        self.assertNotIn("requiresBake", base_color)
        self.assertEqual(["ExpressionIsland"], [item["scope"] for item in jobs])
        self.assertEqual("noise", jobs[0]["sourceNodeId"])

    def test_typed_rgba_mix_uses_active_color_socket_identifiers(self):
        graph = {
            "material": {"name": "TypedMix"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
                {
                    "id": "mix",
                    "op": "Color.Mix",
                    "params": {
                        "blend_type": "OVERLAY",
                        "semantic": "Emission",
                    },
                    "inputs": [
                        {
                            "id": "Factor_Float",
                            "name": "Factor",
                            "valueType": "Scalar",
                            "default": 0.5,
                            "enabled": True,
                            "isUnavailable": False,
                        },
                        {
                            "id": "A_Float",
                            "name": "A",
                            "valueType": "Scalar",
                            "default": 0.0,
                            "enabled": False,
                            "isUnavailable": True,
                        },
                        {
                            "id": "A_Color",
                            "name": "A",
                            "valueType": "Color",
                            "default": [0.0, 0.0, 0.0, 1.0],
                            "enabled": True,
                            "isUnavailable": False,
                        },
                        {
                            "id": "B_Float",
                            "name": "B",
                            "valueType": "Scalar",
                            "default": 0.0,
                            "enabled": False,
                            "isUnavailable": True,
                        },
                        {
                            "id": "B_Color",
                            "name": "B",
                            "valueType": "Color",
                            "default": [0.0, 0.0, 0.0, 1.0],
                            "enabled": True,
                            "isUnavailable": False,
                        },
                    ],
                    "outputs": [
                        {
                            "id": "Result_Color",
                            "name": "Result",
                            "valueType": "Color",
                        }
                    ],
                },
                {
                    "id": "factor",
                    "op": "Input.LayerWeight",
                    "inputs": [{"id": "Blend", "default": 0.25}],
                    "outputs": [{"id": "Facing", "valueType": "Scalar"}],
                },
                {
                    "id": "a",
                    "op": "Input.Value",
                    "outputs": [
                        {
                            "id": "Color",
                            "valueType": "Color",
                            "default": [0.2, 0.4, 0.8, 1.0],
                        }
                    ],
                },
                {
                    "id": "b",
                    "op": "Input.Value",
                    "outputs": [
                        {
                            "id": "Color",
                            "valueType": "Color",
                            "default": [0.8, 0.2, 0.1, 1.0],
                        }
                    ],
                },
            ],
            "edges": [
                {
                    "from": source("factor", "Facing"),
                    "to": source("mix", "Factor_Float"),
                },
                {
                    "from": source("a", "Color"),
                    "to": source("mix", "A_Color"),
                },
                {
                    "from": source("b", "Color"),
                    "to": source("mix", "B_Color"),
                },
                {
                    "from": source("mix", "Result_Color"),
                    "to": source("surface", "Emission Color"),
                },
                {
                    "from": source("surface", "Closure"),
                    "to": source("out", "Surface"),
                },
            ],
            "standardPbrSemantic": {
                "slots": {
                    "Emission": {
                        "default": None,
                        "source": source("mix", "Result_Color"),
                    }
                }
            },
        }
        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        overlay = next(
            item for item in ir["expressions"] if item["op"] == "Color.Overlay"
        )
        expressions = {item["id"]: item for item in ir["expressions"]}
        self.assertNotEqual(
            0.0,
            expressions[overlay["inputs"]["A"]["expressionId"]]["params"].get(
                "value"
            ),
        )
        self.assertNotEqual(
            0.0,
            expressions[overlay["inputs"]["B"]["expressionId"]]["params"].get(
                "value"
            ),
        )

    def test_static_wireframe_closure_weight_uses_baked_expression(self):
        graph = {
            "material": {"name": "WireframeWeight"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {
                    "id": "mix",
                    "op": "Shader.Mix",
                    "inputs": [
                        {"id": "Fac", "default": 0.5},
                        {"id": "Shader", "valueType": "Closure"},
                        {"id": "Shader_001", "valueType": "Closure"},
                    ],
                },
                {
                    "id": "transparent",
                    "op": "Shader.TransparentBSDF",
                    "inputs": [
                        {
                            "id": "Color",
                            "valueType": "Color",
                            "default": [1.0, 1.0, 1.0, 1.0],
                        }
                    ],
                    "outputs": [{"id": "Closure", "valueType": "Closure"}],
                },
                {
                    "id": "emission",
                    "op": "Shader.Emission",
                    "inputs": [
                        {
                            "id": "Color",
                            "valueType": "Color",
                            "default": [0.2, 0.7, 1.0, 1.0],
                        },
                        {"id": "Strength", "default": 2.0},
                    ],
                    "outputs": [{"id": "Closure", "valueType": "Closure"}],
                },
                {
                    "id": "wire",
                    "op": "Input.Wireframe",
                    "inputs": [{"id": "Size", "default": 0.2}],
                    "outputs": [{"id": "Fac", "valueType": "Scalar"}],
                    "source": {
                        "stableId": "wire",
                        "blenderNodeName": "Wireframe",
                        "groupPath": ["Material"],
                    },
                },
            ],
            "edges": [
                {
                    "from": source("wire", "Fac"),
                    "to": source("mix", "Fac"),
                },
                {
                    "from": source("transparent", "Closure"),
                    "to": source("mix", "Shader"),
                },
                {
                    "from": source("emission", "Closure"),
                    "to": source("mix", "Shader_001"),
                },
                {
                    "from": source("mix", "Closure"),
                    "to": source("out", "Surface"),
                },
            ],
        }
        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        expression = next(
            item
            for item in ir["expressions"]
            if item["op"] == "Texture.SampleBaked2D"
        )
        referenced = []

        def collect(value):
            if isinstance(value, dict):
                if value.get("source", {}).get("nodeId") == "wire":
                    referenced.append(value)
                for nested in value.values():
                    collect(nested)
            elif isinstance(value, list):
                for nested in value:
                    collect(nested)

        for term in ir["weightedClosures"]["terms"]:
            collect(term["finalWeight"])
        self.assertTrue(referenced)
        self.assertTrue(
            all(
                item.get("expressionId") == expression["id"]
                and "requiresBake" not in item
                for item in referenced
            )
        )
        jobs = ConversionPlanner().plan(
            ir,
            mode="AllowMeshBake",
        )["bakeJobs"]
        self.assertEqual(
            [expression["id"]],
            [
                item["expressionId"]
                for item in jobs
                if item.get("scope") == "ExpressionIsland"
            ],
        )

    def test_static_closure_parameter_uses_baked_expression(self):
        graph = {
            "material": {"name": "BakedClosureParameter"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {
                    "id": "mix",
                    "op": "Shader.Mix",
                    "inputs": [
                        {"id": "Fac", "default": 0.5},
                        {"id": "Shader", "valueType": "Closure"},
                        {"id": "Shader_001", "valueType": "Closure"},
                    ],
                },
                {
                    "id": "diffuse",
                    "op": "Shader.DiffuseBSDF",
                    "inputs": [
                        {
                            "id": "Color",
                            "valueType": "Color",
                            "default": [1.0, 1.0, 1.0, 1.0],
                        }
                    ],
                    "outputs": [{"id": "Closure", "valueType": "Closure"}],
                },
                {
                    "id": "emission",
                    "op": "Shader.Emission",
                    "inputs": [
                        {
                            "id": "Color",
                            "valueType": "Color",
                            "default": [0.2, 0.7, 1.0, 1.0],
                        },
                        {"id": "Strength", "default": 2.0},
                    ],
                    "outputs": [{"id": "Closure", "valueType": "Closure"}],
                },
                {
                    "id": "wire",
                    "op": "Input.Wireframe",
                    "inputs": [{"id": "Size", "default": 0.2}],
                    "outputs": [{"id": "Fac", "valueType": "Scalar"}],
                    "source": {
                        "stableId": "wire",
                        "blenderNodeName": "Wireframe",
                        "groupPath": ["Material"],
                    },
                },
            ],
            "edges": [
                {
                    "from": source("wire", "Fac"),
                    "to": source("emission", "Strength"),
                },
                {
                    "from": source("diffuse", "Closure"),
                    "to": source("mix", "Shader"),
                },
                {
                    "from": source("emission", "Closure"),
                    "to": source("mix", "Shader_001"),
                },
                {
                    "from": source("mix", "Closure"),
                    "to": source("out", "Surface"),
                },
            ],
        }

        ir = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        strength = next(
            term["parameters"]["Strength"]
            for term in ir["weightedClosures"]["terms"]
            if term["closureKind"] == "Emission"
        )
        expression = next(
            item
            for item in ir["expressions"]
            if item["op"] == "Texture.SampleBaked2D"
            and item["params"]["usage"] == "Scalar"
        )
        self.assertEqual(expression["id"], strength["expressionId"])
        self.assertNotIn("requiresBake", strength)
        self.assertEqual(
            [expression["id"]],
            [
                item["expressionId"]
                for item in ConversionPlanner().plan(
                    ir,
                    mode="AllowMeshBake",
                )["bakeJobs"]
                if item.get("scope") == "ExpressionIsland"
            ],
        )

    def test_bump_normal_is_portable_normal_blend_from_height(self):
        ir = build_material_ir(bump_normal_graph())
        errors = [
            item
            for item in ir["diagnostics"]
            if str(item.get("severity") or "").lower() == "error"
        ]
        self.assertEqual([], errors)
        self.assertEqual("OpaquePBR", ir["surfaceModelPlan"]["kind"])
        normal = next(
            item
            for item in ir["channels"]
            if item["semantic"] == "Normal"
        )
        expression_id = normal["value"]["expressionId"]
        expression = next(
            item
            for item in ir["expressions"]
            if item["id"] == expression_id
        )
        self.assertEqual("Vector.NormalBlend", expression["op"])
        detail_id = expression["inputs"]["Detail"]["expressionId"]
        detail = next(
            item
            for item in ir["expressions"]
            if item["id"] == detail_id
        )
        self.assertEqual("Vector.NormalFromHeight", detail["op"])
        self.assertNotIn(
            "Texture.SampleBaked2D",
            {item["op"] for item in ir["expressions"]},
        )
        self.assertEqual(
            [],
            ConversionPlanner().plan(ir, mode="Auto")["bakeJobs"],
        )

    def test_linked_bump_base_normal_is_portable_normal_blend(self):
        auto = build_material_ir(bump_normal_graph(nested=True))
        self.assertFalse(
            any(
                str(item.get("severity") or "").lower() == "error"
                for item in auto["diagnostics"]
            )
        )
        operations = [item["op"] for item in auto["expressions"]]
        self.assertEqual(2, operations.count("Vector.NormalFromHeight"))
        self.assertEqual(2, operations.count("Vector.NormalBlend"))
        self.assertNotIn("Texture.SampleBaked2D", operations)
        self.assertEqual(
            [],
            ConversionPlanner().plan(auto, mode="Auto")["bakeJobs"],
        )

    def test_unsupported_bump_base_normal_falls_back_to_source_mesh_bake(self):
        graph = bump_normal_graph()
        graph["nodes"].append(
            {
                "id": "opaque-normal",
                "op": "Opaque.BlenderNode",
                "outputs": [
                    {
                        "id": "Fac",
                        "valueType": "Float3",
                        "space": "Tangent",
                    }
                ],
                "source": {
                    "stableId": "opaque-normal",
                    "blenderNodeName": "Unsupported Normal",
                },
            }
        )
        graph["edges"].append(
            {
                "from": source("opaque-normal", "Fac"),
                "to": source("bump", "Normal"),
            }
        )
        auto = build_material_ir(graph)
        self.assertTrue(
            any(
                item.get("code")
                in {
                    "MIKU_SOURCE_MESH_FIDELITY_REQUIRED",
                    "MIKU_RUNTIME_INPUT_UNSUPPORTED",
                }
                for item in auto["diagnostics"]
            )
        )
        source_mesh = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        normal = next(
            item
            for item in source_mesh["channels"]
            if item["semantic"] == "Normal"
        )
        self.assertTrue(normal["requiresBake"])
        self.assertTrue(
            any(
                item.get("scope") == "Channels"
                and "Normal" in item.get("semantics", [])
                for item in ConversionPlanner().plan(
                    source_mesh,
                    mode="AllowMeshBake",
                )["bakeJobs"]
            )
        )

    def test_runtime_dependent_bump_is_supported_without_duplicates(self):
        ir = build_material_ir(bump_normal_graph(runtime_height=True))
        errors = [
            item
            for item in ir["diagnostics"]
            if str(item.get("severity") or "").lower() == "error"
        ]
        self.assertEqual([], errors)
        self.assertEqual(
            1,
            sum(
                item["op"] == "Vector.NormalFromHeight"
                for item in ir["expressions"]
            ),
        )


class TimeDriverTests(unittest.TestCase):
    def test_affine_frame_expression_is_parsed_without_eval(self):
        value = parse_affine_frame("(frame - 1) / 24 + 2")
        self.assertAlmostEqual(1.0 / 24.0, value.scale)
        self.assertAlmostEqual(2.0 - 1.0 / 24.0, value.offset)

    def test_non_affine_and_malicious_expressions_are_rejected(self):
        for expression in ("frame * frame", "__import__('os').system('x')"):
            with self.subTest(expression=expression):
                with self.assertRaises(TimeDriverError):
                    parse_affine_frame(expression)

    def test_externalization_safety_rejects_callable_code_names(self):
        self.assertFalse(miku_blender._unsafe_driver_expression("sin(frame)"))
        self.assertTrue(miku_blender._unsafe_driver_expression("eval('frame')"))
        self.assertTrue(miku_blender._unsafe_driver_expression("open('x')"))
        self.assertTrue(
            miku_blender._unsafe_driver_expression(
                "__import__('os').system('echo unsafe')"
            )
        )


class RuntimeMathOracleTests(unittest.TestCase):
    def test_hue_saturation_value_wraps_clamps_and_blends(self):
        actual = hue_saturation_value(
            (0.8, 0.2, 0.1),
            0.75,
            2.0,
            0.5,
            0.25,
        )
        expected = (0.6357142857, 0.25, 0.075)
        for left, right in zip(actual, expected):
            self.assertAlmostEqual(right, left, delta=1.0e-4)

    def test_two_element_bspline_uses_replicated_endpoint_weights(self):
        self.assertAlmostEqual(
            1.0 / 6.0,
            two_element_bspline(0.0, 1.0, 0.0),
            delta=1.0e-4,
        )
        self.assertAlmostEqual(
            0.5,
            two_element_bspline(0.0, 1.0, 0.5),
            delta=1.0e-4,
        )
        self.assertAlmostEqual(
            5.0 / 6.0,
            two_element_bspline(0.0, 1.0, 1.0),
            delta=1.0e-4,
        )

    def test_dielectric_fresnel_matches_closed_form_reference_values(self):
        self.assertAlmostEqual(
            ((1.45 - 1.0) / (1.45 + 1.0)) ** 2,
            dielectric_fresnel(1.0, 1.45),
            delta=1.0e-4,
        )
        self.assertAlmostEqual(
            0.043323466,
            dielectric_fresnel(math.cos(math.radians(45.0)), 1.45),
            delta=1.0e-4,
        )
        self.assertAlmostEqual(
            1.0,
            dielectric_fresnel(0.25, 1.45, front_facing=False),
            delta=1.0e-4,
        )

    def test_layer_weight_preserves_distinct_outputs_and_half_rule(self):
        fresnel, facing = layer_weight(0.5, 0.25)
        self.assertAlmostEqual(0.318440083, fresnel, delta=1.0e-4)
        self.assertAlmostEqual(0.75, facing, delta=1.0e-4)
        _, shaped = layer_weight(0.25, 0.25)
        self.assertAlmostEqual(0.5, shaped, delta=1.0e-4)


if __name__ == "__main__":
    unittest.main()
