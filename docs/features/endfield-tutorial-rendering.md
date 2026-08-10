# Endfield tutorial rendering in Miku 2.3.0

Miku 2.3.0 adds an opt-in Endfield character-lighting path, a project-owned
full-screen LUT installation workflow, and a shared outline implementation for
the four Game Toon families. The package contains no game model, material,
texture, LUT, logo, or scene asset.

## Compatibility boundary

The tutorial contribution is enabled only while one active
`MikuEndfieldLightingController` owns the scene globals. With no controller,
existing Endfield lit materials retain the 2.2.12 lighting path. Existing
shader names and property references are preserved, and the MaterialIR,
Bundle, Blender/Miku IR, and JSON schemas are unchanged.

The controller publishes a continuous day strength, a top-light color and
direction, the top-light normal remap and Day 0/Day 1 strengths, the
camera-forward specular blend, and backlight compensation. If more than one
enabled controller exists, the lowest Unity instance ID is the only owner and
Miku reports `MIKU_ENDFIELD_LIGHTING_CONTROLLER_DUPLICATE`.

The shared lighting path supplies:

- continuous Day 0/Day 1 lighting, with top light in both states and no main
  directional-light contribution at Day 0;
- sigmoid-shaped character shadow visibility, three diffuse bands, NoF,
  AO/shadow composition, ramp-alpha distribution, and ramp-RGB hue influence;
- camera/light XZ backlight compensation with camera-pitch suppression;
- camera-forward stylized direct specular, finite-safe DFG environment response
  and multiple-scattering compensation;
- separate Fresnel and light-aligned rims; and
- legacy red-mask, RGB, and RGB multiplied by Base Map alpha emission modes.

Body is double-sided. Its final mapped TBN normal is flipped by the fragment
front-face semantic before any NoL, NoV, SH, GGX, SSS, or rim calculation.
Skin, Face, Eye, Hair, Mouth, Overlay, Effect, and HairShadow retain explicit
part/cull invariants. Overlay defaults to legacy unlit behavior. Its public
`_LightingMode` is `0=LegacyUnlit` and `1=ToonLitTransparent`; the scene
controller does not change this material property. After mode 1 selects the lit
path, controller availability selects its tutorial contribution versus the
compatible legacy Body-lighting contribution.

## Tutorial fidelity alignment

The tutorial path follows the published article formulas for the direct
specular, F0-refine lookup, face SDF, eye shading, and rim behavior:

- Body, Skin, and Face use the article's `D*V` specular response
  (`a2 / (NoH*(a2-1)+1)^2` times `0.5 / (2*NoV + r2 + eps)`, clamped to
  `[0, 20]`) with the Day-blended `selfAoShadowEffect * (ao_shadow_lowLight *
  0.5 + 0.5)` envelope. The generic GGX lobe remains the legacy path.
- The Specular Refine F0 texture is sampled with
  `u = lerp(D * roughness2, NoV^2, _RefineF0U_lerp)` and
  `v = 1 - roughness * (1 - AO)` while tutorial lighting is active; the
  historical `(NoV, roughness)` lookup is preserved for legacy materials.
- Face shadowing uses the article's width-scaled SDF smoothstep with the
  camera/light backlight compensation folded into the phase, and the diffuse
  ramp samples `lerp(sdfNoL, NoL, Refine-G)` instead of the geometric NoL.
- The eye shades with the key light projected onto the face plane and does
  not receive scene shadow in the tutorial path.
- Ramp RGB influence preserves luminance through the reference
  `rampColor_control` factor, and the NoF band applies `_NoFPowStrength`;
  Skin and Face keep NoF disabled by default, matching the reference.
- The face rim uses the article's start/end remap, diffuse-brdf lerp,
  `min(AO, shadow)` and the one-sided camera-half mask; face SSS uses the
  `NoV * 0.85 + 0.15` view remap.
- The main light desaturates toward its luminance and the top light whitens
  inside shaded bands, and Body diffuse energy uses
  `0.96 - 0.96 * metallic` while tutorial lighting is active.

New material properties are additive: `_NoFPowStrength`,
`_RefineF0U_lerp`, `_RimLightArea`, `_RimLightDiffuseColorEffect`, and hidden
`_FaceRoughness`/`_FaceReflectivity`. Skin/Face `_NoFStrength` default is
zero. Legacy materials keep their 2.2.x behavior.

## Full-screen game LUT

Open **Miku > Game Toon > Rendering > Endfield LUT & Volume Installer**. Select
the project's Universal Renderer Data and a project-owned 1024 by 32 Texture2D
that represents a flattened 32 by 32 by 32 LUT. Preview before applying.

The installer requires and configures:

- Default Texture type, sRGB sampling, Bilinear filtering, Clamp on every axis;
- no mipmaps, uncompressed import, and no incompatible Standalone override;
- one active URP `FullScreenPassRendererFeature` at
  `BeforeRenderingPostProcessing`, with color fetch enabled and no depth,
  normal, or motion requirement; and
- a project-owned profile containing only Neutral Tonemapping, Bloom
  (`0.85 / 0.20 / 0.65 / clamp 4 / high-quality filtering`) and Vignette
  (`0.04`).

Installation is idempotent and Undo-aware. A failed install restores importer
state and removes assets/features created by that attempt. It does not add a
URP `ColorLookup` Volume override. The shader grades normalized sRGB LUT
coordinates while restoring the original linear HDR peak, preserves alpha,
and uses an exact color bypass when intensity is zero so Bloom still receives
HDR highlights.

The generated profile is not assigned to a scene automatically. Assign it to
the intended global Volume explicitly, keep the target camera's HDR and post
processing enabled, and select the project's desired antialiasing mode. The
validated Endfield setup uses SMAA High and 1x MSAA.

## Outline TangentSpaceV2

New smooth-normal meshes store `float4(normalTS.xyz, 2.0)` in UV7/TEXCOORD7.
The marker distinguishes the skinned tangent-space contract from historical
unmarked object-space UV7. Runtime selection rejects non-finite, zero, invalid
tangent, and opposite-hemisphere values and safely uses the current geometric
normal instead.

Use **Miku > Game Toon > Mesh > Smooth Normal Generator** to create a new mesh
asset. The source mesh is never overwritten. Generation requires finite
positions, normals, and tangents; missing or degenerate tangents fail with
`MIKU_TOON_TANGENTS_REQUIRED` before UV7 is written. The default seam position
tolerance is `1e-6` Unity units. Area normals are aligned to source-normal
hemispheres before UV/submesh seam accumulation, and nearly opposite two-sided
vertices do not share a smoothed result.

All 13 Body/Hair/Face/Skin outline consumers use the shared selector and
clip-space expansion with screen-aspect correction. Outline passes use
`Cull Front`, `ZTest LEqual`, and `ZWrite Off`. The public
`Miku_ToonMask_v1` contract remains R=SSS, G=outline width, B=screen rim, and
A=face correction. Genshin and Endfield no longer reuse A and now read G for
outline width. Wuwa and HSR intentionally do not read G in their outline
passes, preserving their historical constant width-mask input; HSR also keeps
its historical constant screen-distance response.

## Validation boundary

The package-source implementation has passed 53/53 focused tests in a
source-linked isolated Unity 6000.4.5f1 / URP 17.4.0 project, including shader
compilation, lighting math, HairShadow diagnostics, the 13 outline consumers,
and existing Game Toon regressions. Python, Blender 5.2.0, final-ZIP installed
smoke, package identity, and two byte-identical TGZ builds also pass.

Those checks do not constitute a port-8080 scene or D3D11 visual acceptance.
Final-TGZ installation, full EditMode execution, target scene migration,
double-sided luminance, static/skinned outline measurements, LUT/Bloom images,
and retained rollback screenshots remain pending. See the completeness audit
for the current evidence ledger.

See [the TangentSpaceV2 migration note](../migrations/outline-tangent-space-v2.md)
and [the completeness audit](../audits/endfield-tutorial-completeness-2.3.0.md).

## Provenance

The package implementation is original Miku code. Its DFG fit follows the
public NVIDIA Streamline `EnvBRDFApprox2` reference, and the energy-compensation
design follows the Kulla--Conty multiple-scattering approach. Attribution and
the no-game-asset boundary are recorded in
[game workflow backend provenance](../provenance/game-workflow-backends.md) and
the repository third-party notices.
