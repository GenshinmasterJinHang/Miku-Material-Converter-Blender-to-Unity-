import unittest

from miku.planner import ConversionPlanner
from miku.semantic import build_material_ir


def endpoint(node, socket):
    return {"node": node, "socket": socket}


def height_displacement_graph(*, runtime_height=False):
    height_op = "Input.ViewDirection" if runtime_height else "Opaque.BlenderNode"
    height_socket = "Direction" if runtime_height else "Fac"
    return {
        "material": {"name": "HeightPlan"},
        "displacementPolicy": "ALWAYS_VERTEX",
        "heightChannel": {
            "policy": "ALWAYS_VERTEX",
            "sourceKind": "Bump",
            "source": endpoint("height-source", height_socket),
            "midlevel": 0.5,
            "scale": -0.2,
            "format": "OpenEXRHalf",
            "channel": "R",
            "colorSpace": "Linear",
        },
        "nodes": [
            {"id": "out", "op": "Output.Material"},
            {
                "id": "surface",
                "op": "Shader.PrincipledBSDF",
                "inputs": [],
                "outputs": [{"id": "Closure", "valueType": "Closure"}],
            },
            {
                "id": "height-source",
                "op": height_op,
                "outputs": [{"id": height_socket, "valueType": "Scalar"}],
                "source": {
                    "stableId": "height-source",
                    "blenderNodeName": "Height Source",
                    "groupPath": ["Material"],
                },
            },
            {
                "id": "displacement",
                "op": "Vector.Displacement",
                "params": {"space": "OBJECT"},
                "inputs": [
                    {"id": "Height", "valueType": "Scalar", "default": 0.0},
                    {"id": "Midlevel", "valueType": "Scalar", "default": 0.5},
                    {"id": "Scale", "valueType": "Scalar", "default": -0.2},
                ],
                "outputs": [{"id": "Result", "valueType": "Float3"}],
            },
        ],
        "edges": [
            {"from": endpoint("surface", "Closure"), "to": endpoint("out", "Surface")},
            {"from": endpoint("height-source", height_socket), "to": endpoint("displacement", "Height")},
        ],
        "standardPbrSemantic": {
            "slots": {
                "Height": {"default": None, "source": endpoint("height-source", height_socket)},
                "Displacement": {"default": None, "source": endpoint("displacement", "Result")},
            }
        },
        "surfaceSemantic": {
            "model": "StandardLit",
            "requiredChannels": ["Height", "Displacement"],
        },
    }


class HybridHeightPlanTests(unittest.TestCase):
    def test_height_is_raw_channel_and_vertex_reads_material_channel(self):
        ir = build_material_ir(
            height_displacement_graph(),
            conversion_mode="AllowMeshBake",
        )
        channels = {item["semantic"]: item for item in ir["channels"]}
        self.assertTrue(channels["Height"]["requiresBake"])
        displacement_id = channels["Displacement"]["value"]["expressionId"]
        displacement = next(
            item for item in ir["expressions"] if item["id"] == displacement_id
        )
        height_input_id = displacement["inputs"]["Height"]["expressionId"]
        height_input = next(
            item for item in ir["expressions"] if item["id"] == height_input_id
        )
        self.assertEqual("Input.MaterialChannel", height_input["op"])
        self.assertEqual("Vertex", height_input["stage"])
        self.assertEqual(0, height_input["params"]["lod"])
        self.assertFalse(
            any(
                item["op"] == "Texture.SampleBaked2D"
                and item.get("source", {}).get("nodeId") == "height-source"
                for item in ir["expressions"]
            )
        )
        jobs = ConversionPlanner().plan(ir, mode="AllowMeshBake")["bakeJobs"]
        height_job = next(item for item in jobs if item.get("scope") == "Channels")
        self.assertEqual(["Height"], height_job["semantics"])
        self.assertEqual("ALWAYS_VERTEX", height_job["displacementPolicy"])
        self.assertEqual(-0.2, height_job["heightSource"]["scale"])

    def test_runtime_height_is_rejected_before_uv_bake_submission(self):
        ir = build_material_ir(
            height_displacement_graph(runtime_height=True),
            conversion_mode="AllowMeshBake",
        )
        self.assertTrue(
            any(
                item.get("code") == "MIKU_RUNTIME_INPUT_UNSUPPORTED"
                and item.get("semantic") == "Height"
                for item in ir["diagnostics"]
            )
        )
        self.assertFalse(
            any(
                "Height" in item.get("semantics", [])
                for item in ConversionPlanner().plan(
                    ir,
                    mode="AllowMeshBake",
                )["bakeJobs"]
            )
        )

    def test_full_pbr_obeys_semantics_and_adds_only_safe_height(self):
        base_ir = {
            "id": "full-pbr",
            "materialKey": "FullPbr",
            "surfaceModelPlan": {"kind": "CustomMultiLobe"},
            "regions": [{"id": "region", "kind": "OpaqueSemanticRegion"}],
            "channels": [],
            "diagnostics": [],
        }
        without_height = ConversionPlanner().plan(
            base_ir,
            mode="FullPBRBake",
        )["bakeJobs"][0]["semantics"]
        self.assertNotIn("IOR", without_height)
        self.assertNotIn("Height", without_height)
        with_height_ir = {
            **base_ir,
            "channels": [{"semantic": "Height"}],
            "heightChannel": {"source": endpoint("height", "Fac")},
        }
        with_height = ConversionPlanner().plan(
            with_height_ir,
            mode="FullPBRBake",
        )["bakeJobs"][0]["semantics"]
        self.assertIn("Height", with_height)
        self.assertNotIn("IOR", with_height)

    def test_custom_multilobe_does_not_bake_compatibility_channels(self):
        ir = {
            "id": "custom",
            "materialKey": "Custom",
            "surfaceModelPlan": {"kind": "CustomMultiLobe"},
            "weightedClosures": {"terms": []},
            "regions": [],
            "expressions": [],
            "channels": [
                {"semantic": "BaseColor", "requiresBake": True},
                {"semantic": "IOR", "requiresBake": True},
            ],
            "diagnostics": [],
        }
        plan = ConversionPlanner().plan(ir, mode="AllowMeshBake")
        self.assertEqual([], plan["bakeJobs"])
        self.assertFalse(
            any(
                item.get("code") == "MIKU_STATIC_CHANNEL_BAKE_SCHEDULED"
                for item in plan["diagnostics"]
            )
        )


if __name__ == "__main__":
    unittest.main()
