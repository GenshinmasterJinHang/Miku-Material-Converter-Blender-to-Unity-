import unittest
import zipfile
from pathlib import Path

from miku.contracts import DocumentValidationError, validate_document
from miku.semantic import WORKFLOW_KINDS, build_material_ir


ROOT = Path(__file__).resolve().parents[1]


class MikuWorkflowTests(unittest.TestCase):
    @staticmethod
    def _graph(workflow):
        return {
            "material": {"name": "WorkflowFixture"},
            "workflow": {"kind": workflow},
            "nodes": [
                {"id": "out", "op": "Output.Material"},
                {
                    "id": "surface",
                    "op": "Shader.PrincipledBSDF",
                    "inputs": [],
                },
            ],
            "edges": [
                {
                    "from": {"node": "surface", "socket": "Closure"},
                    "to": {"node": "out", "socket": "Surface"},
                }
            ],
            "standardPbrSemantic": {"slots": {}},
        }

    def test_five_current_workflows_are_concrete_and_valid(self):
        self.assertEqual(5, len(WORKFLOW_KINDS))
        for workflow in sorted(WORKFLOW_KINDS):
            with self.subTest(workflow=workflow):
                document = build_material_ir(self._graph(workflow))
                self.assertEqual(workflow, document["workflow"]["kind"])
                validate_document(document, "miku-material-ir-2.0")

    def test_generic_workflow_is_explicitly_retired(self):
        with self.assertRaisesRegex(ValueError, r"MIKU_WORKFLOW_RETIRED:generic_toon"):
            build_material_ir(self._graph("generic_toon"))

    def test_material_ir_2_schema_rejects_generic_without_execution_fallback(self):
        document = build_material_ir(self._graph("standard_pbr"))
        document["workflow"] = {"kind": "generic_toon"}
        from miku.contracts import canonical_hash

        document["canonicalHash"] = canonical_hash(
            {key: value for key, value in document.items() if key != "canonicalHash"}
        )
        with self.assertRaises(DocumentValidationError) as raised:
            validate_document(document, "miku-material-ir-2.0")
        self.assertEqual("MIKU_WORKFLOW_INVALID", raised.exception.code)

    def test_unknown_workflow_is_rejected_before_export(self):
        with self.assertRaises(ValueError):
            build_material_ir(self._graph("guess_from_unity"))

    def test_root_version_field_is_rejected(self):
        document = build_material_ir(self._graph("standard_pbr"))
        document["version"] = "miku-4.0"
        with self.assertRaises(DocumentValidationError) as raised:
            validate_document(document)
        self.assertEqual("MIKU_LEGACY_VERSION_FIELD", raised.exception.code)

    def test_blender_exporter_uses_mikubundle_suffix(self):
        source = (ROOT / "miku_blender" / "__init__.py").read_text(encoding="utf-8")
        self.assertIn('f"{asset_name}.mikubundle"', source)
        self.assertNotIn(".mikubundle.json", source)

    def test_mit_client_invokes_gpl_worker_only_through_operator(self):
        source = (ROOT / "miku_blender" / "bake_client.py").read_text(
            encoding="utf-8"
        )
        self.assertIn('"miku_gpl"', source)
        self.assertIn('"execute_bake_request"', source)
        self.assertNotIn("importlib", source)
        self.assertNotIn("extensions.miku_gpl_bake_worker", source)

    def test_single_extension_archive_is_deterministic_and_complete(self):
        from tools.build_miku_blender_extensions import build

        first = build()
        first_bytes = first.read_bytes()
        second = build()
        self.assertEqual(first_bytes, second.read_bytes())
        self.assertEqual("miku_shader_converter-3.0.0.zip", first.name)
        with zipfile.ZipFile(first) as archive:
            names = set(archive.namelist())
            self.assertIn("bake_worker/automatic_bake.py", names)
            self.assertIn("miku_blender/__init__.py", names)
            self.assertIn("miku/bake_protocol.py", names)
            self.assertIn("miku/contracts.py", names)
            self.assertIn("LICENSE-MIT-ORIGIN.txt", names)
            self.assertIn("LICENSE.txt", names)
            manifest = archive.read("blender_manifest.toml").decode("utf-8")
            self.assertIn("SPDX:GPL-3.0-or-later", manifest)
            self.assertIn('blender_version_min = "5.0.0"', manifest)
            self.assertIn('blender_version_max = "5.3.0"', manifest)


if __name__ == "__main__":
    unittest.main()
