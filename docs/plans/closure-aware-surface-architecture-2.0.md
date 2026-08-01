# MiGR 2.0 Closure-Aware Surface Architecture

This ExecPlan is a living document. Keep `Progress`, `Discoveries`,
`Decision Log`, and `Results` current while implementation proceeds.

## Purpose and outcome

MiGR currently lowers Blender shader closures into a flattened Standard PBR
channel set before the planner can reason about closure meaning. That loses the
distinction between closure weight, alpha, transmission, emission, and
refraction. The visible failures are semi-transparent emission and glass
materials whose Mix Shader or Add Shader topology cannot be represented by one
ordinary URP Lit material.

This change preserves a typed value graph and a separate closure graph, solves
Blender 5.2 closure weights symbolically, analyzes closure domains, selects a
surface model, and only then routes value subgraphs to native generation or
baking. Unity consumes the resulting surface plan through per-model Shader
Graph generators. Ordinary Standard PBR remains a proved fast path.

The first delivery implements the phase 1-5 enhanced scope confirmed by the
maintainer: generic Mix/Add weight propagation, transparent emission,
transparent lit composition, low-quality screen-space glass, and an initial
multi-lobe custom-lighting backend. Higher-quality or multiple independent
refraction, volume rendering, holdout rendering, and Shader to RGB raster
evaluation remain explicit future work.

## Context and constraints

Canonical feature work is limited to `migr/`, `migr_blender/`,
`extensions/migr_semantic_exporter/`,
`extensions/migr_gpl_bake_worker/`,
`unity/Packages/com.migr.shaderconverter/`, schemas, tests, and documentation.
Retired B2U sources, installed Blender extensions, `dist/`, and validation
copies are not source.

The repository is intentionally dirty because it contains the in-progress MiGR
cutover. Preserve all existing edits. Do not reset, clean, or replace the tree.
The exact supported environment is Blender 5.2.0 LTS, Unity 6000.4.5f1
revision `cc83ebd631f8`, URP 17.4.0, Shader Graph 17.4.0, Linear color space,
and D3D11.

The live Unity project and Blender GUI must not be overwritten. Unity validation
uses an isolated project/package copy. Final promotion to the current
`项目4` repository occurs only after all gates pass, with a recoverable backup
and an allow-listed dry run.

## Progress

- [x] Read repository instructions, compatibility documents, existing closure
  plans, tests, and current Git status.
- [x] Verify Blender 5.2 Mix/Add weight behavior from the official Weight Tree
  implementation.
- [x] Verify exact Blender, Unity, URP, and Shader Graph versions.
- [x] Run focused baseline tests:
  `py -3.13 -m unittest tests.test_migr_transparent_surface tests.test_migr_runtime_inputs`
  (27 passed).
- [x] Add target-neutral closure, weight, conversion, surface-plan, and budget
  models with validation.
- [x] Add Blender closure extraction and symbolic weight propagation.
- [x] Add v2 document schemas, migration, planner, bundle, CLI, and exporter
  integration.
- [x] Add Unity v2 models, importer compatibility, per-surface generators,
  transparent composition, low-quality glass, and multi-lobe generation.
- [x] Add unit, Blender, Unity EditMode/Graphics, determinism, and negative
  tests.
- [ ] Capture and approve new Magic Orb Bloom Off/On and multi-angle glass
  golden images; no locked MiGR 2.0 visual reference set exists yet.
- [x] Update public documentation, compatibility matrix, diagnostics,
  changelog, provenance, and release metadata.
- [x] Validate in the exact isolated target environment.
- [x] Back up and promote staging into the current repository, then repeat all
  gates.

## Discoveries

- Blender 5.2 starts closure evaluation with weight 1. Mix Shader clamps its
  factor, sends `parent * (1 - factor)` to shader input 1 and
  `parent * factor` to shader input 2. Add Shader passes the same parent weight
  to both inputs. Multiple paths are added.
- Blender Color-to-Float uses active OCIO luminance coefficients and ignores
  alpha. The bundled configuration currently reports
  `(0.2126, 0.7152, 0.0722)`, so the coefficients and configuration fingerprint
  must travel with the conversion rather than being an undocumented global
  constant.
- The current Blender frontend recursively visits closure topology but
  immediately flattens it into PBR channels. Add Shader therefore behaves like
  a special Mix and cannot preserve additive energy.
- The current planner routes coarse closure regions and runtime expressions but
  has no weighted closure set, compatibility proof, render-state plan, shader
  requirement plan, or closure budget.
- The current Unity backend already has a version-isolated Shader Graph 17.4
  reflection adapter, deterministic IDs, atomic writes, wrapper ownership, and
  a useful low-quality dielectric path. Those mechanisms should be refactored,
  not replaced.
- The current live Unity project matches the installed editor revision. The
  staging validation project has a stale revision and lacks a package lock, so
  it is not authoritative.
- Shader Graph 17.4 exposes `UnityEditor.Graphing.SlotType`, not the namespace
  previously assumed by the adapter. Its MaterialSlot constructors and
  `AddSlot` method require optional arguments to be supplied explicitly through
  reflection.
- Blender 5.2 headless export exposed a v2 regression in channel-value
  replacement: the helper reconstructed the document as MaterialIR 1.0. It now
  preserves the incoming document kind and has a Python regression test.
- Bundle-directory identity discovery originally accepted only
  `migr-bundle-1.0`; v2 re-export would therefore fail to reuse an identity-owned
  directory. The reader now recognizes both bounded v1 and current v2 bundles.
- Shader Graph's Default Screen Position output is already normalized; dividing
  it by `w` produced invalid custom-lighting screen UVs. The HLSL now consumes
  `screenPosition.xy` directly.
- URP 17.4 custom multi-lobe lighting can preserve direct lights, shadows,
  cookies, probes/SH, Forward/Forward+, and pass fog, but it cannot currently
  consume SSAO. Auto reports this as a declared approximation and Strict
  rejects it. Linked per-lobe normals are Unsupported rather than silently
  replaced by the geometry normal.

## Decision log

- **2026-07-29 — Delivery scope:** implement phase 1-5 enhanced functionality.
  Represent unsupported phase-6 closure domains faithfully and fail with
  diagnostics rather than inventing render behavior.
- **2026-07-29 — Fidelity compatibility:** preserve current `Auto` behavior for
  registered, declared approximations and add `Strict` fidelity policy.
- **2026-07-29 — Add Shader policy:** default to `PreserveBlender`. Energy
  normalization and real-time clamping require explicit policy selection and
  are rejected by Strict fidelity.
- **2026-07-29 — Versioning:** shapes changed by closure-aware planning move to
  2.0. Ordinary v1 Standard PBR migrates deterministically; legacy v1
  transparent/dielectric assets keep a frozen compatibility path.
- **2026-07-29 — Promotion:** implement and validate in canonical staging, then
  perform the existing repository cutover only after backup and full gates.

- **2026-07-29 — Custom-lighting boundary:** report missing SSAO as a declared
  approximation and reject it under Strict. Reject linked per-lobe normals
  until the backend evaluates each normal independently.

## Implementation sequence

1. Add pure target-neutral modules for typed socket conversion, closure/value
   IR, symbolic weight expressions, flattening, exact simplification, domain
   analysis, feature analysis, Standard Lit proof, surface-model resolution,
   render state, requirements, and budgets.
2. Change Blender extraction to build complete closure topology with stable
   source identity, socket order, group path, and local closure weight. Preserve
   the old flattened PBR result temporarily as a compatibility projection while
   v2 consumers are introduced.
3. Introduce versioned v2 schemas and validators. Update planning so closure
   selection precedes value-island Native/Bake/Unsupported routing. Add v1
   migration and public CLI/UI fidelity and energy policies.
4. Add Unity v2 serializable models and a surface-generator registry. Move the
   current Standard Lit path behind the Opaque generator, add Cutout,
   TransparentLit, TransparentEmission, RefractiveGlass, and CustomMultiLobe
   generators, and keep all Shader Graph internals inside the 17.4 adapter.
5. Build templates from real Unity 17.4 assets. Implement premultiplied scalar
   transmittance, Scene Color composition where safe, structured project
   requirements, per-lobe custom lighting, and explicit unsupported behavior.
6. Add tests and public documentation. Build distributable artifacts only from
   the canonical sources after source tests pass.
7. Validate on the exact Blender and isolated Unity versions, review the final
   diff, create recovery backups, execute the allow-listed root cutover, and
   rerun validation.

## Validation

The implementation is not complete until all applicable commands and their
actual results are recorded here. Required gates include:

- Full Python unit suite and schema/security/determinism tests.
- Blender 5.2 headless closure corpus and exporter fixtures.
- Layer Weight numerical matrix for Blend
  `[0, .04, .25, .5, .75, .9, .999]`, NdotV
  `[1, .75, .5, .25, .1, 0, -.5]`, front and back faces.
- Unity EditMode and Graphics tests against Unity 6000.4.5f1,
  URP/Shader Graph 17.4.0, including actual Shader Graph import and compilation.
- Magic Orb Bloom Off/On visual regression and multi-angle glass regression
  using raw linear images without alignment, cropping, or scaling.
- Repeated generation and single-source-change deterministic ID tests.
- v1 ordinary PBR migration and legacy v1 surface compatibility tests.

## Results and follow-up

The phase 1-5 implementation is complete and was promoted to the current
repository through an allow-listed source cutover. The prior current-source
state is recoverable at
`C:/Users/22687/Desktop/项目4.pre-migr2-backup.20260729-155425`.

Executed validation:

- `py -3.13 tools/ci/run_checks.py`: passed; parsed 68 Python files,
  validated 14 schemas, ran 136 Python tests, verified package identity, and
  built deterministic `migr_semantic_exporter-2.0.0.zip`,
  `migr_gpl_bake_worker-1.1.1.zip`, and
  `com.migr.shaderconverter-2.0.0.tgz`.
- Blender 5.2.0 LTS headless closure, glass, and runtime-input smokes: passed
  with all expected completion markers.
- Unity 6000.4.5f1 (`cc83ebd631f8`), URP/Shader Graph 17.4.0 isolated EditMode:
  65 passed, 0 failed, 1 graphics-only test skipped under `-nographics`.
- The skipped graphics test was rerun with a real D3D11 11.1 device: 1 passed,
  0 failed, 0 skipped.
- After promotion, the current repository repeated the complete source gate
  and closure smoke successfully.

Known limitations are explicit rather than hidden: custom multi-lobe lighting
does not consume SSAO, linked per-lobe normals are Unsupported, low-quality
glass is screen-space and single-sample, and phase-6 volume/holdout/
Shader-to-RGB/spectral rendering remains deferred. New Magic Orb and
multi-angle glass golden images still require human capture and approval, so
the exact compatibility tuple remains `Experimental`, not `Supported`.
