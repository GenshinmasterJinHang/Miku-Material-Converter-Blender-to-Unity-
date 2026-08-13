# Genshin tutorial rendering in Miku 2.4.0 and 3.0.0

Miku 2.4.0 independently implements the observable material behavior described
by the user-supplied tutorial and the fixed local reference checkout
`PTSXDWD/URP_GenshinImpact@a258a9ef6e18bf45afbbf390a1dacf87f512f231`.
The reference repository declares no license, so Miku distributes none of its
Shader code, model, texture, material, scene, metadata, or screenshots.

## Rendering contract

Genshin Body and Hair use UV0 plus the imported Mikk tangent for optional
normal mapping. Smooth outline normals remain in marked UV7 TangentSpaceV2.
The main lighting path uses LightMap G AO, the tutorial half-Lambert/ramp
mapping, fixed LightMap A material thresholds, tutorial Blinn-Phong, and the
view-normal MetalMap lookup. Main-light color, distance attenuation, realtime
shadowing, SH, skin SSS, highlight compression, screen-depth rim, and the
head-bone Face SDF basis remain Miku behavior.

Miku 3.0.0 repairs this direct-light path under URP Forward+. Every Body and
Hair Forward/backface program and the Face Forward program compiles
`_FORWARD_PLUS` on Unity 6000.0 / URP 17.0 and `_CLUSTER_LIGHT_LOOP` on Unity
6000.1+. Without that variant, URP can expose zero per-object distance
attenuation and suppress both direct lighting and the Face SDF result. The Eye
shader remains intentionally unlit.

Realtime main-light shadows are applied after the authored toon-ramp lookup.
`_MainShadowInfluence` controls the continuous final-colour mix (Body `0.25`,
Hair `0.35`, Face `0` by default), so partial URP PCF visibility cannot jump
between unrelated ramp bands. Body/Hair shadow-smooth values are normalized
transition widths and are no longer internally reduced by `0.02`.

Metal follows the tutorial environment-map contract: LightMap R above `0.9`
selects metal, the view-space normal RG samples `_MetalMap`, and that sample
blends `_MetalMapColor` with base colour. Metal replaces diffuse and is
independent of main-light colour and shadow; `_MetalIntensity` remains an
artistic multiplier with a default of 1.

`_DiffuseA` is the stable serialized alpha mode: `0` None, `1` Cutout, and `2`
Diffuse Alpha Emission. Cutout coverage is shared by Forward, Backface,
Outline, ShadowCaster, DepthOnly, DepthNormals, and CharacterMask. Existing
`_EmissionMap` emission remains additive.

## Renderer setup

Install the features with **Miku > Game Toon > Rendering > Game Toon Renderer
Feature Installer**. Every active Universal Renderer Data must contain exactly
one `MikuGameToonGeometryRendererFeature`. It records two ordered RenderGraph
raster passes after opaques:

1. `MikuGenshinBackface`, which draws only materials with
   `_UseUv1Backface` enabled and reconstructs the UV1 TBN from derivatives.
2. `MikuToonOutline`, which draws the UV7 outline with vertex-color G width.

Missing setup is reported as
`MIKU_GENSHIN_GEOMETRY_RENDERER_FEATURE_REQUIRED:RequiresProjectSetup`.
Degenerate UV1 triangles fall back to a geometric tangent frame.

## Texture and mesh preparation

Required roles are Body Base/Light/ShadowRamp, Hair Base/Light/HairRamp, Face
Base/FaceSDF/ShadowRamp, and Eye Base. Normal, Metal, HairSpec, and Emission are
optional. The Genshin texture audit configures ramps as sRGB/Clamp/no-mips,
Face SDF as Linear/Repeat/no-mips, Light/Metal as Linear, Normal as Unity
NormalMap/Linear, and Base/Emission as sRGB without forcing a platform format.

The public Mesh clone API can map each output vertex-color channel from source
R/G/B/A or constants while generating UV7. It never changes the imported FBX.

## External validation policy

The package does not ship a Furina scene creator, private model/texture fixture,
or other local validation builder. Third-party validation inputs and generated
scenes remain outside the package and public repository.

Windows GPU acceptance must use `-force-d3d12` without `-nographics` and must
assert `GraphicsDeviceType.Direct3D12`. Headless/Null Device runs remain useful
for EditMode logic but are not GPU compatibility evidence. The automated D3D12
lane compares opposed main-light yaw for Body/Hair final pixels and for both
the Face SDF debug mask and debug-disabled final color.
