# WuWa tutorial rendering in Miku 3.0

Miku 3.0 makes the tutorial-derived PBR path the default for
`MIKU/Wuwa/Body`, `Hair`, `Face`, and `Eye`. It is a clean-room implementation
against the supplied behavioral description and URP 17.4 public shader
helpers. No game shader source is copied.

The shared base initializes URP `BRDFData`, applies the main light with
distance and shadow attenuation, samples lightmaps or light probes through
`SAMPLE_GI`, samples reflection probes, and evaluates `EnvironmentBRDF` with a
zero Fresnel term. `Effect` remains the tutorial-undefined transparent
emissive path.

Every lit Forward pass selects the URP Forward+ keyword for its Unity line:
`_FORWARD_PLUS` on Unity 6000.0 / URP 17.0 and `_CLUSTER_LIGHT_LOOP` on Unity
6000.1+. Face routes its directional SDF mask through final direct lighting;
the D3D12 acceptance lane verifies both the SDF debug mask and the normal final
color under opposed main-light yaw.

WuWa body `_N` textures can use `_NormalMapEncoding = 1`: RG is a DirectX
tangent normal (green is flipped for Unity), B is metallic, and A is
roughness. Import these files as uncompressed, linear RGBA data. ID red
suppresses metallic skin areas. `_NormalMapEncoding = 0` retains ordinary
Unity Normal Map decoding for cloned older materials.

The recommended profile selects UV3.y for vertical gradients, A/B Face SDF,
the red Hair HM tutorial highlight, metallic-only MatCap, vertex-color green
outline width, UV7 TangentSpaceV2 smooth outline normals, two-axis screen-space
hair shadow, and the WuWa tutorial Screen Rim mode. Bangs uses one `_Cutoff`
across visible, depth, depth-normal, shadow, outline, and hair-shadow passes.
WuWa outline passes use the dedicated `MikuToonOutline` LightMode; the WuWa
installer also installs the Geometry Renderer Feature that draws this pass.
Leaving a WuWa outline tagged `SRPDefaultUnlit` can replace the Forward PBR
result in URP Forward+ and is not a supported 3.0 configuration.

WuWa exposes two complementary edge-light paths. `Rim Brightness (Fresnel +
Screen)` and `Rim Tint (Fresnel + Screen)` feed both paths. `Fresnel Rim
Power` controls only the forward-pass grazing-angle band; lower values make
that band cover more of the surface. `Screen Rim Radius (Pixels)`, `Screen Rim
Depth Threshold`, and `Screen Rim Softness` control only the depth-based
Screen Rim Renderer Feature. They do not widen the forward Fresnel band. Eye
and Effect do not implement the shared outline or character-mask passes.

Use `MikuWuwa3Migration.CloneAndUpgradeScene` to create a new scene and new
material directory. The transaction never changes the source scene or source
materials and removes every created asset if it fails. See the
[3.0 migration guide](../migration/wuwa-3.0.md).

The Face material Inspector reports missing SDF input, zero SDF strength,
invalid basis/import settings, identical main/soft channels, identical
lit/shadow tints, and an active SDF debug view. These diagnostics are read-only:
they never reimport the texture, apply the recommended profile, or rewrite
authored material values. Identical SDF channels are valid and remain an
informational condition.

Face SDF debug modes 1-4 display raw texture channels; mode 5 displays the
computed directional mask used by final shading. Skin-ramp strength
interpolates from `_ShadowTint` to the authored ramp, so strength zero retains
visible SDF contrast instead of falling back to white. Realtime URP shadowing
is separate and opt-in through `_MainShadowInfluence` (default `0`). The
Inspector also warns when SDF softness exceeds `0.25` or skin SSS can fill the
shadow region; these warnings remain read-only.

Face SDF evaluates both horizontal texture orientations before selecting the
lit result. A and B each use the continuous softness band, with A retaining the
primary gate and B refining only the accepted region. The final masks, rather
than UVs or raw SDF values, crossfade as the main light passes the animated
head-right centre line. `_FaceSdfMirrorBlendWidth` controls that signed blend
region and defaults to `0.10`; set it to `0` only when hard legacy selection is
explicitly required. Mirroring uses `1-u`, so it does not depend on negative UV
wrapping or `sign(0)` behavior.

Eye uses two UV domains. Base, HET, and HDMF represent the recessed iris/pupil
layers and use tangent-space `irisUV`. The eye-socket shadow, authored
upper/lower highlights, and EG highlight use the original `surfaceUV`, so they
do not slide with pupil parallax. `_EyeParallaxStrength` retains shader default
`0`; applying the recommended profile sets `0.02` only when HDMF is bound and
leaves HET-only sclera materials at zero. An invalid tangent basis also produces
exactly zero offset.

Third-party game assets are not part of Miku. The package does not ship a local
validation builder, private character scene, or screenshot capture utility.
Keep all validation inputs and generated evidence outside the package and
public repository, and do not publish them without the necessary rights.
