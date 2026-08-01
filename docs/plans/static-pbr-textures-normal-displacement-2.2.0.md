# MiGR 2.2.0 Static PBR Textures, Normal Maps, and Displacement

## Purpose and outcome

Translate ordinary Blender 5.2 image-based Standard PBR graphs without forcing
a mesh-bound bake: Base Color, Roughness, Metalness, OpenGL or explicitly
selected DirectX tangent Normal, Height, Ambient Occlusion, Alpha, and Emission
Mask through Principled, Normal Map, Displacement, explicit component/Invert/
Multiply topology, and Material Output. MiGR must preserve images as editable
Unity Texture2D properties, retain roughness-to-smoothness conversion, combine
normal-map and height-bump effects, share explicitly packed scalar images, and
route true displacement to Shader Graph Vertex Position.

## Context and constraints

- Canonical source roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`,
  `extensions/migr_gpl_bake_worker/`, and
  `unity/Packages/com.migr.shaderconverter/`.
- The worktree contains extensive pre-existing changes, including the canonical
  MiGR source as untracked files. Preserve all unrelated work.
- Validation targets Blender 5.2.0 and Unity 6000.4.5f1 with URP and Shader
  Graph 17.4.0.
- MaterialIR, Conversion Plan, Conversion Manifest, and Target Profile remain
  schema 2.0. Bundle 2.2 adds static JPEG and Height resource support.
- Generated Sub Graphs and sidecars are MiGR-owned. Wrapper Shader Graphs are
  user-owned after creation and may only be changed automatically when they
  byte-match a known unmodified template or Full Regeneration is explicit.
- Version 2.2.0 supports static PNG, JPEG, and EXR images, Flat projection,
  implicit active UV/UV0, tangent OpenGL/DirectX normal maps, explicit
  Separate Color/XYZ or Image Alpha scalar packing, and Object-space
  displacement with an unlinked Normal input and finite constant
  Midlevel/Scale.
- UDIM, sequence, movie, generated images, other projections/spaces, linked
  displacement Normal, and dynamic Midlevel/Scale must diagnose rather than
  silently bake or substitute.

## Progress

- [x] 2026-07-30: Confirmed canonical source markers, package identity,
  Blender 5.2.0, target Unity tuple, and dirty-worktree constraints.
- [x] 2026-07-30: Reproduced that `ShaderNodeNormalMap` snapshots as opaque and
  BUMP-mode Material Output displacement replaces the Principled Normal source.
- [x] 2026-07-30: Confirmed Auto currently treats `Texture.Image` as requiring
  a portable mesh bake and Bundle 2.1/Unity only accept PNG and EXR resources.
- [x] 2026-07-30: Confirmed the Shader Graph wrapper template already contains
  a Vertex Position block but the runtime backend does not connect it.
- [x] 2026-07-30: Implemented native Blender/Core image, normal, bump, and displacement
  semantics.
- [x] 2026-07-30: Implemented Bundle 2.2 sealing and compatible Unity resource import.
- [x] 2026-07-30: Implemented Shader Graph texture sampling, normal combination, and vertex
  displacement.
- [x] 2026-07-30: Added Python, Blender, and Unity tests plus public
  documentation, diagnostics, compatibility, changelog, and release notes.
- [x] 2026-07-30: Repeated final full-suite, exact-version Blender/Unity,
  D3D11, package identity, and deterministic archive validation after the
  packed-channel and DirectX completion pass.

## Discoveries

- Existing Unity channel texture generation already uses stable public
  references such as `_BaseMap`, `_MetallicMap`, `_RoughnessMap`, and
  `_BumpMap`; Height can extend the same mechanism without renaming them.
- MaterialIR 2.0 leaves `channels`, `resources`, expression parameters, and
  surface-plan feature objects extensible, so Bundle 2.2 is sufficient to gate
  the new resource contract.
- Vertex texture sampling must use a Shader Graph 17.4 vertex-compatible LOD
  sampler. The exact internal type and slots must be verified against the
  installed package and a Unity-created fixture before serialization code is
  committed.
- The supplied metal normal and displacement images are nearly flat, so the
  wood set and generated high-contrast fixtures are required for meaningful
  normal/displacement visual validation.
- One image may need a fragment automatic-LOD sample and a distinct vertex LOD
  0 sample. Unity sampling is therefore cached by resource, stage, UV, and LOD,
  while the imported Texture2D property remains shared.
- An initial Unity Bundle 2.2 test exposed a version-gate bug: the importer
  recognized the root kind but omitted it from the v2 IR path. Adding 2.2 to
  that gate prevents MaterialIR 2.0 from being parsed as legacy v1.
- Missing sealed artifacts previously leaked `FileNotFoundError` on Windows.
  The bundle staging boundary now maps that case to
  `MIGR_ARTIFACT_MISSING`.
- A clean Shader Graph 17.4 package import can transiently compile before its
  API updater has restored `GUID` aliases in immutable package sources. The
  updater completed and the same canonical run then compiled and ran all
  tests; terminal XML is the acceptance result, not the transient first-pass
  compiler line.

## Decision log

- 2026-07-30: Direct static images use target-neutral
  `Texture.SampleImage2D`; `Texture.SampleBaked2D` remains reserved for baked
  artifacts and keeps Baked provenance.
- 2026-07-30: Preserve source bytes for supported external or packed static
  images. Bundle paths are content-addressed and never expose absolute paths.
- 2026-07-30: BUMP combines height-derived fragment normal with the Principled
  normal. DISPLACEMENT emits Object-space Vertex Position. BOTH emits both.
- 2026-07-30: Vertex displacement is generated with a subdivision/setup
  warning; MiGR does not silently add tessellation or mutate source meshes.
- 2026-07-30: Add `_MIGR_HeightMap`, `_MIGR_HeightMidlevel`, and
  `_MIGR_HeightScale`; retain all existing public property references.
- 2026-07-30: Untouched known legacy wrappers may receive the neutral vertex
  contract automatically. Modified wrappers remain untouched and diagnose the
  need for Full Regeneration.
- 2026-07-30: The 2.2 normal route is explicitly
  `TangentOpenGLPositiveY` or explicitly selected
  `TangentDirectXNegativeY`; no filename inference is permitted.
- 2026-07-30: Bundle 2.2 is selected when direct static image resources are
  emitted. Older Bundle 2.0/2.1 reading remains bounded and unchanged.
- 2026-07-30: Explicit Separate Color/XYZ and Image Alpha topology may produce
  Bundle 2.2 scalar `channelBindings`. One Linear image/property/sample is
  reused; mixed color/scalar use is rejected.
- 2026-07-30: AO is multiplied into Base Color through
  `_OcclusionStrength`, then the URP Occlusion output is neutralized to avoid
  double darkening. Explicit Invert remains a One Minus expression.

## Implementation sequence

1. Extend Blender node snapshots with native Normal Map, Displacement,
   Separate Color/XYZ, Invert, Mix, and complete static Image metadata;
   validate image/projection/sampling constraints.
2. Lower supported Image Texture and explicit component paths to direct
   resource expressions; preserve Normal Map strength/convention, One Minus,
   AO, Alpha, and Emission Mask without scheduling the bake worker.
3. Split Material Output displacement from the Normal channel and produce
   explicit fragment bump and/or vertex displacement channel plans.
4. Add deterministic static-image collection and Bundle 2.2 schema/sealing,
   including JPEG, Height/EmissionMask, DirectX normal, and channel bindings.
5. Extend Unity bundle validation/import and texture importer settings for
   shared packed resources, DirectX green-channel flip, AO/Alpha/EmissionMask,
   and Height usage.
6. Extend the Shader Graph 17.4 adapter/backend with shared image sampling,
   component extraction, AO/Base Color composition, normal strength/blending,
   LOD-0 height sampling, and Vertex Position output/wiring.
7. Add safe wrapper-template migration, tests, diagnostics, documentation,
   version metadata, changelog, and release notes.
8. Run Core, Blender 5.2, Unity EditMode, determinism, package, and visual
   validation; record exact results below.

## Validation

- `python -m unittest discover -s tests -p "test_migr_*.py"`
- `python tools/ci/run_checks.py`
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/migr_static_pbr_textures_smoke.py`
- `powershell -File tools/ci/run_unity_editmode.ps1 -UnityPath
  "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"`
- Export the supplied wood and metal sets locally in Auto mode. Confirm no GPL
  bake request, no absolute path in the bundle, and stable repeated output.
- Import Bundle 2.2 in Unity and confirm texture importer settings, material
  bindings, Shader Graph compilation, normal/bump appearance, vertex
  displacement on a subdivided plane, stable GUIDs, and wrapper ownership.
- Confirm safe Bundle 2.0/2.1 imports remain unchanged and malformed/unknown
  Bundle 2.2 inputs fail atomically with structured diagnostics.

## Results and follow-up

Implementation and automated validation are complete:

- `py -3.13 tools/ci/run_checks.py`: passed; canonical boundary, 73 Python
  files, 16 schemas, 179 tests, package identity, and both release builders
  succeeded.
- `py -3.13 -m unittest discover -s tests -p "test_*.py"`: 179 passed.
- Fixed Blender executable
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`: confirmed 5.2.0 LTS
  and passed `migr_static_pbr_textures_smoke.py` plus
  `migr_closure_surface_smoke.py`. The programmatic fixtures cover packed
  RGBA semantics, AO Multiply, OpenGL/DirectX normal metadata,
  BUMP/DISPLACEMENT/BOTH, colored HDR emission, Principled/Emission Mix and
  Add Shader, Dithered Alpha, Opaque linked-Alpha promotion, byte-stable repeat
  export, and absence of absolute source paths.
- Unity 6000.4.5f1 / URP 17.4.0 / Shader Graph 17.4.0 full EditMode:
  101 total, 100 passed, 0 failed, 1 graphics test skipped under
  `-nographics`. Bundle 2.2 JPEG/Height, DirectX normal import, packed PBR
  sample reuse/AO neutralization, and static PBR normal-blend/vertex-
  displacement tests all passed. The staged package and canonical source each
  contained 144 files with no SHA-256 manifest differences.
- The skipped graphics test was rerun separately with D3D11: 1 passed,
  0 failed, 0 skipped.
- The Unity TGZ, Blender Semantic Exporter ZIP, and GPL worker ZIP were built
  twice byte-identically. Final SHA-256 values are
  `11000a7c1fdca8affe548f0bd3a6eeb7a923fcc2a1a308c7e452c31279ba9cd2`,
  `03d222c20378228a640a7e6c3a96f62303d2beb11046f6e8cd1001c470c1abd2`,
  and
  `cbe84dce1999b368acfb1466b004d1db66a70c915126145ee174b751910578b1`.

A human side-by-side review of the new normal direction, height
`BUMP`/`DISPLACEMENT`/`BOTH`, AO, HDR Bloom, transparent shadows, and vertex
displacement on representative production meshes was not automated.
Structural graph generation, texture import settings, Shader Graph compile,
one real-device D3D11 preview, and deterministic export/import are covered;
the exact compatibility tuple therefore remains `Experimental`.

Dynamic image sources, non-UV0 mapping, World-space displacement, linked
displacement normals, dynamic Midlevel/Scale, runtime tessellation,
skinning-aware displacement, and other Shader Graph versions remain outside
this release.
