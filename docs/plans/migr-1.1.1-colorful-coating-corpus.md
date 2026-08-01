# MiGR 1.1.1 Colorful Coating Corpus Support

## Purpose and outcome

Make every material in the locked Blender `彩色镀层.blend` corpus export as a
sealed MiGR bundle and import into Unity 6000.4.5f1 URP/Shader Graph 17.4.0.
View-dependent Layer Weight chains remain editable native Shader Graph math,
runtime-independent procedural islands are baked independently, and the two
closure-heavy materials are emitted as explicitly diagnosed URP Lit
approximations rather than silently flattened or submitted to a whole-material
UV bake.

## Context and constraints

- The authoritative corpus is the saved Blender file whose SHA-256 is
  `B02D5D317AF2787023A71993D90CEACEB2066917637338FEFD95157F9ABD7942`.
- It contains 14 materials. Thirteen are assigned to meshes with `UVMap`;
  `Dots Stroke` has a constant active Principled surface and no mesh binding.
- Canonical feature roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`,
  `extensions/migr_gpl_bake_worker/`, and
  `unity/Packages/com.migr.shaderconverter/`. Retired B2U code is out of scope.
- The dirty worktree contains intentional MiGR 1.0/1.1 work and must be
  preserved. Edits are incremental and do not clean unrelated changes.
- Validation uses only Blender 5.2.0 LTS at the fixed Steam executable and
  Unity 6000.4.5f1 with URP/Shader Graph 17.4.0 on Windows 11.
- MaterialIR, bundle, and bake document kinds remain `migr-*-1.0`; component
  versions advance to 1.1.1.
- Generated Sub Graphs are MiGR-owned. Existing wrapper Shader Graphs remain
  user-owned unless Full Regeneration is explicitly requested.

## Progress

- [x] 2026-07-28: Scanned the saved corpus with Blender 5.2.0 and locked its
  SHA-256, 14-material inventory, node groups, ramp modes, and mesh/UV bindings.
- [x] 2026-07-28: Confirmed the current failures: Hue/Saturation in materials 2
  and 7, a mixed runtime/static Noise and Bump chain in material 5, a dynamic
  two-key B-Spline ramp in material 6, and unresolved mixed closures in
  materials 8 and 9.
- [x] 2026-07-28: Confirmed `Dots Stroke` is affected only by unreachable nodes
  outside its active material output.
- [x] 2026-07-28: Implemented active-output reachability, Hue/Saturation,
  two-key B-Spline,
  static expression islands, and closure channelization.
- [x] 2026-07-28: Extended the GPL bake worker and Shader Graph 17.4 backend;
  real 64px island bakes passed for materials 5, 8, and 9.
- [x] 2026-07-28: Added tests, diagnostics, compatibility documentation, and version 1.1.1
  release metadata.
- [x] 2026-07-28: Passed 113 Python/Core tests, the fixed Blender runtime and
  corpus oracles, and 36 Unity EditMode tests on 6000.4.5f1/D3D11.
- [x] 2026-07-28: Built deterministic release artifacts, installed the Blender
  extensions and embedded Unity package safely, and re-exported/re-imported
  the complete corpus.
- [x] 2026-07-28: Reopened this plan after the visual black-sphere regression
  showed that compilation-only validation had not checked for an effective Lit
  normal.
- [x] 2026-07-28: Reproduced black centers for materials 1, 3, and 10 and
  isolated the cause to a constant tangent normal `[0, 0, 0]`; changing only
  normal strength to use the geometry normal restored center luminance to
  approximately `0.669`, `0.528`, and `0.551`.
- [x] 2026-07-28: Implemented unlinked zero-normal normalization in Core and
  the Shader Graph 17.4 legacy-bundle compatibility path, without changing the
  Fresnel or Layer Weight formulas.
- [x] 2026-07-28: Passed targeted Python tests and the fixed Blender 5.2 corpus
  structural smoke, including neutral Normal, World-space runtime inputs, and
  deterministic expression IDs for materials 1, 3, and 10.
- [x] 2026-07-28: Passed 39/39 Unity EditMode tests, including the legacy
  zero-normal structure/binding test and the D3D11 GPU preview acceptance.
- [x] 2026-07-28: Passed the complete PR profile, deterministic package builds,
  installed-extension export, full 14-material Unity reimport, render checks,
  and ownership/GUID stability audit.

## Discoveries

- The Blender snapshot currently flattens every node and chooses the first
  Material Output record, so an unreachable Diffuse node and inactive output
  can create an unnecessary bake job for `Dots Stroke`.
- Current closure slot extraction handles only Principled and recursive Mix
  Shader branches. Emission, Diffuse, and Anisotropic closures therefore leave
  every required channel unresolved without a specific diagnostic.
- Current runtime Color Ramp lowering accepts only two-key LINEAR and EASE.
- Current mixed runtime/static routing can bake an entire PBR channel but
  cannot replace a maximal static subgraph inside a dynamic channel.
- The Unity test project embeds package 1.0.0 and therefore expects the old
  target-profile hash; current Blender 1.1 bundles use the 1.1 profile.
- The fixed Steam Blender uses its portable extension repository under
  `C:\SteamLibrary\steamapps\common\Blender\portable\extensions\user_default`,
  not the roaming AppData extension copy.
- Maximal-island boundaries may initially land on synthetic closure-channel
  nodes. Synthetic nodes are lowered natively so the actual Blender
  node/socket boundary remains locatable and bakeable.
- The pre-1.1.1 exporter profile hash was `a42e4399...`, while the 1.1.0 Unity
  package expected `e5af9bcb...`. The zero-normal fix produces the current
  `2bfabadc...` profile; the importer retains bounded compatibility for the
  immediately preceding `b5198d82...` profile and the already documented
  1.1/1.0 hashes.
- The bundle semantic whitelist originally rejected otherwise valid
  `ExpressionIsland` resources. The bundle validator and schema now validate
  their binding key, expression identity, usage, UV binding, color space, and
  tangent-space normal convention.
- Python 3.13 and the Unity/Mono runtime did not serialize every binary64 value
  to the same shortest round-trip decimal. The Unity canonical JSON writer now
  reproduces Python's formatting and exponent thresholds, including negative
  zero and tie-to-even selection.
- A generated wrapper with its first texture-bearing Sub Graph input had no
  texture slot template to clone. The backend now creates a deterministic
  `Texture2DMaterialSlot` fallback and marks it as an input.
- Unity `-batchmode -quit` can finish domain reload with imports queued but
  before the editor delay callback drains the MiGR queue. Validation therefore
  used the same private scheduler pump in a live editor session and waited for
  all jobs to reach a terminal state.
- The original corpus validation proved that Shader Graph assets compiled, but
  did not prove that the Lit `Normal TS` input was a valid non-zero direction.
  Blender's unconnected Principled Normal socket reports `[0, 0, 0]`; treating
  that authoring sentinel as a tangent normal makes URP lighting black.
- The externally saved 14-material corpus used for this reopened validation now
  has SHA-256
  `B02D5D317AF2787023A71993D90CEACEB2066917637338FEFD95157F9ABD7942`.
  This corrects the earlier `AF2B...` lock for the current saved validation
  input; both corpus runners were updated together.

## Decision log

- 2026-07-28: Preserve the native editable path. Materials 8 and 9 use a
  documented `MIGR_CLOSURE_FLATTENED_APPROXIMATE` URP Lit channelization rather
  than Custom Function or ShaderLab generation.
- 2026-07-28: Only the corpus case of Anisotropic/Glossy with zero anisotropy is
  accepted. Non-zero anisotropy remains unsupported.
- 2026-07-28: Additive MaterialIR expression operations and bake-job fields
  remain inside the existing 1.0 document family. Unknown operations still
  fail explicitly.
- 2026-07-28: Runtime-independent procedural subgraphs inside dynamic chains
  become mesh-bound `ExpressionIsland` resources. Runtime parents are never
  baked.
- 2026-07-28: The local corpus is validation input only; the Blender binary is
  not copied into the public repository.
- 2026-07-28: Explicit batch material names may include an unbound material.
  It exports when no mesh bake is required; a material that genuinely needs a
  mesh still fails with the specific bake diagnostic.
- 2026-07-28: Coarse static Region jobs are replaced by one channel-scoped job
  covering the proven linked static PBR semantics, avoiding duplicate full
  channel executions.
- 2026-07-28: Establish a target-neutral invariant at MaterialIR construction:
  only an unlinked constant Normal zero sentinel becomes `[0, 0, 1]`. Linked
  expressions, bake resources, and legitimate non-zero constants are not
  rewritten.
- 2026-07-28: Keep a bounded Unity 1.1.1 compatibility defense because already
  exported sealed bundles cannot be repaired at the Blender source boundary.
  Binding reports `MIGR_LEGACY_ZERO_NORMAL_NORMALIZED`; the Shader Graph backend
  connects `[0, 0, 1]` to `Normal TS`.
- 2026-07-28: Do not modify `Math.DielectricFresnel`,
  `Math.LayerWeightFresnel`, or `Math.LayerWeightFacing`; the in-memory normal
  experiment proved those formulas were not the black-output cause.

## Implementation sequence

1. Restrict semantic graph construction to the active Surface output chain and
   retain warnings for safely ignored unreachable nodes.
2. Add portable Hue/Saturation snapshot semantics and runtime lowering, and
   expand two-key B-Spline ramps into target-neutral arithmetic.
3. Channelize supported Diffuse, Emission, zero-anisotropy Glossy, and recursive
   Mix Shader closures with explicit approximation diagnostics.
4. Partition maximal runtime-independent expression islands, add
   `Texture.SampleBaked2D` leaves and deterministic `ExpressionIsland` jobs,
   and preserve specific failure diagnostics.
5. Extend the external GPL worker to bake and merge channel and expression
   resources without mutating source materials.
6. Extend the Shader Graph 17.4 adapter with expression Texture2D properties,
   UV0 sampling, color/scalar selection, normal decoding, and native
   Hue/Saturation nodes.
7. Add tests and documentation, update all components to 1.1.1, build
   deterministic packages, install after Blender and Unity close, and validate
   all 14 materials.
8. Normalize the unlinked Blender Normal sentinel at MaterialIR construction
   and add a legacy constant guard in the Shader Graph 17.4 backend and material
   binder.
9. Add structural, binding, and GPU preview regressions, then repeat the
   deterministic build/install/reimport and generated-asset ownership checks.

## Validation

- `py -3.13 tools/ci/run_checks.py --profile pr`
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/migr_runtime_inputs_smoke.py`
- A dedicated Blender 5.2 corpus runner must export all 14 saved materials
  twice and compare normalized documents, resource IDs, and file hashes.
- `tools/ci/run_unity_editmode.ps1` must run against Unity 6000.4.5f1 with
  `-force-d3d11` and import all generated corpus bundles.
- Build both Blender ZIPs and the Unity TGZ twice and compare bytes and
  SHA-256 before installation.
- Targeted Core regression:
  `py -3.13 -m unittest
  tests.test_migr_runtime_inputs.RuntimeExpressionTests.test_unlinked_zero_normal_is_canonicalized_without_touching_real_normals
  tests.test_migr_runtime_inputs.RuntimeExpressionTests.test_fresnel_and_layer_weight_are_not_shader_graph_power_shortcuts`
- Corpus structural acceptance:
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background --python
  tests/blender/migr_colorful_coating_corpus_smoke.py`
- Run graphics acceptance with a real D3D11 device; the ordinary
  `-nographics` EditMode lane may skip that test but cannot count as its visual
  evidence.

## Results and follow-up

- The installed Steam Blender 5.2.0 extensions exported all 14 materials twice
  into `.validation/colorful-coating-full-1.1.1`. Both passes produced 14
  bundles and 124 files with identical per-file SHA-256 hashes.
- Export diagnostics contained no errors and no
  `MIGR_REQUIRED_CHANNEL_UNRESOLVED`. The expected successful diagnostics were
  9 runtime inputs preserved, 9 static channel bakes, 3 expression-island
  bakes, and 20 explicit closure-flattening approximation warnings.
- The Unity 6000.4.5f1/D3D11 test project imported all 14 bundles: 14 generated
  Sub Graphs, 14 user-owned wrappers, 14 materials, and 14 receipts. The queue
  finished with 14 committed jobs and zero failed jobs.
- Unity shader inspection found 14 generated shaders, zero shader compiler
  errors, and all shaders supported. Six expression textures, including two
  tangent-space normal textures, were bound without missing resources.
- Two repeated seven-material import batches changed no generated or
  user-owned asset bytes.
- The release artifacts were built twice with identical hashes; the final
  post-fix values are recorded below.
- The known intended limitation is that materials 8 and 9 are editable URP Lit
  approximations, not physical Blender closure parity. Light Path, non-zero
  anisotropy, XR, panoramic cameras, custom SRPs, and other material libraries
  remain outside this plan.
- Reopened-result correction: the prior successful compile/import result did
  not establish visual correctness. The zero-normal fix and its targeted Core
  plus Blender structural tests now pass.
- The final PR profile passed 114 Python/Core tests, canonical source-boundary
  validation, nine schemas, package identity, and release-package builds.
  Unity 6000.4.5f1 on Direct3D11 passed all 39 EditMode tests with zero skips.
- Installed Blender 5.2.0 exported the clean 14-material corpus twice into a
  124-file tree with identical hashes. Installed extension contents matched
  their release ZIP members. Final deterministic artifact SHA-256 values were
  `7de3e7da2bb72a802874d5a4f27f4b4ca53839788fba13d877abde0eace28ade`
  (semantic exporter),
  `c0689a0256c1e000374eff3cfeed902af10b9745133be6519b2a37a7b42c5734`
  (GPL bake worker), and
  `928e7bb9220e388572fd038986e2917ad85c81040cbd961de08720cd088d8775`
  (Unity package).
- Two Unity import passes produced 14 wrappers, 14 generated Sub Graphs, 14
  generated base materials, 14 Material Variants, and 14 receipts. All 28
  user-owned wrapper/variant content hashes and GUIDs stayed unchanged; the
  complete 119-file output tree was byte-stable and all 14 shaders were
  supported without compiler errors.
- A final import from the `2bfabadc...` bundles through the installed TGZ
  succeeded for all 14 materials. It changed none of the 56 snapshotted
  wrapper/Material Variant content-or-meta hashes, and all 14 imported
  wrapper shaders remained compiler-error-free.
- Direct3D11 preview renders of materials 1, 3, and 10 measured center
  luminance `0.657`, `0.526`, and `0.539`, with center-to-edge RGB distance
  `0.594`, `0.560`, and `0.490`. The console remained free of errors after the
  final refresh.
- The pre-existing `Assets/migr` validation folders use an older nested output
  layout and lack a current root identity sidecar. The importer correctly
  refused to overwrite that unowned data, so final ownership validation used
  the isolated `Assets/MiGRReview/ColorfulCoatingZeroNormal` root. Migrating
  unrelated legacy validation assets is outside this fix.
