# Generic Toon Anime Lighting and Shadow Repair

## Purpose and outcome

Upgrade the Generic Toon fixed workflow so a material with only BaseMap has a
stable anime-game appearance: monotonic three-tone diffuse, URP-correct direct
and environment lighting, coherent shadows, semantic highlights, procedural
face shading, and rosy skin. Optional control textures refine the result but
must never be required for valid output.

## Context and constraints

- Canonical sources are `miku/`, `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- Exact validation tuple is Blender 5.2.0, Unity 6000.4.5f1, URP/Shader Graph
  17.4.0, Windows D3D11.
- The worktree already contains unrelated and overlapping Miku 1.0.4,
  Portable Hybrid, fixed-workflow texture, and Screen Rim changes. Preserve
  them and merge only the Generic Toon work described here.
- Shader names, package ID, existing property names, Miku schema versions, and
  user-owned Material assets remain compatible.
- Genshin, WuWa, and HSR shader sources are reference implementations only and
  are not modified by this plan.

## Progress

- [x] 2026-08-01: Verified canonical repository markers, package identity,
  exact dependency versions, root instructions, PLANS.md, and dirty worktree.
- [x] 2026-08-01: Implement Generic Toon HLSL, passes, optional textures, and eight shader
  property contracts.
- [x] 2026-08-01: Implement editor bindings, keyword synchronization, semantic defaults,
  face-bounds calibration, and shader-family migration.
- [x] 2026-08-01: Add/update Python and Unity tests plus public documentation.
- [x] 2026-08-01: Run targeted validation, record the unchanged Screen Rim
  baseline and unrelated full-suite blockers, and self-review the task diff.

## Discoveries

- The old `MikuToonBand` quantizes before applying `smoothstep`; it is not
  monotonic and can turn increasing NdotL back into shadow.
- Shadow attenuation is currently multiplied into NdotL before quantization,
  which promotes Shadow Map noise into large cel-shaded fragments.
- Generic ShadowCaster currently reuses the ordinary vertex path and omits
  URP `ApplyShadowBias` and `ApplyShadowClamping`.
- The fixed-workflow role vocabulary already contains NormalMap, IDMap,
  FaceSDF, HairHM, MatCap, and EmissionMap, so Generic can add them without a
  schema role addition.
- A shared animated head-axis binder and face-SDF basis resolver already exist
  under the NPR runtime and should be reused.
- Baseline Unity run before this task: 129 total, 123 passed, four pre-existing
  real-camera Screen Rim failures, two skipped.
- The local Python test environment does not contain NumPy. Installing it into
  `.venv` was blocked by the configured `127.0.0.1:7897` proxy, so the two
  pre-existing Delta-E modules cannot be collected on this machine.
- A concurrent dirty-worktree edit in `MikuBundleImporterTests.cs` temporarily
  introduced undeclared `objects`/`customFunctions` references after the first
  complete Unity run. It was preserved unchanged and later completed by its
  owner, allowing a final complete merged-worktree run.

## Decision log

- 2026-08-01: BaseMap-only is the primary acceptance path; optional maps use
  local keywords and neutral constants when absent.
- 2026-08-01: Quantize geometric light first, then blend system shadow toward
  the deep tone so URP soft penumbrae remain visible.
- 2026-08-01: Keep `_MIKU_ToonSteps=2` as two-tone compatibility and make new
  semantic presets use three tones.
- 2026-08-01: Reuse `IDMap` as Generic ToonControl with R spec/MatCap, G shadow
  offset, B Screen Rim, and A outline width.
- 2026-08-01: Do not synthesize FaceSDF, HairHM, MatCap, or other art-directed
  textures from BaseMap.
- 2026-08-01: Recipe migration updates only values still matching the recorded
  old preset; explicit user overrides remain untouched.

## Implementation sequence

1. Replace Generic Toon lighting helpers and forward fragment logic, add
   correct ShadowCaster, normal/tangent varyings, optional maps, face/Skin/Hair
   paths, and screen-rim/outline masks.
2. Synchronize identical Properties and CBUFFER contracts across all eight
   semantic shaders with semantic defaults and URP 17.4 variants.
3. Extend fixed workflow bindings, Generic Shader GUI, semantic recipes,
   snapshots, migration, and face-bounds calibration.
4. Add math, contract, binding, migration, BaseMap-only, and rendering tests.
5. Update README, changelogs, compatibility/provenance, then validate.

## Validation

- Python: targeted fixed-workflow tests and the repository pytest suite.
- Blender: run fixed-workflow smoke with
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe` after asserting
  `bpy.app.version == (5, 2, 0)`.
- Unity: run package EditMode tests in Unity 6000.4.5f1; targeted Generic Toon
  tests must pass and the full suite must introduce no failures beyond the
  recorded Screen Rim baseline.
- Static review: identical shader properties/CBUFFER order, no undeclared
  keywords, deterministic role ordering, and no unrelated diff changes.

## Results and follow-up

Implementation is complete in canonical Miku sources.

- `.venv\\Scripts\\python.exe -m pytest tests/test_miku_fixed_workflows.py -q`:
  11 passed, including monotonic bands, two/three-tone behavior, system-shadow
  separation, specular/skin/blush math and control-channel isolation.
- Python suite excluding the two NumPy-only Delta-E modules: 212 passed.
- Package identity/profile tests: 11 passed; the previous Miku 1.0.5 profile
  hash remains a bounded accepted input and deterministic package hashes were
  regenerated.
- Fixed `C:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe` smoke:
  Blender 5.2.0 LTS passed deterministic BaseMap-only and all optional Generic
  texture-role exports without bake jobs.
- First complete Unity 6000.4.5f1/D3D11 run after shader repair: 133 total,
  127 passed, four unchanged Screen Rim real-camera failures, two skipped.
  All eight Generic shaders and editor assemblies compiled.
- Final Generic-only Unity run after HairHM and recipe-snapshot changes: 20
  total, 16 passed, the same four Screen Rim baseline failures, zero unexpected
  failures and zero Generic Shader/C# compile errors.
- Final complete merged-worktree Unity run: 134 total, 126 passed, six failed,
  two skipped. Four failures are the recorded Screen Rim baseline. The other
  two are concurrent Miku 1.0.5 `ClosureCompositeUsesPerLobeWorldNormals...`
  assertions for TransparentLit and CustomMultiLobe; neither exercises Generic
  Toon. Generic shaders produced no compile error. No assertion or production
  source was weakened to hide any failure.
- True GPU screenshot review across main-light rotation, Forward+, cookies,
  probes and character animation remains a manual visual acceptance step; the
  available batch runner is `-nographics`, and its four real-camera tests retain
  the known `RenderTexture.Create failed` baseline.

Role-specific hand-authored FaceSDF, hand-painted HairHM/MatCap/control maps,
and animation/modeling improvements remain outside this task. The procedural
fallback is deterministic and complete, but it cannot infer those authored
details from BaseMap.
