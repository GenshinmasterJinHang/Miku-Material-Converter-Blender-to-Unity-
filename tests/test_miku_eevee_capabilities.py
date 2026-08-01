from __future__ import annotations

import types
import unittest

import miku_blender
from miku.contracts import make_document
from miku.planner import ConversionPlanner
from miku.semantic import build_material_ir
from miku_blender.capabilities import (
    CYCLES_ONLY,
    NATIVE_OR_EQUIVALENT,
    REQUIRES_SOURCE_MESH_FIDELITY,
    classify_eevee_graph,
)


def endpoint(node: str, socket: str) -> dict[str, str]:
    return {"node": node, "socket": socket}


def material_graph(
    node: dict,
    socket: str,
    *,
    semantic: str = "Roughness",
) -> dict:
    return {
        "material": {"name": "EeveeFixture"},
        "workflow": {"kind": "standard_pbr"},
        "nodes": [
            {
                "id": "out",
                "op": "Output.Material",
                "params": {"isActiveOutput": True, "target": "EEVEE"},
            },
            {
                "id": "surface",
                "op": "Shader.PrincipledBSDF",
                "inputs": [],
            },
            node,
        ],
        "edges": [
            {
                "from": endpoint(node["id"], socket),
                "to": endpoint("surface", semantic),
            },
            {
                "from": endpoint("surface", "Closure"),
                "to": endpoint("out", "Surface"),
            },
        ],
        "standardPbrSemantic": {
            "slots": {
                semantic: {
                    "default": None,
                    "source": endpoint(node["id"], socket),
                }
            }
        },
    }


class EeveeCapabilityTests(unittest.TestCase):
    def test_only_active_eevee_chain_can_make_material_cycles_only(self):
        graph = {
            "nodes": [
                {
                    "id": "eevee-out",
                    "op": "Output.Material",
                    "params": {"isActiveOutput": True, "target": "EEVEE"},
                },
                {
                    "id": "all-out",
                    "op": "Output.Material",
                    "params": {"isActiveOutput": True, "target": "ALL"},
                },
                {"id": "principled", "op": "Shader.PrincipledBSDF"},
                {"id": "bevel", "op": "Vector.Bevel"},
            ],
            "edges": [
                {
                    "from": endpoint("principled", "Closure"),
                    "to": endpoint("eevee-out", "Surface"),
                },
                {
                    "from": endpoint("bevel", "Normal"),
                    "to": endpoint("all-out", "Surface"),
                },
            ],
        }

        capability = classify_eevee_graph(graph)

        self.assertEqual(NATIVE_OR_EQUIVALENT, capability["quality"])
        self.assertNotIn("bevel", capability["activeNodeIds"])

    def test_cycles_only_evidence_names_exact_active_node(self):
        graph = material_graph(
            {
                "id": "bevel",
                "op": "Vector.Bevel",
                "source": {
                    "displayName": "Bevel",
                    "blenderNodeType": "ShaderNodeBevel",
                },
            },
            "Normal",
            semantic="Normal",
        )

        capability = classify_eevee_graph(graph)

        self.assertEqual(CYCLES_ONLY, capability["quality"])
        self.assertEqual("bevel", capability["evidence"][0]["nodeId"])
        self.assertEqual(
            "ShaderNodeBevel",
            capability["evidence"][0]["blenderNodeType"],
        )

    def test_spatial_unsupported_branch_uses_source_mesh_bake(self):
        graph = material_graph(
            {
                "id": "voronoi",
                "op": "Texture.Voronoi",
                "inputs": [{"id": "Vector", "valueType": "Float3"}],
                "outputs": [{"id": "Distance", "valueType": "Scalar"}],
                "source": {
                    "blenderNodeName": "Voronoi Texture",
                    "blenderNodeType": "ShaderNodeTexVoronoi",
                },
            },
            "Distance",
        )
        graph["nodes"].append(
            {
                "id": "coordinates",
                "op": "Input.TextureCoordinate",
                "outputs": [{"id": "Generated", "valueType": "Float3"}],
                "source": {
                    "blenderNodeName": "Texture Coordinate",
                    "blenderNodeType": "ShaderNodeTexCoord",
                },
            }
        )
        graph["edges"].append(
            {
                "from": endpoint("coordinates", "Generated"),
                "to": endpoint("voronoi", "Vector"),
            }
        )

        portable = build_material_ir(graph, conversion_mode="Auto")
        self.assertTrue(
            any(
                item.get("code")
                == "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
                for item in portable["diagnostics"]
            )
        )

        source_mesh = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        roughness = next(
            item
            for item in source_mesh["channels"]
            if item["semantic"] == "Roughness"
        )
        self.assertTrue(roughness["requiresBake"])
        jobs = ConversionPlanner().plan(
            source_mesh,
            mode="AllowMeshBake",
        )["bakeJobs"]
        self.assertEqual(["Roughness"], jobs[0]["semantics"])

    def test_constant_color_ramp_stays_editable(self):
        graph = material_graph(
            {
                "id": "ramp",
                "op": "Color.Ramp",
                "params": {
                    "colorRamp": {
                        "interpolation": "CONSTANT",
                        "colorMode": "RGB",
                        "hueInterpolation": "NEAR",
                        "elements": [
                            {
                                "position": 0.25,
                                "color": [0.1, 0.2, 0.3, 1.0],
                            },
                            {
                                "position": 0.75,
                                "color": [0.8, 0.7, 0.6, 0.5],
                            },
                        ],
                    }
                },
                "inputs": [
                    {
                        "id": "Fac",
                        "name": "Fac",
                        "valueType": "Scalar",
                        "default": 0.5,
                    }
                ],
                "outputs": [
                    {
                        "id": "Color",
                        "name": "Color",
                        "valueType": "Color",
                    }
                ],
            },
            "Color",
            semantic="BaseColor",
        )

        ir = build_material_ir(graph)

        expression = next(
            item for item in ir["expressions"] if item["op"] == "Color.Ramp"
        )
        self.assertEqual("CONSTANT", expression["params"]["interpolation"])

    def test_generated_coordinates_make_noise_factor_mesh_bound(self):
        graph = material_graph(
            {
                "id": "noise",
                "op": "Texture.Noise",
                "inputs": [{"id": "Vector", "valueType": "Float3"}],
                "outputs": [{"id": "Fac", "valueType": "Scalar"}],
            },
            "Fac",
        )
        graph["nodes"].append(
            {
                "id": "coordinates",
                "op": "Input.TextureCoordinate",
                "outputs": [{"id": "Generated", "valueType": "Float3"}],
            }
        )
        graph["edges"].append(
            {
                "from": endpoint("coordinates", "Generated"),
                "to": endpoint("noise", "Vector"),
            }
        )

        capability = classify_eevee_graph(graph)

        self.assertEqual(
            REQUIRES_SOURCE_MESH_FIDELITY,
            capability["quality"],
        )

    def test_eevee_camera_ray_is_a_native_runtime_expression(self):
        graph = material_graph(
            {
                "id": "light-path",
                "op": "Input.LightPath",
                "outputs": [
                    {
                        "id": "Is Camera Ray",
                        "name": "Is Camera Ray",
                        "valueType": "Scalar",
                    }
                ],
            },
            "Is Camera Ray",
        )

        capability = classify_eevee_graph(graph)
        ir = build_material_ir(graph)

        self.assertEqual(NATIVE_OR_EQUIVALENT, capability["quality"])
        self.assertIn(
            "Input.LightPath.CameraRay",
            {item["op"] for item in ir["expressions"]},
        )
        self.assertFalse(
            any(
                item.get("code") == "MIKU_LIGHT_PATH_UNSUPPORTED"
                for item in ir["diagnostics"]
            )
        )

    def test_full_pbr_bake_resolves_static_surface_backend_errors(self):
        ir = make_document(
            "miku-material-ir-1.0",
            {
                "materialKey": "StaticLegacyMix",
                "regions": [
                    {
                        "id": "region",
                        "kind": "SurfaceMix",
                        "dynamism": "Static",
                    }
                ],
                "surfaceModelPlan": {"kind": "UnsupportedSurface"},
                "diagnostics": [
                    {
                        "severity": "error",
                        "code": "MIKU_SURFACE_MODEL_UNSUPPORTED",
                    }
                ],
                "parameters": [],
            },
        )

        plan = ConversionPlanner().plan(ir, mode="FullPBRBake")

        self.assertEqual(
            ["FullPBRBake"],
            [item["route"] for item in plan["bakeJobs"]],
        )
        self.assertFalse(
            any(
                item.get("severity") == "error"
                for item in plan["diagnostics"]
            )
        )

    def test_source_mesh_mode_requests_full_pbr_for_surface_errors(self):
        ir = make_document(
            "miku-material-ir-1.0",
            {
                "materialKey": "StaticLegacyMix",
                "regions": [
                    {
                        "id": "region",
                        "kind": "SurfaceMix",
                        "dynamism": "Static",
                    }
                ],
                "surfaceModelPlan": {"kind": "UnsupportedSurface"},
                "diagnostics": [
                    {
                        "severity": "error",
                        "code": "MIKU_SURFACE_MODEL_UNSUPPORTED",
                    }
                ],
                "parameters": [],
            },
        )

        plan = ConversionPlanner().plan(ir, mode="AllowMeshBake")
        full_pbr = next(
            item
            for item in plan["diagnostics"]
            if item.get("code") == "MIKU_FULL_PBR_BAKE_REQUIRED"
        )

        self.assertEqual("error", full_pbr["severity"])
        self.assertIn("Full PBR Bake", full_pbr["message"])

    def test_full_pbr_result_becomes_a_baked_supported_surface(self):
        ir = make_document(
            "miku-material-ir-1.0",
            {
                "materialKey": "StaticLegacyMix",
                "channels": [],
                "surfaceModelPlan": {
                    "kind": "UnsupportedSurface",
                    "renderStatePlan": {"surfaceType": "Opaque"},
                },
                "diagnostics": [
                    {
                        "severity": "error",
                        "code": "MIKU_SURFACE_MODEL_UNSUPPORTED",
                    }
                ],
            },
        )

        rewritten = miku_blender._apply_full_pbr_surface_model(ir)

        self.assertEqual(
            "OpaquePBR",
            rewritten["surfaceModelPlan"]["kind"],
        )
        self.assertEqual(
            "Baked",
            rewritten["surfaceModelPlan"]["fidelity"],
        )
        self.assertEqual(
            "info",
            rewritten["diagnostics"][0]["severity"],
        )

    def test_source_mesh_projection_uses_baked_channel_and_standard_surface(self):
        ir = make_document(
            "miku-material-ir-1.0",
            {
                "materialKey": "ProjectedMultiLobe",
                "channels": [
                    {
                        "semantic": "BaseColor",
                        "valueType": "Color",
                        "stage": "Fragment",
                        "required": True,
                        "value": {
                            "kind": "Expression",
                            "expressionId": "runtime-color",
                        },
                    }
                ],
                "surfaceModelPlan": {
                    "kind": "CustomMultiLobe",
                    "renderStatePlan": {"surfaceType": "Opaque"},
                    "channelPlans": [
                        {
                            "semantic": semantic,
                            "valueType": (
                                "Color"
                                if semantic == "BaseColor"
                                else "Float3"
                                if semantic == "Normal"
                                else "Scalar"
                            ),
                            "stage": "Fragment",
                            "route": "MeshBake",
                        }
                        for semantic in (
                            "BaseColor",
                            "Metalness",
                            "Roughness",
                            "Normal",
                        )
                    ],
                    "approximations": [],
                    "diagnostics": [
                        {
                            "severity": "warning",
                            "code": "MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE",
                        }
                    ],
                },
                "diagnostics": [],
            },
        )
        resources = [
            {
                "id": "baked-base-color",
                "semantic": "BaseColor",
            }
        ]

        bound = miku_blender._apply_channel_values(ir, resources)
        rewritten = miku_blender._apply_source_mesh_pbr_surface_model(
            bound
        )

        self.assertEqual(
            {"kind": "TextureResource", "resourceId": "baked-base-color"},
            rewritten["channels"][0]["value"],
        )
        self.assertEqual(
            "OpaquePBR",
            rewritten["surfaceModelPlan"]["kind"],
        )
        self.assertEqual(
            "MeshBake",
            rewritten["surfaceModelPlan"]["channelPlans"][0]["route"],
        )
        self.assertEqual(
            [],
            rewritten["surfaceModelPlan"]["diagnostics"],
        )
        self.assertEqual(
            1,
            sum(
                item.get("kind")
                == "SourceMeshFidelityPbrProjection"
                for item in rewritten["surfaceModelPlan"]["approximations"]
            ),
        )

    def test_refractive_closure_uses_neutral_nonauthoritative_pbr_channels(self):
        ir = make_document(
            "miku-material-ir-1.0",
            {
                "materialKey": "ClosureDrivenGlass",
                "channels": [
                    {
                        "semantic": "BaseColor",
                        "required": True,
                        "default": None,
                    },
                    {
                        "semantic": "Roughness",
                        "required": True,
                        "default": None,
                    },
                ],
                "surfaceModelPlan": {"kind": "RefractiveGlass"},
                "diagnostics": [],
            },
        )

        rewritten = miku_blender._apply_channel_values(ir, [])
        channels = {
            item["semantic"]: item for item in rewritten["channels"]
        }

        self.assertEqual(
            {"kind": "Constant", "value": [0.8, 0.8, 0.8, 1.0]},
            channels["BaseColor"]["value"],
        )
        self.assertEqual(
            {"kind": "Constant", "value": 0.5},
            channels["Roughness"]["value"],
        )
        self.assertTrue(
            any(
                item.get("code")
                == "MIKU_CLOSURE_COMPOSITE_CHANNEL_NEUTRAL"
                for item in rewritten["diagnostics"]
            )
        )

    def test_blender_52_legacy_glossy_identity_is_not_anisotropic(self):
        anisotropy = types.SimpleNamespace(
            name="Anisotropic",
            identifier="Anisotropic",
            default_value=0.0,
            is_linked=False,
            enabled=True,
            is_unavailable=False,
            type="VALUE",
        )
        closure = types.SimpleNamespace(
            name="BSDF",
            identifier="BSDF",
            default_value=None,
            enabled=True,
            is_unavailable=False,
            type="SHADER",
        )
        node = types.SimpleNamespace(
            name="Glossy BSDF",
            bl_idname="ShaderNodeBsdfAnisotropic",
            type="BSDF_GLOSSY",
            inputs=[anisotropy],
            outputs=[closure],
        )

        snapshot = miku_blender._snapshot_node(node)

        self.assertEqual("Shader.GlossyBSDF", snapshot["op"])
        self.assertEqual(
            "ShaderNodeBsdfAnisotropic",
            snapshot["source"]["blenderNodeType"],
        )
        self.assertEqual(
            "BSDF_GLOSSY",
            snapshot["source"]["blenderNodeKind"],
        )


if __name__ == "__main__":
    unittest.main()
