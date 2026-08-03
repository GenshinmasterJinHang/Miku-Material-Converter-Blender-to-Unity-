# Miku 1.0.5

Miku 1.0.5 repairs `CustomMultiLobe` finite lighting, per-lobe normal
ownership, Clear Coat aggregation, and the URP wrapper output contract.
Non-coat composites now send already-evaluated radiance to the Unlit Base
Color final-color path. Clear Coat composites keep Base Color at zero and send
radiance to Lit Emission, avoiding both discarded output and double lighting.

The release retains Material IR, bundle, conversion-plan, manifest, and target
profile schemas at `1.0`. Public shader property names, stable asset roles,
package IDs, and user-owned Wrapper ownership are unchanged. Miku 1.0.3,
1.0.4, and intermediate 1.0.5 target-profile hashes remain bounded import
inputs; new exports use
`3858f66c1a4dfdab9a78ad52b41d3a0ae9c14ef15c4da8354b65331296096b6c`.

## Release artifacts

- `com.miku.shaderconverter-1.0.5.tgz`
  SHA-256: `6cadc5db927e47bb9185c7066ff74eb6dbbc333af1e70e5936cdd3728a1d761a`
- `miku_shader_converter-1.0.5.zip`
  SHA-256: `bb10a23c2882a44ea8827cc03f8efc1403606d4dbefa59698343c8cedd7146d1`

Both artifacts were built twice from the canonical source roots and produced
matching bytes. The installed Blender extension tree was compared file by
file with the ZIP and produced tree hash
`3fadd0c0f83055960266d6c0b411fc5e690d7c4956875c2a059ab30ffa694742`.

## Validation

- Python/Core: 231 tests passed.
- Unity package importer fixture: 114 passed, one external-bundle fixture was
  skipped because its optional environment variable was not set.
- Unity 6000.4.5f1, URP/Shader Graph 17.4.0, Windows D3D12: the three generated
  Wrapper shaders compiled with zero Shader messages. ARGBFloat checks found
  no NaN/Inf pixels. Colorful Coating 5 and Bumpy Stone 3 were finite and
  light-responsive; Colorful Coating 8 remained blue-emissive without lights
  and changed with the view direction.
- Blender 5.2.0 LTS: Colorful Coating 5, Colorful Coating 8, and Bumpy Stone 3
  were exported twice with fixed validation identities. Both 23-file output
  trees matched SHA-256
  `da018508ab61b4e727678b87e50de06e119428812f2db2d45909a4841c83f388`.

D3D11 remains the target-profile graphics API. The interactive Unity instance
available for this release run used D3D12, so a new D3D11 real-camera graphics
run is not claimed by this release note. The full 135-test EditMode aggregate
also remains non-green because two unrelated Generic Toon tests collide on
`Assets/MikuTests` metadata/unique-path state; the focused 115-test bundle
importer suite, including all 1.0.5 multi-lobe regressions, had no failures.
