# MiGR 2.1.0 Mesh-bound Bake Safety and Magic Ball Regression

## Purpose and outcome

Prevent a mesh-bound Blender UV bake from being imported as though it were a
portable Unity texture. Portable conversion must keep the Magic Ball 1, 4, and
9 procedural chains executable on an arbitrary compatible Unity mesh. Explicit
source-mesh fidelity must package the evaluated Blender mesh with every
mesh-bound bake so Magic Ball 10 Wireframe is only presented on matching
topology.

## Context and constraints

- Canonical source roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`,
  `extensions/migr_gpl_bake_worker/`, and
  `unity/Packages/com.migr.shaderconverter/`.
- The repository has a large pre-existing dirty worktree, including the current
  MiGR 2.x source as untracked files. Preserve all unrelated changes.
- Material IR and Value Graph remain schema 2.0. Bundle 2.1 adds a sealed
  `SourceMesh` GLB resource and remains readable alongside safe Bundle 2.0
  inputs.
- `Auto` remains the persisted mode identifier but becomes portable. The
  existing `AllowMeshBake` identifier becomes the explicit source-mesh
  fidelity mode.
- Generated Sub Graphs, source meshes, binding assets, prefabs, and sidecars
  are MiGR-owned. Wrapper Shader Graphs remain user-owned unless Full
  Regeneration is explicitly selected.
- Validation targets Blender 5.2.0 and Unity 6000.4.5f1 with URP/Shader Graph
  17.4.0, Linear color, and D3D11.
- The fixed Steam Blender executable is
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`. Never overwrite an
  installed extension while a Blender GUI process is running.
- `材质库/魔法球/魔法球.migr-fixed.blend` is the authoritative source.

## Progress

- [x] 2026-07-30: Confirmed canonical source markers, package version 2.0.3,
  Blender 5.2.0, the dirty worktree, and no running Blender process.
- [x] 2026-07-30: Reproduced that Magic Ball 1/4/9/10 resources are
  `ExpressionIsland` UV bakes with `MeshFingerprintSet` metadata.
- [x] 2026-07-30: Confirmed the live validation sphere is Unity's built-in
  515-vertex/768-triangle mesh while the bakes target Blender 962-vertex,
  960-polygon spheres.
- [x] 2026-07-30: Confirmed Unity samples every baked island through UV0 and
  does not consume or validate `meshBindingRequired` or `meshBinding`.
- [x] 2026-07-30: Locked the dual-mode policy and bundled-source-mesh delivery.
- [x] Implement portable semantic expressions and safe planner routing.
- [x] Implement deterministic GLB packaging and Bundle 2.1.
- [x] Implement Unity runtime expression generation and source-mesh import.
- [x] Complete tests, documentation, deterministic release builds, and visual
  evidence.
- [x] 2026-07-30: Completed guarded Blender installation after the GUI was
  closed, verified the portable `user_default` module roots and every archive
  member, and completed installed-extension exports. Unity 2.2.0 is installed
  and verified.

## Discoveries

- The reported EXRs contain data only inside the source sphere's UV islands and
  black pixels elsewhere. Successful bake execution does not establish that a
  different Unity mesh can sample the resource correctly.
- Magic Ball 1 and 4 bake the same Noise-to-Color-Ramp island; Magic Ball 9
  bakes a Noise/Math height chain; Magic Ball 10 bakes topology-dependent
  Wireframe.
- Runtime-independent expressions are currently compiled only after reaching a
  runtime-dependent parent. Static unsupported leaves are eagerly replaced by
  `Texture.SampleBaked2D`, before conversion mode is considered.
- `MeshFingerprintSet` is emitted into resource metadata, but neither the
  ScriptedImporter nor `MiGRBundleImporter` validates it.
- The Unity package already depends on glTFast 6.19.0 and the public node
  support documentation already promises GLB for mesh-bound bakes. The missing
  work is integration, not a new dependency.
- The package contains a clean-room Blender Noise HLSL include with a stable
  asset GUID, but the Material IR 2.0 runtime backend does not construct Noise
  custom-function nodes.
- The 2.0.3 visual runner used an incompatible built-in sphere and accepted
  only non-black/variance checks. Those checks cannot establish source-mesh
  parity.
- Baked-resource validation originally ran before the GPL worker had produced
  planned artifacts. The pre-worker channel pass must therefore defer resource
  existence validation; the post-worker pass remains strict.
- Blocking on glTFast's asynchronous import from the Unity main thread
  deadlocks the Editor. Source Mesh Fidelity now copies the sealed GLB into the
  AssetDatabase, waits for its ScriptedImporter result, and instantiates it in
  a Preview Scene. Renderer binding uses the sealed `meshIndex`, not an
  importer-generated root name.
- During implementation the repository's compatible static-PBR texture work
  advanced the exporter and Unity package to 2.2.0. The mesh-safety contract
  remains Bundle 2.1/Material IR 2.0 compatible and ships as part of the 2.2.0
  superset instead of being downgraded.
- A real Magic Ball 3 linked base-normal subtree is unsupported by the
  portable runtime path. Source Mesh Fidelity now bakes that complete Normal
  subtree, while supported linked normals continue to lower through
  `Vector.NormalBlend`.

## Decision log

- 2026-07-30: Preserve existing serialized mode identifiers. `Auto`,
  `PreferNative`, and `ReusableBakeOnly` may not produce mesh-bound resources;
  `AllowMeshBake`, `FullPBRBake`, and `AppearanceSnapshot` must package source
  geometry whenever they produce one.
- 2026-07-30: Support the current corpus's Object coordinate, Point Mapping,
  and Noise Factor path as editable runtime expressions. Label the clean-room
  Noise implementation Approximate and honor Strict fidelity.
- 2026-07-30: Reject unverified Noise Color, non-Point Mapping, and Wireframe in
  portable mode rather than substituting pseudo-color or constants.
- 2026-07-30: Use sealed GLB through the already-declared glTFast dependency.
  Do not invent a private mesh serialization format.
- 2026-07-30: Introduce Bundle 2.1 so older importers reject packages whose
  source-mesh contract they cannot enforce. Material IR remains 2.0.
- 2026-07-30: Reject legacy Bundle 2.0 mesh-bound resources without a sealed
  source mesh. Continue importing mesh-independent Bundle 2.0.
- 2026-07-30: Generated source-mesh prefabs are the supported application path.
  MiGR's apply operation validates mesh identity and refuses mismatches.
- 2026-07-30: Never synchronously block the Unity main thread on a glTFast
  `Task`. Import the verified `.source.glb` through the AssetDatabase and build
  generated bindings in a Preview Scene so the user's active scene is never
  dirtied.
- 2026-07-30: Preserve the existing user Wrapper on ordinary reimport. If a
  newly reachable baked property changes its public property contract, fail
  with the Full Regeneration diagnostic instead of overwriting that Wrapper.

## Implementation sequence

1. Make the runtime expression compiler preserve Object coordinates, Point
   Mapping, Noise Factor, and Wireframe semantics, and move mode-dependent
   bake choice into the planner/export flow.
2. Make portable modes fail before scheduling any mesh-bound channel or
   expression-island bake; retain explicit source-mesh bake jobs.
3. Export evaluated bound objects as deterministic GLB, add material-slot and
   mesh-fingerprint bindings, and seal them in Bundle 2.1.
4. Add Shader Graph runtime nodes for Object Position, Point Mapping, and Noise
   Factor through the Shader Graph 17.4 adapter.
5. Verify and import sealed GLB resources with glTFast, create stable Mesh and
   Prefab assets, bind materials, validate identities, and roll back atomically.
6. Add schema, Core, Blender, Unity, determinism, negative, end-to-end, and
   visual regressions.
7. Update versions, hashes, compatibility, diagnostics, install docs,
   changelogs, and the 2.0.3 follow-up record.
8. Build twice, validate, install only verified archives, re-export the corpus,
   and confirm the live project console is clean.

## Validation

- `python tools/ci/run_checks.py`
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/migr_magic_ball_corpus_smoke.py`
- `powershell -File tools/ci/run_unity_editmode.ps1 -UnityPath
  "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe"`
- Build the Semantic Exporter ZIP, GPL Worker ZIP, and Unity TGZ twice and
  compare archives, manifests, and member SHA-256 values.
- Portable acceptance: Magic Ball 1/4/9 on the Unity built-in sphere are
  non-black, textured, distinct where expected, and show the Normal path.
- Source-mesh acceptance: Magic Ball 1/4/9/10 use the generated prefab; GLB,
  mesh identity, renderer slots, textures, Shader Graph, and material bindings
  all match the sealed bundle.
- Negative acceptance: applying a source-mesh material through MiGR to the
  built-in sphere is rejected; a legacy mesh-bound Bundle 2.0 fails with
  `MIGR_LEGACY_MESH_BOUND_BUNDLE_UNSAFE`.

## Results and follow-up

Implementation and validation are complete in the compatible MiGR 2.2.0
superset:

- `py -3.13 tools/ci/run_checks.py` parsed 73 Python files, validated 16
  schemas, passed 179/179 tests, verified package identity, and built all three
  release archives.
- Blender 5.2.0 exported the 12-material corpus twice. Both 68-file trees are
  byte-identical with tree SHA-256
  `2a792aa5f669830bb56f051b570db397b1e0ba1ee822d495257fca299206bd4a`.
  Magic Ball 1/4/9 use portable runtime chains with no mesh-bound texture;
  Magic Ball 10 uses Bundle 2.1 with a sealed deterministic GLB and Wireframe
  texture. Magic Ball 3 uses Source Mesh Fidelity for its unsupported linked
  base-normal subtree.
- Unity 6000.4.5f1 / URP and Shader Graph 17.4.0 passed all 99 EditMode tests
  with zero failures or skips. The source-mesh fixture
  verifies GLB, Mesh, Prefab, renderer slots, texture binding, stable GUIDs,
  rollback, and Shader compilation.
- Two final builds are byte-identical both at archive level and for every
  member:
  - `migr_semantic_exporter-2.2.0.zip`
    `03d222c20378228a640a7e6c3a96f62303d2beb11046f6e8cd1001c470c1abd2`
    (21 files).
  - `migr_gpl_bake_worker-1.2.0.zip`
    `cbe84dce1999b368acfb1466b004d1db66a70c915126145ee174b751910578b1`
    (12 files).
  - `com.migr.shaderconverter-2.2.0.tgz`
    `11000a7c1fdca8affe548f0bd3a6eeb7a923fcc2a1a308c7e452c31279ba9cd2`
    (144 files).
- The isolated 2.1.0 fixed-environment visual run imported and rendered all 12
  bundles. Magic Ball 1/5 are non-black, textured, emissive, and distinct;
  Magic Ball 9 has Normal variation; Magic Ball 10 uses the generated Prefab
  and shows Wireframe variation. Delta-E2000 means range from 6.12 to 32.33,
  so these results remain explicitly Approximate and are retained for manual
  review rather than claimed as Exact.
- The live Unity project now references the final verified 2.2.0 TGZ. All 144
  PackageCache files match the archive (apart from Unity's injected
  `_fingerprint` field), the domain reload completed, and the final installed
  package passed 99/99 EditMode tests. Eleven of the 12 current corpus bundles
  committed, including Magic Ball 1/4/5/9/10, and their receipts report Shader
  compilation success. Magic Ball 3 correctly refuses ordinary reimport
  because its preserved user Wrapper lacks the newly required baked property;
  resolving that one item requires an explicit reviewed Full Regeneration.
- Blender 5.2.0 now loads the installed 2.2.0 semantic exporter and 1.2.0 GPL
  worker from the fixed portable `user_default` repository. All 21 exporter
  files and 12 worker files match their ZIP members. Installed-extension Auto
  exports of Magic Ball 1/4/9 contain no EXR or GLB, while the Magic Ball 10
  `AllowMeshBake` export contains exactly one Wireframe EXR and one sealed GLB.

Static evaluated meshes are in scope. Skinning, animation, and
runtime-deformed topology remain explicitly unsupported for the first Source
Mesh Fidelity release.
