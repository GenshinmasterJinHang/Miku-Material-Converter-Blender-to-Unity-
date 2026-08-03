import unittest

from miku.contracts import DocumentValidationError, canonical_hash, validate_document
from miku.legacy import LegacyPresetError, project_game_preset
from miku.planner import ConversionPlanner, plan_graph
from miku.semantic import build_material_ir, build_source_map


def graph_with_anisotropy():
    return {
        "material": {"name": "金属"},
        "nodes": [
            {"id": "out", "op": "Output.Material"},
            {"id": "aniso", "op": "Shader.AnisotropicBSDF", "inputs": [], "outputs": []},
            {"id": "noise", "op": "Texture.Noise", "params": {"dimensions": "3D"}},
            {"id": "bump", "op": "Vector.Bump"},
        ],
        "edges": [
            {"from": {"node": "aniso", "socket": "Closure"}, "to": {"node": "out", "socket": "Surface"}},
            {"from": {"node": "noise", "socket": "Fac"}, "to": {"node": "bump", "socket": "Height"}},
            {"from": {"node": "bump", "socket": "Normal"}, "to": {"node": "aniso", "socket": "Normal"}},
        ],
        "diagnostics": [],
    }


class MikuSemanticCoreTests(unittest.TestCase):
    def test_ir_has_regions_not_source_node_translation(self):
        ir = build_material_ir(graph_with_anisotropy(), source_blend_id="blend", material_key="金属")
        self.assertEqual("miku-material-ir-2.0", ir["documentKind"])
        self.assertTrue(any(item["kind"] == "AnisotropicClosure" for item in ir["regions"]))
        self.assertTrue(any(item["kind"] == "OpaqueSemanticRegion" for item in ir["regions"]))
        self.assertFalse(any("bl_idname" in item for item in ir["regions"]))
        validate_document(ir)

    def test_anisotropy_and_mesh_dependency_are_planned_explicitly(self):
        ir = build_material_ir(graph_with_anisotropy(), material_key="金属")
        plan = ConversionPlanner().plan(ir)
        self.assertTrue(any(item["backend"] == "CustomMultiLobeSurfaceModelBackend" for item in plan["regions"]))
        self.assertFalse(plan["bakeJobs"])

    def test_source_map_is_opaque_replay_data(self):
        source_map = build_source_map(graph_with_anisotropy(), source_blend_id="blend", material_key="金属")
        validate_document(source_map, "miku-blender-source-map-1.0")
        self.assertIn("regionBindings", source_map)
        self.assertNotIn("bpyObject", canonical_hash(source_map))

    def test_invalid_hash_is_rejected(self):
        ir = build_material_ir(graph_with_anisotropy(), material_key="金属")
        ir["canonicalHash"] = "0" * 64
        with self.assertRaises(DocumentValidationError):
            validate_document(ir)

    def test_hsr_requires_nested_companion_schema(self):
        payload = {"schema": "migr-preset-2.0", "preset": {"id": "hsr", "version": "1.1"}, "materials": []}
        with self.assertRaises(LegacyPresetError):
            project_game_preset(payload)
        payload["hsrToonPreset"] = {"schema": "hsr-toon-1.1", "workflow": "HSRToon"}
        self.assertEqual("hsr", project_game_preset(payload)["family"])

    def test_normal_miku_payload_does_not_enter_legacy_island(self):
        self.assertIsNone(project_game_preset({"schema": "miku-material-ir-1.0", "preset": {"id": "hsr"}}))

    def test_planner_handles_list_source_semantics_and_stable_parameter_identity(self):
        graph = {
            "material": {"name": "Mesh"},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {"id": "surface", "op": "Shader.PrincipledBSDF"},
                {"id": "n1", "op": "Input.Bump"},
            ],
            "edges": [
                {
                    "from": {"node": "surface", "socket": "Closure"},
                    "to": {"node": "out", "socket": "Surface"},
                },
                {
                    "from": {"node": "n1", "socket": "Normal"},
                    "to": {"node": "surface", "socket": "Normal"},
                },
            ],
            "parameters": [{"id": "p1", "semantic": "Roughness", "default": 0.25}],
        }
        _, auto = plan_graph(graph, material_key="Mesh")
        self.assertTrue(
            any(
                region["route"] == "Unsupported"
                for region in auto["regions"]
            )
        )
        _, plan = plan_graph(
            graph,
            material_key="Mesh",
            mode="AllowMeshBake",
        )
        self.assertTrue(
            any(
                region["route"] == "MeshBake"
                for region in plan["regions"]
            )
        )
        self.assertEqual(plan["parameters"][0]["referenceName"], "_MIKU_Roughness_f64551fcd6f07823cb87")


if __name__ == "__main__":
    unittest.main()
