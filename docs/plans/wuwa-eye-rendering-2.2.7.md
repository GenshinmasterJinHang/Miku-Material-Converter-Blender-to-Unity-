# Miku 2.2.7 Wuwa eye texture semantics and Phi validation

## Purpose and outcome

Replace the Wuwa Eye 2.2.6 single-HET highlight approximation with the
authored eye texture roles observed in the saved Blender material. `EyeHET`
becomes a direct emission mask, HDMF supplies the primary highlight and smooth
pupil field, the authored upper/lower highlight images retain their Point
Mapping alignment, and optional EG supplies a Fresnel secondary highlight that
tracks the main light. The Phi validation keeps the separate `bai` and `eye`
material slots and does not modify the `eye+` overlay.

## Context and constraints

- Canonical implementation roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. Installed extensions, `dist/`,
  package caches, and validation projects are outputs only.
- The worktree contains extensive pre-existing 2.2.x changes. Preserve all
  unrelated edits and extend the current Wuwa Eye implementation in place.
- Validation uses Blender 5.2.0 from the repository-mandated Steam path and
  Unity 6000.4.5f1 with URP/Shader Graph 17.4.0. Do not overwrite an installed
  Blender extension while a GUI session contains unsaved work.
- The private Phi blend and character textures are read-only validation inputs.
  They and derived assets must not enter the public repository.
- `miku-bundle-1.0` remains the document family. New roles and the optional UV
  transform are additive, but require a matching 2.2.7 exporter/importer.

## Progress

- [x] 2026-08-03: Confirm canonical markers, package identity, dirty-worktree
  boundary, current 2.2.6 implementation, and the exact source node values.
- [x] 2026-08-03: Implement bundle roles, validation, effective Mix reachability, and Point
  Mapping export.
- [x] 2026-08-03: Implement Unity recipe transport, material binding, shader keywords, and
  the new Wuwa Eye composition.
- [x] 2026-08-03: Add regression tests, compatibility/provenance/release documentation, and
  coordinated 2.2.7 identities.
- [x] 2026-08-03: Run Python, Blender, Unity, deterministic-build, and private visual
  validation; record exact results and limitations.

## Discoveries

- The saved Phi `eye` material selects the B Eye_D input through a constant
  Factor 1 Mix. Its nested eye group likewise selects `T_HDMF02_EM.tga` through
  a Factor 1 Mix, while `T_Highlight_1.png` and `BottomHighlight_1.png` remain
  active through a Factor 0.5 Mix.
- `Eye_HET` is a grayscale RGB mask with opaque alpha. White is emissive, black
  is not emissive, and gray boundary pixels must remain proportional.
- HDMF red is the authored primary highlight. Alpha is a smoother version of
  the dark-center field and therefore yields the pupil mask as `1 - alpha`.
  Blue contains decorative atlas-like shapes but has no proven runtime use.
- The authored upper/lower Mix feeds a Greater Than `0.0400000215` gate and a
  linear ramp from `0.0803109035` black to `0.9041451216` white. HDMF red uses a
  linear ramp from 0 black to `0.7538858056` white.
- The upper Point Mapping uses location `(0.13, -0.05, 0)` and scale
  `(0.68, 1.27, 1.06)`; the lower mapping uses location
  `(-0.48, -0.27, 0)` and scale `(1.58, 1.61, 0)`. Both rotations are zero.
- Phi has separate `bai` sclera and `eye` iris material slots on the same mesh.
  The saved `bai` graph does not contain HET, so safe same-object HET inheritance
  is required for automatic export. No EG image exists in the saved blend.

## Decision log

- 2026-08-03: Supersede the 2.2.6 decision that sampled one HET mask twice.
  `EyeHET` is emission-only; legacy materials adopt the corrected meaning
  immediately and receive a re-import warning instead of a compatibility
  shading branch.
- 2026-08-03: Add `EyeHDMF`, `EyeUpperHighlight`, and `EyeLowerHighlight` roles;
  retain optional `EyeEG`. Do not interpret HDMF blue in final shading.
- 2026-08-03: Transport static Point Mapping as a target-neutral UV0 affine 2D
  matrix in each material binding. Preserve existing upper/lower offset and
  scale properties only as post-import fine tuning.
- 2026-08-03: Inherit only a unique `EyeHET` from another Eye material on the
  same mesh. Never inherit HDMF, upper/lower highlights, or EG, and never choose
  among ambiguous candidates.
- 2026-08-03: Keep bundle schema version 1.0 following the repository's existing
  additive-contract convention. Older strict readers fail clearly on new
  roles/fields; 2.2.7 bundles declare 2.2.7 as the minimum paired consumer.
- 2026-08-03: Serialize optional recipe UV transforms by reference so an absent
  transform remains null after a Unity domain reload. Material binding still
  resets all UV rows to identity before applying the transforms that are
  present, preventing stale values from leaking across reimports.

## Implementation sequence

1. Extend fixed-workflow roles, schema validation, filename recognition, and
   deterministic binding metadata.
2. Make active-chain traversal constant-Mix-aware and add static UV0 Point
   Mapping extraction plus safe same-object HET inheritance.
3. Extend Unity recipe/importer types and binding logic, reset missing matrices
   to identity, and synchronize the new local shader keywords.
4. Replace the HET decal fragment logic with direct HET emission, HDMF channel
   reuse, authored upper/lower mask processing, automatic EG, and debug views.
5. Update tests, public compatibility/schema/provenance docs, changelogs,
   versions, target-profile hashes, and deterministic package identity.

## Validation

- Run targeted and full Python tests, then `python tools/ci/run_checks.py
  --profile pr`.
- Run `tests/blender/miku_fixed_workflow_textures_smoke.py` only with the fixed
  Blender executable and assert `bpy.app.version == (5, 2, 0)`.
- Run Unity EditMode coverage for Wuwa texture bindings, recipe round-trips,
  shader contracts/compilation, legacy migration warnings, and finite fallback
  behavior.
- Build the Blender and Unity packages twice and compare manifests and SHA-256.
- Re-export/import Phi into new 2.2.7 directories, replace only `bai` and `eye`
  validation slots, and capture front/profile plus left/right-light evidence.

## Results and follow-up

Implemented the new fixed roles, strict UV metadata, constant-Mix reachability,
same-mesh HET inheritance, Unity recipe transport, keyword synchronization,
and the rebuilt Eye shader. The original HET double-decal path is absent.

Validation results:

- `python tools/ci/run_checks.py --profile pr`: passed. The run executed 242
  Python tests, parsed 89 Python files, validated 11 schemas and the canonical
  source/identity boundaries, then rebuilt both packages.
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests\blender\miku_fixed_workflow_textures_smoke.py`:
  passed on Blender 5.2.0. It covers Factor 1 source selection, Factor 0.5 dual
  reachability, Point Mapping inside a nested group instance, root-level Point
  Mapping, unique sibling-HET inheritance, and the ambiguous-HET no-guess
  diagnostic. Pure Python coverage separately exercises Factor 0/1/0.5,
  rotated affine matrices at multiple UV points, and non-finite rejection.
- Unity EditMode job `2f51353f6a0549e2b150136d884bbaab`: succeeded on
  Unity 6000.4.5f1, URP/Shader Graph 17.4.0. Total 157, passed 156, failed 0,
  skipped 1. The skipped external 1.0.3 regression requires the optional
  `MIKU_103_REGRESSION_BUNDLE_ROOT`; all 2.2.7 Eye tests ran and passed.
  Synthetic black/gray/white pixels verify zero/proportional/full HET emission,
  inverse HDMF alpha selects sclera versus pupil parameters, and changing only
  HDMF blue leaves final emission unchanged.
- Unity `ShaderUtil.GetShaderMessages` for `MIKU/Wuwa/Eye`: zero messages.
  The final Editor console contained no compilation or shader errors.
- Two consecutive canonical builds were byte-identical. Final artifacts are
  `miku_shader_converter-2.2.7.zip`, 194226 bytes, SHA-256
  `3df1d1fe35bdbded1945a1d4c9a58ab7b7fb6a19a78318fae1da4678e9a17ada`,
  and `com.miku.shaderconverter-2.2.7.tgz`, 384315 bytes, SHA-256
  `309abcacbb9be04e22ee3d39de1eb2746df6c3a4dbf43032ae99a6e62603ccb3`.
  Blender loaded the installed module from the fixed Steam portable repository;
  all 32 archive files matched the installed files byte-for-byte. Unity loaded
  the final TGZ as `com.miku.shaderconverter@2.2.7`.
- The private Phi blend exported only `bai` and `eye`. `bai` bound BaseMap plus
  inherited EyeHET; `eye` bound BaseMap, EyeHET, EyeHDMF, upper highlight, and
  lower highlight. Exported upper rows were `(0.68, 0, 0.13)` and
  `(0, 1.27, -0.05)`; lower rows were `(1.58, 0, -0.48)` and
  `(0, 1.61, -0.27)`. Both Unity recipes report part Eye and version 2.2.7.
- The isolated validation instance changed only FBX material slots 4 (`bai`)
  and 5 (`eye`). Slot 15 (`eye+`) and every other slot retained the original
  material reference. The 18-file `菲比_2.2.6` tree retained aggregate SHA-256
  `80aeb37fc1f3d3809532cd0f9d8c52a39e0da61d89f32fdf7b7b2f94c2d2ec71`.
- Private screenshots were written under the validation project's
  `Library/Miku/PhoebeEye227/`: front close-up, profile, main-light-left, and
  main-light-right. They are evidence only and are not public repository assets.

Known limitation: the private Phi source has no EG image. Main-light projection,
Fresnel strength, opposing left/right offsets, keyword disabling, and invalid
tangent fallback are covered programmatically, but the Phi screenshots cannot
claim source-image EG calibration. Blender Shader-to-RGB energy mixing remains
Equivalent/Approximate as documented; texture choice, channels, ramps, masks,
and UV transforms are preserved exactly.
