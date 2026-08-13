# Miku 3.0 WuWa Face SDF continuity and eye parallax

## Purpose and outcome

Remove two temporal artifacts from the Miku 3.0 WuWa renderer. Main-light yaw
must move the Face SDF through continuous threshold and mirror transitions, and
camera motion must parallax only the Eye's iris/pupil texture layers while its
surface highlights remain attached to the corneal surface.

## Context and constraints

- Canonical implementation is limited to
  `unity/Packages/com.miku.shaderconverter/`; documentation lives under
  `docs/` and the two changelogs. The worktree already contains user-owned 3.0
  edits, so this work must extend them without broad formatting or reverts.
- Validation targets Unity 6000.4.5f1 with URP and Shader Graph 17.4.0 on
  Windows Direct3D 12. GPU evidence must use `-force-d3d12` without
  `-nographics`.
- Private WuWa character models, textures, scenes, captures, and derived masks
  remain outside the repository and release archives. Validation uses clones
  and must not save the original scene or materials.
- The implementation preserves the animated head-basis repair, URP PBR/GI,
  SSS, Face SDF A/B channel roles, and existing Eye texture-role contracts.
- This repair is folded into the untagged 3.0.0 candidate. It adds one material
  property but does not change the package version, recipe/schema versions,
  texture roles, Shader names, or public C# APIs.

## Progress

- [x] 2026-08-13: Audited the current Face SDF selection, Eye UV ownership,
  profile defaults, diagnostics, tests, release evidence, and dirty worktree.
- [x] 2026-08-13: Accepted ADR 0020 and documented the implementation and
  migration contract.
- [x] 2026-08-13: Implemented continuous per-side Face masks and signed
  mirror crossfade.
- [x] 2026-08-13: Implemented layered tangent-space Eye parallax and the
  HDMF-aware recommendation.
- [x] 2026-08-13: Added CPU/EditMode, source-contract, and required D3D12
  continuity regressions.
- [x] 2026-08-13: Ran Python, Unity EditMode, D3D12, and deterministic-package
  validation and replaced the superseded release hashes.
- [ ] Run the optional private Phoebe clone visual check; no current private
  evidence is claimed by this repair.

## Discoveries

- The Face path used a hard `step` for the primary A channel and selected one
  mirrored UV orientation before evaluation. SDF softness therefore affected
  only B and could not remove the centre-line discontinuity.
- The caller already owns the unmirrored Face SDF sample. Reusing it and adding
  only the explicit `1-u` sample keeps the path at two texture reads.
- `sign(dot(light, left)) * uv.x` has three invalid edge behaviors for this
  contract: negative UV requires Repeat wrapping, the centre value becomes
  zero, and the selected image still changes discontinuously.
- Blending complete side masks preserves each orientation's authored threshold
  shape. Blending UVs collapses toward `u=0.5`; blending raw SDF values changes
  topology before thresholding.
- Eye currently applies one parallax UV to Base, HET, HDMF, authored highlights,
  and EG. The supplied behavior requires an internal-depth domain for iris and
  pupil layers and an unshifted surface domain for corneal highlights.

## Decision log

- 2026-08-13: Evaluate A and B continuously on each side, retain A as the gate,
  then crossfade the two final masks. Reject hard A, UV interpolation, and raw
  SDF interpolation.
- 2026-08-13: Add `_FaceSdfMirrorBlendWidth` with shader/profile default `0.10`.
  Width zero is the explicit hard-selection compatibility path.
- 2026-08-13: Use `surfaceUV` for eye-socket and surface-highlight layers and
  `irisUV` for Base/HET/HDMF. Correct the tangent-space sign convention instead
  of introducing a height texture or a second public parallax control.
- 2026-08-13: Keep `_EyeParallaxStrength` default zero. Recommended setup writes
  `0.02` only for materials with HDMF, and no existing material is migrated in
  place.
- 2026-08-13: Keep 3.0.0 and all interchange versions unchanged. The additive
  Face material property is documented without renaming an existing reference.

## Implementation sequence

1. Add the Face mirror-width property and constant-buffer binding. Evaluate
   unmirrored and `1-u` samples with one shared continuous A/B helper and blend
   their final masks from the signed head-right light component.
2. Mirror the same finite threshold, zero-width, and crossfade math in the CPU
   reference used by Editor tests and diagnostics. Add `0.10` to the WuWa Face
   recommendation.
3. Split Eye sampling into `surfaceUV` and tangent-space `irisUV`; route only
   Base/HET/HDMF through the latter. Apply `0.02` only when the recommendation
   sees a bound HDMF texture.
4. Add focused CPU/source-contract, material-profile, shader-compilation, and
   D3D12 tests. Extend the required GPU-test list rather than relying on the old
   opposed-yaw endpoint check.
5. Validate an isolated package and private scene clone, update this plan with
   actual results, then regenerate deterministic release manifests and hashes.

## Validation

- Run `python -m unittest discover -s tests -p "test_*.py"`; expect no failures
  and no source-contract assertion that retains hard A or sign-based UVs.
- Run `python tools/ci/run_checks.py --profile pr`; expect formatting, package
  identity, documentation, and Python checks to pass.
- Run `tools/ci/run_unity_editmode.ps1` with Unity 6000.4.5f1 and an exact TGZ.
  Threshold sweeps must be finite, bounded, monotonic, and include intermediate
  values. Mirror weights at `-width`, `0`, and `+width` must be `0`, `0.5`, and
  `1`; zero width must remain finite.
- Eye EditMode coverage must verify that positive tangent view decreases U,
  positive bitangent view increases V, invalid tangents do not move UV, HDMF
  recommendations use `0.02`, HET-only recommendations use zero, and surface
  highlight UV remains unchanged.
- Run `tools/ci/run_unity_dx12_gpu.ps1` with the exact TGZ. A synthetic
  asymmetric Face SDF must sweep main-light yaw from -7 to +7 degrees in
  one-degree increments: endpoints differ by at least 0.4, each adjacent
  maximum channel delta is below 0.25, and intermediate frames exist in both
  debug mode 5 and final color. Keep broader direction-response checks.
- D3D12 Eye acceptance must move a synthetic iris marker in the expected
  direction at strength `0.02` while a surface-highlight marker moves no more
  than one pixel. Assert `GraphicsDeviceType.Direct3D12` and never cite a
  `-nographics` run as graphics evidence.
- Build the 3.0.0 release twice into separate output directories. TGZ, ZIP,
  manifest, and checksum files must be byte-identical before replacing the
  superseded release-candidate hashes.
- Apply the recommendation only to cloned private materials. Capture the Face
  centre transition and Eye side views, then verify original scene/material
  hashes are unchanged and keep all private evidence outside Git.

## Results and follow-up

Implementation and public automated validation are complete. The canonical PR
profile passed 271 Python tests. The exact final TGZ ran 347 Unity EditMode
tests on Unity 6000.4.5f1 / URP and Shader Graph 17.4.0: 335 passed, zero
failed, and 12 environment-specific tests skipped. The same package then
passed all ten required Direct3D 12 tests with no skips or inconclusive results.

Two independent release builds were byte-identical. Final SHA-256 values are
`97fdc623d2f809c45151107302182d7089854276f86c9d731fabb87edfb77f47`
for the Unity TGZ and
`7920f27b6b85aaf55a1338e4e97189d823c62bba009fd9863d479ec298919a1f`
for the Blender extension ZIP. The private Phoebe clone visual check and the
Blender 5.2 executable suite were not run for this Unity-only repair and are
not claimed as evidence.

2026-08-13 release-integration correction: those hashes identify the completed
Face/Eye continuity candidate, not the published 3.0.0 package. The final
release TGZ is `ad5581c568a98a32733311af2ae0d3afb42e794ff289bd3d5c59e83e1a89c202`
and the final ZIP is
`dbec2ba1fe8cab625ca8749aeb2f028c3c3e1dcd63e808a5289e79d4ea6605bd`;
both received the broader release validation recorded in
`docs/plans/miku-3.0.0-github-release.md`.
