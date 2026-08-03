# Miku 2.2.0 release validation

Miku 2.2.0 repairs the Endfield fixed workflow without changing the public
shader names of any existing game workflow. Endfield face and hair directions
now derive from the complete renderer object-to-world matrix using local axes
Right `+X`, Forward `-Y`, and Up `+Z`. `_HeadCenterOS` stores the shared face and
head-hair center in mesh object space; tail hair can disable sphere normals with
`_UseHeadSphereNormal`.

Endfield 2.2 assigns explicit MaterialIR texture roles to the flattened 32^3
color LUT, face area/refine maps, and hair refine/shift/line maps. The 2.1 role
names remain accepted and produce `MIKU_ENDFIELD_ROLE_MIGRATED` diagnostics.
MaterialIR remains schema version 2.0.

Validated target tuple:

- Blender 5.2.0 at the fixed Windows installation path
- Unity 6000.4.5f1
- URP 17.4.0
- Shader Graph 17.4.0
- Windows D3D11

## Final validation

- `python -m pytest -q`: 228 passed.
- `python tools/ci/run_checks.py --profile pr`: passed, including schemas,
  identity boundary, deterministic extension/package builds, and 228 unit
  tests.
- Blender fixed-path version assertion: Blender 5.2.0 passed.
- Blender fixed-workflow texture smoke: passed.
- Unity EditMode full run: 122 passed, 1 environment-dependent fixture skipped,
  0 failed.
- Unity focused Game Toon run after final material changes: 9 passed, 0 failed.
- Unity Console after final render and tests: 0 errors.
- Visual evidence: final full-body and bust renders, albedo/packed debug views,
  six-view orbit sheet, and an eight-angle fixed-camera directional-light sweep.
- Two deterministic TGZ builds: 188 entries, equal manifests, equal SHA-256.
- Canonical versus installed PackageCache comparison: all 84 non-meta payload
  files except Unity's rewritten `package.json` matched by SHA-256; package
  metadata reports `com.miku.shaderconverter@2.2.0`.

The final archive hash is recorded in `miku-2.2.0-sha256.txt`.
