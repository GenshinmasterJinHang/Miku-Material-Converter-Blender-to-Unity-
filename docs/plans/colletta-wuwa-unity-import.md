# Colletta Wuwa preset Unity import

## Purpose and outcome

Import the supplied Colletta Blender preset into the existing Unity project
served through the local port-8080 Unity MCP proxy. The completed result is an
isolated `Assets/柯莱塔` import set whose materials use the dedicated Wuwa game
preset, whose model and texture references resolve, and whose preview is
validated without modifying or replacing the existing Phoebe/Wuwa assets.

## Context and constraints

- Source: `柯莱塔-渲染（bilibili三一七片的谎言）/柯莱塔（bilibili三一七片的谎言）.blend`.
- Target project: `unityproject`, reached by the existing port-8080 MCP proxy.
- Validated tuple: Blender 5.0.1, add-on 0.6.0 source, Unity 6000.4.5f1,
  URP 17.4.0, Shader Graph 17.4.0, MiGR `mgir-2.0` plus overlay 3.
- Game presets intentionally use the dedicated Wuwa ShaderLab backend rather
  than the generic editable Shader Graph backend.
- The worktree was already heavily modified. All pre-existing changes and the
  existing `Assets/菲比` / `Assets/鸣潮` content are user-owned and preserved.
- Third-party model and preset files retain their original attribution and must
  not be added to release archives under the repository MIT license.
- Generated output stays under `unityproject/Assets/柯莱塔`; no broad overwrite
  or full-regeneration operation was used.

## Progress

- [x] 2026-07-22: Read repository constitution, `PLANS.md`, relevant
  architecture/ownership/compatibility documents, and current Git status.
- [x] 2026-07-22: Identified port 8080 as `tools/_mcp_port_proxy.py` and selected
  the `unityproject` Unity MCP instance instead of the separate `test` instance.
- [x] 2026-07-22: Audited the `.blend` objects, materials, images, packing state,
  and source diagnostics using Blender 5.0.1.
- [x] 2026-07-22: Exported an isolated, deterministic Wuwa preset bundle.
- [x] 2026-07-22: Imported the bundle below `unityproject/Assets/柯莱塔` and
  waited for Unity asset refresh/compilation.
- [x] 2026-07-22: Validated importer diagnostics, shader assignments, renderer
  bindings, textures, console state, and a dedicated visual preview scene.
- [x] 2026-07-22: Ran scoped Python and Unity Wuwa regression checks.
- [x] 2026-07-22: Self-reviewed the final task diff and recorded exact results.

## Discoveries

- Port 8080 returns HTTP 404 at `/` because it is an MCP proxy rather than a
  website; process inspection and Unity resources identify the target project.
- The Blender outer `柯莱塔` collection/root includes hidden MMD rigid-body and
  joint helpers. Exporting the `柯莱塔_arm` armature hierarchy avoids those
  helpers and produces only the character mesh and its 17 material definitions.
- The source has 62,070 vertices, four UV sets, 37 material slots, 17 distinct
  used material identities, and packed textures. Its author-machine texture
  paths therefore do not block a self-contained export.
- Existing Wuwa name classification placed face details on Eye, overlays on
  Standard PBR, and textureless outline shells on Standard PBR. Narrow semantic
  rules and regression tests were required before importing the final bundle.
- Unity binds 14 distinct generated materials across 14 renderer submeshes;
  three generated source material definitions are unused by rendered triangles.
- A transient AssetDatabase modification-time race occurred during initial
  generation, then Unity reimported successfully. All generated assets resolve,
  and a console query filtered to `Assets/柯莱塔` returns zero errors.

## Decision log

- 2026-07-22: Use the public Blender `b2u.export_unity_bundle` operator with
  `WUWA_TOON`, GLB output, copied images, and the existing automatic-bake path.
  Direct `.blend` copying cannot preserve the required Wuwa shader semantics.
- 2026-07-22: Target a new `Assets/柯莱塔` subtree. Reusing existing Wuwa asset
  folders would risk user-owned GUIDs, scenes, and tuned materials.
- 2026-07-22: Treat textureless materials as Wuwa only for explicit outline
  helper names. Arbitrary textureless materials retain the diagnosed Standard
  PBR fallback, preserving safe failure behavior.
- 2026-07-22: Add an independent additive preview scene and isolate its model,
  camera, and light on layer 30. The original `SampleScene` remains unchanged.

## Implementation sequence

1. Inspect source material and object structure in Blender 5.0.1.
2. Export the armature hierarchy into a temporary staging directory.
3. Validate bundle/MiGR paths, schemas, presets, textures, and diagnostics.
4. Copy the self-contained set into the isolated Unity Assets subtree.
5. Refresh Unity, verify generated materials/shaders, then save an isolated
   preview scene and screenshot.
6. Run scoped regression checks and review only the task-owned changes.

## Validation results

- Blender export command completed with `FINISHED`: one 38,448,208-byte GLB,
  37 exported texture PNGs, 17 MiGR documents, and zero blocking diagnostics.
- Bundle schema plus 17 core MiGR and 17 preset schema checks passed: 35/35.
  All 17 documents use preset `wuwa`; none fell back to Standard PBR and no
  referenced texture is missing.
- Unity generated 17 `.mat` and 17 `.shader` assets. The imported GLB has one
  renderer, 14 non-null material bindings, and zero non-Wuwa shader bindings.
- `B2UWuwaRuntimeVerification.Run()` completed twice and wrote
  `B2U_WUWA_RUNTIME_VERIFY_OK` to the Unity Editor log.
- Unity EditMode test
  `DedicatedGamePresetRemainsOnShaderLabBackend` passed 1/1.
- `python tests/test_wuwa_toon_preset.py`: 14/14 passed.
- `python tests/test_game_preset_outline_defaults.py`: 4/4 passed.
- `python tests/test_exporter_core.py`: 35 passed, 1 unrelated pre-existing HSR
  assertion failed (`SpecThreshold` names versus current `SpecularControl`
  names). No HSR code was changed for this import.
- `python -m py_compile` passed for the modified Python modules and test file.
- Final Unity console query filtered to `Assets/柯莱塔` returned zero errors.
  A force-refresh also surfaced four unrelated existing HSR shader errors under
  `Assets/Json/MGIR/HSR`; they reference missing HSR helper identifiers and are
  outside this import. Visual inspection of `CollettaWuwaPreview.png` confirmed
  resolved face, hair, clothes, textures, normals, and outlines with no magenta
  error shader or helper geometry.

## Results and follow-up

The import is complete at `unityproject/Assets/柯莱塔`. The standalone preview
is `CollettaWuwaPreview.unity`, and the visual evidence is
`CollettaWuwaPreview.png`. Source attribution is copied beside the generated
assets.

No schema version or public API changed. The classification behavior is
additive and remains within the existing Wuwa preset contract. The full Python
suite was not run because pytest is unavailable in both bundled and system
Python; direct unittest execution covered the relevant files. The full Unity
test suite was not run because this task used the already-open 8080 project and
scoped validation to avoid disturbing unrelated dirty project work. The legacy
combined overlay document cannot be validated directly against
`schemas/mgir_v3.json`, whose standalone overlay shape differs from the current
combined core+preset documents; importer acceptance and the versioned core and
preset schemas were validated instead.
