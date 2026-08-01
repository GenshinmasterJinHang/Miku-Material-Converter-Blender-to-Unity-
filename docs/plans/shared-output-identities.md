# Support Multiple Sources in One Export Root

This ExecPlan is a living document. Keep `Progress`, `Discoveries`, `Decision log`,
and `Results and follow-up` current while the work proceeds.

## Purpose

MiGR currently treats `.migr-identities.json` in an export root as exclusive
ownership by one Blender source. That rejects a normal workflow where several
`.blend` files export into the same root. After this change, source and material
identity live primarily in Blender custom properties, bundles from different
sources coexist safely, renames reuse existing outputs, and Unity preserves the
stable asset GUIDs attached to the `(persistentSourceId, persistentMaterialId)`
pair.

The observable success case is that two Blender files with the same material
name can export and import into the same roots without overwriting one another,
while repeated export/import and material rename continue to address the
original generated assets.

## Context

The Blender entry point is `migr_blender/__init__.py`. It currently obtains a
hidden scene source ID, but `_material_identities` stores material IDs in the
root-level `.migr-identities.json` and raises
`MIGR_SOURCE_ID_REGISTRY_MISMATCH` for another source. Bundle directories are
currently named only after the material.

The Unity entry point is
`unity/Packages/com.migr.shaderconverter/Editor/MiGRBundleImporter.cs`. It
currently selects `outputRoot/sourceName`, then writes a per-material
`*.migr-assets.json`. Stable Unity GUIDs already derive from source ID, material
ID, and asset role and must not change.

The supported compatibility tuple remains Blender 5.2.0 LTS, Unity 6000.4.5f1,
URP 17.4.0, and Shader Graph 17.4.0. The bundle schema remains
`migr-bundle-1.0`.

## Progress

- [x] Located the authoritative staging repository and the active Blender and
  Unity installations.
- [x] Read the repository constitution, planning format, relevant architecture
  and compatibility documents, implementations, tests, and current Git diff.
- [x] Implement Blender-owned source/material identities, legacy registry
  migration, collision-safe directories, identity lookup, and source forking.
- [x] Implement Unity identity lookup, collision-safe new directories, stable
  path/GUID reuse, and conflict diagnostics.
- [x] Add Blender and Unity regression tests.
- [x] Update public documentation and changelogs.
- [x] Run Python tests, package builds, Blender headless tests, Unity compilation
  and EditMode tests, then inspect the final diff.
- [x] Synchronize verified artifacts to the active Blender extension and Unity
  package.

## Discoveries

- The checked-out work at `C:\Users\22687\Desktop\项目4.migr-staging` is the
  authoritative MiGR 1.0 source; `C:\Users\22687\Desktop\项目4` contains an
  earlier worktree.
- The current Blender source already stores a hidden source ID on a canonical
  Scene and supports a session-only ID for unsaved files. The remaining root
  lock is isolated in `_material_identities`.
- Unity already writes a per-material identity document and derives GUIDs from
  the stable identity pair, so its GUID algorithm need not change.
- Existing Unity transactions copy the whole material directory before writes
  and can restore it on failure. Identity-aware directory selection must happen
  before that transaction begins.
- Existing Unity identity documents also record user-owned Wrapper Graph and
  Material Variant paths. Reusing only the directory would still create
  renamed duplicates, so import now reuses validated recorded role paths as
  well.
- Unity EditMode tests require a single-mode temporary Scene rather than an
  additive Scene because the Test Runner can begin with an unsaved untitled
  Scene.

## Decision log

- Store the source ID on a canonical Scene and each material ID on the Blender
  Material custom property. Use session caches only if a data-block is not
  writable.
- Treat `.migr-identities.json` as read-only legacy input. Adopt a legacy
  material ID only when its source ID matches; malformed or foreign registries
  emit warnings and never block.
- Use `SanitizedMaterialName__<12-character material ID>` only for new
  directories. Search existing bundles/Unity identity documents first and
  preserve an existing directory and asset paths when the identity pair
  matches.
- Refuse to write only when the exact chosen directory already belongs to a
  different identity or when multiple directories claim the same identity.
- Forking a source creates a new source ID and new material IDs. This changes
  future Unity GUIDs by design and is exposed as an explicit confirmed Blender
  operation.
- Keep all MiGR bundle schemas and the Unity stable GUID function unchanged.

## Implementation sequence

1. Add bounded, path-safe identity readers and target directory resolution to
   the Blender exporter.
2. Replace registry-owned material identity generation with Blender Material
   custom properties, duplicate repair, legacy migration, and session fallback.
3. Add the Fork Source Identity operator and surface non-blocking persistence or
   migration warnings.
4. Add Unity lookup of existing `*.migr-assets.json` documents by source and
   material IDs, preserve recorded asset paths on rename, and namespace new
   directories.
5. Add focused tests at the Blender unit layer and Unity EditMode layer,
   including conflict and rollback cases.
6. Update architecture, compatibility, user, migration, and changelog
   documentation.
7. Build and validate packages, then synchronize the verified implementation to
   the active test installations.

## Validation

Run:

    python -m pytest
    python tools/build_migr_blender_extensions.py
    python tools/build_migr_unity_package.py

Run the repository Blender headless test command documented by the build/test
scripts. In the active Unity 6000.4.5f1 editor, wait for compilation, inspect
console errors, and run the MiGR EditMode tests. Add focused tests that prove:

- different sources and same-name materials coexist;
- re-export and rename reuse the same identity directory;
- foreign, missing, and corrupt legacy registries do not block;
- duplicate Material IDs and explicit source forks produce new identities;
- Unity reuses stable GUIDs and recorded user-owned asset paths;
- conflicting directories fail with both identities in the diagnostic; and
- failed writes/imports restore the previous directory.

## Results and follow-up

Implementation is complete.

- `py -3.13 tools/ci/run_checks.py --profile pr` passed: 48 Python files
  parsed, 9 schemas validated, 87 unit tests passed, package identity matched,
  and both Blender ZIPs plus the Unity TGZ built.
- Blender 5.2.0 LTS build `fbe6228777e7` passed
  `migr_current_material_frontend_smoke.py` and
  `migr_installed_extension_identity_smoke.py`.
- The real `材质库/石头/彩色镀层/彩色镀层.blend` corpus exported 13 Bundles in
  explicit `AppearanceSnapshot` mode while a foreign
  `.migr-identities.json` remained byte-identical. The ordinary `Auto` route
  now advances past registry handling but correctly stops on the material's
  existing view/camera/time-dependent bake limitation.
- Unity 6000.4.5f1 compiled with no Console errors. All 21
  `MiGRBundleImporterTests` EditMode tests passed, including same-name sources,
  rename reuse, legacy directories, conflicts, transaction recovery, stable
  GUIDs, Material Variants, Prefabs, and Scenes.
- Repeated package builds were byte-deterministic:
  Semantic Exporter ZIP
  `fdf53a3d24346ef476716c563339dbd575d8034054db9f6a9b2093f8e8b7fbf2`,
  GPL Worker ZIP
  `bb06b16b01a0576894bfeb8eef055123fecf405970e65d6362b14317f6f957c`,
  and Unity TGZ
  `c013cfc895fe4c355bd432d2169beacb5d8767b254e0035da908eba91966b916`.
- The verified Blender extension was synchronized to both the Blender 5.2 user
  extension directory and the Steam portable extension directory. The active
  Unity test project's embedded package source, tests, README, and changelog
  were synchronized and hash-checked.

The standalone Ruff module was not installed in either available Python
runtime, so a direct `ruff check` was not executed. The repository PR profile's
Python parsing/correctness checks and `compileall` passed. Human visual render
review remains outside this storage/identity change; the compatibility status
therefore remains Experimental.

The only follow-up outside this task is improving the default `Auto` semantic
route for view/camera/time-dependent materials. Users can already export those
materials by explicitly authorizing `AppearanceSnapshot`; this limitation is
independent of shared output identity.
