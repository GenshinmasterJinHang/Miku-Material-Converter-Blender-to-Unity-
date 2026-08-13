# Miku 3.0.0 Manual

Miku is a production-oriented Blender 5.x to Unity 6 material converter. The
public Blender front end exports Standard PBR semantics into target-neutral
MaterialIR 2.0. Unity imports editable Shader Graph assets and also ships four
first-party Game Toon Shader/HLSL preset families for explicit Unity-side
authoring.

This English manual is the canonical version. The
[Simplified Chinese manual](zh-CN/manual.md) mirrors its structure and
compatibility claims. See the [English README](../README.md) for the short
project overview.

![Miku workflow](images/miku-workflow-en.svg)

## 1. Supported environment

| Component | Version | Status |
| --- | --- | --- |
| Blender | 5.0-5.2 (certified: 5.2.0) | Supported on Windows |
| Unity Editor | 6000.0-6000.5 (validated: 6000.4.5f1) | Matching technical-line adapter |
| Universal Render Pipeline | 17.4.0 validated; matching 17.0-17.5 admitted | Required |
| Shader Graph | 17.4.0 validated; matching 17.0-17.5 admitted | Version-specific backend |
| Miku | 3.0.0 | Experimental |

Unity 6000.N requires URP 17.N and Shader Graph 17.N, where N is 0 through 5;
URP and Shader Graph must have exactly the same package version. Stable `f`/`p`
patches and Blender 5.0-5.2 patches outside the recorded matrix emit
unvalidated-version diagnostics and must pass capability preflight. Alpha,
beta, RC, Blender 5.3+, Unity 6000.6+, and package 17.6+ fail before asset
writes. This release is formally validated on Windows only.

## 2. Install from a Release

Download these files from the
[v3.0.0 GitHub Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v3.0.0):

- `miku_shader_converter-3.0.0.zip` — Blender 5.0-5.2 extension.
- `com.miku.shaderconverter-3.0.0.tgz` — single Unity 6000.0-6000.5 package.
- `SHA256SUMS.txt` — release integrity manifest.

Before installing, run `Get-FileHash -Algorithm SHA256` on the ZIP and TGZ and
compare both values with `SHA256SUMS.txt`.

In Blender, choose **Edit > Preferences > Extensions > Install from Disk** and
select the ZIP. In Unity, choose **Window > Package Manager > + > Add package
from tarball** and select the TGZ. Enable an in-range URP and Shader Graph 17.x
version before importing a bundle.

For source development, add
`unity/Packages/com.miku.shaderconverter/package.json` from disk. Do not patch
an installed Blender extension or validation-project copy. The canonical source
roots are `miku/`, `miku_blender/`, `extensions/miku_shader_converter/`, and
`unity/Packages/com.miku.shaderconverter/`.

## 3. Blender: export Standard PBR

1. Open a material in the Shader Editor and select an object whose active
   material slot contains that material.
2. Open the **Miku** sidebar and choose an output folder.
3. The visible route is **Standard PBR**. Set the normal convention and
   displacement policy only when the source needs them.
4. Expand **Advanced** for conversion mode, fidelity policy, additive shader
   energy policy, bake texture quality, or source identity forking.
5. Click **Export Current Material**.

![Blender Standard PBR panel](images/blender-standard-pbr-en.png)

The exporter snapshots the material, validates the lowered IR, and only then
creates staging and output files. A reachable time expression fails with
`MIKU_TIME_INPUT_UNSUPPORTED` before output or bake requests are written.
Disconnected time nodes are allowed. Use a static source or a separate runtime
implementation when animation is required.

The Blender UI no longer offers Game Toon workflow selection, texture-role
guessing, or the legacy identity migration entry point. Existing workflow
properties remain in `.blend` files for compatibility, and the lower-level
Python API still accepts explicit legacy workflows for scripts and fixtures.
The public current-material operator always emits `standard_pbr` without
rewriting old properties.

### Bake quality

Advanced **Bake Texture Quality** values are 512, 1024, 2048, and 4096; the
default is 1024. The setting has no effect when the conversion plan schedules
no bake. Baking uses isolated temporary data and does not modify the source
`.blend` file.

### Exported ownership

The output directory contains a deterministic `.mikubundle`, reports, and any
required baked resources. Move the complete directory into Unity as one unit.
Do not rename files inside the bundle or copy only its JSON document.

## 4. Unity: import an editable Bundle

Copy the complete `.mikubundle` directory under `Assets/`. The importer creates
the version-specific Shader Graph wrapper, Miku-owned generated Sub Graph,
materials, reports, and source mappings with stable IDs and deterministic
ordering.

Ownership is explicit:

- `*.generated.shadersubgraph` and generated reports are Miku-owned and may be
  regenerated.
- The wrapper `*.shadergraph` is user-owned after initial creation.
- A modified wrapper is replaced only after explicitly choosing full
  regeneration.

MaterialIR 2.0, Bundle 1.0, conversion-plan, bake-result, and public Shader
property/reference names remain stable in 3.0.0. Historical bundles, including
older runtime-time contracts, remain readable even though the current Blender
front end creates no new time-dependent bundle.

## 5. Bundled Game Toon Shader/HLSL presets

The Unity package includes original Miku Shader/HLSL implementations under its
four runtime preset families. They are not extracted game shaders and are more
than material-field templates.

| Preset | Parts | Authored features and texture families |
| --- | --- | --- |
| Genshin | Body, Hair, Face, Eye | Light Map and shadow/hair ramps, Face SDF, hair specular, eyes, outlines, and screen-rim integration |
| Honkai: Star Rail (HSR) | Body, Hair, Face, Eye | Light Map/ramp Toon shading, Face SDF, hair highlights, eyes, and outlines |
| Wuthering Waves (Wuwa) | Body, Hair, Face, Eye, Effect | ID/Stockings maps, face basis controls, Face ID/HET/SDF, Eye HET/HDMF/highlights/EG, MatCap, effects, and emission |
| Arknights: Endfield | Body, Skin, Hair, Face, Eye, Mouth, Overlay, Effect, HairShadow | Material parameters, diffuse/specular ramps, shadow/color LUTs, Face SDF, hair line/shift/refine maps, overlays, effects, and hair shadow |

These 22 valid material parts are **Experimental** compatibility presets. They
do not promise pixel-exact parity with any game and do not include game models,
textures, logos, extracted Shader source, or other game assets.

Miku 3.0.0 ships the independently implemented tutorial equations first
developed for the unpublished 2.4.0 candidate as the default Genshin core:
LightMap G AO, half-Lambert/ramp day-night sampling, LightMap
A material rows, tutorial Blinn-Phong, view-normal MatCap metal, and optional
Fresnel. Body and Hair use the imported Mikk tangent for optional `_NormalMap`
shading; UV7 TangentSpaceV2 remains outline-only. `_DiffuseA` is the stable
serialized alpha-mode field (`0` None, `1` Cutout, `2` Diffuse Alpha
Emission), and every color/depth/shadow/mask pass uses the same cutout rule.
The public `MikuGameToonGeometryRendererFeature` separately draws
`MikuGenshinBackface` then `MikuToonOutline`; `_UseUv1Backface` is opt-in per
material. See [the Genshin workflow guide](features/genshin-tutorial-rendering.md).

The HSR Body and Hair presets interpret LightMap green with the tutorial's
literal Shadow AO formula: `HL = 0.5 * NdotL + 0.5`, `shadowAO = 2 * G`, and
`signal = saturate(dot(HL.xx, shadowAO.xx))`, which is
`saturate(4 * HL * G)`. Their ramp U is fixed to
`0.85 * signal + 0.15`. LightMap blue is inverted into a smooth threshold for
one shared Blinn-Phong Toon-specular mask; metal and non-metal responses then
apply their own color and strength. Legacy threshold-center,
threshold-softness, and ramp-offset properties remain readable from old
materials but no longer drive these corrected equations.

HSR Face does not require a LightMap. It provides a parameterized,
skin-gated Blinn-Phong Toon highlight from existing inputs. FaceMap blue keeps
its nose-line meaning and is combined with surface `NdotV`, adjustable power,
strength, and color controls so an authored line can remain view-dependent
without becoming imperceptible. These are shader-level behavior/property
changes only: no MaterialIR, Bundle, schema, or texture-role contract changes.

The recommended HSR Face profile starts with `_FaceSpecularStrength = 0.12`,
`_FaceSpecularExponent = 32`, `_NoseLinePower = 3`, and
`_NoseLineStrength = 8`. Set **Face Debug** to `6` to preview only the computed
nose-line mask. If the mask is present but the final line is still subtle,
increase `_NoseLineStrength` or darken `_NoseLineColor`; if the mask disappears
too quickly as the view changes, reduce `_NoseLinePower`.

### Documentation render gallery

<table>
  <tr><th>Genshin — Hu Tao</th><th>Genshin — Furina</th></tr>
  <tr>
    <td><img src="images/preset-genshin-hu-tao.png" alt="Genshin preset render featuring Hu Tao"></td>
    <td><img src="images/preset-genshin-furina.png" alt="Genshin preset render featuring Furina"></td>
  </tr>
  <tr><th>Honkai: Star Rail — Bronya</th><th>Wuthering Waves — Phoebe</th></tr>
  <tr>
    <td><img src="images/preset-hsr-bronya.png" alt="HSR preset render featuring Bronya"></td>
    <td><img src="images/preset-wuwa-phoebe.png" alt="Wuwa preset render featuring Phoebe"></td>
  </tr>
  <tr><th colspan="2">Arknights: Endfield — 洁尔佩塔</th></tr>
  <tr><td colspan="2" align="center"><img src="images/preset-endfield-jierpeta.png" alt="Endfield preset render featuring 洁尔佩塔"></td></tr>
</table>

> **Non-commercial image notice:** The five character renders above are
> provided solely for non-commercial learning and documentation reference.
> Commercial use is prohibited. All related characters, designs, and
> intellectual property belong to their respective rights holders; Miku grants
> no rights to game assets. These PNGs are tracked with the source
> documentation and therefore appear in GitHub's automatic source archives,
> but they are excluded from Miku's MIT license and from the installable ZIP/TGZ.

## 6. Unity Game Toon material creator

Open **Miku > Game Toon > Materials > Create Material**.

1. Select `genshin_toon`, `hsr_toon`, `wuwa_toon`, or `endfield_toon`.
2. Select one part from the filtered list in section 5.
3. Assign the displayed `Texture2D` fields explicitly. Fields follow the
   selected package shader's public 2D texture declarations in source order.
4. Resolve every field marked **Required**, then click
   **Create User-owned Material** and choose a new `.mat` path under `Assets/`.

![Unity Game Toon material creator](images/unity-game-material-wizard-en.png)

`_MainTex` and `HideInInspector` compatibility properties are excluded.
`_BaseMap` is required for every part except Endfield Mouth. Wuwa Body exposes
one **ID / Stockings Map** field and binds that texture to both `_IDMap` and
`_StockingsMap`.

Before creating a Unity object, the wizard validates the shader, output path,
existing asset, field count, required textures, and property names. It then
binds textures in memory, applies the recommended profile, synchronizes
keywords, and creates a user-owned `.mat`. It never guesses filenames, searches
folders, changes TextureImporter settings, overwrites a material, binds a
Renderer, or modifies an FBX or Prefab.

The public three-argument `CreateMaterialAsset(string, string,
MikuGameMaterialPart)` API remains available for scripts that deliberately need
an empty user-owned template. The visible creator uses the validated configured
creation route.

## 7. Unity Editor tools

### 7.1 Miku settings

Open **Miku > Settings** to open Unity User Preferences at **Preferences/Miku**.
Choose English or Simplified Chinese. This is a per-user `EditorPrefs` value;
it does not alter project files or generated assets. See section 8 for the
localization boundary.

### 7.2 Recommended skin and highlight profile

Select one or more Material assets, then choose
`Miku > Game Toon > Materials > Apply Recommended Skin & Highlight Profile`.
Confirm the dialog to update
only properties supported by each selected Miku preset shader, synchronize its
keywords, and save the materials. The operation supports Undo and never changes
FBX or Prefab assets. A missing required skin mask produces
`MIKU_SKIN_MASK_TEXTURE_MISSING` and disables unsafe whole-surface skin tuning.

### 7.3 Endfield texture import audit

Open **Miku > Game Toon > Textures > Import Audit**, assign an `Assets/` folder,
then click **Apply Recognized Import Settings**. Despite its audit name, this is
a mutating tool: it recognizes complete Endfield filename patterns, adjusts
TextureImporter color space, wrap mode, mipmaps, and type, then reimports
changed textures. Ambiguous `_M` names are left unchanged. It writes the before/
after report to
`Assets/Miku/Reports/endfield-texture-import-audit.json`.

Commit or back up importer metadata before applying it. Review the JSON report
afterward and use source control or Unity Undo if a recognized profile was not
intended.

### 7.4 Smooth Normal Generator

Select a Mesh asset and open
`Miku > Game Toon > Mesh > Smooth Normal Generator`. Confirm the explicit
source Mesh and output folder (default
`Assets/Miku/ToonMeshes`), then set position tolerance, smoothing angle, and
whether bone-weight signatures separate otherwise coincident vertices.

The generated smooth outline normal is written as marked tangent-space
`float4(normalTS.xyz, 2.0)` data in UV7/TEXCOORD7 on a cloned Mesh asset. This
2.3.0 contract deforms with a SkinnedMeshRenderer; unmarked historical
object-space UV7 remains readable. If the source already has UV7, **Preserve**
blocks a normals-only write; choose **Replace** and confirm to replace UV7 only
on the clone. Missing or invalid tangents report
`MIKU_TOON_TANGENTS_REQUIRED` before any UV7 write. The source Mesh,
Texture/Mesh importer, and all Renderer references remain untouched, including
for a non-CPU-readable imported Mesh. See the
[migration note](migrations/outline-tangent-space-v2.md).

### 7.5 Game Toon Renderer Feature Installer

Open **Miku > Game Toon > Rendering > Game Toon Renderer Feature Installer**.
Preview reports Geometry and Screen Rim state. Apply enumerates every active
Universal Renderer Data asset, deduplicates it, and installs one
`MikuGameToonGeometryRendererFeature` plus one
`MikuToonScreenRimRendererFeature` as a single idempotent Undo transaction.
Duplicate or invalid feature state fails before success is reported.
The former **Screen Rim Installer** alias is no longer registered.

### 7.6 Rebuild Anime Global Volume Profile

Choose
`Miku > Game Toon > Rendering > Rebuild Anime Global Volume Profile` only to
restore Miku's package-owned reference profile. The command deletes and
recreates
`Packages/com.miku.shaderconverter/Runtime/Profiles/MikuAnimeGlobalVolumeProfile.asset`
with Miku's Neutral tonemapping, color, Bloom, and Vignette defaults, then
selects it. It is destructive to edits made directly to that package asset and
has no preview. Keep custom grades in a separate user-owned Volume Profile; if
an immutable installed package rejects the rebuild, reinstall the package.

### 7.7 Endfield Volume grading, optional LUT, and tutorial lighting

Open **Miku > Game Toon > Rendering > Endfield LUT & Volume Installer**. The
default Volume-only path creates Color Adjustments (`+0.35` Exposure, `+16`
Contrast, `+8` Saturation), identity Color Curves, Neutral Tonemapping, Bloom,
and Vignette. It needs no LUT. With Renderer Data selected it removes an old
Miku Endfield screen-LUT feature without touching unrelated features.

The extracted cloth and female-skin LUTs are material dark-color maps, not
screen grades. The advanced explicit screen-LUT path remains available for a
genuine project-owned flattened 32-cube, but rejects material LUT evidence
before writing. Both paths support Undo and rollback and do not assign the
generated profile to a scene automatically.

The installer fails closed on corrupt or duplicate Renderer Feature/local-ID state and only
reports success after a forced reimport proves the Feature reference,
configuration, and pass material persisted.

Add exactly one `MikuEndfieldLightingController` to a scene to enable the 2.3.0
Endfield tutorial contribution. Without it, old lit materials retain legacy
lighting. Overlay independently defaults to `_LightingMode=0` (Legacy Unlit);
set `_LightingMode=1` to choose Toon Lit Transparent before the controller can
contribute tutorial lighting to that material.
Setup, defaults, the double-sided Body contract, part-specific behavior, and
validation requirements are in the
[Endfield tutorial rendering guide](features/endfield-tutorial-rendering.md).

### 7.8 Miku Material Inspector

Select a material using a `MIKU/Genshin/`, `MIKU/HSR/`, `MIKU/Wuwa/`, or
`MIKU/Endfield/` shader. The custom Inspector displays the shader's public
properties, synchronizes texture keywords, exposes supported Wuwa/Endfield
debug views, shows Screen Rim installation status, and—when a companion recipe
exists—offers a filtered material-part selector. Changes affect the selected
material; use Undo and review debug views before returning them to **Final**.

### 7.9 Mesh Binding Description Inspector

Select a generated `MikuMeshBindingDescription`, then select a GameObject with
a `MeshRenderer` and `MeshFilter` whose Mesh fingerprint matches the recorded
binding. Click **Apply to Selected Renderer** to assign the recorded material to
the specified slots. A mismatch reports `MIKU_MESH_BINDING_MISMATCH` and makes
no assignment. The successful operation records Renderer Undo; use the
generated Prefab when possible.

### 7.10 Toon Material Recipe Inspector

A `MikuToonMaterialRecipe` records the generated base material, user material,
workflow, part, texture bindings and UV transforms, stable identities, and
Shader family version. Treat GUID and version fields as synchronization
metadata, not ordinary controls. To change a recipe-backed material part, use
the Miku Material Inspector's part selector so the shader, bindings, recommended
profile, and recipe stay synchronized. Raw Recipe Inspector edits alone do not
apply a material regeneration.

### 7.11 Legacy migration tools

For historical MiGR assets only, first select explicit assets or folders and
run **Miku > Migration > Dry Run Selected MiGR Assets**. Review the logged
material, animation-curve, and generated-metadata counts. After committing or
backing up the project, run **Miku > Migration > Upgrade Selected MiGR Assets**
to apply the property and metadata-name migration.

The migration does not traverse scene objects or change Renderer assignments.
It rejects retired Generic Toon materials instead of silently substituting a
different shader. Normal 2.3.0 authoring does not require these commands.

## 8. Editor languages

Blender follows Blender's English/Simplified Chinese interface translation.
Unity's **Miku > Settings** preference is stored for the current user at
`com.miku.shaderconverter.editorLanguage`.

The preference does not change Unity's global language, project files,
generated assets, stable property names, diagnostics, JSON, or static menu
paths. Miku-authored windows, custom inspectors, ShaderGUI labels, dialogs,
help boxes, Undo labels, and friendly status messages follow the preference.

## 9. Diagnostics and troubleshooting

- `MIKU_TIME_INPUT_UNSUPPORTED`: remove a reachable time dependency from the
  Blender output chain or keep the historical bundle on Unity for compatibility.
- `MIKU_WORKFLOW_RETIRED:generic_toon`: export Standard PBR from Blender or use
  one of the four Unity Game Toon presets.
- `MIKU_REQUIRED_TEXTURE_MISSING`: assign the creator's required Base Map;
  Endfield Mouth is the only exception.
- `MIKU_MATERIAL_ALREADY_EXISTS`: choose a new `.mat` path; Miku will not
  overwrite the existing material.
- `MIKU_ASSET_OUTPUT_PATH_INVALID`: choose a `.mat` path under `Assets/` without
  `.` or `..` path segments.
- `MIKU_TEXTURE_AUDIT_FOLDER_INVALID`: select a valid Unity folder before
  applying the Endfield importer profiles.
- `MIKU_RENDERER_DATA_SELECTION_REQUIRED`: select a Universal Renderer Data
  asset before applying Screen Rim.
- `MIKU_MESH_BINDING_MISMATCH`: use the generated Prefab or select a Renderer
  whose Mesh fingerprint matches the description.
- Missing shader or incompatible graph format: verify the exact Unity, URP, and
  Shader Graph tuple before importing or creating materials.

If an upgraded project shows Missing Shader, the package does not delete the
material. Back up the project, then deliberately recreate the user-owned
material with Standard PBR or a supported Unity Game Toon preset.

## 10. Development and validation

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 tools/ci/run_checks.py --profile release
py -3.13 tools/release/build_release.py --output-dir artifacts
```

The required Blender executable is
`C:\SteamLibrary\steamapps\common\Blender\blender.exe`. The validated Unity
executable is
`C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe`. Validation
must assert editor versions before using generated evidence.

Read [CONTRIBUTING.md](../CONTRIBUTING.md), [SECURITY.md](../SECURITY.md),
[SUPPORT.md](../SUPPORT.md), the [compatibility matrix](compatibility.md), and
the [release process](release/process.md) before distributing a build.

## 11. License and documentation assets

The repository's MIT-licensed code retains the MIT License; files with separate
SPDX terms, including the Blender Bake Worker, retain those terms. The five
character renders in section 5 are separately restricted to non-commercial
learning and documentation reference; commercial use is prohibited. Their
hashes and scope
are recorded in [documentation image provenance](provenance/documentation-images.md)
and [Third-party notices](../THIRD_PARTY_NOTICES.md). This image restriction
does not change any existing code license.
