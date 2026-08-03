# Miku 2.2.6 Wuwa face, eye, stockings, hair, and active-texture fidelity

## Purpose and outcome

Repair the Wuwa fixed workflow so Blender's active material logic reaches Unity
without character-name special cases. The observable result is a flatter
FaceSDF-driven face with explicit object-space face axes, one authored EyeHET
mask sampled twice for independently movable upper/lower HDR highlights,
ID-map-driven sheer stockings, brighter hair, and a black voice-mark BaseMap
selected from the active Blender output.

## Context and constraints

- Canonical source roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. Installed Blender extensions,
  Unity `PackageCache`, validation projects, and `dist/` are outputs only.
- Validation is fixed to Blender 5.2.0 at the repository-mandated Steam path and
  Unity 6000.4.5f1 with URP/Shader Graph 17.4.0 on Windows D3D12.
- The repository has extensive pre-existing uncommitted work in every affected
  subsystem. Preserve it and keep edits narrowly additive.
- The open Blender GUI has unsaved private-scene work. Do not overwrite an
  installed extension or use that process for validation until it is saved and
  closed; isolated headless reads of the saved blend are safe.
- Private character models and textures are validation inputs only and must not
  be committed or distributed.
- User-owned materials are not silently rewritten. Version migration may update
  Miku-owned generated base materials; arbitrary materials require the existing
  explicit, Undo-safe recommended-profile action.

## Progress

- [x] 2026-08-03: Confirm canonical markers, package identity, dirty-worktree
  boundary, Blender 5.2.0 source scene, and Unity validation versions.
- [x] 2026-08-03: Trace the voice-mark, eye-highlight, stocking, face-axis, hair,
  and post-processing behavior in the saved Blender file and live Unity scene.
- [x] 2026-08-03: Implement active-chain-aware fixed-workflow texture selection and Wuwa
  single-mask eye/stocking recognition.
- [x] 2026-08-03: Implement Unity Wuwa shader, material-profile, keyword, and project-setup
  changes.
- [x] 2026-08-03: Add tests, diagnostics, compatibility/provenance documentation, and the
  2.2.6 release/version updates.
- [x] 2026-08-03: Build and hash canonical artifacts, install them, re-export, and validate
  the private Phi scene without distributing its assets.
- [x] 2026-08-03: Re-open the completed 2.2.6 implementation as a persisted
  material-replacement phase after the user supplied separate full-body and
  bust reference responsibilities plus final calibrated defaults.
- [x] 2026-08-03: Calibrate Eye, Face, and Body defaults and the explicit
  recommended profile; add the public `_BodyEmissionStrength` property and
  contract coverage.
- [x] 2026-08-03: Rebuild canonical packages and export all nine requested
  materials twice from Blender 5.2.0 with byte-identical bundle evidence.
- [x] 2026-08-03: Create `Assets/鸣潮/Materials/菲比_2.2.6/` without modifying
  the existing `菲比` material directory, overlay only recognized bundle
  texture roles, and persist renderer slots 0, 1, 2, 3, 4, 5, 11, 12, and 14.
- [x] 2026-08-03: Reopen the Unity scene, verify all nine references and shader
  contracts, run EditMode tests/compiler/console checks, and capture full-body
  and bust screenshots for comparison against their respective references.

## Discoveries

- `miku_blender._collect_fixed_workflow_image_resources` currently scans every
  image node independently of output reachability. A disconnected
  `T_5XingStar_D2.tga` therefore wins BaseMap from its `_D` filename while the
  active black `xing 5.png` has no inferred role.
- `Eye_HET.png` is an RGB grayscale mask without alpha. The requested Unity
  result deliberately uses only this mask, sampled twice with independent UV
  transforms for upper and lower highlights. Other eye-group textures remain
  serialized legacy inputs and do not contribute to shading.
- The current private blend stores the official mask as packed
  `T_R2T1FeiBiMd10011Eye_HET.tga`. Fixed-workflow TARGA resources therefore
  require deterministic in-memory PNG transcoding; otherwise the correct role
  is recognized but the resource cannot be sealed into the bundle.
- The Down stocking group converts the linear RGB ID texture to a scalar and
  applies `Greater Than 0.5`. The private graph includes an explicit
  `Separate Color (Green)` node between the ID image and threshold. `Down_ID.png`
  is imported as linear data; it must remain the single sampled texture and the
  stocking feature must not fabricate a separate mask image.
- Wuwa Face already supports optional world-space binder vectors but the Phi
  object has no binder. The material needs explicit local `_FaceRight`,
  `_FaceUp`, and `_FaceForward` vectors transformed by the object matrix.
- Current Phi hair materials disable main-light color and indirect-light usage
  and use very weak HM/spec/rim energy. The active renderer data also lacks the
  existing Wuwa hair-shadow feature.
- The first 2.2.6 validation intentionally restored the original Unity scene
  after checking in-memory clones. The follow-up request explicitly authorizes
  a new persisted material directory and renderer-slot replacement, so scene
  mutation is now in scope while the original material assets remain immutable.
- The full-body and bust references use different lighting and backgrounds.
  Material-layer relationships, highlight coverage, and face continuity are
  acceptance criteria; matching either reference's absolute exposure is not.
- The public selected-mesh export driver correctly rejects this Blender scene
  because the requested materials span one fixed-workflow surface rather than
  one selected mesh. Deterministic validation therefore invoked the extension's
  public `export_material_bundle` entry point once per requested material in an
  isolated Blender 5.2.0 process.
- The active fixed-workflow hair BaseMap in the validation export is a blue
  functional layer, not the renderer's authored golden albedo. The persisted
  hair materials consequently preserve their official Base/HM maps and apply
  only the brighter 2.2.6 hair profile; blindly overlaying that exported primary
  map visibly turns the hair blue.

## Decision log

- 2026-08-03: Make the fix generic to `wuwa_toon`; reject material-name and
  character-name rules.
- 2026-08-03: Treat active-output reachability as authoritative for primary
  color/emission roles while retaining explicitly identified off-chain
  auxiliary maps such as FaceSDF, ID, HET, and HairHM.
- 2026-08-03: Superseded the earlier four-texture eye plan after explicit user
  confirmation. Bind only the existing `EyeHET` role, sample it twice in the
  shader, expose independent upper/lower offsets and scales, and separate base
  eye emission from highlight emission. Do not add an `EyeBottomHighlight`
  interchange role.
- 2026-08-03: Reuse `Down_ID` for both ordinary ID shading and the recognized
  stocking feature. A `StockingsMap` material binding may tag the same resource
  for compatibility, but a different resource is invalid and the shader samples
  `_IDMap` only once.
- 2026-08-03: Accept a single `Separate Color` hop in the stocking recognizer
  and transcode fixed-workflow TARGA inputs through Blender 5.2 `imbuf` to PNG.
  This preserves the existing bundle media-type/schema contract and does not
  mutate the source image datablock.
- 2026-08-03: Keep stockings opaque and simulate sheerness with mask-, normal-,
  and view-dependent color. Real transparency would regress depth, shadows, and
  sorting.
- 2026-08-03: Preserve `_MikuHead*WS` runtime overrides. New local face vectors
  are the material baseline, not a breaking replacement for animated binders.
- 2026-08-03: Persist the follow-up materials under the versioned
  `菲比_2.2.6` directory and update existing assets in place on rerun. This
  preserves their GUIDs and scene references while leaving the original
  `菲比` directory untouched.
- 2026-08-03: Treat `Use ID Stockings` as derived binding state. `Down` enables
  it only when `_IDMap` and `_StockingsMap` reference the same recognized
  linear resource; `Up` is explicitly cleared and disabled. The switch does
  not author the stocking region.
- 2026-08-03: Add only `_BodyEmissionStrength` to the public material surface.
  Eye and Face calibration reuses existing properties; no schema, workflow,
  package identity, or texture-role change is permitted.

## Implementation sequence

1. Add fixed-workflow reachability metadata and role scoring, then recognize the
   active voice-mark, existing EyeHET role, and exact Wuwa stocking ID chain.
2. Extend the existing Wuwa texture bindings/keywords without changing package
   identity or adding character-specific schema fields.
3. Update Wuwa Face, Eye, Body, Effect, and Hair shaders plus recommended
   profiles.
4. Add Python, Blender smoke, CPU-math, shader-contract, binding, migration, and
   Unity EditMode tests; update English canonical documentation and release data.
5. Build twice, compare manifests and SHA-256, install only from the canonical
   archive, re-export twice, import, apply the explicit profile to requested
   validation materials, and capture focused visual evidence.

## Validation

- Run targeted Python tests and the full repository PR profile.
- Launch only the fixed Blender executable, assert `bpy.app.version == (5, 2, 0)`,
  and run fixed-workflow smoke exports for disconnected `_D`, nested eye groups,
  and the ID-to-threshold stocking chain.
- Run Unity EditMode tests for texture-role bindings, one-sample ID stocking
  math, face-basis transforms, HDR eye composition, profiles, and renderer
  feature installation; verify a clean error console after shader import.
- Build the extension and Unity package twice and compare manifests and hashes.
- In the private validation scene, verify black voice mark, independently offset
  single-mask upper/lower eye highlights, ID-bounded stockings, stable FaceSDF
  light response under rotated light, brighter unclipped hair, and no changes
  outside the stocking mask.

## Results and follow-up

Implementation and validation completed on 2026-08-03.

- `tools/ci/run_checks.py --profile pr`: passed; 237 Python tests, canonical
  boundary/schema/identity checks, and both package builds passed.
- Blender 5.2.0 fixed-workflow smoke: passed with
  `MIKU_FIXED_WORKFLOW_TEXTURES_SMOKE_OK`. The only output was Blender's
  `Material.use_nodes` deprecation warning for Blender 6.0.
- The saved private Phi blend was exported twice from canonical source in
  isolated headless Blender processes. All 50 output files were byte-identical.
  Voice mark bound only the active black resource as `BaseMap`; the official
  HET TARGA became one PNG resource bound only as `EyeHET`; `Down_ID` was the
  same SHA-256 resource for `IDMap` and `StockingsMap`.
- Unity EditMode `MikuGameToon225Tests`: 14/14 passed. The five final Wuwa
  shaders reported zero compiler messages through `ShaderUtil`. The installed
  package cache manifest/hash matched canonical source.
- Unity validation used 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0, and D3D12.
  Focused screenshots were saved under the validation project's
  `Assets/miku/Screenshots/` for the whole character, eye/face, and stockings.
  Existing material assets were not rewritten: recommended 2.2.6 values and
  the ID stocking binding were applied only to in-memory clones for evidence,
  then the clean original scene was restored.
- Deterministic artifacts:
  `miku_shader_converter-2.1.2.zip` is 190999 bytes with SHA-256
  `b4503704029eded3106c68ba63eed9d74f4915efc784a6bce8a8e7af6c2aa06d`;
  `com.miku.shaderconverter-2.2.6.tgz` is 378621 bytes with SHA-256
  `fdf30e86a7733def7e9e12b88171ddfffc7381835d0359cffeea4472d456e892`.

No schema version, package ID, workflow ID, or public texture role was added.
The private blend still reports its pre-existing dependency-cycle warnings;
they are unrelated to export output and were not modified by this work.

The persisted replacement continuation also completed on 2026-08-03.

- Eye defaults are now base brightness `1.2`, highlight emission `1.0`, no base
  emission, threshold `0.7`, and upper/lower scales `0.32`/`0.22`. Face defaults
  are flatness `1`, curve power `1.2`, and unit base/final brightness. Body adds
  `_BodyEmissionStrength` with default `1.0`; all three material CBUFFERs carry
  it, the emission sample uses it once, and MatCap defaults to `0.15`.
- Canonical source passed `python tools/ci/run_checks.py --profile pr` after the
  package identity manifest was regenerated: 237 Python tests and both package
  builds passed. The continuation artifacts remained deterministic across two
  builds: Blender ZIP SHA-256
  `b4503704029eded3106c68ba63eed9d74f4915efc784a6bce8a8e7af6c2aa06d`;
  Unity TGZ SHA-256
  `1a8a1f7c9f14716ea9204297fd226e285557fb398f958433c7ab1696ca3cf212`.
- The nine private materials were exported twice. Excluding `export.log`, both
  trees contained the same 81 relative files with identical SHA-256 hashes.
  `Down` bound one linear ID resource to both `IDMap` and `StockingsMap`;
  `eye` exposed one official `EyeHET` resource and no bottom-highlight role;
  the voice mark bound the active black-mark BaseMap rather than the disconnected
  colored `_D` image.
- Nine new material assets were created under
  `Assets/鸣潮/Materials/菲比_2.2.6/`. The original material files remained
  byte-identical. Renderer slots 0, 1, 2, 3, 4, 5, 11, 12, and 14 were replaced,
  the scene was saved and reopened, all nine references persisted, and all
  unrequested slots remained unchanged.
- Unity 6000.4.5f1 EditMode `MikuGameToon225Tests` passed 15/15. All five Wuwa
  shaders reported zero `ShaderUtil` compiler messages and the final console had
  zero errors. Full-body and bust screenshots were inspected at original
  resolution. Two additional bust captures with the main light yaw shifted
  from 145 degrees to 100 and 190 degrees showed continuous FaceSDF transitions
  without cheek or nose-wing pits; camera and light transforms were restored by
  reloading the clean scene.
- The thresholded EyeHET source covers about 40.57 percent before UV scaling;
  applying the configured upper/lower scales bounds combined highlight coverage
  to about 6.12 percent of the eye UV domain, below the 12 percent acceptance
  ceiling and without forming a full white iris ring.

No schema version, package ID, workflow ID, or public texture role changed in
the continuation. `_BodyEmissionStrength` is the only new public shader
property. The validation screenshots and private material assets remain local
to the Unity validation project and are not included in public package output.
