# Endfield 2.2.2 Main Light, Face SDF, Iris, and Metal Repair

## Purpose and outcome

Repair the remaining Endfield 2.2.1 regressions where Hair responds to the
main light but Body, Skin, Face, Eye, and Mouth collapse toward black; Face SDF
appears localized; the iris response is unclear; and authored metal regions do
not retain their base-map color. The 2.2.2 result preserves stylized shadows
without multiplying the final direct diffuse by system shadow attenuation.

## Context and constraints

- Canonical implementation is under
  `unity/Packages/com.miku.shaderconverter/`; the Unity validation project is a
  deployment target, not source of truth.
- The worktree contains substantial user-owned 2.x changes. Preserve unrelated
  edits and the immutable 2.2.1 package hash and material set.
- Exact target: Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0, Windows D3D11.
- The Face material is correctly bound to
  `T_actor_common_female_face_01_SDF.png`; do not replace it with another map.
- Iris and sclera are separate submeshes whose UV ranges overlap. `_EyeMode`
  remains the compatible per-material role selector.
- Do not create screenshots or a `Validation/2.2.2` directory. Visual
  acceptance belongs to the user.
- MaterialIR remains 2.0. Texture roles, public C# data structures, material
  slot order, and existing Shader properties remain compatible.

## Progress

- [x] 2026-08-02: Verified canonical roots, dirty worktree, Unity tuple, scene,
  material bindings, submesh UV ranges, packed material statistics, and current
  shader equations.
- [x] 2026-08-02: Locked 2.2.2 versioning, a 70% geometric Face fallback, and
  the compatible per-material Eye role with the user.
- [x] 2026-08-02: Implemented shared direct-light separation, Face fallback, Eye role paths,
  and visible bounded metal response.
- [x] 2026-08-02: Added regression tests, versions, documentation, identity,
  release notes, and the final SHA-256 record.
- [x] 2026-08-02: Passed Python, package, Unity EditMode, Shader, forced-D3D11
  finite, and deterministic-build validation.
- [x] 2026-08-02: Deployed 2.2.2, created `杰哥_2.2.2`, bound fourteen slots,
  audited the material state, and saved the scene.

## Discoveries

- Body, Skin, Face, Eye, and Mouth multiply final direct diffuse by
  `distanceAttenuation * shadowAttenuation`; Hair uses shadow attenuation only
  to choose its toon tone. This explains why Hair remains lit while the other
  parts collapse to black.
- Face `_SDFLightmap` is the correct 1024x1024 female Face SDF, not the eye
  shadow texture. The remaining failure is SDF dominance, not binding identity.
- Iris uses a full 0-1 UV range while sclera occupies a narrow central strip.
  Both lie inside the analytic circular mask in places, so UV radius cannot
  replace the material role safely.
- The cloth packed map has metallic R above 0.5 over about 22.3% of texels. Its
  metal AO is often useful but is exactly zero for about 6.4% of metal texels;
  direct and indirect specular currently multiply that zero.

## Decision log

- 2026-08-02: Publish 2.2.2 and create a separate `杰哥_2.2.2` material set.
- 2026-08-02: Keep `_UseFaceSDF` default and use
  `max(sdfLight, geometricLight * 0.7)` before author refine blending.
- 2026-08-02: Keep `_EyeMode` values 0/1. Iris consumes authored BaseMap RGB;
  sclera uses fixed warm white and never samples iris RGB for color.
- 2026-08-02: AO affects indirect occlusion, not direct light. Metal indirect
  specular uses `max(ao, metallic)` and receives a bounded base-color response.
- 2026-08-02: Generate no screenshot evidence; use tests, console checks,
  in-memory finite rendering, material audit, and user visual acceptance.

## Implementation sequence

1. Add direct-light, shadow-visibility, Face fallback, Eye role, metal AO, and
   metal-color helpers to the runtime include and the test math mirror.
2. Apply them to Body, Skin, Face, Eye, Mouth, and Hair accessory evaluation
   without changing ordinary Hair strand lighting.
3. Add focused EditMode regression coverage for zero-shadow direct color,
   Face fallback, role-separated Eye color, metal AO, and property compatibility.
4. Raise package and recipe version to 2.2.2; update release, compatibility,
   changelogs, README, package identity expectations, and provenance hashes.
5. Run focused checks, PR profile, full Unity EditMode, shader compilation, an
   in-memory forced-D3D11 finite scan, and two byte-identical package builds.
6. Deploy the final archive, clone fourteen 2.2.1 materials to 2.2.2, verify
   critical bindings/properties, rebind the renderer, and save the scene.

## Validation

- `python -m unittest tests.test_miku_package_identity tests.test_miku_fixed_workflows`
- `python tools/ci/run_checks.py --profile pr`
- Focused and full `Miku.ShaderConverter.Editor.Tests` EditMode runs.
- Two consecutive `python tools/build_miku_unity_package.py` builds with the
  same SHA-256.
- Forced-D3D11 in-memory ARGBFloat scan with no saved image and zero NaN/Infinity.
- Final MCP audit: fourteen `杰哥_2.2.2` slots, correct Face SDF, Eye modes 0/1,
  Body packed map enabled, debug views zero, saved scene, and zero console errors.

## Results and follow-up

Implementation and automated validation are complete.

- The focused Python identity/fixed-workflow run passed 17 tests. The PR
  profile passed 228 tests and rebuilt the Blender extension plus Unity TGZ.
- The complete Unity EditMode assembly ran 132 tests: 131 passed, zero failed,
  and one was intentionally skipped because
  `MIKU_103_REGRESSION_BUNDLE_ROOT` was not supplied. An earlier class-filtered
  MCP request failed to initialize across a domain reload; the subsequent full
  assembly run initialized normally and completed all applicable tests.
- All ten Endfield Shader assets were found and reported
  `ShaderUtil.ShaderHasError == false`. The final Unity Console contained zero
  error entries.
- Two consecutive canonical Unity package builds were byte-identical at
  353,971 bytes with SHA-256
  `ff6392732769513218325301f67ed2ff2e0320c600629aa9f5db7a69b00aac27`.
- A warm forced-D3D11 run rendered the saved scene to a transient 1024x1024
  ARGBFloat target. Its 4,194,304 channels contained zero NaN, zero Infinity,
  minimum 0, and maximum 1.10614276; its log contained no Shader, C# compile,
  or runtime-error matches. No image or Unity validation asset was written.
  The isolated temporary project was moved to the Recycle Bin afterward.
- Unity Package Manager reports `com.miku.shaderconverter@2.2.2` from the final
  local TGZ. Both `杰哥_2.2.1` and `杰哥_2.2.2` contain fourteen materials. The
  renderer's fourteen slots point to 2.2.2 in their original order; Face uses
  the female full-face SDF with `_UseFaceSDF=1`, Iris/Sclera use `_EyeMode=0/1`,
  Body enables its packed map, applicable debug views are zero, and the scene
  is saved with `isDirty=false`.

User visual acceptance remains intentionally outside automated artifact
generation. No screenshot or `Validation/2.2.2` directory was created.
