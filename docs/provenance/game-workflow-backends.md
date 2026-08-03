# Game workflow backend provenance

This document is current for Miku 2.2.7. Generic Toon references below are
historical contract context only; the supported game workflows are Genshin,
WuWa, HSR, and Endfield.

On 2026-07-27, the project maintainer confirmed that the Shader and HLSL files
under the Unity package `Runtime/Genshin`, `Runtime/Wuwa`, and `Runtime/HSR`
directories are original project work and authorized their distribution under
the MIT License as:

```text
SPDX-FileCopyrightText: 2026 Miku Project Authors
SPDX-License-Identifier: MIT
```

The corresponding `genshin_toon`, `wuwa_toon`, and `hsr_toon` backend
selection code is also first-party MIT code. It shares the public Miku
MaterialIR and Bundle contract with Standard PBR and Generic Toon. The game
family names describe unofficial compatibility targets only.

No extracted game Shader source, assets, textures, logos, character data, or
other protected content is included. Miku is not affiliated with, authorized
by, sponsored by, or endorsed by the publishers or developers of Genshin
Impact, Wuthering Waves, or Honkai: Star Rail.

The Endfield backend added in 2.1 and revised in 2.2 is original MIT-licensed
Miku code. The public MyZmdShaders repository was inspected at revision
`650745732f82` only to identify observable material roles and packed-channel
behavior. It had no license file at that revision, so no source, serialized
material, or pipeline include was copied. The MIT-licensed
`congyuxiaoyoudao/Endfield_Character_Rendering` repository was inspected at
revision `2634c570011f` as a behavioral cross-check. The unlicensed
`skyliness1/EndFieldCharacterRender` repository was used only as an additional
observable-output reference. Miku's object-matrix head basis, flattened LUT
sampling, packed-map decoding, lighting, and material interfaces were written
independently as a clean-room implementation.
The 2.2.1 repair rechecked the same repositories only for observable raw-red
eye-shadow coverage, opaque brow geometry, iris/MatCap behavior, and packed
metal channel conventions. The repaired GGX, cornea, face-SDF, blush, and alpha
helpers are independent Miku implementations.
The 2.2.2 regression repair is an internal correction to those independently
implemented lighting equations. It adds no external source, serialized asset,
or third-party dependency.
The 2.2.3 repair rechecked MyZmdShaders only as observable behavioral evidence
for the common hair two-dimensional lookup and material response. Because that
repository has no declared license, no HLSL, serialized material, texture,
screenshot, or algorithm was copied. Main Light evaluation, face basis/SDF,
SSS, specular refinement, and URP reflection-probe integration remain original
Miku implementations built from Unity's public URP 17.4 shader API.
The 2.2.4 repair uses the same no-copy boundary. The red-only LUT observation,
hair/metal/skin target appearance, and environment-response checks are
behavioral evidence only. Scalar LUT interpretation, object-head-up strand
blending, surface rim, bounded metallic band/boosts, and skin grading are
independent Miku implementations; no external HLSL, serialized material,
texture, screenshot, or algorithm is distributed or copied.
The 2.2.5 skin-mask, SSS, Genshin highlight-compression, and presentation
profile work is likewise an independent Miku implementation. It derives skin
coverage only from the documented channels of user-supplied validation
textures and does not distribute those textures, model assets, screenshots, or
game shader code.
The 2.2.6 Wuwa eye, face-basis, ID-stocking, hair, and effect changes are also
independent Miku implementations. The user-supplied EyeHET and ID textures are
validation inputs only; the package contains no model, texture, screenshot, or
extracted game shader asset.

The 2.2.7 eye repair was derived from observable node connections, scalar
defaults, image channels, and UV transforms in a privately supplied Blender
validation file. The private blend, character mesh, textures, screenshots, and
Blender shader group are not distributed. Miku's role inference, affine
transport, URP emission composition, and main-light EG implementation are
independent MIT-licensed project code; Blender Shader-to-RGB composition is
reported as an approximation rather than copied.

Locally supplied Endfield model and texture assets are validation inputs and are
not distributed in the package.
