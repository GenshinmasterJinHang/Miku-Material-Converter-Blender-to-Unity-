# Main-light and face-SDF Forward+ regression repair

## Purpose and outcome

Repair the reported Unity 6 URP Forward+ regression in which the Genshin Body,
Hair, and Face shaders ignore the Directional Main Light and the face SDF is
therefore absent from the final image.  Add deterministic GPU proof that both
Genshin and WuWa face-SDF masks change with light yaw and affect final pixels.
Genshin Eye remains intentionally unlit.  Existing WuWa material values remain
user-owned and are never rewritten by this repair.

## Context and constraints

- The canonical implementation root is
  `unity/Packages/com.miku.shaderconverter/`; the external Hu Tao, Furina, and
  Phoebe assets are private validation inputs only.
- The working tree already contains substantial user changes.  This work must
  edit only the relevant shader, editor-test, GPU-runner, and documentation
  regions and must not reset, reformat, or replace unrelated changes.
- The open Unity validation scene is unsaved.  Automated validation must use a
  separate process/project copy or wait until the editor is closed; it must not
  save or mutate that scene or its source materials.
- Unity 6000.0 / URP 17.0 uses `_FORWARD_PLUS`; Unity 6000.1+ / URP 17.1+
  uses `_CLUSTER_LIGHT_LOOP`.  Unity 6 encodes `UNITY_VERSION` as `6MMMPPPP`,
  making `60010000` the correct boundary.
- Windows graphics evidence requires Direct3D 12 and must not use
  `-nographics`.

## Progress

- [x] 2026-08-13: Reproduced the data-flow failure by source and live-project
  inspection; confirmed the validation renderer is Forward+.
- [x] 2026-08-13: Confirmed WuWa face SDF inputs and head basis vary with light
  yaw and that the shader routes `faceLight` into final direct lighting.
- [x] 2026-08-13: Added the missing version-gated Forward+ variant to every Genshin program
  that calls `GetMainLight`.
- [x] 2026-08-13: Added non-mutating WuWa face-material diagnostics.
- [x] 2026-08-13: Added source, shader-compile, and D3D12 light-yaw regression
  tests; retained the existing CPU face-SDF direction coverage.
- [x] 2026-08-13: Built two byte-identical release candidates and validated the
  exact final TGZ in isolated EditMode and Direct3D 12 projects.
- [x] 2026-08-13: Completed strict private-model pixel checks from an isolated
  clone without saving or modifying the currently open validation project.
- [ ] Capture one manual Frame Debugger screenshot of a selected
  `UniversalForward` draw showing `_CLUSTER_LIGHT_LOOP`; Unity returned zero
  Frame Debugger events to both bounded batch and hidden-GUI automation probes.

## Discoveries

- Genshin Body and Hair each have Forward and backface lighting programs, and
  Genshin Face has one Forward program.  All five call `GetMainLight` and use
  `shadowAttenuation * distanceAttenuation`, but none currently compile a
  Forward+/Cluster variant.  Under Forward+ this can read zero from the
  non-cluster per-object attenuation path, suppressing all direct light and the
  Face SDF result.
- WuWa Body, Hair, Face, and Eye already use the correct `60010000` keyword
  boundary.  WuWa Face computes `faceLight`, maps it through
  `_FaceShadowStrength`, and passes it into `Wuwa_DirectPBR`; the missing proof
  is a final-pixel yaw test, not another unverified formula rewrite.
- Existing GPU coverage renders only one fixed-light WuWa Body frame and tests
  for a non-black pixel.  It cannot detect a frozen light direction or a face
  SDF disconnected from final output.

## Decision log

- 2026-08-13: Preserve Genshin Eye as unlit, per user choice.
- 2026-08-13: Preserve all authored WuWa material values.  Add diagnostics for
  missing SDF input, zero SDF strength, invalid face basis, and invalid texture
  import settings; do not auto-apply the recommended profile.
- 2026-08-13: Reuse the existing WuWa version-gated pragma contract in Genshin.
  Do not compile both keywords unconditionally because that adds variants and
  triggers the deprecated keyword path on Unity 6000.1+.
- 2026-08-13: Require two proofs for each face shader: debug mask changes with
  light yaw, and final color changes with debug disabled.
- 2026-08-13: Treat identical WuWa main/soft SDF channels and an active debug
  view as informational. All other new Face SDF diagnostics are warnings. The
  Inspector localizes their messages but exposes stable, language-neutral codes.
- 2026-08-13: Extend the repair to the directly adjacent URP shadow variants:
  use `GetShadowCoord` for screen-space main shadows and declare the three URP
  soft-shadow quality keywords. The yaw probes still isolate direction and
  attenuation; they do not claim a rendered penumbra comparison.

## Implementation sequence

1. Add the version-gated Forward+ pragma block to Genshin Body Forward and
   backface, Hair Forward and backface, and Face Forward programs.
2. Add a read-only WuWa Face diagnostic helper and show its diagnostic codes in
   the custom material inspector without changing the material.
3. Add program-scoped source tests, CPU face-SDF direction tests, shader compile
   checks, and deterministic D3D12 Forward+ captures for Genshin Body/Hair and
   both Genshin/WuWa Face debug/final output.
4. Extend the D3D12 runner required-test list, correct compatibility claims,
   and record the behavior-only change in both changelogs.
5. Run focused EditMode tests, the Python suite, deterministic package builds,
   and the D3D12 GPU lane.  Install only the resulting canonical package in a
   clean validation copy and compare its PackageCache manifest and SHA-256.

## Validation

- Source tests must discover each `HLSLPROGRAM` containing `GetMainLight(` and
  assert it contains exactly the `UNITY_VERSION >= 60010000` branch with the
  two appropriate keyword pragmas.
- ShaderUtil must report no errors for Genshin Body/Hair/Face and WuWa Face.
- D3D12 tests must assert `GraphicsDeviceType.Direct3D12` and a Forward+
  renderer.  Opposite light-yaw captures must exceed a fixed luminance delta;
  face tests must pass both `_FaceSdfDebugMode = 5` and debug-off comparisons.
- Validate Unity 6000.4.5f1 with URP/Shader Graph 17.4.0 locally.  Other
  declared 6000.0-6000.5 technical lines remain Experimental until their own
  compile/GPU lanes run.
- Private model acceptance uses cloned assets and verifies the original scene
  and material hashes are unchanged before and after testing.

## Results and follow-up

Implementation and package-level validation are complete. The final Unity TGZ
SHA-256 is
`760dc9b365f7a1329483e63ca34ff23f88e5f0a3da7827ab774d7df6146bcb75`.
Two independent builds produced byte-identical TGZ, ZIP, and `SHA256SUMS.txt`
files; archive manifests matched at 251 TGZ and 33 ZIP members.

The exact TGZ ran on Unity 6000.4.5f1 revision `cc83ebd631f8`, URP 17.4.0,
and Shader Graph 17.4.0. Full `-nographics` EditMode discovered 333 tests:
324 passed, zero failed, and nine skipped (seven D3D12-only tests and two
external/visual optional tests). The isolated `-force-d3d12` lane then passed
all seven required graphics tests with zero skips or inconclusive results.

The exact final TGZ was also installed into an isolated clone of the saved
private validation project. Under D3D12 and a verified Forward+ renderer, Hu
Tao, Furina, and Phoebe were rendered from the front at main-light yaw -60,
0, and +60 degrees. The clone-only runner rasterized each Face material
submesh into its own pixel mask and required every adjacent and endpoint pair
to change in both `_FaceSdfDebugMode = 5` and the normal final color. All three
characters passed. Endpoint non-face final-color changed-pixel ratios were
18.00%, 7.25%, and 13.10%; Face SDF debug endpoint ratios were 31.88%, 6.97%,
and 26.02% for Hu Tao, Furina, and Phoebe respectively. The evidence JSON and
screenshots remain outside the repository. Original scene SHA-256 values were
unchanged before and after the run, including the separately open unsaved
validation project, which was never saved or modified.

The only incomplete acceptance item is a manual Frame Debugger screenshot of
a selected draw showing `_CLUSTER_LIGHT_LOOP`. Two bounded attempts to query
Unity's Frame Debugger event API (batch mode and a hidden GUI clone) returned
zero events, so Forward+ configuration was not relabeled as draw-level keyword
evidence. No interchange schema, shader name, material property reference,
public C# API, or serialized material structure changed.

### 2026-08-13 final follow-up evidence

- The visual-fidelity follow-up produced final TGZ SHA-256
  `a950c60659da3aa842835773c89f6de4c77ecd217602eae48527796fc8e5d7c9`.
  Two independent builds were byte-identical; their manifests remained 251 TGZ
  and 33 ZIP members.
- The exact final TGZ passed 343 EditMode tests: 333 passed, zero failed, and
  ten skipped (eight D3D12-only plus two external/visual optional tests). The
  exact same TGZ passed all eight required D3D12 tests with zero skips.
- Before modifying private assets, 222 scene/material/texture-meta/package files
  were copied to `C:\miku-scene-backups\shadow-metal-20260813-1810` and every
  source/backup SHA-256 pair matched. The active scene was clean.
- The final package was installed in the external project. Hu Tao/Furina
  control textures were audited; both Hu Tao 256x20 ramps reimported at their
  original 256x20 size. Phoebe's official SDF and skin ramp were audited as
  uncompressed control data, preserving their 1024x1024 and 512x25 source
  dimensions. All three audit folders reported zero changes on the second run.
- The requested material calibrations were applied without a scene migration,
  recommendation profile, or Head Binder. Both target scene-file SHA-256 values
  remained unchanged because only referenced materials and texture importer
  metadata changed.
- A new post-calibration clone of the two requested scenes passed front-view
  D3D12/Forward+ captures at yaw -60, 0, and +60. Hu Tao and Phoebe changed in
  every adjacent and endpoint comparison for both debug mode 5 and normal final
  colour. Phoebe's normal-colour endpoint changed 22.66% of the Face-material
  ROI; debug-classified visible quartiles had 30.35%-50.17% median luminance
  separation across the three angles, exceeding the requested 10% threshold.
  Private PNG/JSON evidence remains outside the repository at
  `C:\miku-unity-private-acceptance\final-a950c606\evidence`.
- A selected-draw Frame Debugger capture is still pending; verified Forward+
  configuration and compiled Cluster variants are not relabeled as that
  draw-level evidence.

## 2026-08-13 shadow, metal, and Face-SDF fidelity follow-up

### Purpose and outcome

- Preserve the Forward+/Cluster repair while separating URP realtime shadow
  visibility from the authored Genshin toon-ramp coordinate. Partial PCF
  visibility must blend continuously in final colour instead of selecting an
  unrelated discrete ramp band.
- Restore the tutorial metal contract: LightMap R selects metal, view-space
  normal RG samples the metal map, and that environment colour replaces
  diffuse in metal regions without being multiplied by the main light.
- Make the WuWa Face skin ramp interpolate from the authored shadow tint and
  calibrate the private Phoebe face material so debug mode 5 and final shading
  express the same directional SDF mask.

### Progress

- [x] 2026-08-13: Reproduced the Hu Tao artefact and isolated realtime
  self-shadow attenuation as the primary source; temporary light/material
  probes were fully restored and no asset was saved.
- [x] 2026-08-13: Audited Phoebe Face SDF channels, transition values, skin
  ramp, SSS, texture import state, and final direct-light path.
- [ ] Implement shader, diagnostic, texture-audit, CPU, EditMode, and D3D12
  changes.
- [ ] Rebuild the deterministic 3.0.0 archives and replace validation evidence
  only after the exact final TGZ passes.
- [ ] Back up and explicitly calibrate the two requested private scenes without
  adding a head-basis binder or running broad material migrations.

### Discoveries and decisions

- Disabling the directional light's shadow map removes the irregular bands;
  changing LightMap G, `_BodyShadowSmooth`, shadow angle, or shadow strength in
  isolation does not. The existing multiplication of PCF visibility into the
  ramp coordinate is therefore rejected.
- `_BodyShadowSmooth * 0.02` and `_HairShadowSmooth * 0.02` make the documented
  defaults nearly hard steps. The serialized values will become the normalized
  transition widths directly.
- `_MainShadowInfluence` is additive and defaults to Body 0.25, Hair 0.35, and
  both Face shaders 0. Existing property names and interchange schemas stay
  unchanged. Eye remains intentionally unlit.
- Genshin metal is an environment term. `_MetalIntensity` remains the artistic
  multiplier, with a new/default recommended value of 1, but main-light colour,
  distance, and shadow attenuation are removed from its calculation.
- WuWa skin-ramp strength zero must return `_ShadowTint`, not white. Face debug
  modes 1-4 remain raw-channel inspection; debug mode 5 remains the computed
  mask used by final shading.
- Texture repair remains an explicit user action. It will additionally enforce
  NPOT None, uncompressed data, disabled crunch/platform overrides, and include
  those fields in the deterministic audit report; diagnostics never mutate.

### Validation additions

- CPU/EditMode tests cover toon-signal independence from realtime visibility,
  monotonic final shadow blending, direct softness width, tutorial metal
  boundaries/composition, WuWa shadow-tint fallback, read-only diagnostics,
  and idempotent texture repair including 256x20 NPOT preservation.
- D3D12 Forward+ tests add controlled blocker/penumbra and metal-only probes.
  Private-scene acceptance uses fixed -60/0/+60 light yaw and requires at least
  ten percent median luminance separation between debug-5-classified WuWa Face
  light and shadow pixels in normal mode.
- The release remains 3.0.0. Unity 6000.4.5f1 revision cc83ebd631f8 with URP
  and Shader Graph 17.4.0 on Windows D3D12 is the only supported evidence line;
  other Unity 6000.x combinations remain Experimental.
