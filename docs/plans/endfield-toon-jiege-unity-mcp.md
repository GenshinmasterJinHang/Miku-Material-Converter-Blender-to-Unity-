# Endfield Toon and Jie Ge Unity MCP Scene Delivery

## Purpose and outcome

Add the target-neutral `endfield_toon` workflow and a first-party Unity 6 URP
fixed game-Toon family, then use the canonical package build and Unity MCP to
materialize the supplied Jie Ge character in the validation scene. The
observable result is a saved `Assets/Scenes/1.unity` containing exactly one
`杰哥` object whose fourteen renderer slots use external Endfield materials,
whose mesh clone contains deterministic smooth outline normals in UV7, and
whose hair-shadow slot uses the supplied texture as a stencil-clipped overlay.

## Context and constraints

- Canonical sources are `miku/`, `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`. The Unity validation project and
  PackageCache are never implementation roots.
- The worktree already contains substantial user-owned Miku 2.0 and GameToon
  changes. This work must preserve them and avoid unrelated rewrites.
- Supported validation versions are Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0,
  Shader Graph 17.4.0, and Windows.
- Game workflow ShaderLab assets are manually maintained fixed presets, not
  generated Shader Graph output. The generated Standard PBR backend remains
  Shader Graph-only.
- MyZmdShaders is an unlicensed behavioral reference. No source is copied.
  Endfield assets supplied in the validation project remain outside the public
  package and are used only for local validation.
- User-owned `.mat` assets and `Assets/Scenes/1.unity` are modified only through
  Unity MCP after installing a deterministic package built from canonical
  source.

## Progress

- [x] 2026-08-02: Repaired Endfield 2.1 material semantics as 2.2, including
  object-matrix head axes, flattened 32-cube color LUTs, part-specific face,
  skin, hair, and cloth lighting, precise texture roles, migration, and visual
  regression coverage.
- [x] 2026-08-02: Verified canonical source markers, package identity, dirty
  worktree, Unity instance, active scene, imported model, textures, renderer
  features, and current compiler error.
- [x] 2026-08-02: Inspected fixed-workflow contracts, existing game Shader
  passes, screen-rim RenderGraph feature, material recipes, and mesh data tool.
- [x] 2026-08-02: Added `endfield_toon` to Core/schema contracts and deterministic texture
  role inference.
- [x] 2026-08-02: Added Endfield Unity material roles, parts, shaders, templates, audit/setup
  utilities, tests, documentation, and release metadata.
- [x] 2026-08-02: Upgraded UV7 generation for non-readable imported meshes without importer
  mutation.
- [x] 2026-08-02: Built the package twice and verified manifest/SHA-256 stability.
- [x] 2026-08-02: Deployed through Unity MCP, bound all fourteen materials, replaced the scene
  mesh, frame the camera/light, save, test, and capture screenshots.

## Discoveries

- The 2.1 shared fragment path incorrectly treats every Endfield part as the
  same material model. In particular, skin discards base-map alpha AO, the
  1024x32 skin and cloth color LUTs are sampled as one-dimensional lighting
  ramps, and the common hair-line texture is multiplied into hair albedo.
- Unity samples the imported flattened LUT with source-image bottom row at UV
  zero. The first 2.2 draft inverted G a second time and visibly reproduced the
  packed cloth map's green/cyan appearance. Removing that inversion restores
  the D-map albedo while retaining trilinear slice interpolation.
- Eye-shadow `_M` stores coverage in R while G/B are constant cyan. Showing its
  RGB is incorrect; the overlay now uses inverse-R coverage with a separate
  warm shadow tint. The face-highlight map has opaque alpha and sparse
  luminance, so its overlay also uses luminance coverage instead of alpha.
- Hair uses an explicit dual-lobe Kajiya-Kay response. The inverse `sw_M.r`
  region identifies non-strand accessories for a separate isotropic reflection
  and a reduced outline width.
- The supplied static mesh uses local +X as right, -Y as forward, and +Z as up.
  Its renderer transform supplies the complete orientation, so face SDF and
  head-hair normals must derive their orthonormal world basis from the current
  object-to-world matrix rather than a bone or scene binder.
- The face refinement texture, face custom-area mask, hair refinement mask,
  hair shift texture, and hair-line texture have different semantics despite
  sharing broad `_ST` or `_M` filename suffixes. The 2.1 `OutlineMask` and
  `FaceSDFMask` inference is therefore too coarse.
- The live validation project is Unity 6000.4.5f1 at `Desktop/unity/test`; its
  active scene is `Assets/Scenes/1.unity` and is ready for tools.
- The scene already contains Main Camera, Directional Light, and one `杰哥`
  MeshRenderer with fourteen FBX-embedded URP/Lit material references.
- The imported mesh has 78,434 vertices and fourteen submeshes. It is marked
  non-readable, but editor read-only mesh data can be acquired without changing
  importer settings.
- Slot 9 (`发影`) is separate overlay geometry. The supplied 32x32
  `T_actor_common_hairshadow_01_M` is a vertical red-channel ramp suitable for
  inverse-R alpha coverage.
- `PC_Renderer` already contains an active
  `MikuToonScreenRimRendererFeature`; no duplicate installation is required.
- The installed package still contains a stale `MikuGenericToonTests.cs` and
  currently fails compilation. A canonical 2.1.0 deployment must replace it
  before scene setup.

## Decision log

- 2026-08-02: Release the semantic repair as 2.2.0 because it adds public fixed
  workflow texture roles. MaterialIR remains schema 2.0: the role set is
  additive, while legacy 2.1 role names remain accepted and migrate with the
  `MIKU_ENDFIELD_ROLE_MIGRATED` diagnostic.
- 2026-08-02: Endfield face and head-hair directions use the whole renderer's
  object-to-world matrix with local basis right +X, forward -Y, and up +Z.
  Negative-scale handedness is resolved against the transformed +X direction.
  No head bone or `MikuFaceSdfHeadBinder` is added to the validation scene.
- 2026-08-02: Store the head center as `_HeadCenterOS`. The validation setup
  derives it from face submesh bounds and moves it inward by 55 percent of the
  face radius. The tail material disables head-sphere normals.
- 2026-08-02: Keep MaterialIR schema at 2.0 and add an enum value rather than
  introducing a new schema version; the change is additive and old documents
  remain valid.
- 2026-08-02: Keep generic material creation template-only. The automatic
  fourteen-slot mapping is a validation-project-specific MCP setup operation,
  not a general importer heuristic.
- 2026-08-02: Use separate Endfield Face/Eye/Mouth/Overlay/HairShadow shaders so
  each submesh has explicit render-state semantics instead of hidden fallbacks.
- 2026-08-02: Hair shadow uses the face stencil plus inverse red-channel alpha;
  it has no ShadowCaster, outline, or screen-rim pass.
- 2026-08-02: Smooth normals are written to a clone at
  `Assets/endfield/Generated/杰哥_SmoothOutline.asset`; the FBX and importer stay
  unchanged.
- 2026-08-02: Shared Endfield textures use URP inline trilinear-repeat and
  linear-clamp samplers. This stays below D3D11's sixteen active sampler limit
  without changing texture-role bindings.
- 2026-08-02: Eye-shadow and eye-highlight overlays are mask-only. Neutral
  expression overlay is bound for later selection but clipped by default; eye
  MatCap is disabled on the validation material to prevent color stacking.

## Implementation sequence

0. Append the 2.2 repair discoveries and decisions to this plan before source
   edits; preserve all completed 2.1 evidence below as historical state.
1. Add precise Endfield texture roles, legacy aliases, import profiles,
   migration diagnostics, recipe version 2.2.0, and compatibility tests.
2. Split the shared Endfield forward evaluation into body, skin, face, and hair
   material models. Implement object-matrix head axes, flattened 32-cube color
   lookup, face SDF/refinement masks, and directional two-normal hair lighting.
3. Build and compare two canonical archives, deploy through Unity MCP, create
   reversible `杰哥_2.2` materials, calculate `_HeadCenterOS`, bind all fourteen
   slots, save the scene, and capture full-body, bust, debug, and light-sweep
   evidence.
1. Extend Core workflow/part/role validation and JSON schema; add strict full
   filename rules for Endfield resources and tests for ambiguity and color
   space.
2. Extend Unity workflow selection, recipe version, role/property mapping,
   keyword synchronization, and user-owned material-template UI.
3. Add a shared Endfield HLSL implementation and nine fixed ShaderLab assets
   with explicit forward, outline/mask, depth, stencil, and transparent passes.
4. Upgrade mesh cloning to read non-readable sources through
   `MeshUtility.AcquireReadOnlyMeshData`, preserve topology/submeshes, and write
   UV7 only on the clone; add EditMode coverage.
5. Add an Endfield texture audit and deterministic Jie Ge setup entry point that
   is callable via Unity MCP and produces an audit report.
6. Update compatibility, provenance, changelog, package version, and release
   documentation; build the package twice.
7. Deploy the canonical archive through MCP, wait for compilation, run EditMode
   tests, execute Jie Ge setup, save the scene, inspect components and console,
   and capture main/scene/orbit views.

## Validation

- `python -m unittest tests.test_miku_fixed_workflows`
- `python tools/ci/run_checks.py --profile pr`
- `python tools/build_miku_unity_package.py` twice in clean output directories;
  normalized manifests and SHA-256 values must match.
- Fixed Blender executable smoke checks must assert `bpy.app.version == (5,2,0)`
  before export validation.
- Unity EditMode tests must cover workflow selection, Shader discovery, role
  mapping, texture audit, non-readable mesh cloning, UV7 determinism, stencil
  pass contracts, and recipe versioning.
- Unity MCP final inspection must show one `杰哥`, fourteen external materials,
  the generated mesh asset, active screen-rim feature, a saved scene, and no
  compiler/shader/property errors.

## Results and follow-up

Implementation and validation completed on 2026-08-02.

- Canonical PR checks passed: 227 Python tests plus schema, identity, Blender
  extension, and Unity package build checks.
- Unity EditMode passed 120/121 tests with one expected skip for an optional
  immutable Miku 1.0.3 regression-bundle root. The final installed archive then
  passed the focused seven-test GameToon suite.
- Blender validation used the required executable and asserted Blender 5.2.0
  LTS.
- Two consecutive Unity package builds produced SHA-256
  `9FBDD660253798F5DBDE7D7BD172AC7627EBFDB11861D97D21C3A6A946632A0C`.
- Unity MCP confirmed all nine Endfield shaders are discoverable without
  compile errors; `PC_Renderer` contains one active screen-rim feature.
- The saved scene contains three roots and exactly one character. Its fourteen
  material slots point to external Endfield materials and its MeshFilter points
  to the generated 78,434-vertex, fourteen-submesh asset with three-component
  TEXCOORD7 smooth normals.
- The texture audit applied 28 recognized import profiles. Weapon and unused FX
  files remain unbound and are retained in the audit report.
- Validation output includes main-camera front, Scene View, six-view contact
  sheet, and two light-direction comparisons under
  `Assets/endfield/Validation/`.
- Unity Console contained zero errors after the final package deployment and
  scene verification. No Play Mode run was performed, as required.
