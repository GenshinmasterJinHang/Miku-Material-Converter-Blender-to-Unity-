# Miku 2.4.0 Genshin tutorial repair and DX12 Furina validation

> **2026-08-13 supersession:** the local Furina creator, DX12 guard helper, and
> UV1 diagnostic helper recorded in this historical plan were removed from the
> distributable 3.0 package. The executed results remain historical evidence;
> current GPU acceptance is automated with external fixtures.

## Purpose and outcome

Miku 2.4.0 replaces the maintained Genshin fixed-shader lighting with an
independent implementation of the attached tutorial semantics, schedules UV1
backfaces and UV7 outlines as explicit URP RenderGraph passes, strengthens the
fixed-workflow texture contract, and validates the result with the user's local
Furina assets on Unity 6000.4.5f1 / URP 17.4.0 / Windows Direct3D 12.

## Context and constraints

- Canonical source is limited to `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The visual reference is the local snapshot of `PTSXDWD/URP_GenshinImpact` at
  commit `a258a9ef6e18bf45afbbf390a1dacf87f512f231`. It has no license. No source,
  FBX, texture, material, metadata, screenshot, or derived mesh from that
  snapshot may enter the repository or distributable archives.
- Local validation assets live only below
  `Assets/MikuValidationLocal/Genshin/Furina/` in the external Unity project.
- The working tree already contains unrelated Endfield, HSR, documentation, and
  package-identity edits. They are user-owned and must be preserved.
- Material IR 2.0 and Bundle/Plan/Manifest/Receipt 1.0 schemas stay unchanged.
- Windows graphics acceptance for new work is Direct3D 12. Null-device and
  historical Direct3D 11 results are not 2.4.0 graphics evidence.

## Progress

- [x] 2026-08-12: Verified the canonical source markers, Unity package 2.3.0,
  Blender manifest 2.3.0, Unity 6000.4.5f1 / URP 17.4.0 validation project, and
  dirty-worktree boundaries.
- [x] 2026-08-12: Audited the reference Shader, Furina mesh/texture roles, current
  Genshin Shader implementation, renderer scheduling, target profile, and import
  workflow without copying third-party content.
- [x] 2026-08-12: Implemented the Direct3D 12 repository/profile/validation
  rule and separated headless EditMode from real-device acceptance.
- [x] 2026-08-12: Implemented Genshin lighting, alpha, explicit backface, and
  outline passes.
- [x] 2026-08-12: Implemented Genshin material state, renderer feature,
  installer, import
  contract, texture audit, and mesh channel mapping.
- [x] 2026-08-12: Built the local Furina validation scene and source manifest
  without adding the FBX, textures, generated materials, scene, or captures to
  the canonical repository or distributable package.
- [x] 2026-08-12: Ran Python, Blender 5.2.0, focused Unity EditMode, Direct3D 12
  device, Frame Debugger, and deterministic archive validation. The full Unity
  Editor assembly still has one unrelated Wuwa NUnit discovery error; it is
  recorded below rather than hidden.
- [x] 2026-08-12: Completed Genshin documentation, compatibility evidence,
  release notes, package identity, and final self-review.

## Discoveries

- Furina is one static mesh with four submeshes. Only the Dress/Body submesh has
  useful UV1 data; Face, EffectHair, and Hair must not enable UV1 backfaces.
- The reference tangent tool overwrites the real tangent and sets tangent W to
  zero. Miku must retain the real Mikk tangent and store outline normals in UV7
  TangentSpaceV2.
- A RenderObjects pass-name list is fallback selection, not repeated drawing.
  Backface and outline require separate RendererLists.
- Current Miku still serializes `graphicsApi: D3D11`, and its headless Unity
  script forces D3D11 while also using `-nographics`. Headless and GPU evidence
  must be separate commands.
- Current Genshin fixed workflow recognizes `Normalmap` after normalization but
  misses `FaceLightmap`, `Body_Shadow_Ramp`, and `Hair_Shadow_Ramp`.

## Decision log

- 2026-08-12: Replace the default Genshin lighting; do not add a reference-mode
  keyword.
- 2026-08-12: Keep `_DiffuseA` as the only serialized alpha-mode field and expose
  a typed public API over it.
- 2026-08-12: Add `_UseUv1Backface` as the 2.4 material-state authority while
  retaining the old double-sided properties as migration inputs.
- 2026-08-12: Direct3D 12 is a hard release/GPU-validation gate, not a universal
  import ban on non-Windows or headless editors.
- 2026-08-12: Preserve historical D3D11 documents as facts; only current rules,
  current Profile, and new 2.4.0 claims change to D3D12.

## Implementation sequence

1. Add the repository D3D12 rule; update target/runtime metadata, version
   constants, implementation-family hashes, and legacy profile handling.
2. Centralize Genshin tutorial math and alpha coverage, then update Body, Hair,
   Face, Eye, depth, shadow, mask, backface, and outline passes.
3. Add the runtime material-state API and RenderGraph geometry renderer feature;
   upgrade the installer to all active UniversalRendererData assets.
4. Extend fixed-workflow inference, required-role validation, role-based import
   policy, texture audit, and non-destructive mesh channel mapping.
5. Add the ignored local Furina generator and DX12 acceptance harness.
6. Add lowest-layer tests, update public docs and compatibility records, build
   deterministic packages, install the canonical TGZ, and run acceptance.

## Validation

- `python -m unittest tests.test_miku_fixed_workflows tests.test_genshin_tutorial_conformance -v`
- `python tools/ci/run_checks.py --profile pr`
- `C:/SteamLibrary/steamapps/common/Blender/blender.exe --background ...`
  after asserting `bpy.app.version == (5, 2, 0)`.
- Unity EditMode suites for Bundle import, Genshin math/Shader contracts, Game
  Toon renderer features, mesh mapping, and texture import.
- A separate Unity invocation with `-force-d3d12` and no `-nographics`, asserting
  `SystemInfo.graphicsDeviceType == Direct3D12` before rendering or capture.
- Two consecutive Blender extension builds and two Unity TGZ builds must have
  identical manifests and SHA-256 values.

## Results and follow-up

The following evidence was produced from the canonical source and the external
validation project on 2026-08-12:

- Python: `python -m unittest discover -s tests -p "test_*.py"` passed 270/270.
  `python tools/ci/run_checks.py --profile pr` also passed, including schema,
  package-identity, fixed-workflow, documentation, and deterministic build
  checks.
- Blender: the fixed executable reported `bpy.app.version == (5, 2, 0)` and the
  isolated installed-extension Standard, bake-worker, and determinism smoke
  checks passed. The final Blender ZIP SHA-256 is
  `dc2461eb21bcd4b678ae9781c6391b839158aaf45e233dabd87642222f7a66fb`.
- Unity EditMode: the tutorial/Game Toon selection passed 43/43. The final
  Genshin tutorial plus Bundle importer selection passed 178, failed 0, skipped
  2 (180 total); the skips are the pre-existing optional external 1.0.3 fixture
  and a graphics preview that now correctly requires a real Direct3D 12 device
  instead of running in the Null-device headless suite.
  After the final canonical TGZ was installed, the Genshin tutorial class was
  run once more and passed 13/13 with no skips.
  A complete Editor-assembly run reported 303 passed, 5 failed, and 1 skipped:
  the expected Direct3D12 assertion failed while that headless run used D3D11,
  three order-sensitive failures passed when rerun in isolation, and one
  unrelated Wuwa NUnit `Property Count` discovery error remains. The complete
  assembly result is therefore not represented as a clean pass.
- Direct3D 12: Unity 6000.4.5f1 with URP and Shader Graph 17.4.0 was launched
  with `-force-d3d12` and without `-nographics`. The real-device acceptance test
  passed 1/1 after asserting `GraphicsDeviceType.Direct3D12`. The Windows player
  API list in the validation project contains only Direct3D12 and the project
  uses Linear color space.
- GPU scene: the local `Furina_Miku.unity` scene produced 1920x1080 front, back,
  left, and right captures. Frame Debugger recorded four ordinary opaque
  submesh draws, one `MikuGenshinBackface` draw for Dress/Body only, and four
  `MikuToonOutline` submesh draws in that order. Face, EffectHair, and Hair did
  not execute the backface pass. The camera generator was corrected to aim at
  the imported renderer bounds while retaining the reference camera position
  and FOV, avoiding pivot/scale-dependent cropping.
- Determinism: two consecutive Blender package builds and two Unity package
  builds were byte-identical. The final Unity TGZ SHA-256 is
  `87a30faba60371abdd2679dcb78744ccdb7ae3d1b0e7c44230b5ebbc5609c635`.
  The TGZ was installed through Package Manager from a project-local tarball;
  the resolved PackageCache content passed the canonical package-identity
  manifest check.
- Profile compatibility: the current canonical Target Profile hash is
  `8b53d91957a6695c2b2b7d7d3eb182d63617b2fc0739e668b347c3e4b6ebb95b`,
  and the Genshin implementation hash is
  `3ed1078b96a630e302f6e1d2ae74e3fe9061fef8c4639927c1043c140ccb66ec`.
  The exact 2.3.0 profile hash remains an accepted compatibility input with an
  explicit visual-migration diagnostic. Interchange schema versions did not
  change.

The automated shader-contract and CPU mirror tests cover day/night ramp
selection, normal-map enablement, MatCap selection, five outline IDs, Face SDF
directionality, emission, and shared cutout coverage. This run did not save a
separate real-GPU screenshot for every one of those individual toggles, nor a
synthetic cutout shadow/depth image sequence; those are visual-regression
extensions rather than evidence claimed by this plan result.

### 2026-08-13 post-plan Forward+ correction

The captures and hashes above remain historical 2.4.0-candidate evidence; their
fixed views did not prove response to main-light yaw. The 3.0.0 repair adds the
Unity-line-gated Forward+/Cluster variant to all five lit Genshin programs and
requires final-pixel yaw differences for Body/Hair plus Face SDF debug and
normal output. The exact final 3.0.0 TGZ
`760dc9b365f7a1329483e63ca34ff23f88e5f0a3da7827ab774d7df6146bcb75`
passed those tests as part of the seven-test D3D12 lane with zero skips. The
private Hu Tao/Furina scene remains open and unsaved, so it was not modified or
claimed as final-TGZ visual evidence.
