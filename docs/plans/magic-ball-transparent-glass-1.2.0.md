# MiGR 1.2.0 Magic Ball Transparency and Glass Export

## Purpose and outcome

This plan fixes the MiGR semantic exporter and Unity Shader Graph backend so
that the Blender 5.2 `魔法球` material corpus no longer fails with
`MIGR_REQUIRED_CHANNEL_UNRESOLVED:BaseColor` merely because a required surface
chain contains Transparent BSDF. It adds explicit, target-neutral surface and
coverage semantics for transparent emission, transparent Principled, and
Facing-driven transparent glass. It also preserves Blender's source render
method, rejects required Light Path semantics precisely, lowers BUMP
displacement to a fragment normal, and adds Blender-compatible Overlay math.

The observable result is:

- `魔法球1` through `魔法球10` export from a repaired copy of the source file;
- `Dots Stroke` exports through the direct material API;
- the untouched source `魔法球10` fails with
  `MIGR_LIGHT_PATH_UNSUPPORTED:Transparent Depth`;
- Alpha Blend, Dithered, and dielectric screen-refraction contracts select
  versioned Unity 6000.4.5f1 / URP 17.4.0 / Shader Graph 17.4.0 wrappers;
- the Blender semantic exporter and Unity UPM package are released as 1.2.0;
  the GPL bake worker protocol and package remain 1.1.1.

## Context and constraints

The only canonical implementation roots for this work are `migr/`,
`migr_blender/`, `extensions/migr_semantic_exporter/`, and
`unity/Packages/com.migr.shaderconverter/`, plus their schemas, tests,
documentation, and build tooling. Retired B2U sources, installed extensions,
validation-project package copies, and `dist/` artifacts are not source.

The working tree contains extensive pre-existing modifications and removals.
They must not be cleaned, reverted, reformatted, or folded into this work. The
original corpus file remains unchanged. Blender creates
`材质库/魔法球/魔法球.migr-fixed.blend` beside it and removes only the final
Light Path branch of material `魔法球10`.

Generated `*.generated.shadersubgraph` files are MiGR-owned. Wrapper
`*.shadergraph` files become user-owned after creation; a render-contract
change must not overwrite a user-modified wrapper without explicit Full
Regeneration.

`surfaceContract` is an optional companion object inside MaterialIR 1.0. Old
documents without it retain the existing Opaque interpretation. New documents
use a new target implementation hash so an older Unity package cannot silently
ignore the contract. Unknown companion schema identifiers are errors.

## Progress

- [x] 2026-07-28: Read repository constitution, `PLANS.md`, relevant
  architecture/compatibility documents, tests, source paths, versions, and
  working-tree state.
- [x] 2026-07-28: Audited all eleven Blender materials and isolated the
  Transparent/Glass, Overlay, BUMP, Wireframe, render-method, and Light Path
  failure modes.
- [x] 2026-07-28: Implemented target-neutral closure lowering and strict surface contract.
- [x] 2026-07-28: Implemented Overlay, Light Path diagnostics, and BUMP normal lowering.
- [x] 2026-07-28: Added schema validation and Python/Core tests.
- [x] 2026-07-28: Created and verified `魔法球.migr-fixed.blend` without changing the source.
- [x] 2026-07-28: Added Blender 5.2 synthetic and full-corpus smoke tests.
- [x] 2026-07-28: Created real Unity 17.4 wrapper templates and extended the backend/importer.
- [x] 2026-07-28: Added and executed Unity EditMode tests for import, compilation, ownership, setup
  diagnostics, and determinism.
- [x] 2026-07-28: Updated public documentation, compatibility, diagnostics, changelogs, and
  component versions.
- [x] 2026-07-28: Ran Python, Blender, Unity, deterministic-build, installation, and real
  export gates.

## Discoveries

- Blender 5.2.0 loads MiGR 1.1.1 from the Steam portable extension repository;
  the Roaming 1.0.0 copy is inactive.
- Transparent BSDF is currently recorded as an opaque Blender node. A
  Layer Weight dependency keeps the enclosing surface mix native, while
  MaterialIR still requires BaseColor; no value, default, or resource can then
  satisfy the channel.
- `魔法球1`, `魔法球4`, and `魔法球5` reach an unsupported runtime
  `Color.Mix` Overlay operation.
- `魔法球9` uses `displacement_method=BUMP`; the active Material Output
  Displacement chain is currently omitted instead of lowered to a fragment
  normal.
- `魔法球10` uses Light Path `Transparent Depth` only in the final surface
  selection. On the first camera-visible surface its value is zero, so directly
  selecting the first branch is the approved source repair.
- The existing expression-island bake path can preserve the static Wireframe
  island, but Blender's pixel-size behavior remains view-dependent and must be
  reported as Baked/Approximate.
- The Unity package currently has one Opaque Lit wrapper template. Its target
  serialization must not be guessed; new wrappers must be created by the exact
  target Unity/Shader Graph version and then sanitized/versioned.
- `魔法球9` contains a three-stop B-Spline Color Ramp, not merely BUMP. MiGR
  now preserves all ramp elements and emits an explicit piecewise-cubic
  approximation diagnostic.
- The first full-corpus export exposed duplicate stable expression records when
  one source expression was shared by several closure channels. Identical
  records now reuse one DAG node; a differing same-ID record fails as a data
  integrity collision.
- The three new wrappers were produced by deserializing the checked-in real
  Standard template in Unity 6000.4.5f1, setting the URP 17.4 target through
  the loaded package API, validating the graph, and serializing it with
  Shader Graph 17.4 `MultiJson`.

## Decision log

- 2026-07-28: Use surface closure algebra instead of treating shader closures
  as colors. Coverage is independent from surface emission to prevent emission
  being multiplied by the mix and then again by output alpha.
- 2026-07-28: Use `migr-surface-1.0` as a strict optional companion contract in
  MaterialIR 1.0. This is additive for old readers only when the target profile
  hash matches; otherwise import must stop.
- 2026-07-28: Map Blender BLENDED to transparent Lit Alpha Blend. Map DITHERED
  non-optical materials to Opaque + screen Dither + Alpha Clip so they retain
  depth writing.
- 2026-07-28: Map Glass to a transparent Unlit screen-refraction wrapper with
  Scene Color, Schlick Fresnel, Reflection Probe, and independent coverage and
  emission. This is an `Approximate` translation and requires Camera Opaque
  Texture, Linear color space, and a suitable Reflection Probe.
- 2026-07-28: Never mutate URP assets or Player color-space settings. Missing
  configuration yields `RequiresProjectSetup`.
- 2026-07-28: Required Light Path is always `Unsupported`; inactive Light Path
  nodes are pruned. No bake, constant, or BaseColor fallback is allowed.
- 2026-07-28: BUMP is fragment Normal From Height. True
  `DISPLACEMENT`/`BOTH` is rejected with
  `MIGR_VERTEX_DISPLACEMENT_UNSUPPORTED`.
- 2026-07-28: Preserve existing property references and add `_IOR`,
  `_TransmissionWeight`, `_Opacity`, `_RefractionStrength`,
  `_ReflectionStrength`, and `_Thickness`.
- 2026-07-28: Keep the GPL bake worker at 1.1.1 because this work does not
  change its request/result protocol.

## Implementation sequence

1. Extend node normalization and closure analysis for Transparent BSDF, Glass
   BSDF, Displacement, and Light Path. Represent a lowered surface as surface
   parameters plus independent continuous coverage and optional dielectric
   optics.
2. Select required MaterialIR channels from the lowered closure. Add optical
   channels and `surfaceContract`; validate schema, channel references,
   enum values, and companion schema strictly.
3. Implement Blender 5.2 Overlay's piecewise numeric expression and matching
   Unity graph expansion.
4. Inspect output displacement independently of the surface chain. Lower BUMP
   height to fragment normal and reject true vertex displacement.
5. Add focused Core/Blender tests for closure input order, coverage, Overlay,
   Light Path pruning/failure, displacement, schema compatibility, and
   determinism.
6. Create the repaired `.blend` with Blender 5.2 and add a corpus smoke that
   exports all eleven materials and asserts material-specific contracts and
   diagnostics.
7. Create four Unity 17.4 wrapper variants from real assets: Opaque Lit,
   Alpha Blend Lit, Dithered Opaque Lit, and dielectric transparent Unlit.
   Sanitize only nondeterministic identities and submit the templates with
   provenance.
8. Extend backend selection, subgraph outputs, project-setup diagnostics,
   wrapper ownership checks, property binding, and deterministic identity
   generation. Add EditMode tests.
9. Update versions, schemas, migration/compatibility/diagnostic/node-support
   documents, changelogs, and release notes.
10. Run all gates, build exporter and UPM twice, compare SHA-256, install only
    the 1.2.0 exporter into Steam portable, verify installed hashes/module
    paths, and re-export the real fixed corpus.

## Validation

Run:

    py -3.13 tools/ci/run_checks.py

Expected: formatting, linting, schemas, and all Python/Core tests pass.

Run:

    C:\SteamLibrary\steamapps\common\Blender\blender.exe --background --factory-startup --python tests/blender/migr_magic_ball_corpus_smoke.py

Expected: Blender reports 5.2.0; ten assigned materials plus direct
`Dots Stroke` export; no BaseColor unresolved errors; source `魔法球10` has the
specific Light Path failure and the repaired copy succeeds.

Run:

    tools\ci\run_unity_editmode.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe" -ResultsPath "TestResults-MagicBall-1.2.0.xml"

Expected: all wrapper variants import and compile with no pink shader or
compiler error; queue, ZWrite, Alpha Clip, project setup, properties, wrapper
ownership, and repeat-generation stability assertions pass.

Build both exporter and Unity UPM archives twice from identical source state and
compare SHA-256. Install the exporter archive only into the Steam portable
repository after confirming no Blender GUI process is open. Re-run installed
identity and full real-file export smokes.

## Results and follow-up

Implementation and automated validation are complete. `run_checks.py` passed
120 tests. Blender 5.2 exported all eleven repaired-corpus materials and
verified the original Light Path failure. Unity EditMode discovered 51 tests:
50 passed, none failed, and one graphics preview was ignored under
`-nographics`; the final UPM tarball was installed into the validation project
and produced the same result.

Both release archives were built twice with byte-identical results:

- `migr_semantic_exporter-1.2.0.zip`:
  `2b86130615add0d2ddeee5c3eef9fc57a9bc844c835b41ecd7b6530b965b9325`
- `com.migr.shaderconverter-1.2.0.tgz`:
  `bce425208e00b56011a6f50f58ed641b1756dbc3938d87ce3e5fe600def54eca`

The Semantic Exporter was installed from that ZIP only into the Steam portable
repository and its installed tree was verified. The already-installed GPL
worker was enabled for the validation session but not overwritten; it remains
1.1.1. The installed exporter produced the ten object-assigned Magic Ball
bundles plus direct `Dots Stroke` output.

Known intentional limitations are transparent
sorting/ZWrite/shadow behavior; screen-space dither pattern and motion
shimmer; colored DITHERED transparency approximation; Wireframe pixel-width
bake approximation; lack of nested dielectric media, repeated/internal
refraction, caustics, dispersion, transparent-behind-transparent refraction,
and physically exact rough transmission; and unsupported recursive Light Path
depth semantics.

No production dependency is added. Any validation blocked by unavailable Unity
package internals, an open Blender GUI with unsaved work, or missing project
configuration will be recorded here rather than silently substituted.
