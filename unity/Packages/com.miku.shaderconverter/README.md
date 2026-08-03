# Miku Shader Converter 2.2.8

This package imports deterministic `.mikubundle` artifacts produced by the
Miku Blender 2.2.8 exporter for Unity 6000.4.5f1, URP 17.4.0, and Shader Graph
17.4.0.

Blender's visible exporter creates `standard_pbr` only. The package also keeps
explicit legacy `workflow.kind` readers for `genshin_toon`, `wuwa_toon`,
`hsr_toon`, and `endfield_toon`, plus the package-owned game Toon shaders and
recipes. New game materials are created in Unity through
**Miku > Game Toon > Materials > Create Material**; the creator exposes the
filtered 22 parts and explicit public `Texture2D` fields documented in the
Manual.

Open `Miku/Settings` to select the Miku Editor UI language independently of
the Unity Editor. English and Simplified Chinese are stored per user in
`EditorPrefs` under `com.miku.shaderconverter.editorLanguage`; generated
assets, shader properties, diagnostics, JSON, and static menu paths remain
stable in English.

The importer continues to accept historical MiGR and time-dependent Bundles,
including the runtime Time Shader Graph contract. The 2.2.8 Blender exporter
does not create new Bundles with effective time dependencies.

MaterialIR 2.0 is the current export format. MaterialIR 1.0 remains a frozen
compatibility input for the four supported workflows. A Generic Toon bundle or
legacy Generic Toon shader is rejected before any asset transaction begins with
`MIKU_WORKFLOW_RETIRED:generic_toon`.

Installing 2.2.8 does not remove materials, recipes, or wrapper assets under a
user project's `Assets/`. Existing Generic Toon materials can therefore show
Missing Shader and require the manual migration described in
`docs/migrations/retire-generic-toon-2.0.md`.

Endfield 2.2 evaluates face and hair directions from each renderer's complete
object-to-world matrix. `_HeadCenterOS` is an object-space material value; no
bone, per-frame binder, or scene component is required. Legacy Endfield 2.1
texture roles remain accepted and are migrated with
`MIKU_ENDFIELD_ROLE_MIGRATED` diagnostics.

Endfield 2.2.1 corrects raw-red eye-shadow coverage, adds opaque overlay
coverage for brows and lashes, and introduces non-emissive iris/cornea, blush, skin
AO, face-SDF, and metal-response controls. Existing 2.2.0 materials retain their
saved properties; blush defaults to disabled until explicitly enabled.

Endfield 2.2.3 uses one shared directional Main Light contract for Body, Skin,
Face, Eye, Mouth, and Hair. Raw distance attenuation remains available as a
debug diagnostic but cannot erase a valid matching key light. Main Light RGB,
shadow visibility, direct diffuse, direct specular, and SH can be inspected
independently. Face, eye, hair, skin SSS, and metal/reflection fidelity repairs
add the `SpecularRefineF0` and `SpecularRefineColor` roles while preserving
MaterialIR 2.0 and all existing texture roles.

Endfield 2.2.4 adds opt-in scalar-red hair LUT interpretation, a bounded
Main-Light-aligned surface rim, independent metallic direct/environment
boosts, and warm-pale Skin/Face grading. Their compatibility defaults preserve
2.2.3 materials: LUT mode remains authored RGB, surface rim is disabled,
metal boosts are one, and skin grading is neutral.

Miku 2.2.5 extends warm-pale skin treatment to Genshin, HSR, and Wuwa with
authored texture masks. Body SSS requires the workflow's LightMap/IDMap; a
missing required map leaves SSS disabled and emits
`MIKU_SKIN_MASK_TEXTURE_MISSING`. Face-only color fallback is intentionally
bounded to the Face shader. Genshin Body, Face, and Hair also share a final
hue-preserving highlight shoulder; set `_HighlightCompression` to `0` to use
the legacy hard-clipped response.

Use **Miku > Game Toon > Materials > Apply Recommended Skin & Highlight
Profile** to opt in existing ordinary material assets. Imported
`generatedBaseMaterial` recipes migrate once to 2.2.7; their user-owned
material variants are never overwritten. The reusable profile at
`Runtime/Profiles/MikuAnimeGlobalVolumeProfile.asset` provides a shared,
reference-calibrated URP 17.4 grading stack: Neutral Tonemapping, White
Balance, Channel Mixer, Lift/Gamma/Gain, Shadows/Midtones/Highlights, Split
Toning, Color Curves, Color Adjustments, Bloom, and Vignette. Its color controls
remain neutral; a luminance master curve, Contrast `+16`, Saturation `+8`,
Exposure `+0.35`, and restrained white Bloom provide a brighter, clearer anime
image while preserving authored material colors. The package does not inject
it into scenes at runtime.

Miku 2.2.6 adds Wuwa fidelity controls. Eye binds one `_EyeHET` mask and
samples it twice for independently movable upper/lower HDR highlights;
`_EyeBaseEmissionStrength` controls the base eye separately from the retained
`_EmissionStrength` highlight control. Wuwa Face exposes object-space
`_FaceRight`, `_FaceUp`, and `_FaceForward` values transformed through the
renderer object matrix. Wuwa Body uses one authored linear ID texture for both
ordinary ID shading and opaque view-dependent sheer stockings. Its
`_BodyEmissionStrength` scales authored emission without changing the
texture-presence keyword rule. The 2.2.6 recommended profile uses restrained
eye highlights, a flat face response, and `0.15` Body MatCap strength.
Generated base recipes migrate once; user-owned material variants remain
untouched.

Miku 2.2.7 corrects the Wuwa Eye texture contract. `_EyeHET` is sampled once
as a direct linear emission mask; HDMF red supplies the primary highlight and
inverse alpha supplies the smooth pupil field. `T_Highlight_1` and
`BottomHighlight_1` use separate roles and importer-provided UV0 affine
transforms reconstructed from static Blender Point Mapping. Optional `_EyeEG`
uses Fresnel intensity and main-light motion. HDMF blue and BaseMap alpha are
available only in the Eye debug view. Existing 2.2.6 Eye recipes use the new
HET meaning immediately and display a re-import warning until their authored
textures and transforms are rebound.
