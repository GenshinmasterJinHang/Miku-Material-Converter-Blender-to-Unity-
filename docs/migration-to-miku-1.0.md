# Migrating from MiGR to Miku 1.0

Miku 1.0 is an intentional code-level breaking change. There are no
compatibility aliases for the old Python imports, C# namespace, Unity package
ID, diagnostic prefix, Shader names, or Material property names.

## Before migrating

1. Commit or back up the Unity project and `.blend` sources.
2. Preserve all Unity `.meta` files.
3. Keep existing user-edited Wrapper/Sub Graphs and Materials.
4. Update package references from `com.migr.shaderconverter` to
   `com.miku.shaderconverter`.
5. Update source code to `Miku.ShaderConverter`, `Miku/*`, and `_MIKU_*`.

## Blender

Remove the retired extensions and install the single
`miku_shader_converter-1.0.0.zip`. On first access Miku copies recognized
`migr_*` custom properties to `miku_*`, preserving the UUID. Legacy properties
remain untouched; later writes use only Miku names.

New exports use `.mikubundle` and Miku 1.0 document kinds. Existing
`.migrbundle` directories remain valid read-only migration input in Unity.

## Unity persistent assets

Select Materials, AnimationClips, or folders in the Project window, then run:

1. **Miku > Migration > Dry Run Selected MiGR Assets**
2. Review the Console summary.
3. **Miku > Migration > Upgrade Selected MiGR Assets**

Apply migrates serialized Material properties, known legacy Shader references,
and AnimationClip Material curves. It uses Undo and never traverses a scene
hierarchy, changes a Renderer assignment, or generates a Prefab.

The importer validates a legacy bundle and its old canonical hash before
normalizing it in memory to Miku 1.0. Existing generated identities retain
their source/material UUID seeds and existing `.meta` GUIDs. New writes contain
Miku 1.0 identities only.

Existing Wrapper and Sub Graph assets are user assets after migration.
`generic_toon` no longer creates new graphs, but it also never deletes or
overwrites an old graph.

## Generic Toon material conversion

Use **Miku > Generic Toon > Material Builder**. Add Material assets
directly or use the current Project selection. Each row chooses the semantic,
output path/name, and Auto/Override/Solid Albedo policy.

Auto reads `_BaseMap`, `_MainTex`, `_BaseColor`, and `_Color` in deterministic
order. If two available values disagree, Miku stops instead of guessing;
choose Override or Solid explicitly.

The builder always creates a generated base Material, a user-owned derived
Material, and a recipe. It does not select a Model Root, scan Renderers, show a
Mesh/material-slot tree, replace Renderer references, or create a character
Prefab.

## Non-persistent runtime data

`MaterialPropertyBlock` values are runtime state and cannot be discovered or
migrated safely as assets. Update each caller to use the corresponding
`_MIKU_*` property.

## Deliberately unsupported

- renaming an old file without schema migration;
- automatic whole-project or whole-scene migration;
- automatic Renderer material replacement;
- resurrecting `Shader.Find("MiGR/...")` aliases;
- old Python/C# API shims.
