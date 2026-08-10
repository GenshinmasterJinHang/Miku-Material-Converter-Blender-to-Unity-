# Genshin tutorial conformance audit for Miku 2.3.0

Contract source: the attached `pasted-text.txt` tutorial and the
`URP_Genshin_Shader.shader` from `URP_GenshinImpact-main`. Scores:
`0` missing, `1` implemented or structurally verified without target-scene
evidence, `2` implemented and verified by Unity render evidence,
`N/A` outside the Miku architecture or intentionally replaced.

Validation target: Windows D3D11, Unity 6000.4.5f1, URP/Shader Graph 17.4.0,
scene `Assets/原神/原神.unity` (Furina added during validation).

| # | Tutorial contract | Score before | Score now | Implementation |
| --- | --- | ---: | ---: | --- |
| 1 | Texture import settings (no/low compression, sRGB off for non-color, clamp ramp, no ramp/FaceLightmap mips) | 1 | 1 | Documented binding contract; import settings remain the user project's responsibility |
| 2 | RenderObjects with `head/back/edge` LightMode tags, Event 300, layer Everything | 1 | 1 | Miku uses URP forward + `SRPDefaultUnlit` outline + `MikuToonCharacterMask`; double-sided back faces implemented inside the forward pass, so no RenderObjects is required |
| 3 | `_genshinShader` body/face toggle | N/A | N/A | Split Body/Hair/Face/Eye shaders replace the toggle |
| 4 | `shadow_ramp`: AO mask + halfLambert + ramp rows `1,4,3,5,2` + day/night + bright-mask lerp | 1 | 2 | `Genshin_ReferenceLightingSignal` and `Genshin_TutorialRampRow`; row values now per-material (`_LightmapA0..4`), defaults match tutorial |
| 5 | Blinn-Phong specular `pow(NdotH,_gloss) * lightmap.r/b * _glossStrength`, dark-side mask | 1 | 1 | Miku graphic lobe + metal mask using the same LightMap R/B channels; documented deviation |
| 6 | Metal MatCap `metalMap` + `_metalMapColor` on diffuse | 1 | 1 | Miku view-space sphere tint inside specular; documented deviation |
| 6b | Optional normal map (`_bumpMap`/`_bumpScale`) | 0 | 2 | Body/Hair `_NormalMap`/`_BumpScale` + `_GENSHIN_NORMALMAP_ON`; Furina binds body/hair normal maps, Hu Tao leaves it off |
| 7 | Fresnel step rim `_fresnel/_edgeLight` | 1 | 1 | Shared screen-depth `MikuToonCharacterMask` rim; the tutorial itself approximates screen depth with Fresnel |
| 8 | `diffuse.a` self-emission: smoothstep mask + `_glow` + `_flicker` | 0 | 2 | `Genshin_DiffuseAlphaEmission`; `_DiffuseA=2` on Body/Hair/Face |
| 9 | `diffuse.a` cutout: smoothstep(0.05,0.7) + `_Cutoff`, also in outline pass | 0 | 2 | `Genshin_DiffuseAlphaClip`; `_DiffuseA=1` on Body/Hair/Face and their outline passes |
| 10 | Body blend: ramp diffuse + specular + metal + rim + emission | 1 | 2 | Diffuse + indirect SH + specular + SSS + emission + compression |
| 10b | `_AREA_SKIN` skin-tone curve must not tint non-skin regions | 0 | 2 | `Genshin_ReferenceSkinTone` gated by the LightMap skin mask; fixes blue cape back faces turning purple on double-sided body materials |
| 11 | Face SDF: mirrored SDF + front/left/right light dots + ramp V from `_lightmapA4` | 1 | 1 | `Genshin_FaceSDFShadow` head-basis projected light; documented improvement over the tutorial's fixed-up limitation |
| 12 | Back pass: `Cull Front`, second UV set (UV1) | 0 | 2 | `_GENSHIN_DOUBLE_SIDED` + `_BackUV1` on Body/Hair; per-face `SV_IsFrontFace` UV selection |
| 13 | Outline: smoothed normal in tangent data + `vertexColor.a` width mask | 1 | 2 | UV7 TangentSpaceV2 smooth normals (ADR 0016) + `Genshin_OutlineVertexMask` (A primary, G fallback) |
| 14 | Outline color: `lightmap.a` five-region `_outlineColor0..4` | 0 | 2 | `Genshin_OutlineRegionColor` with `_OutlineColorMode=1`; legacy tint path at 0 |
| 15 | Outline cutout with `_DiffuseA==1` | 0 | 2 | Implemented in Body/Hair/Face outline passes |
| 16 | ShadowCaster via `UsePass "Universal Render Pipeline/Lit/ShadowCaster"` | 1 | 2 | Miku `ShadowCaster` pass per shader |

## Evidence

- Furina FBX (`FuFu.fbx`): UV0 + UV1 + `colorSet0`; A channel carries the
  0..0.502 outline mask, G is 0..0.216. Hu Tao FBX: no color attribute and no
  UV1, which the A/G and UV0/UV1 fallbacks preserve.
- Reference project `URP-HighFidelity-Renderer.asset` contains the
  `RenderObjects` feature with `PassNames = head/back/edge`.
- Python PR profile: 275/275 tests passed, Ruff clean, package identity
  13/13, deterministic TGZ double build byte-identical. The follow-up
  skin-tone/normal-map fix ran 276/276 tests with the same gates.
- Unity EditMode: `MikuGenshinTutorialTests` 7/7 passed (including
  `ShaderUtil.ShaderHasError` for Body/Hair/Face with tutorial keywords) and
  `MikuGameToonOutlineTests` + `MikuGameToon225Tests` 37/37 passed. After
  the normal-map and masked-skin-tone additions the focused suite is 9/9 and
  the outline/2.2.5 suite remains 37/37.
- Validation project `Desktop/unity/test` (Unity 6000.4.5f1, URP/Shader
  Graph 17.4.0, scene `Assets/原神/原神.unity`): Furina FBX and textures
  imported with tutorial import settings; four Miku Genshin materials bound;
  double-sided UV1, `_DiffuseA`, and `_OutlineColorMode` enabled; front/back/
  close screenshots captured at
  `Assets/原神/芙宁娜/Validation/Furina_GenshinToon_{Front,Back,Front_Close}.png`.
- Purple-back fix screenshots: `Assets/原神/芙宁娜/Validation/
  Furina_GenshinToon_Fixed_{Back,Front}.png` and Hu Tao front/back at
  `Assets/原神/胡桃/Validation/Hutao_GenshinToon_{Front,Back}.png`. Pixel
  analysis of the Furina back view shows a blue-dominant response
  (B/R=1.09) after the masked skin-tone fix, versus the previous
  R-dominant purple caused by the unconditional `_AREA_SKIN` curve.

## Intentional deviations

- Specular, metal, rim, and face-SDF formulas are Miku-family
  reconstructions/improvements, not byte-for-byte copies of the tutorial.
- Outline normal storage is UV7 TangentSpaceV2 rather than the tutorial's
  tangent-channel data; the tutorial's own tool loses the data on Unity
  restart, which Miku's mesh tool does not.
