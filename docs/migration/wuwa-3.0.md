# Migrating WuWa materials to Miku 3.0

Miku 3.0 changes the default lighting semantics of every existing
`MIKU/Wuwa/Body`, `Hair`, `Face`, and `Eye` material. There is no legacy/new
lighting switch. Clone scenes and materials before comparing or adopting the
new result.

1. Keep the original scene and material directory unchanged.
2. Call `MikuWuwa3Migration.CloneAndUpgradeScene(source, destination,
   materialFolder)` from an editor tool, or use the supplied clone workflow.
3. Rebind WuWa `_N` files as `WuwaPackedNormalRoughnessMetallic` only when
   their channels are RG DirectX normal, B metallic, and A roughness. Ordinary
   normal maps remain `NormalMap` with `_NormalMapEncoding = 0`.
4. Bind Up/Down `_LD` to `OutlineColorMap`. Use UV3 for authored vertical
   grading only when that channel exists; missing selected UV sets are errors.
5. Enable `_AlphaClip` only for cutout parts such as Bangs and set `_Cutoff`.
6. Install the WuWa renderer features. The installer fixes the hair-shadow RT
   name, installs the Geometry Feature for the dedicated `MikuToonOutline`
   pass, and selects `WuwaTutorial` Screen Rim while remaining idempotent and
   rollback-safe.
7. Reapply the recommended profile to cloned materials when adopting the Face
   SDF continuity and Eye parallax correction. Face receives
   `_FaceSdfMirrorBlendWidth = 0.10`. Eye receives
   `_EyeParallaxStrength = 0.02` only when `_EyeHDMF` is bound; HET-only sclera
   remains at zero.
New public properties include `_NormalMapEncoding`, packed metallic/roughness
scales, PBR controls, `_AlphaClip`, `_Cutoff`, `_FaceShadowOffsetX/Y`, and
`_OutlineColorMap`, plus the additive `_FaceSdfMirrorBlendWidth`. Existing
property names remain serialized for migration.
The MaterialIR, Bundle 1.0, Manifest, Receipt, and Blender interchange schemas
do not change in 3.0.

The continuity correction never rewrites existing materials automatically.
Materials that already stored a non-zero `_EyeParallaxStrength` now use the
corrected tangent-space direction and move only Base/HET/HDMF; set it to zero
for an exact flat-eye path. Face width zero retains hard side selection, while
the 0.10 recommendation avoids a one-frame half-face replacement. No texture
role, public C# API, Shader name, recipe/schema version, or package version is
changed by this correction.

`Switch_D` textures are alternate `_BaseMap` presets; normal and Switch maps
must not be sampled simultaneously. `T_5XingStar_D` and its SDF/noise graph
remain unsupported and unbound; they are not public texture roles.
