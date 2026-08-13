# Third-party notices

Miku source is MIT-licensed. Dependencies retain their upstream terms.

| Dependency | Version | Terms | Distribution boundary |
| --- | --- | --- | --- |
| Unity URP | 17.4.0 | Unity Companion License | Resolved by Unity Package Manager |
| Unity Shader Graph | 17.4.0 | Unity Companion License | Resolved by Unity Package Manager |
| Newtonsoft Json for Unity | 3.2.1 declaration | Upstream Json.NET/Unity notices | Resolved by Unity Package Manager |
| Blender | 5.2.0 | GPL and Blender upstream notices | Separate application, not bundled |
| Miku Bake Worker sources | 1.0.0 | GPL-2.0-or-later | Included in the GPL-3.0-or-later Miku Shader Converter extension ZIP |
| NVIDIA Streamline DFG reference | Streamline `main`, DLSS-RR guide section 4.2.1 | MIT; Copyright (c) 2023 NVIDIA Corporation | DFG coefficients/equation reimplemented in original Miku HLSL; no Streamline SDK or binary is bundled |

The Genshin, WuWa, and HSR Shader/HLSL backends are first-party, original Miku
Project Authors code licensed under MIT. They are not third-party ports and do
not require a third-party attribution entry. Their provenance is recorded in
`docs/provenance/game-workflow-backends.md`.

The installable Blender and Unity packages and Miku 3.0.0 Release assets
contain no game-extracted Shader code, models, textures, logos, character data,
or other protected game assets. Miku is unofficial and has no affiliation,
authorization, sponsorship, or endorsement relationship with the relevant game
publishers or developers.

The GitHub manuals contain five Unity-rendered character examples for Genshin
(Hu Tao and Furina), Honkai: Star Rail, Wuthering Waves, and Arknights:
Endfield. Those five PNG files are excluded from Miku's MIT license and are provided solely for
non-commercial learning and documentation reference; commercial use is
prohibited. Related characters, designs, and intellectual property belong to
their respective rights holders, and Miku grants no game-asset rights. Exact
file identities, hashes, and display mappings are recorded in
`docs/provenance/documentation-images.md`. The five PNG files are tracked with
the source documentation and therefore appear in GitHub's automatic source
archives; they are not included in either installable package and are not
uploaded as standalone v3.0.0 Release assets.

The MIT Semantic Exporter communicates with the GPL Bake Worker only through
versioned request/result JSON and baked files. MIT code does not import the GPL
worker implementation.

Miku 2.3.0's Endfield environment response cites NVIDIA Streamline's public
`EnvBRDFApprox2` reference and the Kulla--Conty energy-compensation design.
The surrounding URP integration, finite guards, compatibility switch, material
interfaces, and tests are original Miku code. See
`docs/provenance/game-workflow-backends.md` for the implementation boundary.
