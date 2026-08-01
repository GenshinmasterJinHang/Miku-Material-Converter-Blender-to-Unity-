# MiGR 1.2.1 Magic Ball and Glass Regression Repair

## Purpose and outcome

MiGR 1.2.1 repairs regressions found after the 1.2.0 transparent-surface
release. The observable outcome is that the fixed magic-ball corpus exports in
Auto mode without appearance-snapshot permission, the three-stop color ramp in
Magic Ball 9 imports into Unity without a reflection wrapper exception, and
Blender 5.2 Glass exports with a non-zero transmission weight and the six
documented optical material properties.

The semantic exporter and Unity UPM package become 1.2.1. The GPL bake worker
remains 1.1.1 and its protocol is unchanged.

## Context and constraints

- Canonical implementation work is limited to the MiGR 1.x sources. Retired
  B2U sources must not be restored.
- The worktree contains the larger in-progress MiGR cutover. Preserve all
  unrelated edits and generated evidence.
- `魔法球.blend` and `魔法球.migr-fixed.blend` are user assets and must not be
  modified. The fixed file is the positive corpus; the original Magic Ball 10
  remains the negative Light Path fixture.
- Wrapper Shader Graphs are user-owned after creation. Any targeted Full
  Regeneration must first back up affected Unity asset roots.
- The validated tuple remains Blender 5.2.0, Unity 6000.4.5f1, URP 17.4.0,
  Shader Graph 17.4.0, Windows, Linear color space, and Camera Opaque Texture.

## Progress

- [x] 2026-07-29: Reproduced the Magic Ball 9 Unity failure and unwrapped it to
  a duplicate deterministic Color Ramp literal-node ID.
- [x] 2026-07-29: Reproduced Magic Ball 7 and fixed Magic Ball 10 failing
  because constant Emission color/strength was scheduled as a channel bake.
- [x] 2026-07-29: Verified Magic Ball 5 exports independently; retain it as a
  regression fixture rather than adding an unproven behavior change.
- [x] 2026-07-29: Verified Blender 5.2 exposes an unavailable Glass Weight
  socket with value zero, while a fresh 1.2.0 dielectric IR therefore records
  `TransmissionWeight=0`.
- [x] 2026-07-29: Re-read the current fixed corpus and found a twelfth
  user-added dielectric material, `魔法球10.001`. The smoke now requires the
  canonical 11-material baseline and also validates every additional material.
- [x] 2026-07-29: Implemented Blender/Core corrections and unit tests.
- [x] 2026-07-29: Implemented Unity generation, compatibility, and diagnostic
  corrections.
- [x] 2026-07-29: Updated release metadata, compatibility documentation, and
  changelogs.
- [x] 2026-07-29: Ran Python/Core, Blender, Unity, deterministic-build,
  installed-artifact,
  and targeted-regeneration gates.

## Discoveries

- The 1.2.0 magic-ball smoke passed only because it exported with
  `allow_appearance_approximation=True`. Auto mode with the default false value
  exposes the final-lighting bake refusal for materials 7 and 10.
- Emission lowering always creates a synthetic `Math.Multiply` when constant
  Strength differs from one. MaterialIR sees a linked static source and marks
  Emission `requiresBake`, even though both operands are constants.
- `Shader.GlassBSDF` asks for Weight with a fallback of one, but the snapshot
  includes Blender's disabled/unavailable Weight socket and reads its zero
  default before the fallback can apply.
- The planner advertises screen refraction but has no explicit static
  `GlassClosure` route. A dynamic mixed Glass graph succeeds incidentally
  through the generic dynamic route while direct Glass is rejected.
- Color Ramp expansion calls `Element(index)` repeatedly. Every call creates a
  node from the same stable role, so GraphData rejects the duplicate ID.
- The Unity project already has Linear color space and Camera Opaque Texture.
  Those project settings do not explain the observed black glass.
- The current fixed `.blend` has 12 materials rather than the 11 recorded by
  the 1.2.0 plan. `魔法球10.001` is a Dithered
  `DielectricScreenRefraction` material and must remain covered without editing
  the file.
- Full Regeneration initially left `_TransmissionWeight=0` on an existing
  MiGR-owned generated material because optical constants were not rebound after
  the Shader Graph was regenerated.
- A second black-glass cause remained after restoring transmission weight:
  the dielectric wrapper reused the Standard authoring path and multiplied the
  optical result by Blender Glass's non-semantic black Base Color. Runtime
  Coverage was also not connected to the wrapper Alpha block.
- Fresh Blender sessions cannot provide stable identities for materials whose
  source `.blend` has not persisted MiGR material IDs. The real Magic Ball 10
  identities are stable, but fresh Magic Ball 7/9 and direct-Glass exports
  received new identities. Those assets must be imported alongside existing
  assets rather than matched by localized display name.

## Decision log

- 2026-07-29: Constant-fold only fully constant Emission color/strength.
  Linked values keep expression or bake semantics; the GPL worker will not be
  broadened to bake final lighting.
- 2026-07-29: Snapshot socket availability explicitly and ignore unavailable
  closure inputs. This preserves a future active Glass Weight without
  reinterpreting Blender's current hidden compatibility socket.
- 2026-07-29: Add explicit native Transparent and Glass planner routes before
  mesh fallback. Glass remains `Approximate`.
- 2026-07-29: Accept the 1.2.0 profile for non-dielectric surface contracts in
  Unity 1.2.1. Reject 1.2.0 dielectric contracts with
  `MIGR_DIELECTRIC_REEXPORT_REQUIRED_1_2_1`; zero may be intentional and must
  not be silently rewritten.
- 2026-07-29: Cache Color Ramp element handles per index instead of weakening
  stable-ID collision detection.
- 2026-07-29: Back up and explicitly Full Regenerate only identity-matched
  affected assets. Do not migrate assets by approximate display name.
- 2026-07-29: Reset MiGR-owned generated base-material surface defaults during
  Full Regeneration and bind Coverage plus Glass optical constants. User
  material variants continue to inherit from that generated base.
- 2026-07-29: Dielectric wrappers bypass the Standard Base Color authoring
  multiply and connect the generated optical result directly to Base Color.
  Every non-opaque surface connects runtime Coverage directly to the wrapper
  Alpha block.
- 2026-07-29: Preserve the user's dirty live Unity scene. Validate the final
  package in an isolated Unity project, then replace only backed-up,
  identity-matched generated directories. Import new identities into separate
  roots without overwriting existing assets.

## Implementation sequence

1. Record socket availability, ignore unavailable closure inputs, constant-fold
   Emission color/strength, add closure routes, and normalize diagnostic text.
2. Cache Color Ramp element nodes, add structured adapter errors, recursively
   unwrap reflection exceptions, and implement the 1.2.0 surface-profile
   compatibility policy.
3. Add unit, Blender corpus, and Unity EditMode regressions for the reproduced
   inputs and negative cases.
4. Update implementation hashes, profile hash, versions, public docs,
   diagnostics, compatibility matrix, and release notes.
5. Build twice, install the artifacts, rerun real exports/imports, back up
   identity-matched generated assets, and perform targeted Full Regeneration.

## Validation

- `py -3.13 tools/ci/run_checks.py`
- `C:\SteamLibrary\steamapps\common\Blender\blender.exe --background --factory-startup --python tests/blender/migr_magic_ball_corpus_smoke.py`
- The equivalent Blender 5.2 glass corpus smoke for `玻璃.blend`.
- `tools\ci\run_unity_editmode.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe" -ResultsPath "TestResults-MagicBall-1.2.1.xml"`
- Build the semantic exporter and UPM package twice and compare SHA-256.
- Install only the built 1.2.1 exporter and UPM package, retain worker 1.1.1,
  re-export the real fixtures, explicitly regenerate backed-up Unity assets,
  and verify shader compilation, optical properties, and a non-black glass
  render over a non-black background.

## Results and follow-up

The final implementation profile is
`b08ac3e4506bf127709cef9b42679dbca836615e62eaf2df9b4ca79ff6393f16`.
Python/Core checks passed 123 tests. Blender 5.2 corpus smoke passed the fixed
12-material Magic Ball corpus and direct Glass corpus with appearance snapshots
disabled. Unity 6000.4.5f1 EditMode passed 55 tests with one intentionally
skipped GPU preview test and no failures.

The final deterministic artifacts matched across two builds:

- Semantic Exporter 1.2.1:
  `732cccef088759902e163d216dd37f0e377e7c882611efd64746871c2fb2eab3`
- GPL Bake Worker 1.1.1:
  `df2021f7bbd27836ebd0654ae49b1163e7e1651dda4b3f8ac359626e433c154f`
- Unity UPM 1.2.1:
  `f6bd76ed5bb53ce1600ddc7629db9a41d22f1812e5e2587368280089479c94e7`

A real D3D11/URP Play Mode smoke rendered the direct Glass fixture over a
non-black RGB background in Linear color space. It passed with center mean
`0.591204` and visible-pixel ratio `1.0`, proving that Scene Color remains
visible and the sphere is not black. Evidence is stored under
`.validation/unity-glass-playmode-1.2.1.{png,json}`.

The final UPM archive is installed in the live project's manifest and extracted
package cache. Because the current Unity session has an unsaved dirty scene,
the same-version package assembly was not force-reloaded. Final assets were
therefore generated in the isolated project, backed up, and migrated into the
live project. Magic Ball 10 and 10.001 replaced exact identity matches; Magic
Ball 7/9 and direct Glass were added under distinct final roots. A live
AssetDatabase refresh produced no MiGR, Shader Graph, or shader-compiler
errors. The current session should be restarted normally before the next
interactive import so Unity loads the final 1.2.1 editor assembly.

Known deliberate limits remain transparent sorting, screen-space single-sample
refraction, Reflection Probe dependence, no nested media or caustics, and
approximate Wireframe/Dither behavior.
