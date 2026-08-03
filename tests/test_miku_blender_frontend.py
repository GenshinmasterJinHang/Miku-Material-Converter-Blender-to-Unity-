from __future__ import annotations

import inspect
import json
import os
import tempfile
import unittest
import uuid
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import miku_blender
from miku.contracts import make_document
from miku_blender.translations import TRANSLATIONS


class FakeMaterial(dict):
    def __init__(
        self,
        name: str,
        *,
        legacy_workflow: str = "inherit",
        workflow_kind: str = "standard_pbr",
        workflow_part: str = "Body",
    ) -> None:
        super().__init__()
        self.name = name
        self.miku_workflow = legacy_workflow
        self.miku_workflow_kind = workflow_kind
        self.miku_workflow_part = workflow_part
        self.node_tree = SimpleNamespace(nodes=[], links=[])


class ReadOnlyMaterial(FakeMaterial):
    def __setitem__(self, key, value):
        raise RuntimeError("read-only material")


class ReadOnlyProperties(dict):
    def __setitem__(self, key, value):
        raise RuntimeError("read-only ID properties")


class FakeData(dict):
    def __init__(self, filepath: str = "", materials: list[FakeMaterial] | None = None) -> None:
        super().__init__()
        self.filepath = filepath
        self.scenes = []
        self.materials = list(materials or [])


class StrictSlots:
    def __init__(self, materials: list[FakeMaterial | None]) -> None:
        self.materials = materials
        self.read_indices: list[int] = []

    def __len__(self) -> int:
        return len(self.materials)

    def __getitem__(self, index: int) -> SimpleNamespace:
        self.read_indices.append(index)
        return SimpleNamespace(material=self.materials[index])

    def __iter__(self):
        raise AssertionError("current-material export must not scan material slots")


def make_context(
    slots: StrictSlots | None,
    *,
    active_index: int = 0,
    settings: SimpleNamespace | None = None,
    space_data: SimpleNamespace | None = None,
) -> SimpleNamespace:
    obj = None
    if slots is not None:
        obj = SimpleNamespace(
            material_slots=slots,
            active_material_index=active_index,
        )
    return SimpleNamespace(
        object=obj,
        scene=SimpleNamespace(
            miku_settings=settings
            or SimpleNamespace(
                default_workflow="standard_pbr",
                mode="Auto",
            )
        ),
        space_data=space_data
        or SimpleNamespace(
            tree_type="ShaderNodeTree",
            shader_type="OBJECT",
            pin=False,
        ),
    )


class CurrentMaterialSelectionTests(unittest.TestCase):
    def test_only_active_slot_is_read_and_exported(self):
        first = FakeMaterial("First")
        active = FakeMaterial("Active", legacy_workflow="wuwa_toon")
        slots = StrictSlots([first, active])
        context = make_context(slots, active_index=1)
        data = FakeData("C:/projects/character.blend")

        with tempfile.TemporaryDirectory() as temporary:
            with patch.object(
                miku_blender,
                "export_material_bundle",
                return_value={"materialKey": "Active"},
            ) as exporter:
                result = miku_blender.export_current_material(
                    context,
                    temporary,
                    data=data,
                )

        self.assertEqual([1], slots.read_indices)
        self.assertEqual("Active", result["materialKey"])
        self.assertFalse(result["sourceIdentityTemporary"])
        args, kwargs = exporter.call_args
        self.assertIs(active, args[0])
        self.assertEqual("wuwa_toon", kwargs["workflow_kind"])
        self.assertEqual("Body", kwargs["workflow_part"])
        self.assertEqual(1024, kwargs["bake_resolution"])

    def test_switching_active_slot_switches_material_and_workflow(self):
        first = FakeMaterial("First", legacy_workflow="standard_pbr")
        second = FakeMaterial(
            "Second",
            legacy_workflow="hsr_toon",
            workflow_part="Hair",
        )
        slots = StrictSlots([first, second])
        context = make_context(slots, active_index=0)
        data = FakeData("C:/projects/character.blend")

        with tempfile.TemporaryDirectory() as temporary:
            with patch.object(
                miku_blender,
                "export_material_bundle",
                side_effect=lambda material, *_args, **_kwargs: {"materialKey": material.name},
            ) as exporter:
                first_result = miku_blender.export_current_material(
                    context,
                    temporary,
                    data=data,
                )
                context.object.active_material_index = 1
                second_result = miku_blender.export_current_material(
                    context,
                    temporary,
                    data=data,
                )

        self.assertEqual(
            ["First", "Second"],
            [
                first_result["materialKey"],
                second_result["materialKey"],
            ],
        )
        self.assertEqual("standard_pbr", exporter.call_args_list[0].kwargs["workflow_kind"])
        self.assertEqual("hsr_toon", exporter.call_args_list[1].kwargs["workflow_kind"])
        self.assertEqual("Hair", exporter.call_args_list[1].kwargs["workflow_part"])

    def test_invalid_active_material_states_have_specific_diagnostics(self):
        cases = (
            (make_context(None), "No active object"),
            (make_context(StrictSlots([])), "no material slots"),
            (make_context(StrictSlots([None])), "active material slot is empty"),
            (
                make_context(
                    StrictSlots([FakeMaterial("Material")]),
                    space_data=SimpleNamespace(
                        tree_type="ShaderNodeTree",
                        shader_type="WORLD",
                        pin=False,
                    ),
                ),
                "Object materials",
            ),
        )
        for context, expected in cases:
            with self.subTest(expected=expected):
                material, diagnostic = miku_blender._active_material_slot_state(context)
                self.assertIsNone(material)
                self.assertIn(expected, diagnostic)

    def test_pinned_editor_must_match_active_slot_material(self):
        active = FakeMaterial("Active")
        other = FakeMaterial("Pinned")
        context = make_context(
            StrictSlots([active]),
            space_data=SimpleNamespace(
                tree_type="ShaderNodeTree",
                shader_type="OBJECT",
                pin=True,
                id=other,
            ),
        )
        material, diagnostic = miku_blender._active_material_slot_state(context)
        self.assertIsNone(material)
        self.assertIn("different material", diagnostic)


class HiddenIdentityTests(unittest.TestCase):
    def test_existing_hidden_identity_is_reused_verbatim(self):
        data = FakeData("C:/projects/saved.blend")
        data["miku_source_id"] = "maintainer-stable-source"
        self.assertEqual(
            ("maintainer-stable-source", False),
            miku_blender._ensure_persistent_source_id(data),
        )

    def test_saved_blend_uses_path_derived_uuid5_and_persists_it(self):
        first = FakeData("C:/projects/saved.blend")
        second = FakeData("C:/projects/saved.blend")
        first_id, first_temporary = miku_blender._ensure_persistent_source_id(first)
        second_id, second_temporary = miku_blender._ensure_persistent_source_id(second)
        self.assertEqual(first_id, second_id)
        self.assertEqual(5, uuid.UUID(first_id).version)
        self.assertFalse(first_temporary)
        self.assertFalse(second_temporary)
        self.assertEqual(first_id, first["miku_source_id"])

    def test_unsaved_blend_identity_is_session_stable_and_warns_each_time(self):
        data = FakeData()
        first_id, first_temporary = miku_blender._ensure_persistent_source_id(data)
        second_id, second_temporary = miku_blender._ensure_persistent_source_id(data)
        self.assertEqual(first_id, second_id)
        self.assertEqual(4, uuid.UUID(first_id).version)
        self.assertTrue(first_temporary)
        self.assertTrue(second_temporary)

    def test_read_only_source_and_material_use_session_identities(self):
        data = FakeData("C:/projects/read-only.blend")
        storage = ReadOnlyProperties()
        material = ReadOnlyMaterial("Read Only")
        source_id, temporary = miku_blender._ensure_persistent_source_id(
            data,
            storage=storage,
        )
        repeated_id, repeated_temporary = miku_blender._ensure_persistent_source_id(
            data,
            storage=storage,
        )
        with tempfile.TemporaryDirectory() as temporary_root:
            identities, warnings = miku_blender._ensure_material_identities(
                [material],
                source_id,
                Path(temporary_root),
            )
        self.assertEqual(source_id, repeated_id)
        self.assertTrue(temporary)
        self.assertTrue(repeated_temporary)
        self.assertEqual(
            4,
            uuid.UUID(
                identities[miku_blender._owner_session_key(material)]
            ).version,
        )
        self.assertTrue(
            any("MIKU_MATERIAL_ID_SESSION_ONLY" in item for item in warnings)
        )

    def test_unsaved_identity_uses_one_canonical_scene_across_scene_switches(self):
        first_scene = {}
        second_scene = {}
        data = FakeData()
        data.scenes = [first_scene, second_scene]
        first_storage = miku_blender._source_identity_storage(data, first_scene)
        second_storage = miku_blender._source_identity_storage(data, second_scene)
        self.assertIs(first_scene, first_storage)
        self.assertIs(first_scene, second_storage)
        first_id, _ = miku_blender._ensure_persistent_source_id(
            data,
            storage=first_storage,
        )
        second_id, _ = miku_blender._ensure_persistent_source_id(
            data,
            storage=miku_blender._source_identity_storage(data, second_scene),
        )
        self.assertEqual(first_id, second_id)

    def test_copied_blend_identity_is_detected_without_blocking(self):
        original_scene = {}
        original = FakeData("C:/projects/original.blend")
        original.scenes = [original_scene]
        source_id, _ = miku_blender._ensure_persistent_source_id(
            original,
            storage=original_scene,
        )
        self.assertEqual(
            [],
            miku_blender._source_identity_warnings(
                original,
                original_scene,
                source_id,
            ),
        )

        copied_scene = dict(original_scene)
        copied = FakeData("C:/projects/copy.blend")
        copied.scenes = [copied_scene]
        copied_id, temporary = miku_blender._ensure_persistent_source_id(
            copied,
            storage=copied_scene,
        )
        warnings = miku_blender._source_identity_warnings(
            copied,
            copied_scene,
            copied_id,
        )
        self.assertEqual(source_id, copied_id)
        self.assertFalse(temporary)
        self.assertTrue(
            any("MIKU_SOURCE_ID_COPY_DETECTED" in item for item in warnings)
        )

    def test_repeated_export_keeps_bundle_identities_stable(self):
        material = FakeMaterial("Stable", legacy_workflow="standard_pbr")
        context = make_context(StrictSlots([material]))
        data = FakeData("C:/projects/stable.blend")

        with tempfile.TemporaryDirectory() as temporary:
            with patch.object(
                miku_blender,
                "export_material_bundle",
                return_value={"materialKey": "Stable"},
            ) as exporter:
                miku_blender.export_current_material(context, temporary, data=data)
                miku_blender.export_current_material(context, temporary, data=data)

        first = exporter.call_args_list[0].kwargs
        second = exporter.call_args_list[1].kwargs
        self.assertEqual(first["source_blend_id"], second["source_blend_id"])
        self.assertEqual(
            first["persistent_material_id"],
            second["persistent_material_id"],
        )

    def test_material_rename_keeps_material_identity(self):
        material = FakeMaterial("Before")
        with tempfile.TemporaryDirectory() as temporary:
            first, _ = miku_blender._ensure_material_identities(
                [material],
                "source",
                Path(temporary),
            )
            material.name = "After"
            second, _ = miku_blender._ensure_material_identities(
                [material],
                "source",
                Path(temporary),
            )
        key = miku_blender._owner_session_key(material)
        self.assertEqual(first[key], second[key])
        self.assertEqual(first[key], material["miku_material_id"])

    def test_copied_material_duplicate_identity_is_repaired(self):
        original = FakeMaterial("Original")
        duplicate = FakeMaterial("Original Copy")
        duplicate_id = str(uuid.uuid4())
        original["miku_material_id"] = duplicate_id
        duplicate["miku_material_id"] = duplicate_id
        with tempfile.TemporaryDirectory() as temporary:
            identities, warnings = miku_blender._ensure_material_identities(
                [original, duplicate],
                "source",
                Path(temporary),
            )
        self.assertEqual(
            duplicate_id,
            identities[miku_blender._owner_session_key(original)],
        )
        self.assertNotEqual(
            duplicate_id,
            identities[miku_blender._owner_session_key(duplicate)],
        )
        self.assertTrue(
            any("MIKU_MATERIAL_ID_DUPLICATE_REPAIRED" in item for item in warnings)
        )

    def test_matching_legacy_registry_migrates_without_rewriting_it(self):
        material = FakeMaterial("Legacy")
        legacy_id = str(uuid.uuid4())
        with tempfile.TemporaryDirectory() as temporary:
            registry_path = Path(temporary) / ".migr-identities.json"
            registry_path.write_text(
                json.dumps(
                    {
                        "schema": "miku-source-identity-registry-1.0",
                        "persistentSourceId": "source",
                        "materials": {"Legacy": legacy_id},
                    }
                ),
                encoding="utf-8",
            )
            before = registry_path.read_bytes()
            identities, warnings = miku_blender._ensure_material_identities(
                [material],
                "source",
                Path(temporary),
            )
            self.assertEqual(before, registry_path.read_bytes())
        self.assertEqual(
            legacy_id,
            identities[miku_blender._owner_session_key(material)],
        )
        self.assertEqual(legacy_id, material["miku_material_id"])
        self.assertTrue(
            any("MIKU_LEGACY_MATERIAL_ID_MIKUATED" in item for item in warnings)
        )

    def test_explicit_legacy_migration_adopts_source_and_material_ids(self):
        material = FakeMaterial("Legacy")
        data = FakeData("C:/projects/legacy.blend", [material])
        scene = {}
        data.scenes = [scene]
        legacy_id = str(uuid.uuid4())
        with tempfile.TemporaryDirectory() as temporary:
            registry_path = Path(temporary) / ".migr-identities.json"
            registry_path.write_text(
                json.dumps(
                    {
                        "schema": "miku-source-identity-registry-1.0",
                        "persistentSourceId": "legacy-source",
                        "materials": {"Legacy": legacy_id},
                    }
                ),
                encoding="utf-8",
            )
            before = registry_path.read_bytes()
            result = miku_blender.migrate_legacy_identities(
                data,
                temporary,
                current_scene=scene,
            )
            self.assertEqual(before, registry_path.read_bytes())
        self.assertEqual("legacy-source", result["persistentSourceId"])
        self.assertEqual("legacy-source", scene["miku_source_id"])
        self.assertEqual(legacy_id, material["miku_material_id"])
        self.assertEqual(1, result["materialCount"])

    def test_foreign_or_corrupt_legacy_registry_only_warns(self):
        cases = (
            '{"persistentSourceId":"other","materials":{"Material":"legacy"}}',
            "{broken",
        )
        for contents in cases:
            with self.subTest(contents=contents):
                material = FakeMaterial("Material")
                with tempfile.TemporaryDirectory() as temporary:
                    registry_path = Path(temporary) / ".migr-identities.json"
                    registry_path.write_text(contents, encoding="utf-8")
                    identities, warnings = miku_blender._ensure_material_identities(
                        [material],
                        "current",
                        Path(temporary),
                    )
                material_id = identities[miku_blender._owner_session_key(material)]
                self.assertEqual(4, uuid.UUID(material_id).version)
                self.assertTrue(warnings)

    def test_fork_source_identity_replaces_source_and_all_material_ids(self):
        first = FakeMaterial("First")
        second = FakeMaterial("Second")
        first["miku_material_id"] = "old-first"
        second["miku_material_id"] = "old-second"
        data = FakeData("C:/projects/copied.blend", [first, second])
        scene = {"miku_source_id": "old-source"}
        data.scenes = [scene]

        result = miku_blender.fork_source_identity(data, current_scene=scene)

        self.assertNotEqual("old-source", result["persistentSourceId"])
        self.assertEqual(result["persistentSourceId"], scene["miku_source_id"])
        self.assertNotEqual("old-first", first["miku_material_id"])
        self.assertNotEqual("old-second", second["miku_material_id"])
        self.assertNotEqual(first["miku_material_id"], second["miku_material_id"])
        self.assertEqual(2, result["materialCount"])


class SharedOutputDirectoryTests(unittest.TestCase):
    @staticmethod
    def write_bundle(
        directory: Path,
        source_id: str,
        material_id: str,
        *,
        file_name: str = "Material.mikubundle",
        document_kind: str = "miku-bundle-1.0",
    ) -> None:
        directory.mkdir(parents=True, exist_ok=True)
        (directory / file_name).write_text(
            json.dumps(
                {
                    "documentKind": document_kind,
                    "persistentSourceId": source_id,
                    "persistentMaterialId": material_id,
                }
            ),
            encoding="utf-8",
        )

    def test_different_sources_and_same_name_allocate_distinct_directories(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            first = miku_blender._resolve_bundle_directory(
                root,
                "Rock",
                "source-a",
                "11111111-1111-4111-8111-111111111111",
            )
            self.write_bundle(
                first,
                "source-a",
                "11111111-1111-4111-8111-111111111111",
            )
            second = miku_blender._resolve_bundle_directory(
                root,
                "Rock",
                "source-b",
                "22222222-2222-4222-8222-222222222222",
            )
        self.assertNotEqual(first, second)
        self.assertEqual("Rock__111111111111", first.name)
        self.assertEqual("Rock__222222222222", second.name)

    def test_rename_and_legacy_name_directory_are_reused_by_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            legacy = root / "Old Material"
            self.write_bundle(
                legacy,
                "source",
                "material-id",
                file_name="Material.migrbundle",
                document_kind="migr-bundle-2.2",
            )
            resolved = miku_blender._resolve_bundle_directory(
                root,
                "Renamed Material",
                "source",
                "material-id",
            )
        self.assertEqual(legacy, resolved)

    def test_miku_1_bundle_directory_is_reused_by_identity(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            existing = root / "Closure Material"
            self.write_bundle(
                existing,
                "source",
                "material-id",
                document_kind="miku-bundle-1.0",
            )
            resolved = miku_blender._resolve_bundle_directory(
                root,
                "Renamed Closure Material",
                "source",
                "material-id",
            )
        self.assertEqual(existing, resolved)

    def test_only_exact_target_conflict_blocks_and_lists_both_identities(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            requested_material = "12345678-1234-4234-8234-123456789abc"
            target = root / (
                "Rock__" + miku_blender._short_identity(requested_material)
            )
            self.write_bundle(target, "other-source", "other-material")
            with self.assertRaisesRegex(
                RuntimeError,
                "requestedSourceId=source.*existingSourceId=other-source",
            ):
                miku_blender._resolve_bundle_directory(
                    root,
                    "Rock",
                    "source",
                    requested_material,
                )

    def test_duplicate_identity_directories_are_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            self.write_bundle(root / "First", "source", "material")
            self.write_bundle(root / "Second", "source", "material")
            with self.assertRaisesRegex(
                RuntimeError,
                "MIKU_OUTPUT_IDENTITY_DUPLICATE",
            ):
                miku_blender._resolve_bundle_directory(
                    root,
                    "Material",
                    "source",
                    "material",
                )

    def test_directory_claimed_during_staging_is_not_overwritten(self):
        material_id = "44444444-4444-4444-8444-444444444444"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            candidate = root / "Rock__444444444444"

            def stage_bundle(_material, staging, **_kwargs):
                self.write_bundle(staging, "source", material_id)
                self.write_bundle(candidate, "other-source", "other-material")
                return {
                    "materialKey": "Rock",
                    "bundleFileName": "Material.mikubundle",
                }

            with patch.object(
                miku_blender,
                "_export_material_bundle_to_directory",
                side_effect=stage_bundle,
            ), patch.object(
                miku_blender,
                "_prepare_material_export",
                return_value=(
                    {"material": {"name": "Rock"}},
                    "Rock",
                    {"expressions": []},
                ),
            ):
                with self.assertRaisesRegex(
                    RuntimeError,
                    "MIKU_OUTPUT_IDENTITY_CONFLICT",
                ):
                    miku_blender.export_material_bundle(
                        FakeMaterial("Rock"),
                        temporary,
                        source_blend_id="source",
                        persistent_material_id=material_id,
                    )
            self.assertEqual(
                [("other-source", "other-material")],
                miku_blender._directory_bundle_identities(candidate),
            )

    def test_atomic_commit_failure_restores_previous_directory(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            target = root / "Material"
            staging = root / ".Material.stage"
            target.mkdir()
            staging.mkdir()
            (target / "state.txt").write_text("before", encoding="utf-8")
            (staging / "state.txt").write_text("after", encoding="utf-8")
            real_replace = os.replace
            replace_count = 0

            def fail_second_replace(source, destination):
                nonlocal replace_count
                replace_count += 1
                if replace_count == 2:
                    raise OSError("injected commit failure")
                return real_replace(source, destination)

            with patch.object(
                miku_blender.os,
                "replace",
                side_effect=fail_second_replace,
            ):
                with self.assertRaisesRegex(OSError, "injected commit failure"):
                    miku_blender._commit_material_directory(staging, target)
            self.assertEqual(
                "before",
                (target / "state.txt").read_text(encoding="utf-8"),
            )
            self.assertFalse(any("miku-backup" in item.name for item in root.iterdir()))


class WorkflowFrontendTests(unittest.TestCase):
    def test_legacy_inherit_resolves_scene_default_once(self):
        material = FakeMaterial("Legacy")
        settings = SimpleNamespace(default_workflow="genshin_toon")
        self.assertEqual(
            "genshin_toon",
            miku_blender._migrate_material_workflow(material, settings),
        )
        settings.default_workflow = "standard_pbr"
        self.assertEqual(
            "genshin_toon",
            miku_blender._migrate_material_workflow(material, settings),
        )
        self.assertEqual("genshin_toon", material.miku_workflow_kind)

    def test_explicit_legacy_workflow_and_game_part_are_preserved(self):
        material = FakeMaterial(
            "Legacy",
            legacy_workflow="hsr_toon",
            workflow_part="Face",
        )
        workflow = miku_blender._migrate_material_workflow(
            material,
            SimpleNamespace(default_workflow="standard_pbr"),
        )
        self.assertEqual("hsr_toon", workflow)
        self.assertEqual("Face", material.miku_workflow_part)

    def test_invalid_legacy_workflow_falls_back_to_standard_pbr(self):
        material = FakeMaterial("Legacy", legacy_workflow="unknown")
        self.assertEqual(
            "standard_pbr",
            miku_blender._migrate_material_workflow(
                material,
                SimpleNamespace(default_workflow="unknown"),
            ),
        )

    def test_batch_resolver_uses_new_explicit_workflow_after_migration(self):
        material = FakeMaterial("Migrated", legacy_workflow="inherit")
        miku_blender._migrate_material_workflow(
            material,
            SimpleNamespace(default_workflow="wuwa_toon"),
        )
        material.miku_workflow_kind = "generic_toon"
        with self.assertRaisesRegex(ValueError, r"MIKU_WORKFLOW_RETIRED:generic_toon"):
            miku_blender._resolved_material_workflow(material, "standard_pbr")

    def test_game_part_is_serialized_only_for_game_workflows(self):
        for workflow in ("standard_pbr",):
            with self.subTest(workflow=workflow):
                snapshot = miku_blender.snapshot_material(
                    FakeMaterial("Material"),
                    workflow_kind=workflow,
                    workflow_part="Hair",
                )
                self.assertEqual({"kind": workflow}, snapshot["workflow"])
        for workflow in miku_blender.GAME_WORKFLOWS:
            with self.subTest(workflow=workflow):
                snapshot = miku_blender.snapshot_material(
                    FakeMaterial("Material"),
                    workflow_kind=workflow,
                    workflow_part="Hair",
                )
                self.assertEqual(
                    {"kind": workflow, "part": "Hair"},
                    snapshot["workflow"],
                )

    def test_panel_hides_legacy_identity_and_scene_workflow_controls(self):
        source = inspect.getsource(miku_blender.register)
        draw_source = source.split("def draw(self, context):", 1)[1].split(
            "_REGISTERED_CLASSES", 1
        )[0]
        self.assertNotIn('layout.prop(settings, "source_id")', draw_source)
        self.assertNotIn('layout.prop(settings, "default_workflow")', draw_source)
        self.assertIn('"show_advanced"', draw_source)
        self.assertIn('"bake_texture_quality"', draw_source)
        self.assertIn("Used only when conversion schedules a bake.", draw_source)
        self.assertIn('"miku_workflow_kind"', draw_source)
        self.assertIn("if workflow in GAME_WORKFLOWS", draw_source)
        self.assertIn("MIKU_OT_fork_source_identity.bl_idname", draw_source)
        self.assertNotIn(
            "MIKU_OT_add_time_node.bl_idname",
            draw_source,
        )
        self.assertNotIn(
            "MIKU_OT_migrate_legacy_identities.bl_idname",
            draw_source,
        )

    def test_reachable_time_expression_is_rejected_before_output_root_creation(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary) / "new-output"
            with patch.object(
                miku_blender,
                "_prepare_material_export",
                return_value=(
                    {"material": {"name": "Timed"}},
                    "Timed",
                    {"expressions": [{"op": "Input.Time.Frame"}]},
                ),
            ):
                with self.assertRaisesRegex(
                    RuntimeError,
                    "MIKU_TIME_INPUT_UNSUPPORTED:Input.Time.Frame",
                ):
                    miku_blender.export_material_bundle(
                        FakeMaterial("Timed"),
                        str(output),
                        source_blend_id="source",
                        persistent_material_id="material",
                    )
            self.assertFalse(output.exists())

    def test_disconnected_time_expression_is_not_an_export_error(self):
        miku_blender._assert_no_export_time_inputs({"expressions": []})

    def test_internal_operators_remain_script_compatible_but_hidden(self):
        source = inspect.getsource(miku_blender.register)
        self.assertIn("MIKU_OT_add_time_node", source)
        self.assertIn("MIKU_OT_migrate_legacy_identities", source)
        self.assertIn('"INTERNAL", "UNDO"', source)


class BakeQualityAndTranslationTests(unittest.TestCase):
    def test_quality_presets_map_to_supported_resolutions(self):
        self.assertEqual(
            {
                "LOW_512": 512,
                "STANDARD_1024": 1024,
                "HIGH_2048": 2048,
                "ULTRA_4096": 4096,
            },
            miku_blender.BAKE_QUALITY_RESOLUTIONS,
        )
        for quality, resolution in miku_blender.BAKE_QUALITY_RESOLUTIONS.items():
            with self.subTest(quality=quality):
                self.assertEqual(
                    resolution,
                    miku_blender.bake_resolution_for_quality(quality),
                )
        with self.assertRaisesRegex(RuntimeError, "MIKU_BAKE_QUALITY_INVALID"):
            miku_blender.bake_resolution_for_quality("UNKNOWN")

    def test_bake_resolution_rewrites_jobs_and_rebuilds_only_plan_hash(self):
        plan = make_document(
            "miku-conversion-plan-1.0",
            {
                "materialKey": "Material",
                "bakeJobs": [
                    {
                        "jobId": "job-a",
                        "route": "MeshBake",
                        "resolution": 1024,
                        "samples": 16,
                    },
                    {
                        "jobId": "job-b",
                        "route": "ReusableBake",
                        "resolution": 1024,
                        "samples": 16,
                    },
                ],
            },
        )
        updated = miku_blender._apply_bake_resolution_to_plan(plan, 2048)
        self.assertEqual(plan["id"], updated["id"])
        self.assertNotEqual(plan["canonicalHash"], updated["canonicalHash"])
        self.assertEqual(
            [2048, 2048],
            [job["resolution"] for job in updated["bakeJobs"]],
        )
        self.assertEqual([16, 16], [job["samples"] for job in updated["bakeJobs"]])
        self.assertEqual(1024, plan["bakeJobs"][0]["resolution"])
        self.assertEqual(
            updated,
            miku_blender._apply_bake_resolution_to_plan(plan, 2048),
        )

    def test_simplified_chinese_catalog_covers_visible_quality_ui(self):
        catalog = TRANSLATIONS["zh_HANS"]
        expected = {
            "Advanced": "高级",
            "Bake Texture Quality": "烘焙贴图质量",
            "Low (512 × 512)": "低（512 × 512）",
            "Standard (1024 × 1024)": "标准（1024 × 1024）",
            "High (2048 × 2048)": "高（2048 × 2048）",
            "Ultra (4096 × 4096)": "超高（4096 × 4096）",
            "Export Current Material": "导出当前材质",
        }
        for source, translation in expected.items():
            with self.subTest(source=source):
                self.assertEqual(translation, catalog[("*", source)])

    def test_registration_uses_blender_translation_lifecycle(self):
        source = inspect.getsource(miku_blender)
        self.assertIn("_register_translations()", source)
        self.assertIn("_unregister_translations()", source)
        self.assertIn("bpy.app.translations.register(__name__, TRANSLATIONS)", source)


if __name__ == "__main__":
    unittest.main()
