# ADR 0015: Bounded Unity and Blender technical-line compatibility

## Status

Accepted (2026-08-08). Supersedes ADR 0014 for Miku 2.2.12 and later.

## Context

ADR 0014 admitted every Blender 5.x, Unity 6000.x, and URP/Shader Graph 17.x.
It also allowed unknown Shader Graph minors to fall back to the newest known
adapter. That policy let UPM reject older Unity 6 projects because the package
manifest required Unity 6000.5 and URP 17.5.4, while runtime validation could
still accept unimplemented future serialization formats. Both behaviors were
unsafe: installation and runtime compatibility disagreed, and an unknown
internal Shader Graph format could be used before asset writes.

UPM package manifests provide minimum versions, not dependency ranges. A
single package therefore cannot encode the complete supported matrix in
`package.json`; runtime validation and reproducible editor tests must enforce
the upper bounds and matching technical lines.

## Decision

- The Unity package manifest declares `unity: 6000.0` and a minimum URP
  dependency of `17.0.0`. Projects directly lock the URP and Shader Graph
  versions supplied with their Editor.
- Unity 6000.N is paired with URP 17.N and Shader Graph 17.N for N=0..5.
  URP and Shader Graph must have the same exact package version. A mismatch
  fails with `MIKU_UNITY_PACKAGE_VERSION_MISMATCH` before any asset write.
- Stable Unity `f` and `p` patches inside those six technical lines are
  admitted. Unrecorded patches warn and run the full Shader Graph capability
  and template identity/import preflight. Alpha, Beta, RC, 6000.6+, 17.6+,
  and missing or malformed package versions are rejected.
- Shader Graph selection is explicit: `ShaderGraph17_0Adapter` through
  `ShaderGraph17_5Adapter`. Unknown minors are never clamped to another
  adapter. The existing StableId namespace remains unchanged.
- The five shared Standard, Clear Coat, Alpha Blend, Dithered, and Dielectric
  wrappers have fixed SHA-256 identities. Each admitted Editor must import
  those exact assets and pass the structured graph preflight for properties,
  nodes, ports, connections, Custom Function nodes, serialization, and every
  generated surface output before conversion begins.
- Blender is bounded to `>=5.0.0,<5.3.0` with explicit 5.0, 5.1, and 5.2
  capability adapters. Unrecorded in-range patches warn; a missing required
  `bpy` capability fails with `MIKU_BLENDER_CAPABILITY_MISSING`. Blender 5.3+
  is not guessed compatible.
- The warning-free target tuple is Blender 5.2.0, Unity 6000.5.7f1, and
  URP/Shader Graph 17.5.4. Compatibility is formally claimed only on Windows
  and only for rows with retained execution evidence.

## Consequences

- One TGZ can be installed by Unity 6000.0 through 6000.5 without forcing
  every project to URP 17.5.4.
- A new Unity or Shader Graph technical line requires a new adapter and actual
  validation instead of inheriting support from a major-version comparison.
- Miku 2.2.11 target profiles remain accepted by the 2.2.12 importer, while
  the 2.2.12 profile records the new certified Unity tuple and backend hashes.
- MaterialIR, Bundle, Bake Result, public shader property names, generated
  asset StableIds, and wrapper ownership rules do not change.
- macOS and Linux remain Unknown until their own reproducible validation
  records exist.
