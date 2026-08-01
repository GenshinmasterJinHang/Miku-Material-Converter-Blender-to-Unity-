# ADR 0009: Narrow ShaderLab Exception for Three Existing Game Presets

## Status

Approved for the current migration window.

## Decision

The Genshin, Wuthering Waves (Wuwa), and Honkai: Star Rail (HSR) preset
families may keep their existing ShaderLab providers only when the input is
`mgir-1`, `mgir-2`, or `mgir-3` and the import policy is
`LegacyCompatible`. The route is:

`LegacyCompatible` → `LegacyGamePresetDispatcher` → typed family backend.

`mgir-4.0/StrictExact`, generic Toon, Standard PBR and ordinary Node Graph
documents must never enter this route.

## Safety boundary

Family aliases are strict and canonicalized before dispatch. The dispatcher
validates schema/policy, emits a typed token, computes a canonical validation
digest, and verifies that digest before writing assets. Provider failure is an
error; there is no fallback to another family or to a generic ShaderLab graph.

## Compatibility and ownership

Existing legacy provider fixtures remain supported. Newly generated legacy
ShaderLab, material, mapping and report assets are MiGR-owned. A fourth family
requires a new ADR and a new backend. This exception does not reintroduce a
generic ShaderLab backend.
