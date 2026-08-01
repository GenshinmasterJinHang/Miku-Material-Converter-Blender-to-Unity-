# Miku 1.0 Identity Migration and Generic Toon

This ExecPlan is a living record. It follows `PLANS.md` and must be updated
whenever implementation or validation differs from the decisions below.

## Purpose and outcome

Release a Miku 1.0 toolchain that preserves safe MiGR 1.x/2.x data migration
while replacing the active product identity, package names, schemas, Blender
extension, and Unity APIs. Replace the `generic_toon` editable Shader Graph
backend with eight fixed URP semantic shaders, a material-asset builder, recipe
sidecars, independent mesh-data tools, and an original-material RenderGraph rim
feature.

The primary Toon workflow starts from explicitly selected Material assets. It
must not scan a Model Root, enumerate Renderers, provide Mesh-selection rows,
expand material slots, replace Renderer references, or generate a Prefab.

## Context and constraints

- The canonical MiGR 2.2.1 worktree contained maintainer changes before this
  plan. They were preserved and passed the PR profile on 2026-07-30 with
  Python 3.13 before identity migration.
- New canonical roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- Blender validation is restricted to
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe` and Blender 5.2.0.
- Unity validation targets Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0,
  Windows desktop. XR validation is out of scope.
- Root/core and Unity remain MIT. The combined Blender extension is distributed
  as GPL-3.0-or-later and retains notices for MIT-origin source.
- Legacy MiGR formats are read-only compatibility inputs. New writes use Miku
  names and Miku 1.0 documents.

## Progress

- [x] 2026-07-30: Preflighted all canonical MiGR 1.x repository markers.
- [x] 2026-07-30: Recorded the dirty MiGR 2.2.1 baseline and passed
  `py -3.13 tools/ci/run_checks.py --profile pr` (179 tests).
- [x] 2026-07-30: Confirmed connected Unity 6000.4.5f1 instance and URP 17.4
  project readiness.
- [x] 2026-07-30: Renamed active product/source/package identities to Miku
  1.0 while retaining the pre-existing 2.2.1 changes.
- [x] 2026-07-30: Added Miku 1.0 schemas and explicit, hash-first MiGR legacy
  normalization.
- [x] 2026-07-30: Merged Blender exporter and bake worker into one
  deterministic GPL-3.0-or-later extension.
- [x] 2026-07-30: Replaced `generic_toon` generation with the fixed eight
  semantic Shader family and removed its Wrapper/Sub Graph output path.
- [x] 2026-07-30: Added material recipes, Material Builder, custom Shader GUI,
  Mesh-data tools, and the opt-in RenderGraph Screen Rim feature/installer.
- [x] 2026-07-30: Removed the automatic source-mesh/Prefab/Renderer-assignment
  import path; legacy source-mesh records now produce an explicit diagnostic.
- [x] 2026-07-30: Completed Python, Blender, Unity EditMode, actual
  Blender-to-Unity import, determinism, and migration validation. The only
  skipped Unity test requires a real D3D11 graphics device.
- [x] 2026-07-31: Locked the follow-up Editor UX: top-level `Miku` menus,
  dedicated smooth-normal and vertex-color windows, a combined Mesh entry,
  and an explicit Screen Rim installer with open-only material shortcuts.
- [x] 2026-07-31: Remove the temporary 73-material corpus menu, rebuild the
  unchanged-version Unity package, and validate the installed project.
- [x] 2026-07-31: Repaired the combined extension's cold-start package-relative
  imports, rebuilt and reinstalled the deterministic ZIP, and repeated actual
  Blender-to-Unity magic-ball import validation for three materials.

## Discoveries

- The 2.2.1 baseline has 489 tracked files; 354 tracked paths contain MiGR/MGIR
  identity text.
- `MiGRBundleImporter` currently assumes every workflow emits wrapper and
  subgraph assets. The backend contract must explicitly support material-only
  output.
- Stable Unity asset IDs derive from persistent source/material IDs and role
  strings. Those inputs must remain unchanged during identity migration.
- The existing generic Toon shader already consumes smooth normals from
  TEXCOORD7, but no canonical mesh generator exists.
- The connected renderer data currently contains only URP SSAO; adding Miku's
  feature must therefore be explicit and deduplicated.
- The old Bundle 2.1 importer instantiated a GLB, assigned materials to its
  Renderers, and emitted a Prefab. Keeping that path, even behind a legacy
  bundle, contradicted the approved material-only workflow, so it was removed
  rather than hidden in the new UI.
- Unity's D3D11 HLSL compiler treats `triangle` as a reserved token. The Face
  lighting helper uses the non-reserved `faceTriangle` identifier.
- On this machine plain `python` is Python 3.8.10. Miku requires Python 3.11+;
  validation therefore uses the installed Python 3.13 launcher (`py -3.13`).
- A direct installed-export script could import the nested exporter module even
  though enabling the combined extension failed on a clean Blender restart.
  The bake worker used `.miku`, which resolves beneath `bake_worker`; the
  packaged private core is a sibling and therefore requires `..miku`.

## Decision log

- Miku product/package version and every new protocol family start at 1.0.
- Schema IDs use `urn:miku:schema:<kind>:1.0`.
- Legacy compatibility covers serialized data/assets, not old Python imports,
  C# namespaces, package IDs, or Shader.Find names.
- The Blender extension ID is `miku_shader_converter`; exporter and bake worker
  ship in one GPL-3.0-or-later ZIP.
- Missing Toon semantics normalize to `GenericOpaque`.
- New Toon imports do not create Shader Graph assets. Existing user-owned
  wrapper/subgraph assets are preserved and never deleted.
- Toon material rebuild uses a three-way property merge and never overwrites a
  detected user override.
- Smooth outline normals use UV7/TEXCOORD7. Neutral vertex masks use
  `Color32(255,255,255,0)`.
- Face orientation uses object space (+X right, +Y up, +Z forward) with manual
  center/extents; no hierarchy or bone binder is introduced.
- Editor authoring commands use the top-level `Miku` menu; the former
  `Window > Miku` aliases are intentionally removed.
- The five-file/73-material corpus importer was validation-only and is not a
  supported package API or release tool.
- Packaged bake-worker imports use explicit parent-relative `..miku` paths.
  Repository-side tests retain the top-level `miku` fallback because the
  private core is copied beside `bake_worker` only in the release ZIP.

## Implementation sequence

1. Rename active roots, source paths, manifests, namespaces, diagnostics,
   properties, menu names, build tools, and documentation. Preserve `.meta`
   GUIDs and the existing 2.2.1 changes.
2. Introduce Miku 1.0 schemas plus a legacy reader that validates MiGR
   versions and normalizes them before active processing. Add explicit selected
   asset migration for serialized Unity material/animation data.
3. Build one deterministic Blender extension with the semantic exporter and
   isolated bake operator in one package.
4. Extend the Unity backend contract with graph-output policy. Route
   `generic_toon` to fixed semantic shaders and skip graph writes while
   retaining old assets.
5. Add the shared Toon HLSL contract, eight pass-complete semantic shaders,
   recipe assets, material builder, custom Shader GUI, mesh-data tools, and
   RenderGraph rim feature/installer.
6. Update public documentation, compatibility, release notes, provenance, CI
   allowlists, and deterministic package builders.
7. Verify the installed extension root module itself imports on a clean
   Blender 5.2 process; importing only `miku_blender` is insufficient evidence
   that the combined exporter/bake-worker entrypoint can be enabled.

## Validation

- `py -3.13 tools/ci/run_checks.py --profile pr`
- `py -3.13 -m unittest discover -s tests -p "test_*.py"`
- Build the Miku extension and Unity package twice; compare manifests and
  SHA-256 hashes.
- Install the one Blender extension with the certified Blender executable,
  assert `(5, 2, 0)`, export `.mikubundle`, and exercise legacy property copy.
- Run the Unity EditMode assembly on Unity 6000.4.5f1 with URP/Shader Graph
  17.4.0; record compilation, importer, shader, recipe, mesh, and renderer
  feature results.
- Import the same source twice and verify stable content and unrelated IDs.

## Results and follow-up

The implementation now writes only Miku 1.0 identities and assets. MiGR
bundle/property/material/animation/generated-metadata compatibility is
isolated behind read-only normalization or explicit selected-asset migration.
`generic_toon` creates a Miku-owned generated base material, a user-owned
derived material, and a deterministic `MikuToonMaterialRecipe`; it creates no
new Shader Graph, Prefab, mesh binding, or Renderer assignment.

Commands and results executed on 2026-07-30:

- `py -3.13 tools/ci/run_checks.py --profile pr`: passed, including 179
  Python tests, all nine active schemas, the active identity allowlist,
  package identity verification, and both release builds.
- `py -3.13 -m unittest discover -s tests -p "test_*.py"`: passed, 179 tests.
- Unity 6000.4.5f1 EditMode batch run against URP/Shader Graph 17.4.0:
  109 total, 108 passed, zero failed, one skipped. The skip is the existing
  LayerWeight preview test that requires a real D3D11 graphics device.
- Connected Unity 6000.4.5f1 script refresh completed with zero console errors
  or warnings.
- Certified Blender 5.2.0 installed the single extension and exported an
  actual `generic_toon` `.mikubundle`. The installed extension tree matched
  the deterministic build manifest.
- The actual Blender bundle was imported by Unity 6000.4.5f1. The receipt
  selected `Miku/GenericToon/GenericOpaque` and produced exactly the generated
  base material, user material, recipe, manifest, asset map, and receipt. No
  `.shadergraph`, `.shadersubgraph`, Prefab, or Renderer binding was emitted.
- Two consecutive Blender extension builds were byte-identical:
  SHA-256
  `50c3ee0314253d7c08bd0b688668c72f7e1d3911ec2ed16d3cfbbf62de3b6d91`,
  144962 bytes.
- Two consecutive Unity package builds were byte-identical:
  SHA-256
  `83560054c4ec339cf6038d008f7734e17a72cb65a8dc0d03a31f41044cbe4138`,
  329338 bytes.

The literal requested command `python tools/ci/run_checks.py --profile pr`
cannot run on this machine because `python` resolves to Python 3.8.10 while
Miku requires Python 3.11 or newer. The equivalent pinned command above was
run successfully with Python 3.13.

Additional commands and results executed on 2026-07-31:

- Archived the desktop MiGR output and the Unity project's previous
  `Assets/miku` input without deleting either backup.
- Uninstalled `migr_semantic_exporter` and `migr_gpl_bake_worker` from
  Blender's portable `user_default` repository, then verified that their
  modules and installed directories were absent.
- Built the Blender extension twice and obtained byte-identical archives:
  SHA-256
  `b8ff42aead797498e9f72c2a7bf9e3bea5bfc502b4b060ede073f1f1013d39a9`,
  144963 bytes.
- Installed and enabled `bl_ext.user_default.miku_shader_converter` in
  Blender 5.2.0. A separate clean Blender process imported the root module,
  found callable `register` and `unregister` entrypoints, and found no loaded
  or installed MiGR extension.
- Exported three magic-ball materials. All three bundles use
  `miku-bundle-1.0`, all IR documents use `miku-material-ir-1.0`, and every
  `surfaceContract.schema` is `miku-surface-1.0`; no legacy MiGR filename was
  emitted.
- `py -3.13 -m unittest discover -s tests -p
  "test_miku_bake_protocol.py"`: passed, four tests.
- `py -3.13 -m unittest discover -s tests -p "test_miku_workflows.py"`:
  passed, six tests.
- `py -3.13 tools/ci/run_checks.py --profile pr`: passed, including 179
  Python tests, schema and identity checks, and both release builds.
- Replaced Unity's watched `Assets/miku` input with the verified export and
  completed all three imports. Each material produced one Shader Graph, one
  Sub Graph, one generated material, and one Miku import receipt. Every
  generated shader reported `hasError == false`, and a clean final asset
  refresh left the Unity console with zero errors, warnings, or logs.
- Unity's delayed import callback committed the first queued material but did
  not advance the remaining two while the Editor was otherwise idle. Invoking
  the scheduler's existing pump entrypoint processed those queued imports
  successfully. Improving automatic queue wake-up is follow-up reliability
  work and is not part of this identity/schema repair.
- Removed `MikuMetalCorpusMenu.cs` and its `.meta`, removed its CI allowlist
  exception, and regenerated the current 1.0.0 package identity manifest.
  Historical provenance and sample material data remain unchanged.
- Added and verified the five top-level `Miku > Generic Toon` menu entries.
  The three Mesh windows accept only an explicitly selected Mesh asset and
  share the same deterministic clone-generation implementation.
- `py -3.13 tools/ci/run_checks.py --profile pr` passed, including 179 Python
  tests. The explicit full unittest discovery command also passed all 179.
- Unity 6000.4.5f1 EditMode runs against URP/Shader Graph 17.4.0 passed both
  in the isolated validation project and in
  `C:\Users\22687\Desktop\unity\test`: 112 total, 111 passed, zero failed,
  and one graphics-device-dependent test skipped.
- Two consecutive Unity package builds were byte-identical:
  SHA-256
  `2737cfcc7406e5a3f4c8dbfa46f1e11228bfdfa4727bc4b5823c145c489f528c`,
  327056 bytes. The installed project's TGZ has the same hash, resolves to
  one 1.0.0 cache entry, and contains no 73-material temporary tool.

Visual character screenshot acceptance and XR-specific rendering validation
remain outside the automated evidence: the repository has no approved
character fixture for that capture, and XR was explicitly excluded. A
maintainer should perform the Face/Hair/BodySkin/Alpha Clip/outline/screen-rim/
shadow/motion-vector screenshot pass on the release character and target
desktop GPU before publication.
