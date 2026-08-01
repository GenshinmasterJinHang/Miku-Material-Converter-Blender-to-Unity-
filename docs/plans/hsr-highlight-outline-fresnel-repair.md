# HSR Metal Highlight, Outline, and Fresnel Repair

## Purpose and outcome

Repair the maintained HSR preset so body metal regions follow the authored ILM
channels, Body/Hair/Face use a depth-independent Fresnel rim, and smooth-normal
outlines extrude along authored UV7 normals. The checked-in Bronya showcase is
the visual reference.

## Context and constraints

- Target versions: Blender 5.0.1, Unity 6000.4.5f1, URP 17.4.0, Shader Graph
  17.4.0.
- HSR remains on the repository's approved specialized ShaderLab compatibility
  backend. This change does not introduce another backend or copy HoyoToon code.
- The worktree already contains broad uncommitted work, including edits to the
  HSR extractor, material writer, shaders, tests, schema, docs, and generated
  reference materials. Preserve those edits and patch only the requested paths.
- Existing shader property reference names are public compatibility surfaces.
  Deprecated depth-rim fields remain readable and serialized but are not used by
  the new Fresnel calculation.
- MiGR-owned generated assets may be regenerated. User-modified editable shader
  copies must continue to be protected by template metadata/hash checks.

## Progress

- [x] 2026-07-22: Inspected repository structure, current diffs, target package
  versions, HSR ILM textures, existing tests, generated materials, and Bronya
  reference assets.
- [x] 2026-07-22: Verified the installed URP 17.4 shader APIs and confirmed the
  current project has depth textures enabled, although the repaired rim will not
  depend on them.
- [x] 2026-07-22: Added `hsr-toon-1.1`, legacy migration, range validation, and
  structured diagnostics.
- [x] 2026-07-22: Implemented tutorial-faithful metal specular and a pure,
  depth-independent Fresnel rim.
- [x] 2026-07-22: Replaced the outline path with UV7 smooth-normal world-space
  extrusion after the user explicitly rejected clip-space outline offsetting.
- [x] 2026-07-22: Added read-only UV7 validation, template migration, tests, and
  migration/compatibility documentation.
- [x] 2026-07-22: Regenerated only the six named HSR reference documents and
  verified a second regeneration preserves every SHA-256 hash.
- [x] 2026-07-22: Rebuilt the Blender 0.6.1/Unity 0.9.1 release candidates,
  completed targeted Python and Unity validation, and reviewed the new Bronya
  front, three-quarter, side, and depth-disabled captures.

## Discoveries

- Upper- and lower-body ILM alpha contain an explicit region around 132-133
  (`~0.52`). Their metallic-region blue channel is non-empty and suitable as the
  continuous metallic specular control.
- The current metal branch broadens the lobe and thresholds both material types;
  this diverges from the HSR ILM convention where non-metal is thresholded and
  metal remains continuous and base-color tinted.
- The current rim samples camera depth after a horizontal-only screen offset.
  Legacy MGIR files also carry old factory values that make this effect overly
  bright when the material writer treats every value as an authored override.
- Current outline meshes already carry smooth normals in UV7. The unstable part
  is the world-space distance compensation, not missing Bronya mesh data.
- Existing HSR tests are mainly source-contract checks; they did not test the
  numerical lighting behavior or semantic-version migration.

## Decision log

- 2026-07-22: Use `hsr-toon-1.1` inside the unchanged `mgir-preset-2.0`
  envelope. Missing semantic schema means legacy; a non-empty unknown value is
  an error.
- 2026-07-22: Migrate only an exact, order-independent match of the 0.6.0 factory
  control dictionary. Preserve all values for documents that contain any custom
  key/value.
- 2026-07-22: Remove depth, screen offset, main-light color, and distance fade
  from HSR rim lighting. Use only view-angle Fresnel, MiGR tint, and brightness.
- 2026-07-22: Keep `rimLightWidth`, `rimLightThreshold`, and
  `rimLightFadeout` as deprecated compatibility fields/properties.
- 2026-07-22: Superseded the initial clip-space decision at the user's request.
  Body/Hair/Face now extrude geometry in world space along UV7 smooth normals;
  `outlineDistanceScale` only blends natural perspective with reference-distance
  world-width compensation.
- 2026-07-22: HoyoToon is a behavior reference only. Its GPL-3.0 implementation
  is not copied, adapted, or added as a dependency.

## Implementation sequence

1. Add the v1.1 semantic schema, canonical defaults/requirements, exact legacy
   factory fingerprint, and structured migration diagnostics in Python and C#.
2. Update HSR shader helpers and Body/Hair/Face call sites for the corrected
   specular, pure Fresnel rim, and UV7 smooth-normal geometry extrusion.
3. Add read-only UV7 validation and importer diagnostics; bump Blender, Unity,
   preset, and template patch versions.
4. Extend semantic/schema/importer/shader tests, then migrate the six checked-in
   HSR MGIR reference documents and regenerate only their owned outputs.
5. Rebuild and capture the Bronya showcase after automated checks pass.

## Validation

- `python -m unittest tests.test_hsr_toon_preset tests.test_game_preset_outline_defaults -v`
- `python -m unittest discover -s tests -p "test_*.py"`
- Unity EditMode tests in Unity 6000.4.5f1, followed by a Console error check.
- Compile every HSR Body/Hair/Face keyword variant and assert no reference to
  `_CameraDepthTexture`, `SampleSceneDepth`, or `LoadSceneDepth` remains.
- Render Bronya front, three-quarter, and side views. Review metal region
  isolation, Fresnel-only edge response, UV7 outline continuity, seams, and
  natural perspective behavior before replacing goldens.

## Results and follow-up

Implementation is complete for the HSR scope.

- `python -m unittest tests.test_hsr_toon_preset -v`: 18/18 passed. This covers
  v1.1 schema/migration, metal and Fresnel numerics, depth-reference removal,
  UV7 world-space extrusion, and the 720p/1080p/1440p, 1.3m/3m/6m,
  26/50-degree projection-width matrix.
- HSR/exporter/editable-asset/release/add-on targeted suite: 96/96 passed.
- Unity 6000.4.5f1 EditMode HSR suite: 7/7 passed after the final C# edit,
  including Unity camera projection at every requested resolution, distance,
  and FOV. The full package suite run before the final additive semantic/test
  edits passed 29/29; the final focused rerun covers those changed HSR paths.
- Unity compiled `MGIR/HSR/Body`, `Hair`, `Face`, and `Eye` without shader
  messages. The HSR sources contain no `_CameraDepthTexture`,
  `SampleSceneDepth`, `LoadSceneDepth`, or `DeclareDepthTexture` references.
- Bronya front, three-quarter, and side captures were reviewed. Metal remains
  isolated to the authored region, the UV7 extrusion is continuous, and the
  Fresnel produces no internal depth silhouette. A front capture with depth
  disabled differed by at most 2/255 per channel; normal repeated depth-enabled
  captures differed by up to 3/255 because of render dithering.
- All six reference MiGR documents now contain `hsr-toon-1.1`. The four
  outline/rim consumers (UpperBody, LowerBody, Hair, and Face) regenerated their
  fixed HSR templates. Eye and eyebrow remain on their intentionally independent
  existing shaders; forcing their unrelated full generic graphs through the
  Shader Graph backend still reports pre-existing port-resolution errors, so no
  failed partial assets were retained.
- The deterministic release builder produced
  `b2u-blender-to-unity-0.6.1.zip` (SHA-256
  `f90609b75c3f7dfb9456849a55f5d81d343c629ba51a2c9f38ecb293edf74348`)
  and `com.migr.shaderconverter-0.9.1.tgz` (SHA-256
  `ccd01bb9bb934d1f6becf6257c00bc0c823578aa587a5fa25b00f54c88ddfc06`).
- The complete Python discovery command ran 317 tests: 264 passed, 19 skipped,
  4 failed, and 30 errored. All remaining failures are outside the HSR path and
  reproduce in focused runs: the current generic ShaderGraph/Dynamic/Toon
  compiler returns editable-asset metadata or a null `shaderSource`, while old
  tests still assert generated ShaderLab source and blocking exit codes. They
  were not changed as part of this scoped repair.

No further HSR work is required. Updating the unrelated generic compiler tests
or restoring their legacy ShaderLab backend is a separate compatibility task.
