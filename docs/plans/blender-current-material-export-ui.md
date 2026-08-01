# Blender current-material export and simplified UI

## Purpose and outcome

The Blender 5.2 semantic exporter must export exactly the material in the
active material slot of the active object. The Shader Editor sidebar exposes
one material-level workflow, conditionally exposes game-part selection, hides
persistent source identity, and keeps conversion mode in a collapsed Advanced
section. Stable MiGR 1.0 bundle identities remain unchanged.

## Context and constraints

- The only source workspace for MiGR 1.0 is this staging repository. The legacy
  `项目4` workspace must not be modified.
- The worktree already contains the larger MiGR 1.0 cutover. Changes for this
  task must preserve that work and stay focused on Blender UI/export behavior,
  tests, release artifacts, and user documentation.
- Supported versions remain Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0, and
  Shader Graph 17.4.0.
- `persistentSourceId` and `persistentMaterialId` remain required MiGR 1.0
  bundle fields. Unity schemas and import behavior are out of scope.
- `export_selected_materials` remains available for automation. Only the
  Blender panel operator changes to single-material export.
- The old `migr_workflow` enum stored `inherit` before the five explicit
  workflows. Re-registering the same property with a shifted item list can
  reinterpret saved enum indices, so the old property must remain hidden as
  migration data while a new explicit property drives the UI.
- Blender extensions are installed as ZIP files. No Blender `Scripts`
  directory is modified.

## Progress

- [x] 2026-07-27: Confirmed the staging workspace, dirty-worktree scope, exact
  supported versions, current Blender panel, batch export API, identity
  registry, and relevant documentation.
- [x] 2026-07-27: Implemented current-slot selection, source identity automation, workflow
  migration, conditional game-part UI, and collapsed Advanced UI.
- [x] 2026-07-27: Added focused Python tests for current-slot isolation, invalid contexts,
  identity stability, workflow migration, and conditional serialization.
- [x] 2026-07-27: Updated English and Chinese documentation, the UI screenshot,
  architecture notes, migration notes, and changelog.
- [x] 2026-07-27: Built deterministic extension ZIPs and passed Python validation.
- [ ] Validate in Blender 5.2 and run a fresh 金10 Blender-to-Unity regression where
  the required applications and source asset are available.
- [x] 2026-07-27: Recorded results, limitations, and blocked validation below.

## Discoveries

- `migr_blender.export_selected_materials` currently scans every object in
  `bpy.data.objects`; the panel operator calls it directly.
- The panel currently requires a visible scene `source_id`, displays both
  scene `default_workflow` and material `migr_workflow`, and always displays
  `migr_workflow_part`.
- The bundle writer already omits `workflow.part` for `standard_pbr` and
  `generic_toon`; this behavior can be retained.
- `.migr-identities.json` and the bundle contract already derive stable
  material identities from persistent source identity and material name.
- The MIT extension ZIP includes the repository `migr_blender` package
  directly, so no duplicate extension implementation is needed.
- Blender `BlendData` exposes mapping-like methods but rejects ID properties at
  runtime. A hidden source identity therefore cannot be stored on `bpy.data`;
  it is stored in one canonical Scene ID property while the identity seed still
  uses `bpy.data.filepath`.
- Blender forbids writing Material ID data during `Panel.draw()`. Legacy
  workflow migration must preview the resolved value during draw and persist it
  through a zero-delay Blender timer, or synchronously during export.
- The local exact Unity target is connected and healthy at `6000.4.5f1`.
  Blender 5.2 is not installed in the discovered local locations; Blender
  4.2.13 Goo Engine was available only for compatibility smoke testing.
- The locked 金1 source exists only in the untouched legacy workspace. Its
  current SHA-256 is
  `263dc1950cbce79b241a44cb7b1c9a860f80b8e1451bba23427c08d38f2190ef`,
  which does not match the provenance record
  `7cd6df4f2f65cf809d1fb9a9fe8829e569318ed928f747de7179fcbc8c500d94`.
  A fresh certified re-export must not proceed until that discrepancy is
  resolved.

## Decision log

- 2026-07-27: Keep `export_selected_materials` behavior and signature for
  automation compatibility. Add a separate current-material entry point for
  the panel.
- 2026-07-27: Keep the old scene `default_workflow`, scene `source_id`, and
  material `migr_workflow` properties registered but hidden. They exist only
  to recover saved settings and do not participate in new exports after
  migration.
- 2026-07-27: Add a new five-value material workflow property. A hidden
  per-material migration marker prevents scene defaults from affecting future
  exports after the first resolution.
- 2026-07-27: Saved blend source identity is UUIDv5 over a normalized absolute
  blend path. Unsaved blends receive one UUIDv4 stored on `bpy.data` for the
  current session and emit a warning recommending a save before long-term use.
- 2026-07-27: Shader Editor pinning to a different material is treated as an
  invalid active-material context; export remains strictly tied to the active
  object slot.
- 2026-07-27 correction: runtime evidence showed that `bpy.data` cannot hold ID
  properties. The UUID is stored in one canonical Scene ID property instead.
  This preserves one identity across scene switches and persists it in the
  blend without exposing it in the UI.
- 2026-07-27: First-display workflow migration is deferred through
  `bpy.app.timers` because Blender prohibits material writes inside
  `Panel.draw()`. Export performs the same migration synchronously and reports
  failures through the operator.

## Implementation sequence

1. Add pure helpers for active-slot validation, source identity creation, and
   legacy workflow migration.
2. Add `export_current_material` without changing the batch API.
3. Replace panel/operator behavior and labels while retaining hidden migration
   properties.
4. Add focused unit tests that use lightweight Blender-like fakes and mocks.
5. Update canonical English docs, Chinese installation guidance, changelog, and
   extension README.
6. Build both extension ZIPs and validate deterministic packaging.
7. Run Blender and Unity acceptance checks when the local applications and 金10
   source mapping are available.

## Validation

Planned commands:

- `python -m unittest tests.test_migr_blender_frontend tests.test_migr_workflows`
- `python tools/build_migr_blender_extensions.py`
- `python -m unittest discover -s tests -p "test_migr*.py"`
- repository formatter/linter commands discovered from `pyproject.toml` or CI
  tooling
- Blender 5.2 clean-profile extension install and scripted current-slot smoke
  test
- 金10 export into the Unity 6000.4.5f1 project followed by EditMode/import,
  shader compilation, variant preservation, repeated import, and render checks

Expected results:

- One panel export creates one material directory and one `.migrbundle`.
- Switching `active_material_index` switches the target without scanning other
  objects or slots.
- Invalid context states cancel with specific diagnostics.
- Repeated exports retain source/material identities.
- Standard/Generic workflows omit `workflow.part`; game workflows include the
  selected part.
- Both Blender extension ZIPs build byte-deterministically.

## Results and follow-up

- **passed:** `py -3.13 tools/ci/run_checks.py` parsed 47 Python files, validated
  9 schemas, passed 73 tests, verified Unity package identity, and built both
  Blender ZIPs plus the Unity package.
- **passed:** `uvx ruff check` and `uvx ruff format --check` passed for all
  changed Python sources.
- **passed:** focused frontend/workflow tests passed 21/21, including strict
  active-slot isolation, scene switching, identity stability, old workflow
  migration, batch API behavior, and workflow-part serialization.
- **passed (compatibility smoke only):** Blender 4.2.13 Goo Engine registered
  the real RNA properties and exported only the active material through a
  patched file boundary. The actual UI opened without draw errors and produced
  the checked-in updated screenshot. This does not establish Blender 5.2
  support.
- **passed:** deterministic release archives were rebuilt. Semantic Exporter
  SHA-256 is
  `9ba7037b096dcc8ddc89d994fd8c634c91f03194d1280253a11169bd480352c3`;
  GPL Bake Worker SHA-256 is
  `bb06b16b01a0576894bfebe8eef055123fecf405970e65d6362b14317f6f957c`.
- **passed:** Unity `6000.4.5f1` EditMode tests passed 16/16. The existing 金10
  bundle was force-reimported twice as `MiGRBundleAsset`; its user Material,
  wrapper Shader Graph, and generated Sub Graph hashes remained byte-identical.
  The committed receipt reports `standard_pbr`, `shaderCompiled: true`, no
  compiler messages, valid asset references, and valid texture bindings.
- **observed unrelated warning:** Unity Console contains only a Unity Connect
  token-exchange exception, not a MiGR import or shader error.
- **blocked:** a clean Blender 5.2 installation test, installation of both ZIPs
  through Blender 5.2 Extensions, and a fresh 金1 to 金10 export were not
  executed because Blender 5.2 was not found and the current source hash
  disagrees with locked provenance.
- **not rerun:** the existing 金10 render evidence was inspected and remains a
  metallic gold result, but no new render was claimed for this UI-only change.

Schema and Unity importer behavior are unchanged. The public panel behavior
changes, while the `migr.export_materials` operator ID and
`export_selected_materials` batch API remain available. Follow-up outside this
task is limited to supplying the exact Blender 5.2 runtime and resolving the
金1 source provenance mismatch before certifying a fresh end-to-end export.
