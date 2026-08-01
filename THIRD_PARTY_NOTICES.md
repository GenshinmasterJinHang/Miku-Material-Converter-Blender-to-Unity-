# Third-party notices

Miku source is MIT-licensed. Dependencies retain their upstream terms.

| Dependency | Version | Terms | Distribution boundary |
| --- | --- | --- | --- |
| Unity URP | 17.4.0 | Unity Companion License | Resolved by Unity Package Manager |
| Unity Shader Graph | 17.4.0 | Unity Companion License | Resolved by Unity Package Manager |
| Newtonsoft Json for Unity | 3.2.1 declaration | Upstream Json.NET/Unity notices | Resolved by Unity Package Manager |
| Blender | 5.2.0 | GPL and Blender upstream notices | Separate application, not bundled |
| Miku Bake Worker sources | 1.0.0 | GPL-2.0-or-later | Included in the GPL-3.0-or-later Miku Shader Converter extension ZIP |

The Genshin, WuWa, and HSR Shader/HLSL backends are first-party, original Miku
Project Authors code licensed under MIT. They are not third-party ports and do
not require a third-party attribution entry. Their provenance is recorded in
`docs/provenance/game-workflow-backends.md`.

The repository and public artifacts contain no game-extracted Shader code,
models, textures, logos, character data, or other protected game assets. Miku
is unofficial and has no affiliation, authorization, sponsorship, or
endorsement relationship with the relevant game publishers or developers.

The MIT Semantic Exporter communicates with the GPL Bake Worker only through
versioned request/result JSON and baked files. MIT code does not import the GPL
worker implementation.
