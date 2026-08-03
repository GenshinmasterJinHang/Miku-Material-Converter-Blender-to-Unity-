# Miku 1.0.5 Multi-Lobe Finite Lighting

## Purpose and outcome

Miku 1.0.5 fixes two real D3D11 regressions that remained after the 1.0.4
normal-ownership and radiance-routing correction. Colorful Coating 5 currently
writes NaN radiance, which becomes black in ordinary render targets. Bumpy
Stone 3 writes stable zero radiance under a valid URP directional light. The
release must produce finite, light-responsive CustomMultiLobe materials while
preserving user-owned wrappers and routing final radiance through the output
that the selected URP wrapper actually consumes.

## Context and constraints

Canonical implementation roots are `miku/`, `miku_blender/`,
`extensions/miku_shader_converter/`, and
`unity/Packages/com.miku.shaderconverter/`. The worktree was already dirty;
only narrow overlapping edits are allowed. Validation targets Blender 5.2.0
from the fixed Steam path and Unity 6000.4.5f1 with URP and Shader Graph 17.4.0
on Windows D3D11. Bundle, Material IR, plan, and manifest schemas remain 1.0.
The original external `output` directory is immutable.

## Progress

- [x] 2026-08-01: Confirmed the connected Unity project resolves Miku 1.0.4
  and the installed backend hash matches the release artifact.
- [x] 2026-08-01: Reproduced Colorful Coating 5 as NaN in ARGBFloat and Bumpy
  Stone 3 as finite zero under a real URP camera and directional light.
- [x] 2026-08-01: Implemented bounded lobe lighting, safe per-lobe normals, and normalized
  clear-coat composition.
- [x] 2026-08-01: Isolated the remaining Bumpy Stone 3 zero output to the
  `UniversalUnlitSubTarget`: its final-color input is Base Color, so an
  Emission-only closure output was discarded. Routed non-coat closure
  radiance to Unlit Base Color while retaining Radiance-to-Emission for the
  Lit Clear Coat wrapper. An ARGBFloat D3D12 check then produced finite zero
  without lights and finite non-zero color with a directional light.
- [x] 2026-08-01: Added and ran structural, compatibility, shader-compile,
  and D3D12 ARGBFloat graphics regressions. The connected Editor did not offer
  a D3D11 session, so no new D3D11 graphics claim is made.
- [x] 2026-08-01: Coordinated source identities and compatibility documentation
  to 1.0.5; release artifact hashes remain pending deterministic builds.
- [x] 2026-08-01: Built both artifacts twice with identical SHA-256, installed
  the Unity package and Blender extension from those artifacts, exported the
  three requested materials twice with a byte-identical 23-file tree, and
  imported them into `Assets/Miku/Generated/Fixed105` without overwriting the
  supplied `output` tree or existing wrappers.
- [x] 2026-08-01: Removed failed-export retry directories, Unity `MikuTests`
  artifacts, Unity console/execution history, `.pytest_cache`, `.ruff_cache`,
  and source-tree `__pycache__` directories. Retained release artifacts,
  `.codegraph`, `.venv`, final imports, and all pre-existing user changes.

## Discoveries

- The generated Sub Graph did connect closure Radiance to Emission and a
  literal zero to Base Color, but non-coat `CustomMultiLobe` intentionally
  uses `MikuDielectricTemplate` and `UniversalUnlitSubTarget`. URP Unlit does
  not consume that Emission block as final color. A diagnostic HLSL return was
  visible immediately after routing radiance to Base Color, proving that the
  black result was an output-contract mismatch rather than stale package code.
- Colorful Coating 5 returns NaN at the center pixel for every tested emission
  multiplier, while Bumpy Stone 3 remains exactly zero for every multiplier,
  light direction, and flat-normal substitution.
- The generated Clear Coat smoothness currently sums `weight * smoothness`
  without coat contribution normalization. This can saturate overlapping
  lobes to a singular value and does not describe the coat being averaged.
- `C:/Users/22687/Desktop/output` currently contains Colorful Coating 5,
  Colorful Coating 7, and Bumpy Stone 3. Colorful Coating 8 remains recoverable
  from the original material-library blend and prior Miku staging data.

## Decision log

- 2026-08-01: Publish as 1.0.5 instead of replacing the immutable 1.0.4
  artifact.
- 2026-08-01: Keep `MikuEvaluateLobe_half/float` signatures source-compatible;
  safety changes belong inside the implementation and generated input graph.
- 2026-08-01: Preserve the user-owned wrapper. Non-coat closure composites
  feed evaluated radiance to the Unlit Base Color final-color path and emit
  zero through Emission; Clear Coat composites keep Base Color zero and feed
  evaluated radiance to the Lit wrapper's Emission path. This avoids both the
  black Unlit output and URP double lighting.
- 2026-08-01: Treat finite-value guards as a last-resort containment boundary.
  Inputs and BRDF denominators are bounded first; validation must still fail if
  a regression relies on the guard.

## Implementation sequence

1. Replace the fragile lobe evaluator with bounded diffuse/specular terms,
   guarded normals, roughness, weights, light attenuation, and finite output.
2. Add generated-graph normal validity selection before each lobe call. Change
   Clear Coat smoothness to coat-contribution-weighted averaging with a safe
   upper bound, while keeping deterministic IDs.
3. Add the structured non-finite diagnostic and update Unity/backend tests for
   routing, normal fallback, coat composition, legacy profiles, and compiler
   messages.
4. Coordinate all active identities to 1.0.5, recalculate the backend digest
   and target-profile hash, retain 1.0.3 and 1.0.4 profile compatibility, and
   update English canonical docs plus release notes.
5. Run Core and Blender smoke tests, Unity EditMode and D3D11 graphics tests,
   PR checks, and two deterministic builds. Install only canonical-source
   artifacts and compare installed manifests and hashes.
6. Export the three materials to `output-fixed-1.0.5` and import them into a
   fresh Unity validation folder. Never overwrite the supplied `output` tree.

## Validation

Python and Blender tests must cover zero/unconnected/explicit normals, zero
roughness, dynamic weights, Colorful Coating 8 emission, and deterministic
identities. Unity structural tests must parse the generated MultiJson rather
than rely on substrings. D3D11 tests render to ARGBFloat and reject every NaN or
Inf pixel. Colorful Coating 5 must be dark without light and colored with light;
Colorful Coating 8 must retain blue view-dependent emission; Bumpy Stone 3 must
be lit and differ directionally from a flat-normal baseline. Asset previews
must not be pure black.

## Results and follow-up

Core/Python completed with 231 passing tests. The focused Unity importer suite
completed 114 passed, zero failed, and one optional external-bundle fixture
skipped. All 115 importer tests completed without failure. Full 135-test
EditMode runs exposed unrelated Generic Toon isolation failures: the first run
had one real-camera threshold failure that passed immediately in isolation;
later runs reported pre-existing `Assets/MikuTests` metadata/path collisions
in `LegacySelectedAssetMigrationPreservesMetadataGuidAndCurves` and
`MaterialBuilderCreatesOwnedBaseUserMaterialAndRecipe`. The latter migration
test also failed alone and is not claimed as passing. Shader Graph compilation
for all three newly imported wrappers reported zero messages.

The final artifacts are `com.miku.shaderconverter-1.0.5.tgz` with SHA-256
`6cadc5db927e47bb9185c7066ff74eb6dbbc333af1e70e5936cdd3728a1d761a`
and `miku_shader_converter-1.0.5.zip` with SHA-256
`bb10a23c2882a44ea8827cc03f8efc1403606d4dbefa59698343c8cedd7146d1`.
The fixed Blender output tree contains 23 files and was byte-identical across
two builds at
`da018508ab61b4e727678b87e50de06e119428812f2db2d45909a4841c83f388`.

No schema, package ID, public shader property, or user Wrapper ownership
migration was introduced. The unavailable D3D11 real-camera run remains the
only graphics validation gap for this session.
