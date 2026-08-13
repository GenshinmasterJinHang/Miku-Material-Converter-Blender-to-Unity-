# HSR tutorial lighting and face-detail alignment

## Purpose and outcome

Align the Experimental HSR Body and Hair lighting masks with the formulas
described by the user-supplied tutorial, and make Face highlights and the
authored nose line visibly controllable. Body and Hair will use LightMap green
as the tutorial Shadow AO input and LightMap blue as an inverted Toon-specular
threshold. Face will gain a skin-gated parametric Blinn-Phong Toon highlight
without requiring another texture, while FaceMap blue continues to author the
nose line through the tutorial-style view-angle response.

## Context and constraints

- Canonical implementation lives under
  `unity/Packages/com.miku.shaderconverter/Runtime/HSR/`; tests live in the
  package EditMode test assembly.
- HSR remains an Experimental fixed-workflow preset. It does not promise
  pixel-exact game parity.
- The validation tuple is Unity 6000.4.5f1 with URP 17.4.0 and Shader Graph
  17.4.0 on Windows.
- Existing shader property names must remain deserializable. Body/Hair legacy
  threshold and ramp-offset properties remain declared, but the tutorial path
  no longer reads them.
- Face must not gain a LightMap binding. No MaterialIR, Bundle, schema, shader
  name, material part, or texture-role change is in scope.
- User-supplied models, textures, tutorial text, and rendered comparisons are
  validation inputs only and must not enter the repository or release archive.
- Unrelated working-tree changes, including `vibe-kanban/` and Endfield edits,
  are outside this plan and must be preserved.

## Progress

- [x] 2026-08-12: Created this ExecPlan before implementation.
- [x] 2026-08-12: Finished the manuals, compatibility, ADR, provenance, and
  changelog edits.
- [x] 2026-08-12: Public documentation regression suite passed (8/8).
- [x] 2026-08-12: Implemented the shared HSR Shadow AO and inverted-blue
  specular helpers.
- [x] 2026-08-12: Updated HSR Body and Hair to use the fixed tutorial ramp
  coordinate.
- [x] 2026-08-12: Added the skin-gated Face Toon highlight and controllable
  FaceMap-blue nose
  line while retaining legacy serialized properties.
- [x] 2026-08-12: Added focused CPU/formula, shader-source-contract,
  shader-import, property-default, and recommended-profile regression coverage.
- [x] 2026-08-12: Executed the bounded HSR Unity validation; 10/10 focused tests
  passed after correcting one overly strict floating-point test expectation.
- [ ] Capture and review an on-character render comparison. Opening the Bronya
  validation scene was intentionally deferred after the interactive Unity
  Editor restarted during a broader adjacent-suite attempt.

## Discoveries

- With `HL = 0.5 * NdotL + 0.5`, `shadowAO = 2 * G`, and
  `dot(HL.xx, shadowAO.xx)`, the literal Body/Hair signal is
  `saturate(4 * HL * G)`, not a Toon-threshold shift.
- The tutorial ramp coordinate is fixed at `0.85 * signal + 0.15`; preserving
  the old configurable ramp offset in evaluation would not match that formula.
- LightMap blue controls a threshold after inversion. Metal and non-metal
  surfaces therefore need the same smooth Toon mask even though their final
  highlight colors and strengths can remain different.
- The Face workflow already has enough geometry and skin-mask inputs for a
  controllable highlight; requiring a Face LightMap would add an unnecessary
  texture contract.
- FaceMap blue remains the authored nose-line mask. Its former fixed, weak
  response needs independent strength and color controls around the tutorial
  `pow(NdotV, power)` term.

## Decision log

- **2026-08-12 — Use the literal tutorial Shadow AO formula.** Body and Hair
  compute `signal = saturate(dot(HL.xx, (2 * G).xx))`, equivalently
  `saturate(4 * HL * G)`, and sample the ramp at
  `U = 0.85 * signal + 0.15`. Older threshold-center, threshold-softness, and
  ramp-offset properties remain serialized compatibility surface but no longer
  drive these calculations.
- **2026-08-12 — Share one inverted-blue specular mask.** Compute Blinn-Phong,
  invert LightMap blue into the threshold, and smooth-cut it with bounded
  softness. Both metal and non-metal branches consume this mask.
- **2026-08-12 — Keep Face texture-neutral.** Face receives a skin-gated,
  parameterized Blinn-Phong Toon highlight from existing inputs. Rejected:
  adding LightMap to Face, because it would introduce a new binding expectation.
- **2026-08-12 — Preserve and strengthen the authored nose line.** FaceMap blue
  stays the mask. The response uses surface `NdotV`, a configurable exponent,
  strength, and color so the feature is visible and art-directable.
- **2026-08-12 — No interchange-version bump.** This is a fixed HSR shader
  behavior correction. MaterialIR, Bundle, fixed-workflow texture roles, and
  public C# schemas do not encode these equations. The existing single-pass
  layout remains; the tutorial's two-pass layout is not restored.

## Implementation sequence

1. Add finite-safe shared HSR helpers for the Shadow AO signal, fixed ramp U,
   inverted-blue specular mask, Face Toon highlight, and nose line.
2. Route Body and Hair through those helpers while retaining old property
   declarations for material deserialization.
3. Add Face highlight and nose-line controls, gate the highlight by the existing
   skin classification, and keep Face free of LightMap sampling.
4. Add focused formula and shader contract tests, then import and render the
   affected shaders in the bounded Unity lane.
5. Review the final diff for unrelated files, public/schema drift, deterministic
   output changes, and documentation accuracy.

## Validation

The focused HSR EditMode filter ran in Unity 6000.4.5f1 with URP and Shader
Graph 17.4.0. The suite proves:

- representative `HL/G` values match `saturate(4 * HL * G)` and ramp U equals
  `0.85 * signal + 0.15`;
- increasing LightMap blue lowers the inverted threshold and cannot introduce
  a separate metal-only continuous-mask path;
- Face highlight is disabled outside skin regions, remains finite at boundary
  inputs, and requires no Face LightMap property;
- FaceMap-blue nose-line coverage increases with strength and stays controlled
  by `pow(saturate(NdotV), power)`;
- HSR shaders import without Shader errors and expose safe visible defaults.

An actual on-character pixel/render comparison remains unexecuted; therefore
the implementation does not claim final artistic calibration across characters.

No test is recorded as passed until its command has actually completed. If a
running Unity GUI prevents safe package replacement, record validation as
blocked rather than copying canonical files into that project.

Executed documentation validation:

- `python -m unittest tests.test_public_docs -v` — passed, 8/8 tests.
- `python -m pytest tests/test_public_docs.py -q` — not executed because the
  active Python 3.13 environment does not have `pytest`; the equivalent standard
  library `unittest` command above completed successfully.

Executed Unity and repository validation:

- Focused `MikuHsrTutorialLightingTests` plus `MikuHsrBodyBackFaceTests` — first
  run passed 9/10. The only failure was a test expecting an exact `0.5` from a
  near-zero-width `smoothstep`; the assertion was corrected to evaluate the
  mathematical threshold center instead of a rounded decimal input.
- The same focused Unity filter after that test-only correction — passed 10/10,
  with 0 failures and 0 skipped tests. This includes Body, Hair, and Face shader
  import/error checks.
- `python tools/ci/run_checks.py --profile pr` — first run passed 265/268; the
  three failures correctly reported an out-of-date package asset-identity
  manifest after adding the new C# files.
- `python tools/miku_package_identity.py` followed by
  `python tools/miku_package_identity.py --check` — regenerated and verified
  the current canonical package manifest.
- `python tools/ci/run_checks.py --profile pr` after manifest regeneration —
  passed 268/268, including deterministic Blender-extension ZIP and Unity TGZ
  package builds.
- `git diff --check` — passed.

A broader adjacent Unity EditMode filter was attempted but is not counted as a
test result: the interactive Editor cleanly restarted from DX12 to DX11 before
the run produced a result file. The project recovered with its scene loaded and
no compile errors. Further scene switching and render capture were stopped to
avoid disturbing the user's interactive validation session.

## Results and follow-up

The shader implementation, focused Unity contracts, documentation, changelogs,
ADR, and package identity record are complete. Compatibility remains
Experimental on the existing Unity 6000.4.5f1 / URP 17.4.0 / Shader Graph
17.4.0 lane. There is no schema, texture-role, shader-name, or public C# API
change. On-character screenshot comparison and artistic calibration across
additional characters remain follow-up work.
