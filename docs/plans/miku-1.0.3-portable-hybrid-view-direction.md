# Miku 1.0.3 Portable Hybrid View-Direction Support

## Purpose and outcome

Make the existing `PreferNative` conversion mode an explicit Portable Hybrid
workflow. Supported view-, camera-, and time-dependent expressions remain live
Shader Graph expressions, while runtime-independent UV0-only unsupported
expression islands may be baked on a canonical UV plane. Portable Hybrid output
must not contain source-mesh assets, mesh fingerprints, or mesh-bound textures.

## Context and constraints

- Canonical sources are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`.
- The worktree already contains the in-progress Miku 1.0.2 fixed-workflow and
  height-channel changes. This work preserves and builds on those edits.
- `FullPBRBake` remains an explicit source-mesh-bound fidelity route.
- The public mode identifier remains `PreferNative`; only its displayed label
  and behavior contract are clarified.
- A portable Texture2D may require UV0 on the destination mesh, but it may not
  depend on the source mesh topology, vertex data, or UV layout fingerprint.
- Validation targets Blender 5.2.0 at the repository-mandated executable and
  Unity 6000.4.5f1 with URP/Shader Graph 17.4.0.

## Progress

- [x] 2026-08-01: Confirmed the canonical repository markers and Unity package
  identity and reviewed the pre-existing dirty worktree.
- [x] Add target-neutral dependency-domain proof and Portable Hybrid planning.
- [x] Add canonical UV0 reusable expression-island baking.
- [x] Enforce non-mesh-bound Portable Hybrid bundles in core and Unity.
- [x] Update UI, schemas, diagnostics, versions, and public documentation.
- [x] Complete Python, Blender, Unity, determinism, and packaging validation;
  record unrelated/environmental failures rather than hiding them.

## Discoveries

- `FullPBRBake` intentionally schedules a complete material-channel bake and the
  worker correctly rejects `ViewDirection`, because one fixed UV texture cannot
  encode a value that changes with the rendering camera.
- Runtime lowering for View Direction, Fresnel, Layer Weight, Camera Data, and
  Time already exists and is covered by Python and Unity tests.
- Existing expression-island baking always uses source material meshes and the
  worker always emits a SourceMesh plus mesh binding, even when a job is named
  `ReusableBake`; this must be split at the protocol boundary.
- `Texture.SampleBaked2D` and the Unity UV0 sampler already support the required
  target representation. The missing distinction is the source dependency
  domain and resource ownership/binding metadata.
- A whole channel that mixes Layer Weight with an unsupported UV0 texture is
  Runtime-domain even though one child is bakeable. Nested input lowering must
  extract the UV0 child before compiling the runtime Mix; channel-level domain
  classification alone is intentionally insufficient.

## Decision log

- 2026-08-01: Preserve `FullPBRBake` behavior and strengthen `PreferNative`
  rather than silently changing an existing fidelity contract.
- 2026-08-01: Treat only a statically proven UV0/constant/image dependency island
  as eligible for a reusable Texture2D bake. Runtime inputs remain live;
  Generated/Object/geometry/topology-dependent islands fail before execution.
- 2026-08-01: Reuse the existing `ReusableBake` route and
  `Texture.SampleBaked2D` operation with additive `coordinateDomain` and
  `meshBindingRequired` metadata; retain the Miku 1.0 schema family.
- 2026-08-01: A Portable Hybrid request may not mix reusable and mesh-bound bake
  jobs. The worker and import boundaries both enforce this invariant.

## Implementation sequence

1. Add dependency-domain classification to the semantic compiler and emit
   reusable baked-island metadata only for UV0-proven static sources.
2. Teach the planner to schedule `ReusableBake` expression-island jobs for
   `PreferNative`, preserve runtime expressions, and diagnose mesh dependencies.
3. Split worker execution into canonical-plane reusable baking versus existing
   mesh-bound baking; omit SourceMesh and mesh binding for reusable results.
4. Validate Portable Hybrid bundle invariants in Python and Unity before asset
   mutation, then update UI labels, schemas, diagnostics, versions, and docs.
5. Add regression, Blender headless, Unity EditMode, determinism, and package
   tests; self-review the final diff without touching unrelated user changes.

## Validation

- `.venv\\Scripts\\python.exe -m pytest` for focused semantic, planner, bundle,
  bake-protocol, and package tests.
- `py -3.13 tools/ci/run_checks.py --profile pr` using the configured environment.
- `C:\\SteamLibrary\\steamapps\\common\\Blender\\blender.exe --background
  --factory-startup --python tests/blender/miku_portable_hybrid_smoke.py`.
- `tools/ci/run_unity_editmode.ps1` against Unity 6000.4.5f1.
- Build Blender and Unity packages twice and compare bytes and SHA-256 values.

## Results and follow-up

Implemented Runtime/Uniform/UV0/MeshSurface classification, maximal reusable
island extraction, canonical-plane baking, resource metadata, mixed-job
rejection, Full PBR runtime preflight, Python/Unity bundle guards, UI labels,
schema enums, diagnostics, coordinated 1.0.3 versions, and public docs.

Executed evidence:

- Focused Python suites: 74 passed, followed by 55 passed after the final
  resource-contract hardening.
- Full non-Delta-E Python suite: 204 passed. The two NumPy-dependent Delta-E
  modules ran separately under the bundled Python runtime: 19 passed.
- Ruff on all touched Python implementation/test files: passed.
- Nine JSON Schemas and Unity package asset identity manifest: passed.
- Blender 5.2.0 (`fbe6228777e7`) Portable Hybrid smoke: passed twice. A
  triangle and cube produced identical file manifests/SHA-256 values; neither
  bundle contained GLB, SourceMesh, or `meshBinding`. Replaying each request hit
  the reusable cache without rewriting its Texture2D, and the cache contained
  no `targetMeshes` or mesh fingerprint.
- Unity 6000.4.5f1 EditMode: all 111 `MikuBundleImporterTests` relevant to this
  change passed (110 passed plus one intentional ignore at fixture level),
  including Portable Hybrid rejection, UV0 sampling, live Layer Weight/View
  Direction composition, and baked texture import. The full 127-test package
  run was 122 passed, 4 failed, 1 ignored; all four failures are the pre-existing
  dirty-worktree Generic Toon screen-rim camera pixel assertion and are outside
  Portable Hybrid.
- The ignored `LayerWeightPreviewIsLitAndViewDependent` graphics case was rerun
  alone without `-nographics` on a real D3D11 device and passed 1/1, confirming
  the two-camera view-dependent result.
- Deterministic package builds passed two consecutive byte comparisons:
  Blender ZIP `c53fbc3f25abf58524d9da4a2309e31e16f9b7461eac1ddc40a0e8591a9eccb8`;
  Unity TGZ `b8c0b3c8fc644d297f88124dbd20643fdd7e04c3e09a21e87b86454566cbf678`.

The one-shot PR script cannot import NumPy from the repository `.venv`, and its
configured package index proxy is unavailable. Its gates were therefore run in
the split environments above; no dependency or lockfile was changed.

Directional/light-path snapshots, UV-free triplanar conversion, and general
multidimensional material baking remain outside this change. Portable textures
still require UV0 on the destination mesh, and different UV layouts naturally
change placement.
