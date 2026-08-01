from __future__ import annotations

import copy
import unittest

from miku.bundle import (
    compute_sealed_digest,
    validate_bundle_document,
)
from miku.contracts import DocumentValidationError, make_document
from miku.planner import ConversionPlanner, default_target_profile
from miku.semantic import build_material_ir


def endpoint(node: str, socket: str) -> dict[str, str]:
    return {"node": node, "socket": socket}


def portable_noise_graph(*, noise_output: str = "Fac") -> dict:
    nodes = [
        {"id": "out", "op": "Output.Material"},
        {
            "id": "surface",
            "op": "Shader.PrincipledBSDF",
            "inputs": [
                {
                    "id": "Base Color",
                    "valueType": "Color",
                    "default": [0.8, 0.8, 0.8, 1.0],
                },
                {
                    "id": "Normal",
                    "valueType": "Float3",
                    "default": [0.0, 0.0, 0.0],
                },
                {"id": "Alpha", "valueType": "Scalar", "default": 1.0},
            ],
            "outputs": [{"id": "Closure", "valueType": "Closure"}],
        },
        {
            "id": "object",
            "op": "Input.TextureCoordinate",
            "outputs": [
                {
                    "id": "Object",
                    "name": "Object",
                    "valueType": "Float3",
                    "space": "Object",
                }
            ],
        },
        {
            "id": "mapping",
            "op": "Vector.Mapping",
            "params": {"vectorType": "POINT"},
            "inputs": [
                {"id": "Vector", "valueType": "Float3"},
                {
                    "id": "Location",
                    "valueType": "Float3",
                    "default": [0.1, 0.2, 0.3],
                },
                {
                    "id": "Rotation",
                    "valueType": "Float3",
                    "default": [0.0, 0.0, 0.5],
                },
                {
                    "id": "Scale",
                    "valueType": "Float3",
                    "default": [2.0, 2.0, 2.0],
                },
            ],
            "outputs": [{"id": "Vector", "valueType": "Float3"}],
        },
        {
            "id": "noise",
            "op": "Texture.Noise",
            "params": {"noiseDimensions": "3D", "normalize": True},
            "inputs": [
                {"id": "Vector", "valueType": "Float3"},
                {"id": "Scale", "default": 5.0},
                {"id": "Detail", "default": 2.0},
                {"id": "Roughness", "default": 0.5},
                {"id": "Lacunarity", "default": 2.0},
                {"id": "Distortion", "default": 0.1},
            ],
            "outputs": [
                {"id": "Fac", "name": "Factor", "valueType": "Scalar"},
                {"id": "Color", "name": "Color", "valueType": "Color"},
            ],
            "source": {
                "stableId": "noise",
                "blenderNodeName": "Noise Texture",
                "groupPath": ["Material"],
            },
        },
        {
            "id": "multiply",
            "op": "Math",
            "params": {"operation": "MULTIPLY"},
            "inputs": [
                {"id": "Value", "default": 0.0},
                {"id": "Value_001", "default": 0.8},
            ],
            "outputs": [{"id": "Value", "valueType": "Scalar"}],
        },
        {
            "id": "ramp",
            "op": "Color.Ramp",
            "params": {
                "colorRamp": {
                    "interpolation": "LINEAR",
                    "elements": [
                        {"position": 0.0, "color": [0.1, 0.0, 0.0, 1.0]},
                        {"position": 1.0, "color": [1.0, 0.4, 0.1, 1.0]},
                    ],
                }
            },
            "inputs": [{"id": "Fac", "default": 0.0}],
            "outputs": [{"id": "Color", "valueType": "Color"}],
        },
        {
            "id": "bump",
            "op": "Vector.Bump",
            "params": {"invert": False},
            "inputs": [
                {"id": "Strength", "default": 0.3},
                {"id": "Distance", "default": 0.2},
                {"id": "Height", "default": 0.0},
                {
                    "id": "Normal",
                    "valueType": "Float3",
                    "default": [0.0, 0.0, 0.0],
                },
            ],
            "outputs": [
                {
                    "id": "Normal",
                    "valueType": "Float3",
                    "space": "Tangent",
                }
            ],
        },
    ]
    edges = [
        {
            "from": endpoint("object", "Object"),
            "to": endpoint("mapping", "Vector"),
        },
        {
            "from": endpoint("mapping", "Vector"),
            "to": endpoint("noise", "Vector"),
        },
        {
            "from": endpoint("noise", noise_output),
            "to": endpoint("multiply", "Value"),
        },
        {
            "from": endpoint("multiply", "Value"),
            "to": endpoint("ramp", "Fac"),
        },
        {
            "from": endpoint("multiply", "Value"),
            "to": endpoint("bump", "Height"),
        },
        {
            "from": endpoint("ramp", "Color"),
            "to": endpoint("surface", "Base Color"),
        },
        {
            "from": endpoint("bump", "Normal"),
            "to": endpoint("surface", "Normal"),
        },
        {
            "from": endpoint("surface", "Closure"),
            "to": endpoint("out", "Surface"),
        },
    ]
    return {
        "material": {"name": "PortableNoise"},
        "nodes": nodes,
        "edges": edges,
        "standardPbrSemantic": {
            "slots": {
                "BaseColor": {
                    "default": None,
                    "source": endpoint("ramp", "Color"),
                },
                "Normal": {
                    "default": None,
                    "source": endpoint("bump", "Normal"),
                },
            }
        },
    }


def wireframe_graph() -> dict:
    return {
        "material": {"name": "Wire"},
        "nodes": [
            {"id": "out", "op": "Output.Material"},
            {
                "id": "surface",
                "op": "Shader.PrincipledBSDF",
                "inputs": [{"id": "Alpha", "default": 1.0}],
                "outputs": [{"id": "Closure", "valueType": "Closure"}],
            },
            {
                "id": "wire",
                "op": "Input.Wireframe",
                "inputs": [{"id": "Size", "default": 0.1}],
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
                "from": endpoint("wire", "Fac"),
                "to": endpoint("surface", "Alpha"),
            },
            {
                "from": endpoint("surface", "Closure"),
                "to": endpoint("out", "Surface"),
            },
        ],
        "standardPbrSemantic": {
            "slots": {
                "Alpha": {
                    "default": None,
                    "source": endpoint("wire", "Fac"),
                }
            }
        },
    }


def file_ref(path: str, media_type: str = "application/json") -> dict:
    return {
        "relativePath": path,
        "sha256": "1" * 64,
        "byteLength": 1,
        "mediaType": media_type,
    }


class MeshBoundBakeSafetyTests(unittest.TestCase):
    def test_auto_preserves_portable_object_noise_and_normal_chain(self):
        first = build_material_ir(
            portable_noise_graph(),
            conversion_mode="Auto",
        )
        second = build_material_ir(
            portable_noise_graph(),
            conversion_mode="Auto",
        )
        self.assertEqual(first, second)
        ops = {item["op"] for item in first["expressions"]}
        self.assertTrue(
            {
                "Input.TextureCoordinate.Object",
                "Vector.Mapping",
                "Texture.Noise.Factor",
                "Math.Multiply",
                "Math.Lerp",
                "Vector.NormalFromHeight",
            }.issubset(ops)
        )
        self.assertNotIn("Texture.SampleBaked2D", ops)
        self.assertFalse(
            any(
                item.get("severity") == "error"
                for item in first["diagnostics"]
            )
        )
        plan = ConversionPlanner().plan(first, mode="Auto")
        self.assertEqual([], plan["bakeJobs"])

    def test_strict_rejects_runtime_noise_approximation(self):
        ir = build_material_ir(
            portable_noise_graph(),
            conversion_mode="Auto",
            fidelity_policy="Strict",
        )
        self.assertTrue(
            any(
                item.get("code") == "MIKU_APPROXIMATION_FORBIDDEN"
                for item in ir["diagnostics"]
            )
        )

    def test_noise_color_is_not_replaced_by_pseudo_color(self):
        graph = portable_noise_graph(noise_output="Color")
        graph["standardPbrSemantic"]["slots"]["BaseColor"][
            "source"
        ] = endpoint("noise", "Color")
        auto = build_material_ir(graph, conversion_mode="Auto")
        self.assertNotIn(
            "Texture.SampleBaked2D",
            {item["op"] for item in auto["expressions"]},
        )
        self.assertTrue(
            any(
                item.get("code")
                == "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
                for item in auto["diagnostics"]
            )
        )
        source_mesh = build_material_ir(
            graph,
            conversion_mode="AllowMeshBake",
        )
        baked_channels = [
            item
            for item in source_mesh["channels"]
            if item.get("requiresBake")
        ]
        self.assertIn(
            "BaseColor",
            {item["semantic"] for item in baked_channels},
        )
        jobs = ConversionPlanner().plan(
            source_mesh,
            mode="AllowMeshBake",
        )["bakeJobs"]
        self.assertTrue(
            any(
                item.get("scope") == "Channels"
                and "BaseColor" in item.get("semantics", [])
                for item in jobs
            )
        )

    def test_wireframe_requires_source_mesh_fidelity(self):
        auto = build_material_ir(
            wireframe_graph(),
            conversion_mode="Auto",
        )
        self.assertTrue(
            any(
                item.get("code")
                == "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
                for item in auto["diagnostics"]
            )
        )
        source_mesh = build_material_ir(
            wireframe_graph(),
            conversion_mode="AllowMeshBake",
        )
        alpha = next(
            item
            for item in source_mesh["channels"]
            if item["semantic"] == "Alpha"
        )
        self.assertTrue(alpha["requiresBake"])
        jobs = ConversionPlanner().plan(
            source_mesh,
            mode="AllowMeshBake",
        )["bakeJobs"]
        self.assertTrue(
            any(
                item.get("scope") == "Channels"
                and item.get("semantics") == ["Alpha"]
                for item in jobs
            )
        )

    def test_planner_never_schedules_old_mesh_island_in_auto(self):
        source_mesh = build_material_ir(
            wireframe_graph(),
            conversion_mode="AllowMeshBake",
        )
        auto = ConversionPlanner().plan(source_mesh, mode="Auto")
        self.assertEqual([], auto["bakeJobs"])
        self.assertTrue(
            any(
                item.get("code")
                == "MIKU_SOURCE_MESH_FIDELITY_REQUIRED"
                for item in auto["diagnostics"]
            )
        )

    def test_bundle_21_seals_source_mesh_with_texture_binding(self):
        mesh_binding = {
            "kind": "MeshFingerprintSet",
            "sha256": "2" * 64,
            "meshes": [],
        }
        texture = {
            **file_ref("Baked/value.exr", "image/x-exr"),
            "id": "texture",
            "semantic": "ExpressionIsland",
            "bindingKey": "_MIKU_Baked_test",
            "expressionId": "expression",
            "usage": "Scalar",
            "channel": "R",
            "colorSpace": "Linear",
            "width": 4,
            "height": 4,
            "channelCount": 1,
            "componentBytes": 2,
            "meshBinding": copy.deepcopy(mesh_binding),
        }
        source_mesh = {
            **file_ref("SourceMesh/source.glb", "model/gltf-binary"),
            "id": "source-mesh",
            "kind": "SourceMesh",
            "semantic": "SourceMesh",
            "meshBinding": copy.deepcopy(mesh_binding),
            "rendererBindings": [
                {
                    "rendererPath": "Sphere",
                    "sourceObject": "Sphere",
                    "meshIndex": 0,
                    "materialSlots": [0],
                    "meshFingerprint": "3" * 64,
                    "sourceVertices": 8,
                    "sourcePolygons": 12,
                    "sourceUv": "UVMap",
                    "exportedVertices": 24,
                    "exportedIndices": 36,
                    "hasUv0": True,
                }
            ],
            "meshCount": 1,
            "vertexCount": 24,
            "indexCount": 36,
            "hasUv0": True,
        }
        payload = {
            "materialKey": "Wire",
            "sourceName": "Wire",
            "persistentSourceId": "source",
            "persistentMaterialId": "material",
            "targetProfileHash": default_target_profile()[
                "canonicalHash"
            ],
            "ir": file_ref("Wire.miku-ir.json"),
            "plan": file_ref("Wire.miku-plan.json"),
            "manifest": file_ref("Wire.miku-manifest.json"),
            "sourceMap": {
                **file_ref("Wire.miku-source-map.json"),
                "editorOnly": True,
            },
            "resources": [texture, source_mesh],
        }
        payload["sealedDigest"] = compute_sealed_digest(payload)
        bundle = make_document("miku-bundle-1.0", payload)
        self.assertEqual(
            bundle,
            validate_bundle_document(bundle),
        )

        legacy_payload = copy.deepcopy(payload)
        legacy_payload["resources"] = [texture]
        legacy_payload["sealedDigest"] = compute_sealed_digest(
            legacy_payload
        )
        legacy = make_document("miku-bundle-1.0", legacy_payload)
        with self.assertRaises(DocumentValidationError) as raised:
            validate_bundle_document(legacy)
        self.assertEqual(
            "MIKU_SOURCE_MESH_RESOURCE_INVALID",
            raised.exception.code,
        )


if __name__ == "__main__":
    unittest.main()
