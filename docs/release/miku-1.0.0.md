# Miku 1.0.0 release notes

Miku 1.0 establishes the new product and protocol identity and introduces a
material-driven Generic Toon workflow for Unity 6 URP.

Validated tuple:

- Blender 5.2.0
- Unity 6000.4.5f1
- URP 17.4.0
- Shader Graph 17.4.0
- Windows

Highlights:

- one deterministic `miku_shader_converter` Blender extension;
- `.mikubundle` and Miku 1.0 schemas;
- `com.miku.shaderconverter` and `Miku.ShaderConverter`;
- read-only MiGR 1.x/2.x data migration;
- eight fixed Generic Toon semantic shaders;
- embedded original-Material geometry outline and character-mask passes;
- opt-in RenderGraph Screen Rim Renderer Feature;
- Material Builder with Miku-owned base/recipe and user-owned derived Material;
- deterministic three-way recipe rebuild;
- explicit cloning smooth-normal and vertex-color Mesh tools.
- top-level `Miku > Generic Toon` Editor menus with dedicated smooth-normal,
  vertex-color, combined Mesh, and explicit Screen Rim installer windows.

The temporary 73-material corpus import/review-scene menu is not part of the
released Unity package.

The release intentionally provides no Model Root selection, Renderer scan,
Mesh checkbox list, material-slot tree, automatic Renderer reference
replacement, or character Prefab generation.

Code-level MiGR APIs are not retained. See
`docs/migration-to-miku-1.0.md` for asset and source migration.
