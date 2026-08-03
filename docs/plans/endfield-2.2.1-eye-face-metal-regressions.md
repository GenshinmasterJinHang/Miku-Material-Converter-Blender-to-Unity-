# Endfield 2.2.1 Eye, Face, Blush, and Metal Regression Repair

## Purpose and outcome

Repair the Endfield 2.2 character rendering regressions reported against the
Jie Ge validation character. The observable 2.2.1 result must have eye shadow
coverage that clips from bottom to top, opaque brows and lashes, authored iris
color with a restrained anime cornea response, readable face SDF lighting,
bright but non-emissive skin, an authored cheek blush, and visible cloth and
hair-accessory metal reflections.

## Context and constraints

- Canonical implementation roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. The validation project and its
  installed package are deployment targets, never implementation roots.
- The worktree contains substantial user-owned Miku 2.x work, including the
  currently untracked Endfield package sources. Preserve all unrelated changes
  and do not rewrite or clean the worktree.
- The validation tuple is Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0,
  Windows D3D11. The package manifest requires Unity 6000.4.
- Existing `杰哥_2.2` materials are rollback assets. Validation changes create
  a separate `杰哥_2.2.1` material set and `Validation/2.2.1` evidence.
- MaterialIR remains schema 2.0. Existing texture roles, shader property names,
  material slot order, and `_AlphaSource` values 0, 1, and 2 are compatibility
  surfaces and must remain stable.
- MyZmdShaders has no declared license and is behavioral evidence only. The MIT
  Endfield Character Rendering repository is also behavioral evidence; no
  implementation source is copied.

## Progress

- [x] 2026-08-02: Verified canonical roots, package identity/version, exact
  validation tuple, dirty worktree, relevant shaders, materials, and textures.
- [x] 2026-08-02: Confirmed the eye-shadow R gradient, iris binding, Emotion
  Atlas blush mask, packed material channels, and hair accessory mask polarity.
- [x] 2026-08-02: Locked the anime-cornea direction and visibly stylized blush
  strength with the user.
- [x] 2026-08-02: Implemented finite shader math, additive material properties,
  full 0-7 custom debug selectors, and the requested debug outputs.
- [x] 2026-08-02: Added EditMode regression coverage and updated the package,
  compatibility, provenance, changelog, and release documentation.
- [x] 2026-08-02: Passed Python, package, focused/full Unity, D3D11, finite HDR,
  and deterministic-build validation; deployed the final archive.
- [x] 2026-08-02: Created, bound, captured, and saved the reversible 2.2.1
  scene/material evidence without modifying the 2.2 rollback materials.

## Discoveries

- `T_actor_common_eyeshadow_01_M.png` has red values that decrease from roughly
  173 at the top to zero at the bottom, while G/B/A are constant. Inverse red
  therefore clips in the reported wrong direction; raw red has the required
  bottom-to-top behavior.
- The eye material already binds `T_actor_aglina_iris_01_D.png`. The regression
  is in `EndfieldEvaluateSimple`, which treats it as generic diffuse and samples
  MatCap from world-space normals without an iris/cornea model.
- `T_actor_common_female_emotion_atlas_01_D.png` is a 2x2 atlas. Tile zero alpha
  contains a symmetric authored cheek mask, so blush can be independently
  sampled without adding a texture role.
- The brow/lash overlay uses face color-map luminance as coverage. Dark feature
  pixels consequently become transparent even though the feature submesh
  itself supplies the intended coverage.
- Cloth material parameters are correctly decoded as R metallic, G
  reflectivity, B AO, and A smoothness. The black metal regression is caused by
  the lighting response, not the packed mask polarity.
- `sw_M.r` is white over normal strands and black over small accessory islands.
  `1 - sw_M.r` is the correct accessory mask; the accessory reflection response
  is too dark because it derives F0 mainly from near-black hair albedo.
- Body base alpha has a wide AO-like distribution. Taking it through a hard
  minimum with direct lighting prevents lit skin from reaching its authored
  brightness.

## Decision log

- 2026-08-02: Correct the earlier 2.2 plan statement that eye shadow uses
  inverse red. Preserve inverse red as legacy enum value 2, add raw red as value
  3, and add opaque coverage as value 4.
- 2026-08-02: Implement a restrained anime cornea: shallow tangent-space
  parallax, analytic hemispherical cornea normal, view-space MatCap, and bounded
  wet highlight. Do not implement a refractive eye mesh or emission.
- 2026-08-02: Keep blush independent from `_EmotionTileIndex`. Sample tile zero
  through `_BlushTileIndex`, default `_BlushStrength` to zero for compatibility,
  and set the Jie Ge 2.2.1 material to 0.60 with `(1, 0.82, 0.88, 1)` tint.
- 2026-08-02: Keep the public MaterialIR and texture-role set unchanged. All
  new controls are additive Shader properties; package and recipe version move
  to 2.2.1.
- 2026-08-02: Replace narrow ad-hoc metal specular with bounded GGX direct and
  environment response while retaining the existing packed channels and
  Kajiya-Kay strand lobe.

## Implementation sequence

1. Add reusable finite lighting, GGX, face-SDF phase, atlas UV, cornea, and
   alpha-source helpers to the Endfield runtime include.
2. Split Eye and Mouth evaluation, add compatible Eye/Skin/Face/Overlay shader
   properties, and repair body, skin, face, hair, overlay, and debug behavior.
3. Add deterministic EditMode tests for public properties, enum compatibility,
   alpha coverage direction, face-SDF phase, atlas independence, and finite
   metal/cornea math.
4. Raise package and recipe version to 2.2.1 and update canonical changelog,
   compatibility, package documentation, release notes, and provenance.
5. Run focused tests, the PR profile, and two canonical builds. Compare archive
   manifests and SHA-256 hashes before deploying through Unity MCP.
6. In the validation project, clone `杰哥_2.2` to `杰哥_2.2.1`, change only the
   intended material properties, rebind all fourteen slots, save the scene, and
   capture the acceptance evidence without altering source textures or FBX data.

## Validation

- `python -m unittest tests.test_miku_fixed_workflows`
- `python tools/ci/run_checks.py --profile pr`
- Focused and full Unity EditMode tests through Unity MCP after compilation.
- Two consecutive `python tools/build_miku_unity_package.py` builds whose
  normalized manifests and SHA-256 hashes are identical.
- Eye-shadow thresholds 0, 0.15, 0.30, 0.45, 0.60, 0.75, and 1 must have
  monotonically decreasing coverage and an upward-moving coverage centroid.
- Graphics evidence must cover opaque brows/lashes, iris visibility, camera
  parallax/highlight movement, zero-light non-emission, mirrored face SDF,
  blush on/off and expression independence, lit skin, and non-black metal.
- Unity Console and shader compilation must contain zero errors. HDR validation
  output must contain no NaN or Infinity.

## Results and follow-up

Implementation and validation are complete.

- `python tools/ci/run_checks.py --profile pr` passed with 228 Python tests,
  schema validation, package identity validation, and both package builds.
- Focused Unity EditMode coverage passed 15/15. The first initialization attempt
  timed out before discovering tests; the immediate retry with the assembly
  filter and a 120-second initialization window passed. The complete editor
  assembly run passed 128/129 with zero failures; one external Miku 1.0.3
  regression-bundle test was intentionally skipped because
  `MIKU_103_REGRESSION_BUNDLE_ROOT` was not supplied.
- Two consecutive final package builds produced the identical SHA-256
  `fbbae09925b30b32bb8f7335138023e21bac09d9d9935f15fde7ddb54b20bafb`.
  Unity Package Manager deployed that archive as `com.miku.shaderconverter`
  2.2.1 after the MCP package-source deployment action reported that its local
  source directory was not configured.
- The fourteen renderer slots all point to `杰哥_2.2.1`. The original
  `杰哥_2.2` directory remains unchanged and available for rollback. Final
  material state has every debug view at zero, raw-red eye shadow at clip 0.02,
  opaque brow/lash coverage, blush tile zero at strength 0.60, skin AO strength
  0.35, and the requested cornea defaults.
- The eye-shadow captures at 0, 0.15, 0.30, 0.45, 0.60, 0.75, and 1 contained
  268, 267, 252, 194, 96, 0, and 0 changed pixels respectively. Their screen-Y
  centroids moved from 224.959 to 223.177 before disappearing, proving the
  surviving coverage moves upward while area decreases monotonically. The
  forced-D3D11 768 x 768 replay independently measured 856, 852, 777, 603, 311,
  0, and 0 pixels with centroids moving from 371.799 to 368.531.
- D3D11 was forced in an isolated copy of the exact validation project. Unity
  6000.4.5f1 reported Direct3D11 on the NVIDIA RTX 5070 Laptop GPU; a 1024 x
  1024 ARGBFloat render contained 4,194,304 channel samples, zero NaN, zero
  Infinity, minimum 0, and maximum 4.412076. The second warm render contained
  the full character and the log contained no MIKU shader compile errors. A
  second isolated replay generated the complete acceptance matrix from the
  final package; its warm-run log contained zero compile/error matches and its
  final HDR scan again found zero NaN/Infinity (maximum 1.35200119).
- `Assets/endfield/Validation/2.2.1` contains the final full/face/torso views,
  left/right light sweeps, face-SDF and blush diagnostics, metal/accessory masks
  and specular contributions, zero-light evidence, the alpha-clip sweep, and
  `Jiege_2.2.1_D3D11.png` plus its finite-value text report. The temporary D3D11
  project copy was moved to the Recycle Bin after its artifacts were imported.

The accepted limitation is a single-layer anime cornea approximation rather
than physically refracted ocular geometry. Blush depends on tile zero of the
current female Emotion Atlas. Metal remains scene-light dependent, but the mask
response no longer collapses authored metallic regions to black.
