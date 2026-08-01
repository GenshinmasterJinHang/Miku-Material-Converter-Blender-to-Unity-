# Blender 5.2 EEVEE node support matrix

This document defines the project scope for Miku 1.0.1. It distinguishes
accepted input from visual parity: a node can be accepted and translated while
still requiring an explicit approximation or bake.

Evidence:

- Blender official manual branch: `blender-v5.2-release`, commit
  [`e74f0a2b`](https://projects.blender.org/blender/blender-manual/commit/e74f0a2b4c5475fe8bc50434d869b07ea7adfa4f).
- Official
  [EEVEE supported-node limitations](https://docs.blender.org/manual/en/5.2/render/eevee/limitations/nodes_support.html).
- Installed Blender: 5.2.0 LTS build `fbe6228777e7`.
- Target: Unity 6000.4.5f1, URP 17.4.0, Shader Graph 17.4.0.
- Automated smoke coverage: 16 non-volume surface closures and all 13 texture
  nodes present in the Blender 5.2 texture menu.

## Status language

| Status | Meaning |
| --- | --- |
| Supported — Exact | The represented function is equivalent for the documented inputs. |
| Supported — Equivalent | The target uses a different implementation with the same intended material meaning. |
| Supported — Approximate | The graph remains usable/editable, but a documented visual or physical difference is expected. |
| Supported — Baked | Blender evaluates the requested state into a generated texture resource. |
| Requires project setup | The generated graph is valid only after the reported URP/project option is enabled. |
| Deferred | Deliberately outside the current release scope. |
| Unsupported | No safe conversion is currently emitted. A required-chain occurrence blocks that material. |

## Output and scope rules

- The exporter resolves an active EEVEE-target Material Output first, then an
  active `ALL` output.
- A required surface connected only to a Cycles-target output is unsupported.
- Nodes outside the active output chain may produce a warning without blocking
  the material.
- The public Miku 1.0 contract contains EEVEE material semantics only.
- The isolated internal channel baker may temporarily select Cycles because
  Blender exposes material bake operators through that engine. It restores
  engine, selection, active object, materials, and generated temporary data.
- Auto, Prefer Native, and Reusable Bake Only are portable and never emit a
  mesh-bound Texture2D. Source Mesh Fidelity is required for Wireframe, Noise
  Color, non-Point Mapping, complex static closure projection, or another
  UV/topology-bound expression. Auto reports
  `MIKU_SOURCE_MESH_FIDELITY_REQUIRED` and never changes mode implicitly.

## Miku 1.0.1 corpus evidence

The Blender 5.2 parameterized audit covers 24 source files, 302 bound
materials, and 24 unbound `Dots Stroke` materials. Compiler-level routing
classifies the bound/unbound set as 26 Cycles-only, 60 native/equivalent, and
240 requiring explicit Source Mesh Fidelity. The audit stores only paths
relative to its runtime `--library-root`; the private corpus and generated
artifacts are not repository fixtures.

## Surface and BSDF nodes

| Blender 5.2 node | Miku 2.0 route | Status | Important limitations / possible problems |
| --- | --- | --- | --- |
| Add Shader | Per-channel closure composition | Supported — Equivalent/Approximate by topology | Principled plus Emission retains additive weights. URP Lit has no general arbitrary BSDF-lobe addition, so other closure combinations remain diagnosed approximations. |
| Mix Shader | Typed factor plus per-channel closure composition | Supported — Equivalent/Approximate by topology | Principled/Emission with a scalar texture factor preserves `0 = Principled`, `1 = Emission`, and continuous intermediate weights as an Emission Mask. General closure mixing is not a color lerp and remains a diagnosed approximation. |
| Diffuse BSDF | URP Lit diffuse surface | Supported — Equivalent | Blender documents Diffuse Roughness as Cycles-only; EEVEE uses Lambertian diffusion, so Miku does not invent Oren–Nayar behavior. |
| Emission | URP Emission block | Supported — Equivalent | Strength/color are editable. HDR exposure and bloom remain Unity project/render settings. |
| Glass BSDF | Transparent URP surface, Scene Color refraction, Fresnel/probe reflection | Supported — Approximate; requires project setup | Blender 5.2's unavailable compatibility Weight socket is ignored and transmission defaults to `1`. Enable URP Opaque Texture where the report requests it. One screen-space refraction sample cannot reproduce nested media, caustics, spectral effects, or full rough transmission. |
| Glossy BSDF / Anisotropic BSDF UI alias | Specular/smoothness surface | Supported — Approximate | EEVEE supports GGX/Multiscatter GGX but not anisotropy. Miku preserves color and roughness/smoothness intent; anisotropy and unsupported distributions are diagnosed. |
| Holdout | Alpha/coverage approximation | Supported — Approximate | Blender itself marks Holdout as partially supported in EEVEE. Unity camera background/compositor semantics are not identical. |
| Metallic BSDF | URP metallic/F0 surface | Supported — Approximate | Physical conductor F82 behavior is reduced to URP metallic/specular controls. Cycles-only anisotropy, rotation, and unsupported distributions are not reproduced. |
| Principled BSDF | Expanded semantic PBR surface | Supported — Exact / Equivalent / Approximate by socket | Base Color, Metallic, Roughness→Smoothness, Alpha, Emission, Normal, and common Coat/Transmission intent are editable. EEVEE-incomplete SSS, anisotropy, IOR, thin film, and transmission details are diagnosed approximations. |
| Refraction BSDF | Scene Color refraction without invented reflection lobe | Supported — Approximate; requires project setup | No reflection is added unless present in the source graph. Limited by URP screen-space availability and single-event refraction. |
| Specular BSDF | URP specular workflow | Supported — Equivalent / Approximate | The node is EEVEE-only. Base Color, Specular/F0, Roughness, Emission, Alpha, and Normal map cleanly; exact energy response may differ. |
| Subsurface Scattering | Lit color plus diagnosed thickness/back-light approximation | Supported — Approximate | URP has no general equivalent to Blender random-walk SSS. Radius, scale, IOR, anisotropy, and roughness cannot all be preserved physically. |
| Toon BSDF | Editable stepped-light/toon approximation | Supported — Approximate | Blender's 5.2 EEVEE manual lists Toon BSDF as unsupported. Miku accepts it to preserve authoring intent, but this is not a Blender EEVEE parity claim. |
| Translucent BSDF | Two-sided/reversed-normal back-light approximation | Supported — Approximate | Blender EEVEE itself does not diffuse light through the object; Unity lighting and shadowing still differ. |
| Transparent BSDF | URP transparent/alpha coverage | Supported — Equivalent / Approximate | White transparency maps cleanly. Blender documents colored/additive transparency as blend-mode dependent; those cases require review. |
| Sheen BSDF | Fresnel-tinted cloth/sheens approximation | Supported — Approximate | Blender's 5.2 EEVEE manual lists standalone Sheen BSDF as unsupported. URP has no identical sheen lobe. |
| Material Output — Surface | EEVEE-first active-chain selection | Supported — Equivalent | Multiple active outputs are resolved deterministically and reported. |
| Material Output — Displacement | `BUMP` → fragment Normal From Height; `DISPLACEMENT` → Object-space Vertex Position; `BOTH` → both | Supported — Equivalent / Requires project setup | Height uses LOD 0 in Vertex. The Displacement node must use Object space, an unlinked Normal input, and finite constant Midlevel/Scale. True displacement reports that sufficient mesh subdivision is required. |

The following shader families are outside the generic material converter:

| Node | Status | Reason |
| --- | --- | --- |
| Hair BSDF / Principled Hair BSDF | Unsupported | Blender documents them as unsupported in EEVEE; hair-specific scattering and geometry are outside this release. |
| Ray Portal BSDF | Unsupported | Cycles-only ray transport. |
| Background | Unsupported for material conversion | World/sky configuration is not a mesh material surface. Sky Texture on a material chain is handled separately below. |
| Shader to RGB | Unsupported in the generic PBR backend | EEVEE-only evaluated-lighting conversion requires a dedicated toon/runtime contract; it is not silently flattened. Existing game presets remain separate. |

## Texture nodes

Not every procedural texture requires baking.

| Blender 5.2 texture node | Default route | Status | Representation / limitations |
| --- | --- | --- | --- |
| Brick Texture | Channel bake | Supported — Baked | UV/mesh-dependent input becomes Texture2D; spatial input becomes Texture3D. Offset, squash, mortar smoothing, and bias have no verified exact Shader Graph equivalent. |
| Checker Texture | Expanded Shader Graph arithmetic | Supported — Exact | Uses the 3D floor/parity formula, including negative coordinates, colors, scale, Factor, and Color outputs. |
| Environment Texture | Direct direction sampling when supported; otherwise direction LUT | Supported — Equivalent / Baked | Equirectangular and Mirror Ball with supported interpolation remain editable. Unsupported projection/filter combinations use an HDR direction lookup. |
| Gabor Texture | Channel bake; editable runtime fallback where baking cannot represent a live input | Supported — Baked / Approximate | 2D/3D Gabor parameters are preserved in metadata. Unity has no semantically identical stock node. |
| Gradient Texture | Expanded Shader Graph arithmetic | Supported — Exact | Linear, Quadratic, Easing, Diagonal, Radial, Spherical, and Quadratic Sphere modes. |
| IES Texture | Linear HDR direction LUT | Supported — Baked | Blender's EEVEE manual lists IES Texture as unsupported. Miku accepts a readable internal text/external profile and converts the current profile to a lookup; a missing profile is an error. |
| Image Texture | Native Texture2D for supported static images | Supported — Exact | File-backed or packed PNG, JPEG, and EXR images with Flat projection, implicit active UV/UV0, Closest/Linear sampling, and Repeat/Extend wrapping are sealed directly. Explicit component/Alpha wiring can share one Linear physical resource across scalar PBR semantics; no filename layout inference is used. UDIM/tiled, sequence, movie, generated images, linked custom Vector inputs, Cubic/Smart filtering, Clip wrapping, and non-Flat projection fail explicitly. |
| Magic Texture | Channel bake | Supported — Baked | Turbulence depth, scale, distortion, Factor, and Color are evaluated by Blender. Spatial input may require Texture3D. |
| Noise Texture | Baked parity plus editable fBM approximation | Supported — Baked / Approximate | Native Shader Graph uses multi-axis Gradient Noise with at most five fBM octaves; it does not exactly match Blender detail/distortion. A linked 4D `W` remains a live diagnosed approximation. |
| Point Density | No Blender 5.2 node | Unsupported / unavailable | It is absent from the Blender 5.2 texture-node index and could not be constructed in the installed 5.2 build. No legacy 4.5 bridge is part of the 0.11 release scope. |
| Sky Texture | Linear HDR equirectangular direction LUT | Supported — Baked | Material-chain use only; it does not configure Unity's project skybox. Blender EEVEE does not support Nishita Sun Disc, so that feature cannot be claimed. |
| Voronoi Texture | Channel bake; editable stock-node fallback where required | Supported — Baked / Approximate | Dimensions, feature, distance metric, randomness, and requested output are retained. Shader Graph's Voronoi is not an exact Blender implementation. Linked 4D `W` remains a live approximation. |
| Wave Texture | Expanded native graph when undistorted; otherwise baked parity | Supported — Exact / Approximate / Baked | Bands/Rings, direction, profile, phase are editable. Distortion/detail/roughness use an approximation and/or baked branch. |
| White Noise Texture | Channel bake | Supported — Baked | 1D–4D Value/Color behavior is evaluated by Blender; no unrelated random function is substituted. Linked 4D `W` uses a diagnosed runtime approximation. |

### Bake and model binding

- A material with no generated bake/resource is exported without a model.
- If a generated Texture2D depends on object UVs/geometry, the exporter writes
  a GLB containing the bound material objects and records exact renderer slots.
- A purely spatial procedural may use Texture3D and can still require the model
  when the generated material binding must be reviewed on source geometry.
- Generated textures are snapshots, not runtime simulations of animated source
  nodes.

## Common supporting nodes

| Family | Status | Notes |
| --- | --- | --- |
| Value/RGB, Math, Vector Math | Supported — Exact/Equivalent for mapped operations | Invalid type, NaN/Infinity, or stage combinations fail validation. |
| Texture Coordinate / UV Map / Mapping | Supported — Exact/Equivalent/Approximate by mode | UV0/UV1, Object, World, View, Tangent, and Screen spaces remain distinct. Generated coordinates and unsupported instancer behavior are diagnosed. |
| Separate/Combine Color or XYZ | Supported — Equivalent | Explicit scalar/vector/color conversions are emitted. Separate Color/XYZ and Image Alpha may define deterministic packed Metalness, Roughness, Ambient Occlusion, Height, Alpha, and Emission Mask channel bindings. One physical packed image/property/sample is reused per resource, UV, and LOD. |
| Invert / Mix Color Multiply / legacy MixRGB Multiply | Supported — Equivalent for mapped routes | Invert remains an explicit One Minus expression. Multiply uses Blender's Factor semantics. AO topology is recognized from the connection to Base Color; no filename or pixel inspection is used. |
| Color Ramp | Supported — Expanded/Baked | Two-element Linear, Ease, and B-Spline ramps expand to native math; B-Spline uses replicated endpoints and cubic weights. Other verified static forms bake, while unsupported dynamic forms fail explicitly. |
| Hue/Saturation/Value | Supported — Exact | RGB/HSV conversion, centered and wrapped Hue, clamped Saturation, Value multiplication, and Factor mixing expand to native Shader Graph nodes. |
| Normal Map / Bump | Supported — Equivalent/Approximate/Baked by input | A Non-Color image through a Tangent Space Normal Map node is preserved with its Strength. OpenGL positive-Y is the default; DirectX negative-Y is an explicit per-material setting and becomes Unity green-channel flip. Bump preserves Height, Strength, Distance, Invert, and its base Normal through editable Normal From Height and normal blending; the finite-difference kernel difference is diagnosed. Dynamic Filter Width has no equivalent and requires a diagnosed bake or fails. |
| Geometry Incoming / Backfacing / View Direction | Supported — Equivalent | Incoming lowers to the world-space surface-to-current-camera direction. Backfacing lowers to `1 - IsFrontFace`; both remain runtime Fragment-stage values and are never UV-baked. |
| Camera Data | Supported — Equivalent | View Vector, absolute View Z Depth, and Euclidean View Distance expand to native Position(View) math; Fragment-only. |
| Miku Time v1 | Supported — Equivalent | Seconds, Frame, Sine, and Cosine remain dynamic. Unity time supports stable scale, offset, override, and override-enable properties. |
| Fresnel / Layer Weight | Supported — Equivalent | Physical dielectric Fresnel and distinct Layer Weight Fresnel/Facing outputs expand to native math; numerical oracle tolerance is `1e-4`. |
| Static expression islands | Supported — Baked | Maximal runtime-independent subgraphs inside a dynamic chain bake as deterministic UV0 Texture2D resources. Color uses linear EXR, scalar reads the linear R channel, and normals use tangent-space normal-map encoding. |
| Ambient Occlusion | Supported — Approximate | A linked scalar AO image is multiplied into Base Color through `_OcclusionStrength`, while the URP Occlusion output stays neutral to avoid double darkening. EEVEE `Only Local` is not supported and is diagnosed. |
| Bevel, Particle Info, any Light Path output, OSL Script | Unsupported on a required chain | Cycles-only or unavailable semantics; Light Path reports `MIKU_LIGHT_PATH_UNSUPPORTED:<socket>` with no bake or constant fallback. |

## Deferred volume nodes

| Blender 5.2 node | Status in 0.11.0 |
| --- | --- |
| Principled Volume | Deferred |
| Volume Absorption | Deferred |
| Volume Scatter | Deferred |
| Volume Coefficients | Deferred |

If a deferred volume node is connected only to Material Output Volume while a
valid surface is present, the surface can export with a clear warning. A
volume-only material does not produce a fake surface.

## Miku 2.0 closure composition boundary

Mix Shader uses symbolic clamped Blender socket-order weights `w*(1-f)` and
`w*f`. Add Shader copies the parent weight to both branches and does not
normalize by default. Independent lobe color, roughness, IOR, and weight are
not averaged into one Standard Lit surface.

Scalar transparency is premultiplied once. Colored transmittance uses Scene
Color and requires URP Opaque Texture. Custom multi-lobe lighting currently
omits screen-space ambient occlusion and reports
`MIKU_CUSTOM_LIGHTING_SSAO_UNAVAILABLE`; Strict rejects the material. A single
supported surface lobe's linked Normal is the global surface Normal. Multiple
active lobes with genuinely distinct Normal sources remain Unsupported until
the Unity backend can evaluate each independently.

Principled Coat supports the bounded URP 17.4 subset: Weight becomes Coat Mask,
Roughness becomes Coat Smoothness through `1 - roughness`, IOR must be the
constant `1.5`, Tint must be white, and Coat Normal must be unlinked/default.
Auto records `Urp17ClearCoat` as Approximate because the Blender and URP BRDFs
differ; Strict rejects it.

## Diagnostics and review

Every route records one of `Exact`, `Equivalent`, `Approximate`, `Baked`,
`RequiresProjectSetup`, `RequiresRuntimeSupport`, or `Unsupported`. The Unity
import report and generated mapping sidecar are the authoritative per-material
record. Human visual review remains required for every `Approximate`, `Baked`,
or project-setup-dependent material.
