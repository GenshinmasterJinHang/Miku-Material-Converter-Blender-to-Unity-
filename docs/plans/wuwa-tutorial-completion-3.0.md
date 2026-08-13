# Miku 3.0 WuWa tutorial completion

> **2026-08-13 supersession:** the private request/response validation builder
> recorded in this historical plan was removed from the distributable package.
> Its execution record is preserved; Phoebe fixtures and scene construction now
> remain entirely outside the public package. The candidate hashes and test
> totals below are historical; `docs/release/miku-3.0.0.md` records the later
> cleanup candidate built and tested from the final canonical source.

## Purpose and outcome

Complete the already-started WuWa tutorial renderer so Body, Hair, Face, and
Eye share the URP 17.4 PBR/GI foundation described by the tutorial, while
preserving the existing authored NPR layers.  The observable outcome is a
private Phoebe validation scene built from the user-provided seven-part FBX and
textures, plus a deterministic public Miku 3.0 package whose tests do not
contain or redistribute those private assets.

## Context and constraints

- The canonical roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The implementation started from `main@d3b1e06`.  Before implementation the
  working tree advanced to an uncommitted 2.4.0 Genshin/HSR/Endfield change set.
  Those edits are user-owned and must be preserved.  WuWa changes are applied
  incrementally without reverting or formatting unrelated files.
- `vibe-kanban/` is an unrelated nested untracked repository and is excluded.
- The external Phoebe model and textures are private validation inputs.  They
  may be copied only into the local Unity validation project and must not enter
  Git, UPM archives, fixtures, or public screenshots.
- The authoritative model has seven material regions, four UV sets, vertex
  colors, custom normals, 273 bones, and no animation or blend shapes.  No
  eighth region or morph data will be fabricated.
- Supported validation versions are Blender 5.2.0 at the constitution-mandated
  path, Unity 6000.4.5f1, URP 17.4.0, and Shader Graph 17.4.0 on Windows.

## Progress

- [x] 2026-08-12: Re-audited current committed WuWa code, dirty worktree, fixed
  workflow contracts, public APIs, tests, and live Unity validation state.
- [x] 2026-08-12: Confirmed the live project references the canonical package
  by `file:` and that the existing scene still uses the wrong static FBX and
  mixed 2.2/2.3 textures.
- [x] 2026-08-12: Implemented URP BRDF/GI, packed NRM, corrected
  gradient/SDF/Hair paths, and alpha-consistent Bangs passes.
- [x] 2026-08-12: Implemented tutorial Screen Rim, fixed hair-shadow
  screen-offset/RT contract, LD outline color, fixed-workflow roles,
  clone-only migration, and the private validation builder.
- [x] 2026-08-12: Corrected WuWa outline passes to the dedicated
  `MikuToonOutline` LightMode and made the WuWa installer add the Geometry
  Renderer Feature transactionally. The previous `SRPDefaultUnlit` tag was
  replacing the Forward PBR result in Forward+.
- [x] 2026-08-12: Before the later Forward+ regression was found, Python passed
  270/270, certified Blender 5.2.0 headless smoke passed 8/8, and the earlier
  D3D11 Unity candidate passed 306/309 with zero failures and three skips.
  These are retained as historical evidence, not final-package validation.
- [x] 2026-08-12: Built the private seven-renderer Phoebe scene from the exact
  FBX hash, verified 273 bones and selected textures, and captured eight D3D12
  views in isolated Unity processes.
- [x] 2026-08-12: Built byte-identical 3.0 release candidates, verified package
  identity, and completed migration, ADR, compatibility, and release docs.
- [x] 2026-08-12: Repaired the post-validation Forward+ main-light regression,
  added source-contract and real D3D12 pixel tests, installed the deterministic
  final TGZ, and repeated live shader and front-view visual validation.
- [x] 2026-08-12: Revalidated the user-reported outline/rim follow-up after
  installing the missing Geometry Feature in the active Renderer, exposing
  the consumed Fresnel power, and calibrating only the private model materials.

## Discoveries

- The first WuWa pass added helper names but not the tutorial data flow:
  `BRDFData`, `SAMPLE_GI`, `EnvironmentBRDF`, and lightmap variants are absent.
- The handwritten direct specular uses `4*LoH*NoH` as normalization and clamps
  the highlight; URP 17.4 derives the normalization from roughness.
- Phoebe `_N` textures encode DirectX tangent normals in RG, metallic in B, and
  roughness in A, but the importer currently marks them as Unity NormalMap and
  the shader calls `UnpackNormal`, losing the BA contract.
- Gradient channel 2 silently selects UV0, and strength zero currently applies
  the strongest low-color multiplication instead of disabling the effect.
- The current WuWa outline reads vertex-color G, while ADR 0016 and one legacy
  test still require a neutral constant.  A superseding ADR is required.
- The current package identity is already uncommitted 2.4.0 work.  The WuWa
  breaking renderer is therefore layered on that work and changes the final
  release identity to 3.0.0 rather than reverting it.
- WuWa Body/Hair/Face used `SRPDefaultUnlit` on their outline pass. In URP
  Forward+ that pass was selected as an ordinary object pass after Forward and
  wrote the dark outline color across the complete visible mesh. The dedicated
  `MikuToonOutline` tag plus Geometry Renderer Feature is required for the PBR
  forward result to remain visible.
- Batch-mode URP does not reliably refresh main-light globals between several
  manual `Camera.Render` calls in one editor frame. Release visual evidence is
  therefore captured one requested view per fresh D3D12 Unity process; this is
  an evidence-tool constraint, not a scene runtime limitation.
- The live PC Renderer uses Forward+ (`m_RenderingMode: 2`). URP 17.4's
  `GetMainLight()` selects its constant directional attenuation only in the
  `_CLUSTER_LIGHT_LOOP` variant. The WuWa Forward passes omitted that variant,
  fell through to Forward's per-object `unity_LightData.z`, and multiplied the
  new direct PBR result by zero. Low GI strengths made the affected materials
  appear almost completely black.
- The live camera uses `PC_Renderer`, which had Hair Shadow and Screen Rim but
  no Geometry Renderer Feature. Consequently no `MikuToonOutline` pass was
  scheduled. Screen Rim was already operational; its pixel radius does not
  control the separate forward Fresnel band, whose active `_FresnelPower`
  property was incorrectly hidden as a legacy value.

## Decision log

- 2026-08-12: Replace the default WuWa lighting behavior directly; do not add a
  legacy/tutorial material switch.  Consequence: release as 3.0.0 and provide
  clone-based material migration.
- 2026-08-12: Use official URP BRDF/GI helpers and keep current Eye, stockings,
  SSS, UV7 smooth-normal, and Fresnel extensions as post-foundation layers.
- 2026-08-12: Keep generic Screen Rim legacy behavior by default and add an
  explicit WuWa tutorial algorithm selected by the WuWa setup path.
- 2026-08-12: Treat five `Switch_D` images as mutually exclusive BaseMap
  presets.  Keep the unverified tacet/star effect unbound and diagnostic-only.
- 2026-08-12: Build the external validation scene transactionally in a cloned
  destination; never mutate the existing scenes/materials in place.
- 2026-08-12: Match URP 17.4's official Lit shader contract by compiling the
  `_CLUSTER_LIGHT_LOOP` variant in all four WuWa PBR Forward passes. Do not
  bypass `distanceAttenuation` in shared lighting, because doing so would break
  Forward culling-mask semantics and hide future pipeline mismatches.
- 2026-08-12: Keep the package's declared 6000.0/17.0 floor by selecting
  `_FORWARD_PLUS` below Unity 6000.1 and `_CLUSTER_LIGHT_LOOP` from 6000.1
  onward using the official `UNITY_VERSION >= 60010000` encoding boundary.
  Both official technical lines expose the five-argument reflection-probe
  overload, so normalized screen UV is passed through shared indirect lighting
  instead of relying on the non-Cluster convenience overload.
- 2026-08-12: Preserve every WuWa rim property reference and numerical default,
  but expose `_FresnelPower` and label Fresnel/Screen-shared controls separately
  from Screen-only controls. Model-specific artistic calibration remains in
  private project materials and does not change package defaults.

## Implementation sequence

1. Replace WuWa shared math with packed-NRM decoding, URP BRDF initialization,
   direct lighting, `SAMPLE_GI`, reflection-probe Environment BRDF, complete
   UV0-UV3 selection, hard/soft SDF, and corrected strength composition.
2. Migrate Body/Hair/Face/Eye forward passes to the shared foundation; add
   Bangs alpha clipping to every coverage pass and LD outline sampling.
3. Add the WuWa Screen Rim algorithm and fixed hair-shadow texture/screen-offset
   contract; make installers idempotently select WuWa mode.
4. Extend fixed texture roles, import settings, recommended profiles, keyword
   sync, diagnostics, and clone-based 3.0 migration/setup tooling.
5. Update version identities, ADRs, compatibility, migration, changelogs, and
   historical 2.3 documentation without rewriting unrelated 2.4 work.
6. Build the private local scene, verify exact model/texture bindings and render
   views, then build and hash final packages.
7. Add the missing URP Cluster Light Loop variants, lock the contract with an
   EditMode source test, and repeat the live Forward+ D3D12 acceptance capture.
8. Reapply the transactional WuWa Renderer Feature installer to the active
   Renderer, clarify the two rim control paths, calibrate the private model,
   and repeat EditMode plus D3D12 screenshot acceptance.

## Validation

- `python -m unittest discover -s tests -p "test_*.py"` must be run and every
  failure triaged; no WuWa or package-identity drift failure may remain hidden.
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe` must report
  `bpy.app.version == (5, 2, 0)` and pass the Blender exporter suite.
- Unity EditMode must cover URP-reference BRDF values, packed RGBA decoding,
  MatCap gating, all four UV choices, gradient disable semantics, SDF A/B,
  Hair HM, hair-shadow projection, tutorial rim, pass clipping, importer
  settings, migration rollback, and all five WuWa shader compilations.
- The deterministic UPM TGZ must pass Direct3D 12 EditMode and graphics
  acceptance without `-nographics`; the live local project must compile and
  render on D3D12 with an empty console.
- Private validation must retain the recorded FBX/texture, renderer, skinning,
  UV, color, and UV7 evidence. The post-regression repair must recapture the
  live front view and pass the independent Forward+ main-light pixel test; the
  earlier side, opposite-light, distance, and hair-shadow views remain
  historical rather than being relabeled as final-package evidence.

## Results and follow-up

The WuWa Forward+, outline, and rim repair is complete.

- Root cause: the Forward passes omitted the pipeline keyword variant, so URP
  17.4 Forward+ read `unity_LightData.z == 0` through the ordinary Forward
  branch and zeroed direct lighting. Body, Hair, Face, and Eye now select the
  Unity-line-appropriate keyword and use the Forward+-safe reflection overload.
- Final-TGZ Unity: Unity 6000.4.5f1, URP/Shader Graph 17.4.0, and Direct3D 12
  ran 323 EditMode tests in 152.27 seconds: 322 passed, zero failed, and one was
  skipped because `MIKU_103_REGRESSION_BUNDLE_ROOT` was not configured.
- Graphics: all four required D3D12 graphics tests passed. The new
  `WuwaBodyForwardPlusUsesDirectionalMainLight` test creates a Forward+
  renderer, disables GI/reflection/emission fallbacks, renders once, and
  verifies a non-black center pixel with the directional light's color.
- Shader and visual evidence: Body, Hair, Face, and Eye are supported, report
  `ShaderHasError == false`, and have zero compiler messages. The final TGZ was
  installed into the private validation project, its front view visibly
  responds to the main light, and `Assets/鸣潮/鸣潮.unity` remained clean.
- Outline and rim evidence: the active Renderer contains one enabled Geometry,
  Screen Rim, and Hair Shadow Feature. The saved material values render the
  dedicated outlines; comparing them with temporary zero rim brightness changed
  337,789 pixels (4.07% of the 3840x2160 frame). Diagnostic property blocks were
  cleared and the active scene remained clean.
- Determinism and identity: two independent TGZ builds were byte-identical at
  SHA-256
  `870c15fc6b03d5d999b4e6ed39d21cacc13fa86224da9a99699c654ff8f55135`.
  PackageCache matched all 263 payload files semantically; Unity only injected its
  expected `_fingerprint` into the semantically identical `package.json`.
  `python tools/miku_package_identity.py --check` passed after regeneration.
- Python: `python -m unittest discover -s tests -p "test_*.py"` ran 270 tests;
  269 passed and one pre-existing `gameToonScreenRim` profile-hash assertion
  failed. The Wuwa rim-visibility contract now passes; the remaining hash is a
  release-wide Core target-profile reconciliation, not rendering evidence for
  this Unity-only repair.
- Blender was not rerun because this repair changes only the Unity package.
  The existing Blender ZIP was hash-checked at
  `dc2461eb21bcd4b678ae9781c6391b839158aaf45e233dabd87642222f7a66fb`.
- The standalone `run_unity_dx12_gpu.ps1` process was not launched while the
  same project was open in Unity. Its four required tests were instead run by
  the active D3D12 Editor, and the script now requires the new Forward+ test.
- Unity 6000.0 / URP 17.0 and Unity 6000.5 / URP 17.5 were source-reviewed but
  were not installed locally. Exact-lane compile runs remain outside this
  repair's available environment.
- Private assets and screenshots remain only under the local Unity validation
  project and are excluded from Git, release archives, fixtures, and public
  documentation.

### 2026-08-13 post-plan Face-SDF verification correction

The results above remain the historical WuWa completion-candidate record. A
later cross-family regression repair added read-only WuWa Face diagnostics and
two opposed-yaw pixel assertions in one required D3D12 test: debug SDF mask and
normal final color. The exact final TGZ is
`760dc9b365f7a1329483e63ca34ff23f88e5f0a3da7827ab774d7df6146bcb75`.
It passed 324/333 full EditMode tests with zero failures (seven D3D12-only and
two optional skips) and all seven required D3D12 tests with zero skips. The
earlier private-scene capture is not relabeled as evidence for this final TGZ.
