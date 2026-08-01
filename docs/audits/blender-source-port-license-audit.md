# Blender provenance and clean-room audit

## Decision

Miku 1.0 contains no Blender source code and does not depend on Blender source
headers, generated shader ports, or copied implementation structure. The root
repository is MIT-licensed after a per-file provenance review. Blender 5.2 is
invoked as an external executable through supported `bpy` APIs and black-box
results.

## Implementation boundary

- `miku` and `miku_blender` use public node/socket values, material data and
  renders only.
- `OpaqueSemanticRegion` keeps typed boundaries and a digest; Blender node
  identifiers are retained only in the editor-only SourceMap.
- Unity HLSL is centralized by target-neutral semantic contract. It is authored
  from public node specifications, public mathematical references, and binary
  oracle tests. No per-Blender-node source port is shipped.
- The three game preset ShaderLab adapters are isolated and separately reviewed;
  ordinary Miku conversion has no ShaderLab route.

## Release split

The normal repository and `com.miku.shaderconverter` are MIT. The Blender
Extensions distribution ZIP is packaged separately under GPL-3.0-or-later to
meet the official platform policy and carries both the GPL artifact license and
the root MIT notice. Private assets and old generated outputs are excluded.

Any future file with uncertain provenance is rejected from the MIT allowlist and
must be independently rewritten or omitted; a header change is never treated as
license conversion.
