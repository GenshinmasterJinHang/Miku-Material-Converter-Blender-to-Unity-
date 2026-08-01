# URP crystal Shader Graph backend

The current optical adapter is locked to Unity Editor 6000.4.5f1, Universal RP
17.4.0, and Shader Graph 17.4.0. It generates a Miku-owned editable Sub Graph and
a transparent Unlit wrapper. The wrapper becomes user-owned after creation and
is preserved unless Full Regeneration is explicitly selected.

## Generated approximation

- one Scene Color sample offset by the dielectric normal, IOR, refraction
  strength, and thickness;
- Schlick Fresnel derived from IOR;
- Reflection Probe sampling for the reflection branch;
- `smoothness = 1 - roughness`;
- optional emission composed separately from coverage;
- Beer-Lambert RGB transmittance
  `exp(-((1 - absorptionColor.rgb) * density) * thickness)`;
- Principled Volume surface glow using
  `(1 - exp(-density * thickness)) * emissionColor * emissionStrength`;
- constant thickness or a Non-Color texture/channel/UV/meter-scale chain.
- front-face rendering for `SolidApproximation` and both-face rendering for
  explicitly authored `ThinSurface` materials.
- target-neutral `ImplicitGeometryNormal` resolution, including legacy
  constant-zero normal compatibility, without generating `normalize(0)`.

Absorption controls transmitted color, not alpha. The Unlit wrapper avoids
lighting already-lit Scene Color and Reflection Probe results a second time.
`_BaseColor`, `_EmissionColor`, and `_EmissionStrength` are identity-default
authoring controls. Base Color modulates the final optical color so it remains
visible when an editor preview has no usable Scene Color; the translated Cycles
tint remains on the transmission branch.

## Required project configuration

1. Select every URP asset used by the active Graphics and Quality levels and
   enable **Rendering > Opaque Texture**. Miku only reports this requirement; it
   never edits the asset.
2. Open **Project Settings > Player > Other Settings** and set **Color Space**
   to **Linear**. Reimport/restart if Unity requests it.
3. Add a Reflection Probe enclosing the crystal objects or provide the
   project's reviewed probe/cubemap environment. Bake or refresh it as required
   by the selected probe mode.
4. Keep transparent objects within expected render ordering. Scene Color sees
   the opaque texture, not nested transparent layers or transparent objects
   omitted from that texture.
5. Use closed meshes with outward normals for solid absorption. Preserve flat
   shading on faceted gems; do not recalculate split normals merely to satisfy
   the converter. Review non-uniform scale and thickness units.

Missing Opaque Texture or Linear color space changes report quality to
`RequiresProjectSetup`; the backend does not silently claim success or replace
Scene Color with black. See the
[reference-scene procedure](../testing/crystal-reference-scene.md).

## Reading generated assets

The generated files are the wrapper `.shadergraph`, Miku-owned
`.generated.shadersubgraph`, `.migrmap.json`, and `.migrreport.json`. The map
associates stable source identities with deterministic Shader Graph objects.
The report records project requirements, source features, optical parameters,
texture bindings, TranslationQuality, diagnostics, and known limitations.

## Adding another optical backend

Create an exact-version adapter keyed by Unity Editor, render pipeline, package,
Shader Graph, and verified template version. Inspect the installed package
source and real assets created by that editor. Keep Unity internal types, slot
IDs, and MultiJson serialization fields inside the adapter/bridge. Add template
compatibility, import, shader compilation, determinism, wrapper ownership,
missing-project-setting, and malformed-asset tests. Never select a nearby
backend version by guessing and never fall back to ShaderLab for new generic
optical support.
