# ADR 0014: Major-version-only version validation

## Status

Superseded by ADR 0015 (2026-08-08). This major-only policy applied to Miku
2.2.11 and is retained as historical context.

## Context

Miku 2.2.9 introduced strict closed ranges for the Blender extension
(5.0.0 through 5.2.0), the Unity editor (6000.0.0f1 through 6000.4.5f1), and
URP/Shader Graph (17.0.0 through 17.4.0). Versions above the certified upper
bound failed before any asset write, and in-range non-certified versions
emitted `MIKU_..._UNVALIDATED` warnings. The maintainer's requirement is that
only the major version must match (Blender 5.x, Unity 6.x / 6000.x, URP and
Shader Graph 17.x) and that the certified target moves to Unity 6000.5.4f1 and
URP/Shader Graph 17.5.4.

## Decision

Replace the closed-interval comparisons with a hard major-version gate plus a
soft unvalidated warning:

- **Blender** (`miku_blender/versioning.py`): any `5.x.y` is supported;
  `5.2.0` stays certified. `4.x` / `6.x` fail with
  `MIKU_BLENDER_VERSION_UNSUPPORTED`.
- **Unity editor / URP / Shader Graph** (`MikuRuntimeCompatibility`): any
  Unity `6000.x` and any package `17.x` is accepted; wrong-major or
  unparseable or missing versions fail with `MIKU_..._UNSUPPORTED` before any
  asset write. In-major versions not exactly equal to the certified tuple emit
  `MIKU_..._UNVALIDATED`.
- **Certified reference** moves to Blender `5.2.0`, Unity `6000.5.4f1`, and
  URP/Shader Graph `17.5.4`. The Unity package manifest minimum rises to
  `unity: 6000.5` with a URP dependency floor of `17.5.4`.
- **Shader Graph adapters**: `17_5` and `17_6` placeholder adapters are added;
  any higher `17.x` minor clamps to the highest-known adapter instead of
  throwing. The generated-asset StableId namespace
  (`miku-shadergraph-17.4:`) is intentionally unchanged so existing generated
  asset identities stay byte-stable.
- The bake-protocol Blender range and the extension manifest install gate
  (`blender_version_max`) relax to major-only for coherence.

## Consequences

- Blender 5.3+, Unity 6000.5.x, and URP/SG 17.5+ are accepted with a soft
  warning instead of a hard failure.
- Wrong-major versions (Blender 4.x/6.x, Unity 5.x/7.x, URP/SG 16.x/18.x) and
  missing or unparseable versions still fail before any asset write.
- The Unity package no longer installs below Unity 6.5 / URP 17.5 because the
  manifest minimum rose to `6000.5` / `17.5.4`.
- The 2.2.9/2.2.10 target-profile canonical hash remains accepted via
  `Package2210ProfileHash` so existing bundles keep importing.
- Versions accepted under a warning are not claimed as validated; only
  Blender 5.2.0 and the previously validated 6000.4.5f1/17.4.0 tuple are
  locally validated in this repository's environment.
