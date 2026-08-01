# MiGR 2.0.3 release notes

MiGR 2.0.3 fixes runtime-expression regressions found while validating the
complete Magic Ball material corpus. The semantic exporter and Unity package
are released together as 2.0.3. The GPL bake worker is 1.1.2 because its
bundled canonical core identity changed.

## Changes

- Blender typed sockets are resolved by exact identifier, active state, and
  value type. Ambiguous sockets fail with `MIGR_SOCKET_AMBIGUOUS` instead of
  selecting a positional fallback.
- Principled emission is normalized as Emission Color multiplied by Emission
  Strength. Constant zero and one are folded deterministically; other values
  use one stable multiply expression.
- Static closure weights and closure parameters that require baking now point
  at executable `Texture.SampleBaked2D` expressions.
- Closure-composite graphs consume the material Normal TS channel. The runtime
  backend transforms the final tangent normal to world space for multi-lobe
  lighting and normal-dependent expressions without recursively consuming the
  surface normal while constructing it.
- Every sealed `_MIGR_Baked_*` resource must be reachable as a generated Shader
  Graph property. Generation fails with
  `MIGR_GENERATED_RESOURCE_UNREFERENCED:<bindingKey>` otherwise.
- Closure-composite materials use a neutral white `_BaseColor` final modulator
  and clear `_BaseMap`, so source closure colors cannot erase already evaluated
  Transparent Emission, Transparent Lit, or Custom Multi Lobe radiance.
- Unity 2.0.3 explicitly accepts the known 2.0.2 target profile. Existing 2.0.2
  Magic Ball 9 bundles retain enough Normal semantics to import directly.
  Magic Ball 1, 5, and 10 must be re-exported because their older IR lost
  information that cannot be reconstructed safely.

## Compatibility and public surfaces

- Blender: 5.2.0.
- Unity Editor: 6000.4.5f1.
- Universal Render Pipeline: 17.4.0.
- Shader Graph: 17.4.0.
- Validated color/API: Linear and D3D11 on Windows.
- Material IR, Conversion Plan, and Bundle remain schema 2.0.
- No public interchange field, shader property reference name, or
  deterministic identity rule changed.
- Existing user-owned Wrapper Shader Graphs remain protected. Full
  Regeneration is still required when their public property contract changes.
- Clear Coat, colored transparency, dithered coverage, and low-quality
  screen-space glass retain their documented Approximate classifications.

## Validation evidence

- Python 3.13 repository checks passed 149 tests, 69 Python parse checks, and
  all 14 MiGR schema validations.
- Blender 5.2.0 exported all 12 corpus materials twice with identical complete
  bundle trees. Magic Ball 1/5 preserve distinct linked Overlay inputs and
  Emission Strength 12.8; Magic Ball 9 seals its Normal resource; Magic Ball
  10 seals an executable baked Wireframe weight.
- Release ZIP/TGZ archives were built twice and were byte-identical. See
  `migr-2.0.3-sha256.txt`.
- Unity EditMode executed 94 tests: 93 passed, 0 failed, and 1 was intentionally
  skipped.
- The isolated end-to-end project imported 12/12 corpus bundles and rendered
  11/11 paired 512-by-512 images with no MiGR import or shader compilation
  error. Magic Ball 1/5 are distinct colored emissive materials, Magic Ball 9
  shows Normal variation, and Magic Ball 10 shows a bound baked Wireframe
  contribution.
- Eleven Delta-E heatmaps are retained as manual Approximate evidence. Their
  strict Exact thresholds do not pass (mean Delta-E2000 6.12 to 32.33), so
  clear coat, transparency, and multi-lobe behavior is not misreported as
  Exact.
- The final Unity TGZ, canonical package, and isolated installed copy each
  contain the same 140 files with identical SHA-256 values.

The Blender extension archives must not be installed over a running Blender
process with unsaved work. Validation of the canonical source and release
archives does not authorize discarding unsaved editor state.

The open live Unity project was staged with the verified 2.0.3 TGZ and all 12
bundles, but its current process remains stuck compiling/updating after
pre-existing AssetImportWorker crashes and still has the 2.0.2 assembly loaded.
Restart that editor after saving work before accepting its live-project import
as release evidence.
