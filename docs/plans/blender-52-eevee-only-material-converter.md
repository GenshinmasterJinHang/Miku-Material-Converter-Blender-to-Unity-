# Blender 5.2 EEVEE-only material conversion

This ExecPlan is a living implementation record. It follows `PLANS.md` and must
be updated whenever implementation or validation changes the assumptions below.

## Purpose and outcome

Refocus MiGR on Blender 5.2.0 LTS EEVEE material graphs and Unity 6 URP Shader
Graph. Non-volume shader and texture nodes in the approved Blender 5.2 menus
must prefer editable graph translation, use an explicit approximation when no
equivalent graph exists, and automatically bake only when that is the safest
way to preserve a static material. A bake makes the material mesh-dependent, so
the Blender add-on must also export the selected hierarchy as GLB plus a stable
material-slot binding manifest.

The Blender panel must expose one automatic export path rather than separate
material, model, optical, dynamic-effect, and bake workflows. Current public
documentation must contain exact English and Chinese support matrices. The
non-public material library validation scope is 18 blend files and 253 assigned
materials after excluding the Glass, Gem, Magic Ball folders and the material
named `玻璃雾岩`.

The deliverable stops at a local human-review package. Source and release
archives may be uploaded to GitHub only after the user explicitly approves the
Unity renders.

## Context and constraints

- The worktree was heavily modified before this task. Shared files contain
  unrelated user work. Do not reset, restore, or replace them wholesale.
- The exact target stack is Blender 5.2.0 LTS, Unity 6000.4.5f1, URP 17.4.0,
  and Shader Graph 17.4.0 on Windows.
- Blender's official `blender-v5.2-release` manual was inspected at commit
  `e74f0a2b4c5475fe8bc50434d869b07ea7adfa4f`. The installed Blender build is
  commit `fbe6228777e7`.
- Blender source semantics are EEVEE-only. Cycles may be used internally on
  isolated temporary data to bake unlit channel expressions, but it is not a
  supported material dialect and the original scene state must be restored.
- Volume Principled, Volume Absorption, Volume Scatter, and Volume Coefficients
  are explicitly deferred for this release.
- Old Cycles optical companions and dynamic effects are removed from the current
  producer and consumer. Game presets remain.
- Generated Shader Graph wrapper ownership and deterministic ID rules remain in
  force. Do not generate generic ShaderLab.
- Material-library models, textures, screenshots, and generated review assets
  have no approved redistribution rights and must not enter source archives or
  GitHub releases.
- Current version targets are Blender add-on 0.11.0, Unity package 0.11.0,
  `mgir-3.0`, `b2u-hybrid-plan-2.0`, and an updated bundle manifest with an
  optional model path.

## Progress

- [x] 2026-07-23: Read repository instructions, inspect the dirty worktree,
  versions, release tooling, material-library scope, current translation routes,
  and official Blender 5.2 node documentation.
- [x] 2026-07-23: Agree on EEVEE-only public scope, hidden channel-bake backend,
  complete optical/dynamic-effect retirement, automatic GLB-on-bake behavior,
  volume deferral, 253-material review scope, and the human approval gate.
- [x] 2026-07-23: Add the target-neutral EEVEE output resolver and feature
  report, switch new exports to `mgir-3.0`, add hybrid-plan 2.0 and bundle 1.2
  schema generators, preserve Blender 5.2 Metallic/Gabor parameters, and make
  volume routing explicitly Deferred.
- [x] 2026-07-23: Simplify the public Blender panel and couple generated bake
  resources to automatic GLB export while keeping source-image-only exports
  material-only.
- [ ] Publish the current schemas and retire optical/dynamic-effect public
  surfaces with migration diagnostics.
- [ ] Implement Blender 5.2 node extraction, translation decisions, bake/model
  coupling, and the simplified panel.
- [ ] Update the Unity importer, models, Shader Graph backend, templates, and
  tests for the current contracts.
- [ ] Publish English and Chinese README files and detailed support,
  compatibility, migration, diagnostic, and release documentation.
- [ ] Run Python, Blender 5.2, .NET, and Unity EditMode validation.
- [ ] Export/import the 253-material review corpus and generate paired Blender
  EEVEE/Unity captures plus a local review report.
- [ ] Obtain explicit human approval.
- [ ] Build deterministic archives, publish cleaned source, and create the
  GitHub release.

## Discoveries

- 2026-07-23: The official Blender 5.2 manual states that EEVEE Diffuse ignores
  Roughness; Glass/Refraction and Glossy support only GGX families; Principled
  accumulates several approximations; standalone anisotropy and sheen are not
  supported; IES is not supported; and Image/Sky have mode-specific limits.
- 2026-07-23: Blender 5.2.0 exposes all requested non-volume node RNA types,
  including Metallic BSDF, Volume Coefficients, and Gabor Texture. Point Density
  no longer exists as a Blender 5.2 RNA node type.
- 2026-07-23: After the exclusions, 18 blend files contain 253 assigned review
  materials and 18 unused material datablocks. The unused datablocks are not
  visual review targets.
- 2026-07-23: A read-only analysis of the corpus with the current planner routes
  only 11 materials through native/semantic paths and 242 through PBR baking.
  The new implementation must improve graph coverage without claiming false
  parity.
- 2026-07-23: The current repository has no Git remote, but Git Credential
  Manager has access to `GenshinmasterJinHang` and the target public repository
  currently contains only its initial README and license commit.
- 2026-07-23: The installed Shader Graph package includes native Checkerboard,
  Gradient Noise, Simple Noise, Voronoi, texture, cubemap, Fresnel, and refract
  building blocks. It does not provide Blender-equivalent Brick, Gabor, Magic,
  White Noise, subsurface, anisotropy, sheen, or volumetric targets.
- 2026-07-23: The available Python 3.13 environment has `jsonschema` but not
  `pytest`; the first targeted run therefore used `unittest`. After updating
  the current contract assertions, 60 EEVEE/core/hybrid tests pass.

## Decision log

- 2026-07-23: Blender 5.2.0 LTS is the sole certified Blender version. Other
  versions are Unknown/Unsupported until independently validated.
- 2026-07-23: Non-volume requested nodes must not fail merely because an exact
  Unity equivalent is absent. The route order is Graph, Approximate Graph,
  Baked Resource, then a blocking diagnostic only for invalid or missing data.
- 2026-07-23: Static procedural graphs use baking when matching Blender's hash
  or noise implementation matters. Animated inputs may use deterministic
  runtime approximations with an explicit diagnostic.
- 2026-07-23: Glass and Refraction use an EEVEE-style transparent screen-space
  approximation and project-setup diagnostics. The prior Cycles optical
  material pipeline is not retained.
- 2026-07-23: Volume nodes are deferred by explicit user direction. They produce
  a structured `Deferred` diagnostic and are documented, not silently lowered.
- 2026-07-23: Any generated texture resource classified as a material bake
  triggers GLB export. Directly copied source images do not.
- 2026-07-23: The main panel has one automatic export operator. Low-level bake
  settings move to stable presets; optical and dynamic-effect controls are
  removed.
- 2026-07-23: GitHub publication is a hard human gate. Review assets remain
  local because of third-party rights.

## Implementation sequence

1. Read the active architecture, schema, compatibility, ownership, testing, and
   release documentation plus the overlapping implementations and tests.
2. Add `mgir-3.0` and `b2u-hybrid-plan-2.0`; update bundle/bake contracts;
   remove current optical and dynamic-effect fields; add explicit legacy and
   deferred-volume diagnostics; update schema fixtures and validation.
3. Refactor the Blender exporter into extraction, EEVEE capability analysis,
   translation planning, and export execution. Add requested Blender 5.2 node
   identifiers and mode/socket validation.
4. Implement graph lowering for compatible BSDFs and Brick, Checker, Gradient,
   common Wave/Noise/Voronoi modes, Metallic F82/complex-IOR conversion, and
   compatible Add/Mix closure recognition. Implement documented approximations
   for Glass, Refraction, Holdout, SSS, Translucent, and standalone Sheen.
5. Extend automatic baking for static Magic, high-parity Noise/Voronoi/Gabor/
   White Noise, complex closure channels, thickness, Texture3D, and direction
   LUT resources. Work on temporary object/material copies and restore Blender
   state on every exit path.
6. Replace the Blender panel with output root, workflow preset, quality preset,
   automatic preflight summary, and one export button. Export materials only
   when no bake is required; otherwise export selected roots and descendants as
   GLB plus bindings.
7. Update Unity current-version models/import/compiler and the Shader Graph 17.4
   backend. Add versioned specular, transparent/refraction, pseudo-SSS, and
   procedural wrapper/subgraph resources without introducing generic ShaderLab.
8. Delete active optical/dynamic-effect implementation and tests, retain only
   clearly labelled historical documentation, and update current docs in
   English and Chinese.
9. Run unit, schema, Blender headless, .NET, Unity EditMode, determinism,
   negative, and release checks. Fix regressions attributable to this work while
   reporting unrelated pre-existing failures separately.
10. Export the 18 source files, import all 253 materials into the verification
    project, validate shaders/bindings, and generate local paired captures,
    diagnostics, aggregate JSON, and an HTML review index.
11. Stop for user review. After approval, create a fresh clean publication
    workspace from the remote main branch, copy only release-allowlisted source,
    build twice, compare hashes, push main, and create release `v0.11.0`.

## Validation

Planned commands and environments:

- `python -m pytest -q`
- Blender 5.2.0 LTS headless fixture scripts with `--factory-startup` and
  `--python-exit-code 1`.
- `.tools`/system .NET 8 compiler harness restore, build, and run.
- Unity 6000.4.5f1 EditMode tests through the connected editor or
  `tools/ci/run_unity_editmode.ps1`.
- `python tools/ci/run_checks.py --profile pr`
- `python tools/ci/run_checks.py --profile release`
- Two `python tools/release/build_release.py` runs with identical file lists and
  SHA-256 hashes.
- Residual identifier searches, schema validation, `git diff --check`, package
  entry inspection, and a final dirty-worktree review.

Material review acceptance:

- 18/18 blend files complete and 253/253 assigned materials produce a terminal
  translation decision.
- No imported material uses an error/fallback shader, appears pink, has a
  missing generated texture, or loses its material-slot binding.
- Every approximation and bake is visible in diagnostics and the review index.
- Blender and Unity captures use recorded camera, lighting, color-management,
  and environment settings.
- GitHub publication remains blocked until the user explicitly approves the
  review output.

## Results and follow-up

Implementation is in progress. Volume shader support is the only intentionally
deferred feature in the approved node lists. This section will be updated with
actual commands, outcomes, compatibility effects, known limitations, review
status, and release identifiers.
