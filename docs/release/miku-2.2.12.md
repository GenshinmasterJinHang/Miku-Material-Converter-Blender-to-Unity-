# Miku 2.2.12

Miku 2.2.12 restores installation on every supported Unity 6 technical line
without guessing compatibility with future Editors. It ships one Unity TGZ and
one Blender ZIP. The verified candidates are published manually as the public
GitHub Release `v2.2.12`.

## Release assets

- `miku_shader_converter-2.2.12.zip`
- `com.miku.shaderconverter-2.2.12.tgz`
- `SHA256SUMS.txt`

Final deterministic SHA-256 values:

- ZIP: `3344a2e7fc93e08412b6929511bdd26d814309cc5f2ce834864f63db1cb518c4`
- TGZ: `9d03d5b0cac5da6dbfa81e7f3b34f12a26de1fc226607353bb8fd173f2fe971d`

Install the ZIP through Blender **Extensions > Install from Disk** and the TGZ
through Unity Package Manager **Add package from tarball**.

## Compatibility

- Blender is bounded to 5.0-5.2. The recorded Windows matrix is 5.0.1, 5.1.2,
  and certified 5.2.0. Unrecorded in-range patches warn and run `bpy`
  capability preflight; Blender 5.3+ is rejected.
- Unity 6000.0 through 6000.5 uses explicit Shader Graph 17.0 through 17.5
  adapters. Unity 6000.N must use URP 17.N and Shader Graph 17.N, and the two
  package versions must be identical.
- Stable `f`/`p` patches are admitted after capability preflight. Alpha, Beta,
  RC, Unity 6000.6+, and package 17.6+ are rejected before asset writes.
- The warning-free target is Blender 5.2.0, Unity 6000.5.7f1, and URP/Shader
  Graph 17.5.4. Formal validation is Windows-only; see the compatibility matrix
  for actual execution status.

The six new Unity technical-line adapters were implemented against Unity's
official package manifest and Shader Graph 17.0-17.5 documentation. At the
request of the release owner, no additional Unity Editors were installed
locally; only the existing 6000.4.5f1 / 17.4.0 final-TGZ regression was
executed. The other exact Unity rows therefore remain Experimental rather than
being presented as runtime-validated support.

The package manifest now declares the install floors `unity: 6000.0` and URP
`17.0.0`. UPM has no dependency-range syntax, so each Unity project directly
locks its matching URP and Shader Graph versions.

## Safety and behavior

- Version/package mismatches fail with
  `MIKU_UNITY_PACKAGE_VERSION_MISMATCH` before any asset transaction.
- Shader Graph preflight covers properties, nodes, ports, connections, Custom
  Function nodes, serialization, all surface outputs, and the import/hash of
  Standard, Clear Coat, Alpha Blend, Dithered, and Dielectric wrappers.
- Unknown Shader Graph minors no longer clamp to the highest known adapter.
- Source Mesh PBR texture binding is derived from the exact generated runtime
  properties. Superseded source Height resources remain available for
  provenance but no longer require a nonexistent `_MIKU_HeightMap`; reachable
  Height retains strict missing-property failure.
- Stable generated IDs, public shader property references, user-owned wrapper
  behavior, and explicit Full Regeneration rules are unchanged.

## Schema and public API impact

MaterialIR, Bundle, Bake Request 1.2, Bake Result, conversion-plan, manifest,
source-map, and target-profile schema versions do not change. The target-profile
canonical hash changes for the 2.2.12 certified tuple and implementation
identities; the importer continues to accept the 2.2.11 profile. No public
Blender operator, C# request/result field, shader property reference, or
generated asset structure is renamed.

## Recorded validation

- Python/Core: 262 tests passed; Ruff and the release check profile passed.
- Blender 5.0.1, 5.1.2, and 5.2.0: the final ZIP installed and passed UI,
  Standard PBR, Bake Worker, TARGA conversion, stable identity, and
  determinism checks. All three produced normalized IR SHA-256
  `bf20e49c08b960ce8bd6945445850723c4d35ce990718c90f410a2a5a6da9c97`.
- Unity 6000.4.5f1 with URP/Shader Graph 17.4.0: the final TGZ completed 218
  EditMode tests, with 216 passed, zero failed, and two skipped, including the
  Source Mesh superseded/reachable Height regressions.
- Two independent release builds produced identical ZIP, TGZ, and
  `SHA256SUMS.txt` bytes.
