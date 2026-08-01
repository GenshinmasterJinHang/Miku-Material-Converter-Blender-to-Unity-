# Architecture overview

The active system boundaries are maintained as Mermaid source in
[`system-boundaries.mmd`](system-boundaries.mmd). Generated diagrams are local
documentation artifacts and are not part of the source-of-truth tree.

The checked-in source has four ownership boundaries. Dependencies point from
Blender integration into the target-neutral core, from schemas into validation,
and from Unity integration into the Miku model. Core must not import `bpy` or
Unity types; the interchange format must not expose Shader Graph internals.

## Current implementation

`miku_blender` and the two Miku Blender extensions read Blender materials and
emit sealed, versioned Miku bundles. The target-neutral core validates and
normalizes MaterialIR without importing `bpy` or Unity types. Unity's Miku
ScriptedImporter selects the exact Shader Graph 17.4 URP backend and preserves
generated-versus-user-owned asset boundaries.

## Target architecture

The Unity boundary first selects an adapter using exact Unity, URP, and Shader
Graph versions. A supported adapter consumes normalized Miku, a Unity-created
wrapper template, and checked-in versioned fixtures. It owns all internal
MultiJson details and creates a Miku-owned generated subgraph plus a user-owned
wrapper. No adapter match returns a structured `Unsupported` result; it never
falls back to invented serialization.

See the subsystem documents and ADRs in this directory and `docs/adr/`.
