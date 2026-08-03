# Generated asset ownership

Every generated file has an explicit role, owner, and regeneration policy.

| Asset | Owner | Default behavior |
| --- | --- | --- |
| Standard PBR `*.generated.shadersubgraph` | Miku | Replace atomically |
| Standard PBR Wrapper `.shadergraph` | User after initial creation | Preserve unless Full Regeneration is explicit |
| Fixed-workflow generated base `.mat` | Miku | Rebuild from exported textures and recipe selection |
| Fixed-workflow Material Variant `.mat` | User | Preserve GUID, parent, and user overrides |
| `MikuToonMaterialRecipe` | Miku | Update deterministically; no timestamps or absolute machine paths |
| Textures, identity, manifest, receipt | Miku | Replace atomically when the role is owned |
| Legacy Wrapper/Sub Graph | User | Never delete or overwrite automatically |
| Explicit Mesh-tool output `.asset` | User-selected output | Create a clone; never mutate the source Mesh |

Fixed-workflow rebuild is a three-way merge. A value follows the new source only
when the derived Material still equals the recipe's last synchronized value.
Reset Semantic Preset and Restore Source Values are separate explicit commands.

Ownership is determined by the recorded role and stable GUID, not by a filename
guess. Stable IDs derive from persistent source/material UUIDs plus the existing
role strings. A conflicting ownership claim or GUID fails before writes.

Fixed texture asset identity derives from the exported resource ID, not its
material role. Changing `miku_texture_role` does not change the Unity Texture
GUID, and one physical image may serve multiple compatible roles. The Recipe
owns role-to-Texture relationships; the user owns semantic/game-part choice.

Generators validate their output root and use atomic replacement for owned text
or binary data. Failure leaves the prior asset intact and emits a structured
diagnostic.

Miku does not own or rewrite Renderer assignments, scene objects, imported
source Meshes, or character Prefabs.
