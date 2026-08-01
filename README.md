# Miku 1.0

Miku converts Blender 5.2 materials into target-neutral semantic data and
imports them into Unity 6000.4.5f1, URP 17.4.0, and Shader Graph 17.4.0.
All newly written interchange documents use Miku 1.0 kinds and
`schemaVersion: "1.0"`; a complete export is rooted at a `.mikubundle`.

The supported source and package identities are:

- Python: `miku` and `miku_blender`
- Blender extension: `miku_shader_converter`
- Unity package: `com.miku.shaderconverter`
- C# namespace: `Miku.ShaderConverter`
- diagnostics and material properties: `MIKU_*` and `_MIKU_*`

## Blender workflow

Build or install the single GPL-3.0-or-later
`miku_shader_converter-1.0.1.zip`. It contains the semantic exporter, the
certified bake worker, the MIT core sources and their notices.

In Blender's Shader Editor:

1. Select the material in the active material slot.
2. Select its semantic workflow.
3. Choose an output directory.
4. Click **Export Current Material**.

The exporter writes one deterministic material directory containing a
`.mikubundle` and hashed sibling artifacts. Hidden `miku_*` source and material
IDs keep output identity stable across renames. On first access, matching
legacy `migr_*` custom properties are copied once without changing their UUID;
all subsequent writes use only Miku properties.

The exporter does not ask for a Model Root, scan Renderers, show a Mesh or
material-slot tree, replace object references, or create a character Prefab.

## Unity workflows

Install `unity/Packages/com.miku.shaderconverter` through Package Manager.
Copy a complete `.mikubundle` directory below `Assets/`.

Standard PBR and closure-aware surface workflows continue to generate
editable, version-specific Shader Graph assets. Generated Sub Graphs, base
materials, receipts, mappings, and recipes are Miku-owned. Wrapper graphs,
derived materials, and legacy wrapper graphs are user-owned and are never
silently deleted.

### Generic Toon Material Builder

Open **Miku > Generic Toon > Material Builder**. Each row operates on a
Material asset, not a Renderer or Mesh. A source Material may appear more than
once with different semantics:

- Face
- BodySkin
- Hair
- Eye
- Mouth
- Cloth
- MetalAccessory
- GenericOpaque

The builder always creates a Miku-owned base Material, a user-editable derived
Material, and a `MikuToonMaterialRecipe`. It never changes the source Material
or a Renderer reference. Albedo may be read deterministically from
`_BaseMap`/`_MainTex` and `_BaseColor`/`_Color`, explicitly overridden, or
replaced with a solid color. Conflicting source values are reported instead of
guessed.

Rebuild is a three-way merge: a generated value follows the source only while
the user value still equals the last synchronized value. **Reset Semantic
Preset** and **Restore Source Values** are separate explicit actions.

### Fixed Generic Toon Shader family

Generic Toon does not generate Shader Graph wrappers. It resolves missing
semantic information to `Miku/GenericToon/GenericOpaque` and uses these eight
package shaders:

- `Miku/GenericToon/Face`
- `Miku/GenericToon/BodySkin`
- `Miku/GenericToon/Hair`
- `Miku/GenericToon/Eye`
- `Miku/GenericToon/Mouth`
- `Miku/GenericToon/Cloth`
- `Miku/GenericToon/MetalAccessory`
- `Miku/GenericToon/GenericOpaque`

Every shader has the same property/CBUFFER contract and explicit
`UniversalForwardOnly`, `ShadowCaster`, `DepthOnly`, `DepthNormalsOnly`,
`MotionVectors`, `MikuToonOutline`, and `MikuToonCharacterMask` passes.
Geometry outline rendering always uses the original material's embedded
`MikuToonOutline` pass. UV7/TEXCOORD7 supplies smooth normals; missing or zero
data falls back to the object-space normal.

Face orientation uses the current Renderer's object space: local +X is right,
+Y is up and +Z is forward. Face center and extents are material properties;
there is no runtime head-bone binder.

Screen Rim is provided by the URP 17.4 RenderGraph
`MikuToonScreenRimRendererFeature`. Use
**Miku > Generic Toon > Rendering > Screen Rim Installer**. Select the
Renderer Data used by the target pipeline (for example `PC_Renderer.asset`),
review **Preview**, then choose **Apply**. The installer deduplicates and rolls
back failures; it never modifies every URP asset automatically. The Material
Builder and Generic Toon Shader GUI only open and prefill this installer.

### Explicit Mesh data tools

Use **Miku > Generic Toon > Mesh** to open the dedicated **Smooth Normal
Generator**, **Vertex Color Initializer**, or **Combined Mesh Data** window.
Each takes an explicitly selected source Mesh and output asset path. It clones
the Mesh and can:

- write deterministic, area-weighted smooth outline normals to UV7;
- initialize `Miku_ToonMask_v1` vertex colors;
- preserve, replace, or merge individual color channels.

The neutral vertex color is `(255,255,255,0)`: R is SSS, G outline width, B
screen rim and A face correction. The source Mesh, importer settings, topology,
SubMeshes, skinning, BlendShapes, Bounds, UVs and Renderer references are not
modified.

## Legacy data

`.migrbundle` and MiGR 1.x/2.x document kinds are read-only compatibility
inputs. They are validated before in-memory normalization to Miku 1.0. New
exports never write MiGR documents.

Use **Miku > Migration** in Unity for a Dry Run and then explicit
upgrade of selected persistent assets. The tool migrates serialized material
properties, Shader references, AnimationClip material curves and generated
asset identities. It does not traverse scene hierarchies. Runtime
`MaterialPropertyBlock` values are not persistent assets and callers must
switch those names to `_MIKU_*`.

See [the migration guide](docs/migration-to-miku-1.0.md) and
[compatibility matrix](docs/compatibility.md).

## Validation

The certified Windows validation tuple is:

- Blender 5.2.0 at
  `C:\SteamLibrary\steamapps\common\Blender\blender.exe`
- Unity 6000.4.5f1
- URP 17.4.0
- Shader Graph 17.4.0

Run:

```text
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 -m unittest discover -s tests -p "test_*.py"
```

The repository root and Unity package are MIT. The unified Blender extension
distribution is GPL-3.0-or-later and preserves per-file MIT and
GPL-2.0-or-later source notices.
