# MiGR Unity 2.0.1 — Persistent Identity Reuse and Premultiplied Transparency

## Purpose and outcome

Release Unity package 2.0.1 so a material exported to a different bundle/output
directory reuses the generated assets already owned by the same persistent
source/material identity. The importer must preserve Wrapper, Sub Graph, base
Material, Material Variant, Scene/Prefab references, and their GUIDs.

Transparent-emission surfaces must also generate a URP 17.4 Premultiply wrapper
and a base Material tagged `RenderType=Transparent`, with premultiplication
performed exactly once.

The on-machine acceptance materials are `魔法球10.001` and `魔法球6`.
Blender exporter code and Blender package version 2.0.0 are outside this change.

## Context and constraints

- Repository root is already heavily modified and contains many untracked plan
  and Unity-package files. Those changes belong to the user. This work must not
  reset, clean, reformat, or overwrite unrelated files.
- The supported target remains Unity 6000.4.5f1, URP 17.4.0, and Shader Graph
  17.4.0.
- Generated Sub Graphs, generated base Materials, identity files, and receipts
  are MiGR-owned. Wrapper graphs and Material Variants are user-owned unless
  Full Regeneration is explicitly requested.
- `MiGRImportRequest.outputRoot` is the preferred creation root for a new
  identity. A previously recorded persistent identity takes precedence even
  when its generated directory is outside that root.
- The bundle, MaterialIR, and generated identity schema versions remain 2.0,
  2.0, and 1.0 respectively.
- Global identity discovery must be bounded by directory count, identity-file
  count, file size, project-root containment, and reparse-point checks.
- Stable-GUID collisions must be detected before any generated asset write.
- Field absence in legacy wrapper contracts remains compatible with Alpha mode.

## Progress

- [x] 2026-07-29: Confirmed package 2.0.0, Unity 6000.4.5f1, URP/Shader Graph
  17.4.0, and captured the dirty-worktree baseline.
- [x] 2026-07-29: Confirmed `魔法球10.001` reaches Unity as
  `RefractiveGlass`; failure is stable-GUID ownership under a second output
  root, followed by `MIGR_MATERIAL_GUID_MISMATCH`.
- [x] 2026-07-29: Confirmed `魔法球6` is committed as
  `TransparentEmission`, while its wrapper has `m_AlphaMode=0` and its base
  Material has `RenderType=Opaque`.
- [x] 2026-07-29: Implement bounded Assets-wide identity reuse and deterministic duplicate
  detection.
- [x] 2026-07-29: Implement stable-GUID ownership preflight before
  generated-asset writes.
- [x] 2026-07-29: Propagate `blendMode`, map URP Alpha Mode, and derive Material
  RenderType.
- [x] 2026-07-29: Add and execute EditMode regression tests; final result was
  75/75 passed.
- [x] 2026-07-29: Update version/docs/changelogs and build a deterministic 2.0.1
  archive.
- [x] 2026-07-29: Back up the live Unity project, install 2.0.1, Full Regenerate the two
  acceptance materials, and verify receipts, GUIDs, compilation, Console, and
  visual output.

## Discoveries

- 2026-07-29: `ResolveMaterialIdentityLocation` only scans direct children of
  the requested output root. Stable GUID derivation ignores output root, so a
  second root creates files whose `.meta` files request GUIDs already owned by
  the original generated directory.
- 2026-07-29: Unity resolves the duplicate `.meta` GUID by assigning a different
  GUID to the new asset. `CreateOrUpdateMaterial` then reports
  `MIGR_MATERIAL_GUID_MISMATCH`, after the transaction has already written
  partial generated files.
- 2026-07-29: MaterialIR 2.0 records `blendMode=Premultiply` and
  `premultiplyCount=1` for `魔法球6`; `WrapperContract` drops the blend mode and
  `ApplyWrapperContract` hard-codes `m_AlphaMode=0`.
- 2026-07-29: `CreateOrUpdateMaterial` hard-codes every base Material to
  `RenderType=Opaque`.
- 2026-07-29: Unity's AssetDatabase can briefly retain a GUID-to-path entry
  after a test deletes the owning asset tree. A collision is real only when the
  reported asset, directory, or `.meta` still exists. The preflight now rejects
  live owners and ignores only verified nonexistent stale entries.
- 2026-07-29: Updating a local tarball in place changed PackageCache correctly,
  but the first hot reload caught two AssetImportWorkers while the new asmdef
  files were being extracted. A clean editor restart loaded 2.0.1; reflection
  confirmed `PackageVersion=2.0.1` before tests or material regeneration.
- 2026-07-29: The live `Assets` tree had already lost the prior 10.001
  directory, but transaction `61809ecdbe0b41ed431138543cc4d26d`
  retained a verified backup and its material-root `.meta`. Restoring that
  backup to its recorded path allowed the cross-root behavior to be validated
  against the original four GUIDs.

## Decision log

- 2026-07-29: Reuse the original generated directory when exactly one identity
  document under `Assets` owns the same persistent source/material pair.
  Moving assets was rejected because it would create unnecessary path changes
  and risk user-authored references.
- 2026-07-29: Treat more than one owner document as a hard duplicate failure.
  Timestamp, newest bundle, and directory proximity are not ownership evidence.
- 2026-07-29: Add a GUID ownership preflight using Unity's AssetDatabase before
  writes. Rewriting a colliding `.meta` and relying on Unity reassignment was
  rejected because it permits half-complete transactions.
- 2026-07-29: Use Shader Graph 17.4 Alpha Mode values Alpha=0,
  Premultiply=1, Additive=2, and Multiply=3. Missing blend mode remains Alpha;
  unknown explicit values fail.
- 2026-07-29: Derive Material RenderType from the wrapper render method:
  Opaque, TransparentCutout for Dithered, and Transparent for AlphaBlend or
  dielectric screen refraction.

## Implementation sequence

1. Extend material identity location metadata with whether it was reused
   outside the requested output root.
2. Add a deterministic, bounded, non-reparse recursive scan for generated
   identity documents under `Assets`; merge and deduplicate matches from the
   preferred root and global scan.
3. Resolve all primary generated paths and stable GUIDs, then preflight GUID
   ownership before directory creation or asset writes. Apply the same check to
   resource textures before writing them.
4. Add the reuse diagnostic and preserve recorded paths from the authoritative
   identity document.
5. Carry MaterialIR 2.0 `renderStatePlan.blendMode` into the wrapper contract,
   map it in the URP 17.4 target, and pass the render contract to base Material
   creation/update.
6. Add focused EditMode tests for cross-root reuse, global duplicate ownership,
   orphan GUID collision, user-wrapper preservation, Premultiply generation,
   legacy Alpha compatibility, and Material RenderType.
7. Update Unity package version/constants, package/root changelogs, README, and
   identity compatibility documentation.
8. Build the archive twice and compare hashes.
9. Back up live manifests, lock file, affected bundle inputs, wrappers,
   materials, identities, receipts, and `.meta` files. Install 2.0.1, wait for
   compilation, run EditMode tests, archive the obsolete 1.2.1 input outside
   `Assets`, and Full Regenerate the two materials.

## Validation

Repository validation:

    python tools/build_migr_unity_package.py
    python -m unittest tests.test_migr_package_identity
    python tools/migr_package_identity.py --check

Run the package EditMode tests through the connected Unity 6000.4.5f1 editor.
Expected result: all MiGR EditMode tests pass, including new identity and
transparency regressions. Rebuild the archive and compare SHA-256 hashes.

Live-project acceptance:

- `魔法球10.001` receipt is committed, shader compilation succeeds, and asset
  GUIDs remain Wrapper `32e3db1b0ac152eba464af38f0736613`, Sub Graph
  `331d47c276481e4bbf7db9e0aeb93608`, base Material
  `b58b47b81d3796c765dd0e050cf7c97b`, and Material Variant
  `fb2fa0461eff645609ebe792847816f7`.
- Its receipt includes `MIGR_SURFACE_MODEL_PRESERVED:RefractiveGlass` and
  `MIGR_OUTPUT_IDENTITY_REUSED_OUTSIDE_OUTPUT_ROOT`.
- `魔法球6` wrapper has `m_AlphaMode=1`, its base Material reports
  `RenderType=Transparent`, the receipt remains committed with
  `shaderCompiled=true`, and the Scene Sphere keeps its Material reference.
- The Console contains no new GUID collision,
  `MIGR_MATERIAL_GUID_MISMATCH`, MiGR import error, or Shader Graph compile
  error.
- Before/after captures use the same active SampleScene camera.

## Results and follow-up

Completed on 2026-07-29.

- `python -m unittest tests.test_migr_package_identity`: 11/11 passed.
- `python tools/migr_package_identity.py --check`: passed after regenerating the
  package content-hash manifest.
- `python tools/build_migr_unity_package.py`: two consecutive builds produced
  SHA-256
  `87914360d8a489ca88d7fae260e170d05d40511ffe3c9b589d6373353b54e8f6`.
- Unity EditMode assembly `MiGR.ShaderConverter.Editor.Tests`: 75/75 passed in
  69.18 seconds. The first run exposed stale AssetDatabase GUID mappings after
  test teardown; the production preflight was corrected and the complete suite
  then passed.
- Live backup:
  `Library/MiGR/Backups/migr-unity-2.0.1-20260729-171536`.
- Installed PackageCache:
  `com.migr.shaderconverter@335229695cf9`, package version 2.0.1, UPM
  fingerprint `335229695cf9392c6f9f14eaf9420036`.
- `魔法球10.001`: committed receipt, toolVersion 2.0.1,
  `shaderCompiled=true`, RefractiveGlass preserved, outside-output-root reuse
  diagnosed, and GUIDs retained:
  `32e3db1b0ac152eba464af38f0736613`,
  `331d47c276481e4bbf7db9e0aeb93608`,
  `b58b47b81d3796c765dd0e050cf7c97b`, and
  `fb2fa0461eff645609ebe792847816f7`.
- `魔法球6`: committed receipt, toolVersion 2.0.1,
  `shaderCompiled=true`, wrapper `m_AlphaMode=1`, base Material
  `RenderType=Transparent`, and the SampleScene Sphere retained Material Variant
  GUID `9345fd9a42b4ecaed780f4c7d60e356f`.
- Final forced imports reported no Shader Graph errors. After clearing the
  Console, final verification reported zero errors and zero warnings.
- Same-camera captures are stored at
  `Library/MiGR/VisualRegression/magic6-before-2.0.1.png` and
  `magic6-after-2.0.1.png`; a closer post-fix inspection is
  `magic6-after-2.0.1-close.png`.
- Compatibility remains Unity 6000.4.5f1, URP/Shader Graph 17.4.0, with Blender
  exporter 2.0.0. Bundle, MaterialIR, generated identity schema, and public
  shader-property names are unchanged. The only public behavior change is that
  `outputRoot` is a preferred location for new identities rather than an
  override for existing ownership.
