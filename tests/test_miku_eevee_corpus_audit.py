from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import types
import unittest
from pathlib import Path
from unittest import mock


def _load_audit_module():
    module_path = (
        Path(__file__).resolve().parents[1]
        / "tools"
        / "miku_eevee_corpus_audit.py"
    )
    spec = importlib.util.spec_from_file_location(
        "miku_eevee_corpus_audit_test_module",
        module_path,
    )
    if spec is None or spec.loader is None:
        raise RuntimeError("Could not load the EEVEE corpus audit tool")
    module = importlib.util.module_from_spec(spec)
    with mock.patch.dict(sys.modules, {"bpy": types.ModuleType("bpy")}):
        spec.loader.exec_module(module)
    return module


AUDIT = _load_audit_module()


class EeveeCorpusAuditTests(unittest.TestCase):
    def test_resume_hash_ignores_export_binding_but_detects_semantic_change(self):
        material_ir = {
            "documentKind": "miku-material-ir-1.0",
            "schemaVersion": "1.0",
            "toolVersion": "1.0.3",
            "materialKey": "Material",
            "workflow": {"kind": "standard_pbr"},
            "closureGraph": {"root": {"kind": "Principled"}},
            "weightedClosures": {"terms": []},
            "surfaceModelPlan": {"kind": "OpaquePBR"},
            "surfaceContract": {"model": "StandardLit"},
            "expressions": [{"op": "Texture.SampleBaked2D"}],
            "channels": [{"semantic": "BaseColor"}],
        }
        rebound = {
            **material_ir,
            "expressions": [{"op": "Texture.SampleBaked2D", "resourceId": "r"}],
            "channels": [{"semantic": "BaseColor", "resourceId": "r"}],
        }
        changed = {
            **material_ir,
            "closureGraph": {"root": {"kind": "Emission"}},
        }

        self.assertEqual(
            AUDIT._material_ir_resume_hash(material_ir),
            AUDIT._material_ir_resume_hash(rebound),
        )
        self.assertNotEqual(
            AUDIT._material_ir_resume_hash(material_ir),
            AUDIT._material_ir_resume_hash(changed),
        )

    def test_resume_rejects_unfinalized_source_mesh_pbr_projection(self):
        material_ir = {
            "surfaceModelPlan": {
                "kind": "CustomMultiLobe",
                "renderStatePlan": {"surfaceType": "Opaque"},
                "channelPlans": [
                    {
                        "semantic": semantic,
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
            }
        }

        self.assertFalse(AUDIT._resume_finalization_valid(material_ir))
        material_ir["surfaceModelPlan"]["kind"] = "OpaquePBR"
        self.assertTrue(AUDIT._resume_finalization_valid(material_ir))

    def test_resume_index_rejects_duplicate_persistent_material_identity(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in ("one", "two"):
                target = root / name
                target.mkdir()
                (target / f"{name}.mikubundle").write_text(
                    json.dumps({"persistentMaterialId": "same-id"}),
                    encoding="utf-8",
                )

            with self.assertRaisesRegex(
                RuntimeError,
                "MIKU_CORPUS_RESUME_IDENTITY_DUPLICATE",
            ):
                AUDIT._existing_bundle_index(root)

    def test_active_or_label_policy_records_both_exclusion_reasons(self):
        status, reasons = AUDIT._scope(
            bound=True,
            active_cycles_only=True,
            cycles_labelled=True,
            cycles_policy="active-or-label",
        )

        self.assertEqual("excluded-cycles-active-and-label", status)
        self.assertEqual(
            [
                "active-eevee-chain-is-cycles-only",
                "material-or-object-name-contains-cycles",
            ],
            reasons,
        )

    def test_cycles_label_matches_material_or_bound_object_case_insensitively(self):
        self.assertTrue(AUDIT._cycles_labelled("Paint", ["Cycles Preview"]))
        self.assertTrue(AUDIT._cycles_labelled("cYcLeS Glass", []))
        self.assertFalse(AUDIT._cycles_labelled("Eevee Glass", ["Sphere"]))

    def test_sample_selection_is_bounded_deterministic_and_scope_safe(self):
        records = [
            {
                "blend": "b.blend",
                "material": "Full",
                "objects": ["B"],
                "scopeStatus": "supported",
                "recommendedMode": "FullPBRBake",
                "capability": {"quality": "RequiresSourceMeshFidelity"},
                "features": ["file:b.blend", "op:Texture.Magic"],
                "export": {"status": "not-requested"},
            },
            {
                "blend": "a.blend",
                "material": "Native",
                "objects": ["A"],
                "scopeStatus": "supported",
                "recommendedMode": "Auto",
                "capability": {"quality": "NativeOrEquivalent"},
                "features": ["file:a.blend", "op:Input.LightPath"],
                "export": {"status": "not-requested"},
            },
            {
                "blend": "cycles.blend",
                "material": "Cycles",
                "objects": ["C"],
                "scopeStatus": "excluded-cycles-label",
                "recommendedMode": "",
                "capability": {"quality": "NativeOrEquivalent"},
                "features": ["file:cycles.blend", "op:Texture.Brick"],
                "export": {"status": "excluded-cycles-label"},
            },
        ]

        forward = AUDIT._select_samples(records, 2)
        reverse = AUDIT._select_samples(list(reversed(records)), 2)

        self.assertEqual(forward, reverse)
        self.assertEqual(2, len(forward))
        self.assertEqual({"Native", "Full"}, {item["material"] for item in forward})
        self.assertNotIn("Cycles", {item["material"] for item in forward})


if __name__ == "__main__":
    unittest.main()
