# Miku 3.0.0 release notes

Miku 3.0.0 completes the default WuWa tutorial rendering path. Body, Hair,
Face, and Eye now share URP 17.4 BRDF, shadowed direct light, lightmap/light
probe GI, reflection probes, and zero-Fresnel Environment BRDF. This is an
intentional visual breaking change with clone-only migration; the interchange
schemas remain unchanged.

The final package also repairs the Forward+ main-light regression in all four
WuWa Forward passes. Unity 6000.0 / URP 17.0 compiles `_FORWARD_PLUS`, while
Unity 6000.1+ compiles `_CLUSTER_LIGHT_LOOP`; shared indirect lighting uses the
five-argument reflection-probe overload required by both Forward+ paths.
Realtime shadow attenuation selects the authored shadow tint rather than
turning the complete direct-light result black.

The same Unity-line gate is now present in all five lit Genshin programs:
Body and Hair Forward/backface plus Face Forward. Directional-main-light yaw
therefore reaches Body, Hair, and the Face SDF again under Forward+. Genshin Eye
remains intentionally unlit. Genshin and WuWa lit passes use URP
`GetShadowCoord` for screen-space shadows and declare all URP 17 soft-shadow
quality variants. WuWa Face also reports read-only SDF setup diagnostics;
diagnostics never rewrite authored material values or texture-import settings.

The release also adds linear packed WuWa NRM decoding, metallic-gated MatCap,
UV0-UV3 grading, A/B Face SDF, tutorial Hair HM, all-pass Bangs alpha clipping,
screen-space Hair Shadow, WuWa Screen Rim, LD outline color, and dedicated
`MikuToonOutline` rendering through the Geometry Renderer Feature.
The consumed `_FresnelPower` is visible as `Fresnel Rim Power`; material labels
now distinguish shared Fresnel/Screen brightness and tint from the Screen-only
pixel radius, depth threshold, and softness controls without renaming any
serialized property.

Face A and B thresholds are continuous, and both authored horizontal SDF
orientations are evaluated before their final masks crossfade around the
head-right light boundary. The additive `_FaceSdfMirrorBlendWidth` property
defaults to `0.10`. Eye tangent-space parallax now affects only Base, HET, and
HDMF; eye-socket and surface-highlight layers retain the original UV. The
existing `_EyeParallaxStrength` shader default remains zero, while recommended
setup selects `0.02` only for an HDMF-bound Eye material. Existing materials
are not rewritten automatically, and interchange schemas and public C# APIs
remain unchanged.

The distributable package no longer contains local/private validation scene
builders or the Endfield hierarchy diagnostic menu. Automated EditMode and
D3D12 acceptance remain. Renderer setup now has one canonical entry at
**Miku > Game Toon > Rendering > Game Toon Renderer Feature Installer**; the
duplicate **Screen Rim Installer** alias was removed.

## Validation

- `py -3.13 tools/ci/run_checks.py --profile pr` passed 272 Python tests,
  canonical-source checks, parsing of 99 Python files, all 12 schemas, package
  identity, and deterministic component builds.
- Two new output directories produced byte-identical ZIP, TGZ, and
  `SHA256SUMS.txt` files. Archive inspection found no private scenes, models,
  textures, documentation PNGs, `vibe-kanban/`, `dist/`, or `artifacts/` data.
- The fixed Blender 5.2.0 executable (`fbe6228777e7`) passed all eight public
  headless scripts. Installing the exact final ZIP then passed the extension
  compatibility smoke, including UI registration, Standard PBR export, and the
  GPL Bake Worker boundary.
- The exact final TGZ ran in an isolated project on Unity 6000.4.5f1 revision
  `cc83ebd631f8` with URP and Shader Graph 17.4.0. Full EditMode discovery
  reported 347 tests: 335 passed, zero failed, and 12 skipped. The skipped
  tests are the separately executed GPU cases plus documented optional
  external/visual cases and are not counted as graphics evidence.
- The same TGZ then ran with `-force-d3d12` and without `-nographics`. All ten
  required D3D12 tests passed with zero failures, skips, or inconclusive
  results. The evidence records `Direct3D12` and the exact TGZ SHA-256.

Five 1920x1080 Unity renders are tracked under `docs/images/` for the public
manuals. They were captured from temporary scene copies on Unity 6000.4.5f1,
URP/Shader Graph 17.4.0, and Direct3D 12. They are documentation examples, not
compatibility evidence, are excluded from Miku's MIT license, and are omitted
from the installable ZIP/TGZ. Private scenes, models, textures, materials, and
other validation inputs remain outside the repository and release artifacts.
The unknown star SDF/noise graph remains unsupported.

## SHA-256

The twice-reproduced manifest contains:

- `miku_shader_converter-3.0.0.zip` (199,677 bytes):
  `dbec2ba1fe8cab625ca8749aeb2f028c3c3e1dcd63e808a5289e79d4ea6605bd`
- `com.miku.shaderconverter-3.0.0.tgz` (514,365 bytes):
  `ad5581c568a98a32733311af2ae0d3afb42e794ff289bd3d5c59e83e1a89c202`
- `SHA256SUMS.txt` (201 bytes):
  `a3c5bf25384ec68cf3b939d8f343669ea2af196ac43c410bdd7ecea1a10b1fec`
