# MiGR 2.0.3 Magic Ball Runtime Expression Regressions

## Purpose and outcome

Restore correct export and Unity import for the Magic Ball 1, 5, 9, and 10
materials while retaining regression coverage for Magic Ball 1-10 and the
adjacent `Magic Ball 10.001`/`Dots Stroke` materials. Typed Blender Mix inputs,
Principled emission strength, closure-composite normals, and baked closure
weights must remain executable and resource-complete through the sealed Bundle
and generated Shader Graph assets.

## Context and constraints

- Canonical source roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`,
  `extensions/migr_gpl_bake_worker/`, and
  `unity/Packages/com.migr.shaderconverter/`.
- The repository has a large pre-existing dirty worktree. This change must
  preserve unrelated modifications.
- Material IR, Conversion Plan, and Bundle remain schema 2.0. No public field,
  property reference, or deterministic identity rule changes.
- Generated Sub Graphs and MiGR sidecars are MiGR-owned. Existing Wrapper Shader
  Graphs remain user-owned unless Full Regeneration is explicitly selected.
- Validation targets Blender 5.2.0 and Unity 6000.4.5f1 with URP/Shader Graph
  17.4.0, Linear color space, and D3D11.
- The Blender GUI contains unsaved work. Installed Blender extensions must not
  be overwritten until the user saves and closes Blender.
- `材质库/魔法球/魔法球.migr-fixed.blend` is the authoritative Blender
  appearance source for this regression.

## Progress

- [x] 2026-07-29: Confirmed the canonical repository boundary, exact Blender
  and Unity versions, dirty worktree, installed package hashes, and unsaved
  Blender GUI state.
- [x] 2026-07-29: Reproduced the Magic Ball 1/5 typed Mix socket and emission
  strength loss, Magic Ball 9 generated-resource loss, and Magic Ball 10
  non-executable closure weight.
- [x] 2026-07-29: Implemented exact/active/type-aware socket resolution and
  Principled emission-strength normalization.
- [x] 2026-07-29: Lowered static baked closure weights to executable
  `Texture.SampleBaked2D` expressions.
- [x] 2026-07-29: Connected closure-composite Normal TS, transformed it to
  world space, and added generated baked-resource reachability validation.
- [x] 2026-07-29: Added focused Python and Unity regression tests; 59 focused
  Python tests passed.
- [x] 2026-07-29: Completed the expanded Blender corpus export/determinism run
  for 12 materials. Both complete 89-file trees are SHA-256 identical.
- [x] 2026-07-29: Updated release versions, implementation/profile hashes,
  compatibility policy, diagnostics, documentation, and changelogs.
- [x] 2026-07-29: Ran repository CI, isolated Unity batch tests, deterministic
  archive builds, and paired visual validation.
- [ ] 2026-07-29: Complete the live-project import after the open Unity Editor
  is safely restarted. The 2.0.3 TGZ and 12 bundles are staged, but the current
  editor remains in `isCompiling/isUpdating` after pre-existing
  AssetImportWorker crashes and still exposes the loaded 2.0.2 assembly.
- [ ] 2026-07-29: Install deterministic Blender archives only after the user
  saves and closes Blender.

## Discoveries

- Blender 5.2 `ShaderNodeMix` exposes simultaneously named typed sockets.
  Inactive `A_Float`/`B_Float` records precede the linked `A_Color`/`B_Color`
  records, so name-only lookup silently selected scalar zero.
- Principled `Emission Color` and `Emission Strength` are independent sockets.
  The prior snapshot normalized only color and therefore discarded the 12.8
  strength used by Magic Ball 1 and 5.
- The Magic Ball 9 schema-2.0 IR already contains its complete
  `Vector.NormalFromHeight` expression and `_MIGR_Baked_*` height resource.
  The Unity closure backend ignored the material Normal channel and emitted a
  constant tangent normal, leaving the baked property absent.
- The Magic Ball 10 Wireframe island was baked, but `finalWeight` still carried
  a pre-bake `Parameter` leaf with `requiresBake`, which the Unity backend
  correctly rejected as non-executable.
- A Normal-channel expression can itself depend on `Input.Normal`. It must be
  compiled against geometric normal while other Layer Weight/Fresnel consumers
  use the final transformed surface normal, or the graph becomes recursive.
- Closure-composite Wrapper graphs evaluate their final radiance in the
  generated Sub Graph and then multiply it by `_BaseColor`. Binding the
  source closure's black authoring Base Color to that final modulator erased
  Magic Ball 9/10 even when their Normal/Wireframe resources were valid.
  Composite surfaces therefore require a neutral white Wrapper tint and no
  legacy `_BaseMap` binding.
- Unity's linear RenderTexture values must be converted to sRGB before PNG
  encoding. The validation script retains the original linear pixels for
  numeric acceptance and writes a separately gamma-encoded display image for
  Blender/Unity Delta-E comparison.
- MCP test-name filtering can report a zero-test placeholder as passed. The
  assembly-level Unity run discovered and executed 94 real tests and is the
  accepted in-editor evidence.

## Decision log

- 2026-07-29: Resolve sockets by exact identifier first, then active display
  name, then value type. Remaining ambiguity is an explicit
  `MIGR_SOCKET_AMBIGUOUS` failure, never a positional fallback.
- 2026-07-29: Fold constant emission color/strength products deterministically.
  Preserve the original closure parameters and emit one stable multiply only
  for a non-unit dynamic strength, avoiding double multiplication in
  multi-closure paths.
- 2026-07-29: Bake static closure weights and non-Normal closure parameters
  into runtime sample expressions. A global closure Normal retains its existing
  channel-bake ownership so the same Bump chain is not duplicated as a second
  expression island.
- 2026-07-29: Resolve Normal TS before weighted-closure evaluation, use
  geometric normal only during that recursive build, then use the transformed
  final world normal for closure lighting and normal-dependent expressions.
- 2026-07-29: Treat every sealed `_MIGR_Baked_*` resource as required generated
  graph state. Missing reachability is a hard
  `MIGR_GENERATED_RESOURCE_UNREFERENCED:<bindingKey>` failure.
- 2026-07-29: For `TransparentEmission`, `TransparentLit`, and
  `CustomMultiLobe`, treat `_BaseColor` as a final neutral Wrapper modulator:
  clear `_BaseMap`, initialize `_BaseColor` to white, and do not bind the
  source closure Base Color over it. Standard PBR materials retain their
  existing authoring bindings.
- 2026-07-29: Do not heuristically repair old Magic Ball 1/5/10 IR by material
  name or black-output detection. Those bundles must be re-exported with 2.0.3.

## Implementation sequence

1. Correct Blender snapshot and core runtime socket selection.
2. Normalize Principled emission strength and lower static baked closure
   weights.
3. Correct Unity closure-normal data flow and validate baked property
   reachability before serialization.
4. Add focused unit, Blender corpus, and Unity import/generation regressions.
5. Update package versions, implementation hashes, target-profile hashes,
   compatibility policy, diagnostics, release notes, and public documentation.
6. Run repository, Blender, Unity, packaging, end-to-end, and visual validation.
7. Install only deterministic canonical-source builds after editor safety
   conditions are satisfied.

## Validation

- Python/core:
  `python tools/ci/run_checks.py`
- Blender corpus:
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/migr_magic_ball_corpus_smoke.py`
- Unity:
  `powershell -File tools/ci/run_unity_editmode.ps1 -UnityPath
  "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"`
- Packaging:
  build semantic-exporter ZIP, GPL worker ZIP, and Unity TGZ twice; compare
  complete manifests and SHA-256 values before installation.
- End to end:
  export/import Magic Ball 1-10 plus `10.001` and `Dots Stroke`, verify no
  `MIGR_AUTO_IMPORT_FAILED`, no shader compilation errors, stable GUIDs, and
  preserved Wrapper ownership.
- Visual:
  render paired 512-by-512 Blender/Unity images under fixed mesh, UV, camera,
  light, background, exposure, Linear color, and D3D11 settings; produce
  Delta-E heatmaps and retain declared clear-coat approximation evidence.

Expected behavior is distinct colored/emissive Magic Ball 1 and 5 output,
reachable Magic Ball 9 Normal resources and bump detail, reachable Magic Ball
10 Wireframe weight resources, successful import, and deterministic files.

## Results and follow-up

Implementation and isolated validation are complete:

- `python tools/ci/run_checks.py` parsed 69 Python files, validated 14 schemas,
  and passed 149/149 tests.
- Blender 5.2.0 exported all 12 corpus materials twice. Both 89-file trees are
  byte-identical; Magic Ball 1/5 retain the correct Overlay links and 12.8
  emission strength, Magic Ball 9 seals its Normal resource, and Magic Ball 10
  seals an executable Wireframe-weight resource.
- Unity 6000.4.5f1 / URP and Shader Graph 17.4.0 / Linear / D3D11 passed 93 of
  94 EditMode tests with 0 failures and 1 intentional skip.
- The isolated end-to-end run imported 12/12 bundles and rendered 11/11
  materials at 512 by 512. All structural visual acceptance checks passed:
  Magic Ball 1/5 are non-black, textured, emissive, and distinct; Magic Ball 9
  has Normal variation; Magic Ball 10 has Wireframe variation and a bound
  non-empty baked texture. Its log contains no `MIGR_AUTO_IMPORT_FAILED`,
  `MIGR_IMPORT_FAILED`, or shader compilation error.
- Eleven paired PNGs and Delta-E heatmaps are retained under
  `.validation/magic-ball-2.0.3/visual/`. Strict Exact thresholds intentionally
  do not pass (mean Delta-E2000 6.12 to 32.33) because these clear-coat,
  transparency, and multi-lobe paths are declared Approximate; the results are
  evidence for manual review, not an Exact compatibility claim.
- Two deterministic builds produced identical archives. Canonical Unity
  source, the isolated installed package, and the final TGZ each contain the
  same 140 files with zero path/hash differences.

Final archives:

- `migr_semantic_exporter-2.0.3.zip`:
  `8f20a969aac17a04cb63bb15755b878161bc33d6f3ba6395a37cf32647de1408`
- `migr_gpl_bake_worker-1.1.2.zip`:
  `1c88c5c7e3f2d86faf27f8c2322a74a2d7123951f34ce9645f22e7c3048038fa`
- `com.migr.shaderconverter-2.0.3.tgz`:
  `ad6b451c7486721b62881d9bc34bdd8587326c53e396c9cafc466e7e1e7a75d7`

The open live Unity project now references the verified 2.0.3 TGZ and contains
the 12 staged bundles, but its running domain still exposes 2.0.2 and remains
stuck updating after existing AssetImportWorker crashes. The first guarded
import correctly failed before writing assets. Save and restart that editor,
then let the staged bundles import and verify the Console. Blender installation
likewise remains intentionally pending until the unsaved Blender GUI is saved
and closed.

## 2.1.0 follow-up — mesh identity invalidates the structural visual claim

The 2.0.3 structural render used Unity's built-in Sphere while the generated
ExpressionIsland EXRs were bound to the Blender source sphere's UV layout and,
for Wireframe, its triangle topology. Checking only that the render was
non-black and had variance did not prove those mesh-bound resources were
correct. That evidence remains useful for the 2.0.3 runtime-expression fixes,
but it is not acceptance evidence for mesh-bound bake fidelity.

MiGR 2.1.0 therefore makes Auto portable and moves all topology/UV-bound
textures behind Source Mesh Fidelity, Bundle 2.1, a sealed deterministic GLB,
renderer-slot bindings, and a generated Unity Prefab. See
`docs/plans/magic-ball-mesh-bound-bake-safety-2.1.0.md`.
