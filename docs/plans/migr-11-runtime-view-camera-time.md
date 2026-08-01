# MiGR 1.1 Runtime View, Camera, and Time Support

## Purpose and outcome

Preserve supported Blender 5.2 view-, camera-, and time-dependent material
expressions as target-neutral MiGR MaterialIR and generate editable native
Unity 6000.4.5f1 URP Shader Graph 17.4 nodes. Supported runtime expressions
must never be sent to a UV bake, and installed Blender/Unity packages must be
identifiable as MiGR 1.1.0 artifacts built from the canonical MiGR source.

## Context and constraints

- Canonical feature roots are `migr/`, `migr_blender/`,
  `extensions/migr_semantic_exporter/`,
  `extensions/migr_gpl_bake_worker/`, and
  `unity/Packages/com.migr.shaderconverter/`.
- Retired B2U roots and the sibling B2U worktree are outside this plan.
- The staging worktree contains the intentional MiGR 1.0 cutover and other
  uncommitted user work. All edits must be scoped and preserve that work.
- Exact versions are Blender 5.2.0 LTS commit `fbe6228777e7`, Unity
  6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0, Windows 11.
- Blender validation must use
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`.
- MaterialIR document kinds and schema versions remain `migr-*-1.0`; package
  versions become 1.1.0.
- Generated Sub Graphs remain MiGR-owned. Existing user wrapper Shader Graphs
  must not be overwritten outside explicit Full Regeneration.
- Light Path, XR, panoramic cameras, custom SRPs, game-workflow ShaderLab
  backends, and nested node-tree FCurves are outside the Exact claim.

## Progress

- [x] 2026-07-28: Located the canonical MiGR staging worktree and confirmed the
  installed Blender extensions are built from its MiGR 1.0 sources.
- [x] 2026-07-28: Confirmed Blender currently loads
  `migr_semantic_exporter`/`migr_gpl_bake_worker`, while the earlier B2U
  runtime-input implementation was not installed or active.
- [x] 2026-07-28: Added the canonical source boundary and fixed Blender
  executable policy to `AGENTS.md`.
- [x] 2026-07-28: Added typed runtime expressions and Blender
  authoring/export support.
- [x] 2026-07-28: Routed supported runtime expressions natively and hardened
  bake diagnostics.
- [x] 2026-07-28: Added the native structured Shader Graph 17.4 runtime
  backend, deterministic serialization, stage validation, and EditMode tests.
- [x] 2026-07-28: Upgraded and reproducibly built the MiGR 1.1.0 Blender and
  Unity artifacts, recorded their SHA-256 hashes, and validated the source
  packages.
- [x] 2026-07-28: Installed both Blender 1.1.0 extensions from the fixed
  executable after the GUI was closed and confirmed Blender uses its portable
  extension profile.
- [x] 2026-07-28: Added channel-scoped mesh baking after the original material
  exposed a mixed runtime/static case: Layer Weight remains native while its
  independent procedural Roughness branch is baked separately.
- [x] 2026-07-28: Reinstalled the rebuilt channel-scoped artifacts and
  completed the original `彩色镀层3` material export acceptance twice with
  byte-stable generated output.

## Discoveries

- The installed GPL worker raises `MIGR_BAKE_EXECUTION_FAILED` after receiving
  a mesh bake for a channel that depends on view, camera, light-path, or time.
- MiGR 1.0 already marks Fresnel, Layer Weight, and Camera Data as runtime at a
  region level, but MaterialIR channels cannot yet reference executable typed
  expressions and the Unity importer copies a fixed generated Sub Graph.
- The installed exporter matches the staging source except for a stale target
  profile implementation hash, so package installation alone cannot provide
  the requested runtime behavior.
- A Blender GUI process is open with unsaved work. Building can proceed, but
  installed-extension replacement must wait until the GUI is saved and closed.
- Shader Graph reflection initially serialized some secondary object IDs
  nondeterministically. A deterministic MultiJson pass now derives node, slot,
  property, and remaining serialized IDs from material identity and stable
  semantic fingerprints.
- An in-memory URP test pipeline can be unloaded during forced AssetDatabase
  refresh or Scene tests. EditMode setup now persists the exact URP test asset
  beneath the disposable test root before running importer tests.
- Shader Graph 17.4 requires a `SubGraphNode` to serialize matching
  `m_PropertyGuids` and `m_PropertyIds` arrays for generated runtime inputs.
  Keeping both arrays synchronized allows Unity to rebuild slots safely and
  exposes the four Time controls on the final material.
- The fixed Blender installation is configured with a portable profile under
  `C:\SteamLibrary\steamapps\common\Blender\portable`. That profile, rather
  than the stale APPDATA 1.0 copy, is authoritative for installed-extension
  acceptance.
- The original active material (`彩色镀层3`) combines a preserved Layer Weight
  color expression with a linked procedural Roughness value shared by both
  closure branches. Coarse region routing was insufficient; MaterialIR now
  carries an additive `requiresBake` channel proof and the worker ignores a
  dynamic closure factor only when both branch values for the requested
  semantic have identical source signatures.

## Decision log

- 2026-07-28: Keep MaterialIR/schema 1.0 and add optional expression records and
  Expression channel values. This is additive; old Constant and TextureResource
  values remain valid.
- 2026-07-28: Upgrade both Blender extensions and the Unity package to 1.1.0 so
  installed artifacts are distinguishable from the old implementation.
- 2026-07-28: Use target-neutral semantic expressions rather than serializing
  Blender node types or Unity Shader Graph class names into MaterialIR.
- 2026-07-28: Only safe affine scalar frame drivers are lowered. Complex scalar
  drivers become exposed parameters; unsafe required non-scalar drivers fail.
- 2026-07-28: Unsupported runtime chains fail with
  `MIGR_RUNTIME_INPUT_UNSUPPORTED`; they do not fall back to whole-material UV
  bake or appearance snapshot.
- 2026-07-28: Preserve the existing two recorded MiGR 1.0 target-profile hashes
  as bounded import compatibility entries while making the MiGR 1.1 profile
  hash current. The document schema family stays at 1.0.
- 2026-07-28: Keep Shader Graph internal reflection in one 17.4 adapter. The
  MaterialIR contains only semantic operation names; runtime formulas expand to
  stock native nodes without Custom Function or MonoBehaviour dependencies.
- 2026-07-28: Use additive channel-level `requiresBake` metadata inside
  MaterialIR 1.0 rather than exposing Blender node references or changing the
  schema family. Channel-scoped jobs carry only semantic names, and the GPL
  worker independently proves the selected channel has no runtime dependency.

## Implementation sequence

1. Enforce canonical MiGR/package identities and fixed Blender executable in
   repository policy and CI.
2. Extend Blender snapshots with output-level runtime metadata, the versioned
   MiGR Time group, and safe root-node-tree driver extraction.
3. Add deterministic typed expression lowering, MaterialIR validation, SourceMap
   bindings, channel Expression values, and planner routing.
4. Prevent runtime expressions from reaching the GPL worker and preserve
   specific diagnostic codes at the worker boundary.
5. Add a version-specific MiGR Shader Graph 17.4 generator for View Direction,
   Camera Data, Time, physical Fresnel, Layer Weight, and supported math.
6. Add Python, Blender headless, package, and Unity EditMode coverage; update
   public docs, provenance, diagnostics, compatibility, and changelogs.
7. Build deterministic 1.1.0 ZIP/TGZ artifacts, record hashes, close Blender,
   install with overwrite, and verify the original failing material.

## Validation

- `py -3.13 tools/ci/run_checks.py --profile pr`
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background
  --factory-startup --python tests/blender/migr_runtime_inputs_smoke.py`
- `tools/ci/run_unity_editmode.ps1` against Unity 6000.4.5f1.
- Build both Blender extensions twice and compare bytes/SHA-256.
- Build `com.migr.shaderconverter-1.1.0.tgz` and verify package identity.
- Install both Blender ZIPs with overwrite only after no Blender GUI remains;
  verify enabled IDs, version 1.1.0, module paths, and installed file hashes.

## Results and follow-up

- Passed the PR profile with 107 Python tests covering source boundary, exact
  Blender path, schemas, expressions, deterministic IDs, stage conflicts,
  Time driver parsing, Fresnel/Layer Weight numerical oracles, and
  deterministic package builds.
- Passed the fixed executable Blender headless fixture:
  `MIGR_RUNTIME_INPUTS_SMOKE_COMPLETE` on Blender 5.2.0 LTS.
- Passed all 31 Unity EditMode tests on Unity 6000.4.5f1 with URP/Shader Graph
  17.4.0, including runtime Sub Graph import, final shader compilation, Time
  material controls, stage conflicts, and wrapper/subgraph determinism.
- Built each 1.1.0 artifact twice with identical bytes. Recorded SHA-256:
  `f37550906c0c38d6b105e9f3afd38c36c801fd78af4b62fcda4e2eeb1a212d80`
  (semantic exporter),
  `68e24a2223006dc939987d23d199727a79964e62c38f43b77015a654a6bdb274`
  (GPL bake worker), and
  `2e34267f29b79a3ee55ece852e2f57430b0c79e5d4f3ec7635391aa7948ea782`
  (Unity package).
- The initial installed-source retest proved the new 1.1.0 extension was
  active and replaced the generic bake failure with
  `MIGR_RUNTIME_INPUT_UNSUPPORTED`; it also exposed the channel-scoped bake gap
  now covered by the source and Blender fixtures above.
- Reinstalled both final ZIPs through
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`. Installed module
  roots, archive contents, and version 1.1.0 were verified before export.
- Exported the original `彩色镀层3` material successfully. Base Color is an
  Expression chain containing `Input.ViewDirection` and
  `Math.LayerWeightFacing`; Roughness alone is a verified TextureResource from
  a `scope: Channels` MeshBake. The plan reports
  `MIGR_RUNTIME_INPUT_PRESERVED` and
  `MIGR_STATIC_CHANNEL_BAKE_SCHEDULED`, while the worker reports
  `MIGR_BAKE_COMPLETED`.
- Repeated the installed-extension export and compared all eight public
  generated artifacts byte-for-byte. Validated the seven versioned JSON entry
  and companion documents against the unchanged `migr-*-1.0` contracts.
