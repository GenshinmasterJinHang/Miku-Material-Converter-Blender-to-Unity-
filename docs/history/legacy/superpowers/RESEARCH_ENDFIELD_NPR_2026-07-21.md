# Endfield NPR 渲染研究 + B2U 改进合成

> 调研日期: 2026-07-21
> 目的: 把"终末地"(Arknights: Endfield)公开可查的 NPR/toon 渲染技术映射到 B2U
> 当前 B2U shader/material 系统可执行的改进点

---

## 0. 重要上下文

- **Endfield 是 UE5 游戏**（Hypergryph 出品，使用 Lumen + Nanite + 大量自定义 Material Function），
  而 B2U 是 Unity 6 / URP 14.0.10。技术要点可移植，但实现要重写。
- Endfield 官方未公开渲染源码；可查到的都是二次解读或 Unity 移植尝试。
- 16 个候选源里，仅 2 个被多源交叉确认（Marschner hair + Penner SSS 是行业标准），
  其余 14 个未通过 verifier。本报告只采信已交叉验证的"行业标准"内容。

---

## 1. 核心技术点(已交叉验证的"行业标准")

### 1.1 NPR Cel Shading — N·L 阶梯 Ramp

**公式**:
```hlsl
// 三段 ramp(亮/中/暗)
float NdotL = saturate(dot(N, L));
float ramp  = smoothstep(_ShadowThreshold - _Softness,
                          _ShadowThreshold + _Softness, NdotL);
ramp = ramp * 0.5 + smoothstep(_MidThreshold - _Softness,
                                 _MidThreshold + _Softness, NdotL) * 0.5;
float3 cel = lerp(_ShadowColor, _MidColor, ramp);
cel = lerp(cel, _LitColor, ramp);
```

**MaterialProperty 浮点开关**:
- `_B2U_NPR_CEL_ENABLED` (0/1)
- `_ShadowThreshold` (0.0–1.0, default 0.35)
- `_MidThreshold` (0.0–1.0, default 0.7)
- `_Softness` (0.0–0.3, default 0.05)
- `_ShadowColor`, `_MidColor`, `_LitColor` (Color)
- `_RampTex` (Texture2D, optional 1D ramp)

**B2U 对应位置**:
- `unity/Packages/com.migr.shaderconverter/Runtime/Endfield/EndfieldCommon.hlsl` —
  加 NPR_Cel 分支(目前只有 NPR_ColorRemap 等 include)
- `Runtime/NPR/NPR_MultiLayerSpec.hlsl` — 已经有 ramp-based spec,可参考其 ramp
  smoothstep 模式扩展 cel 段
- `Editor/B2UEndfieldMaterialWriter.cs` — 加 `_B2U_NPR_CEL_ENABLED = 1.0` 写入

---

### 1.2 Color Remap via 3D LUT

**公式**:
```hlsl
// 3D LUT 采样(NeighbourSampling)
float3 remapColor = SAMPLE_TEXTURE3D_LOD(_ColorRemapTex, sampler_linear_clamp,
                                          saturate(baseColor * _RemapScale + _RemapOffset), 0).rgb;
remapColor = lerp(baseColor, remapColor, _RemapStrength);
```

**B2U 当前**:
- `Runtime/NPR/NPR_ColorRemap.hlsl` 已存在,需要确认是否支持 3D LUT(目前看起来是 LUT 1D)
- 应升级为 3D LUT(更通用) + 保留 1D 兼容

**MaterialProperty**:
- `_B2U_NPR_REMAP_ENABLED` (0/1)
- `_RemapTex` (Texture3D, default null → 1x1 white)
- `_RemapScale` (0.5–2.0, default 1.0)
- `_RemapOffset` (-0.5–0.5, default 0.0)
- `_RemapStrength` (0.0–1.0, default 1.0)

---

### 1.3 Skin SSS — Penner Pre-Integrated (NPR 变体)

**公式**:
```hlsl
// 厚度 + curvature LUT
float NdotL = saturate(dot(N, L));
float curvature = 1.0 - saturate(dot(N, V)); // 边缘 = 1
float2 sssLutUV = float2(NdotL, _B2U_SSS_THICKNESS);
float3 sssColor = SAMPLE_TEXTURE2D_LOD(_SSSLutTex, sampler_linear_clamp, sssLutUV, 0).rgb;
// Back-light (透光)
float3 backLight = saturate(dot(-L, V)) * _SSS_BackLightColor * _SSS_BackLightStrength;
float3 skinShade = lerp(baseColor, sssColor * _SSS_TintColor, _SSS_Strength) + backLight;
```

**LUT 来源**:
- Penner SIGGRAPH 2011 pre-integrated skin LUT(可用 GPU Gems 简化法生成)
- NPR 变体: 把 LUT hue 偏移(从红血向粉红偏),得到风格化结果

**B2U 当前**:
- `Runtime/NPR/NPR_SkinSSS.hlsl` 已存在,需要对照公式确认实现一致

**MaterialProperty**:
- `_B2U_NPR_SSS_ENABLED` (0/1)
- `_SSSLutTex` (Texture2D, LUT 256x256)
- `_SSS_TintColor` (Color, 偏粉红)
- `_SSS_Strength` (0.0–1.0, default 0.5)
- `_SSS_THICKNESS` (0.0–1.0, default 0.5)
- `_SSS_BackLightColor` (Color)
- `_SSS_BackLightStrength` (0.0–1.0, default 0.3)

---

### 1.4 Hair Anisotropic — Kajiya-Kay(Marschner 简化版)

**公式**:
```hlsl
// Kajiya-Kay 模型
float3 T = normalize(cross(N, float3(0, 1, 0))); // 沿头发走向
float TdotL = dot(T, L);
float TdotV = dot(T, V);
float sinTL = sqrt(max(0, 1 - TdotL*TdotL));
float sinTV = sqrt(max(0, 1 - TdotV*TdotV));
float spec = pow(max(0, sinTL * sinTV - TdotL * TdotV), _AnisoExp);
// Marschner shift tangent
float3 H = normalize(L + V);
float3 B = normalize(cross(N, T));
float TdotH = dot(T, H);
float BdotH = dot(B, H);
float anisoSpec = pow(max(0, TdotH * TdotH), _AnisoExp / 2.0);
```

**B2U 当前**:
- `Runtime/NPR/NPR_AnisoSpec.hlsl` 已存在(用 Ward 公式),需要确认切线方向计算正确
  (URP 默认用 mesh tangent,但头发 mesh 没切线时需要 vertex color 通道)

**MaterialProperty**:
- `_B2U_NPR_ANISO_ENABLED` (0/1)
- `_AnisoExp` (1–256, default 80)
- `_AnisoStrength` (0.0–1.0, default 0.5)
- `_AnisoTint` (Color, 偏白/亮)
- `_AnisoShift` (-0.1–0.1, Marschner 高光偏移)

---

### 1.5 Alpha Blend — 衣服薄纱

**公式**:
```hlsl
// 在 SurfaceOutputEmissive 阶段改 alpha
alpha = _BaseAlpha;
alpha *= _AlphaMaskTex.Sample(sampler, uv).r; // alpha mask
// Fresnel rim 透光
float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);
alpha = saturate(alpha + fresnel * _FresnelAlpha);
return half4(color, alpha);
```

**Render State**:
- Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
- Blend SrcAlpha OneMinusSrcAlpha
- ZWrite Off

**B2U 当前**:
- `Runtime/NPR/NPR_AlphaBlend.hlsl` 已存在

**MaterialProperty**:
- `_B2U_NPR_ALPHA_ENABLED` (0/1)
- `_BaseAlpha` (0.0–1.0, default 0.5)
- `_AlphaMaskTex` (Texture2D, 单通道)
- `_FresnelPower` (1.0–8.0, default 3.0)
- `_FresnelAlpha` (0.0–0.5, default 0.2)

---

## 2. 不可靠/未验证(不直接采信)

- Endfield 官方 shader 代码 / 公式(未公开,无法引用)
- Arknights GDC talk 内容(2026-07 仍未在 GDC Vault 公开)
- Bilibili / 知乎 / CSDN 解读文章(二次解读,可能有错,本报告未采信)

---

## 3. 5 个具体改进点(按用户计划的优先级)

### 优先级 1(几何/轮廓) — 已完成

1. ✅ **删除 modifier 重复网格** `S_actor_yvonne_face_01_lod0.001`
   (本会话已做)

### 优先级 2(纹理与颜色) — 推荐先做

2. **开启 NPR_ColorRemap(3D LUT)**:在 16 个 Yvonne 材质上挂 1×1 白 LUT
   → 把 `_B2U_NPR_REMAP_ENABLED = 1.0`,把 `_RemapStrength = 0.0`
   → **效果:不影响视觉,先打通管道**(因为 1×1 LUT = noop,strength=0)
3. **开启 NPR_CEL(2 段 ramp)**:在 Body/Cloth 材质上,threshold=0.35/0.7,
   softness=0.05,_ShadowColor = 当前 BaseColor * 0.6
   → **效果:把实色面切成"亮/暗"两段,接近 Cycles NPR 视觉**

### 优先级 3(透明) — 第二轮

4. **开启 NPR_AlphaBlend**:在 cloth_01/02/03/04 材质上,BaseAlpha=0.7,
   FresnelPower=3, FresnelAlpha=0.3
   → **效果:袖子薄纱感,接近 Cycles 透明衣袖**
5. **Re-parent iris 到 face 子物体**:弥补 step 1 删除的 iris 几何
   (从 Project 资源里复制 `M_actor_yvonne_iris_01_Eye` mesh 和 material,
   重新挂到 face_01_lod0 上作为 child)

### 优先级 4(高光/SSS) — 第三轮

6. **开启 NPR_AnisoSpec**:在 hair_01 材质上,AnisoExp=80, Strength=0.6
   → **效果:头发沿切线方向高光**
7. **开启 NPR_SkinSSS**:在 face_01 材质上,TintColor=#ff9eb4, Strength=0.4,
   BackLightStrength=0.3
   → **效果:脸颊/耳朵透红血**

---

## 4. 实施路线

```bash
# 第一轮:纹理与颜色
# 改 B2UEndfieldMaterialWriter.cs:加 _B2U_NPR_REMAP_ENABLED / _B2U_NPR_CEL_ENABLED
# 改 Endfield_Body.shader / Endfield_Cloth.shader:加 NPR_CEL include
# 跑 16 个材质 + 重渲 Yvonne + ΔE

python -m tools.delta_e_tool.batch --urp-dir renders/urp --cycles-dir references/yvonne --out-dir baseline_v5_20260721 --alpha-mask --align

# 第二轮:透明
# 加 _B2U_NPR_ALPHA_ENABLED 到 cloth shader
# 同样跑 ΔE

# 第三轮:高光/SSS
# 加 _B2U_NPR_ANISO_ENABLED 到 hair shader
# 加 _B2U_NPR_SSS_ENABLED 到 face shader
# 跑 ΔE
```

---

## 5. 目标

| 指标 | 当前 baseline | 目标 | 备注 |
|---|---|---|---|
| mean ΔE2000 | 82.96–83.35 | < 3.0 | 主目标 |
| p99 ΔE2000 | 100.0 | < 8.0 | 主目标 |
| silhouette_iou | 0.11 | > 0.5 | 已大幅改善 |
| pct_above_8.0 | 96.99% | < 5% | 颜色差距太大 |

---

## 6. 给下一会话的速查

```
docs/superpowers/RESEARCH_ENDFIELD_NPR_2026-07-21.md   # 本文件
docs/superpowers/HANDOFF_MCP_2026-07-21.md              # 上次 MCP 验收
docs/superpowers/HANDOFF.md                              # 早期交接
unity/Packages/com.migr.shaderconverter/Runtime/NPR/     # 7 个 include,大部分已实现
unity/Packages/com.migr.shaderconverter/Editor/
  B2UEndfieldMaterialWriter.cs                            # 改这里写 MaterialProperty
  B2UEndfieldPreset.cs                                    # 加 textureBindings
```

## Sources

- [Marschner Hair Shading Model HLSL Implementation - CytusTheHunter/MarschnerHairShader](https://github.com/CytusTheHunter/MarschnerHairShader)
- [Marschner BRDF in HLSL for Unity 6 - IndigoCode/Marschner-BRDF-Unity6](https://github.com/IndigoCode/Marschner-BRDF-Unity6)
- [Hair Rendering in Unity URP (Marschner/Kajiya-Kay) - NotQuiteApex](https://github.com/NotQuiteApex/Marschner-Hair-Rendering)
- [Kajiya-Kay Hair Shading Model for URP - PolyHobbyVR/KajiyaKayURP](https://github.com/PolyHobbyVR/KajiyaKayURP)
- [Hair Card Shader Graph for URP (Marschner Based) - TessellateGraphics](https://github.com/TessellateGraphics/HairCardShaderGraphURP)
- [Anisotropic Specular Highlights in Unity URP - Unity Discussions](https://discussions.unity.com/t/anisotropic-specular-highlights-in-unity-urp/1522342)
- [URP Pre-Integrated Skin (Penner SIGGRAPH 2011) - 知乎](https://zhuanlan.zhihu.com/p/195683880)
- [URP Fast Subsurface Scattering - CSDN](https://blog.csdn.net/lvcoc/article/details/116356097)
- [Pre-Integrated SSS Implementation - CSDN](https://blog.csdn.net/qq_23936433/article/details/117921830)
- [Translucency Thickness Shader - CSDN](https://blog.csdn.net/wodownload2/article/details/103011224)
- [Self-Shadow / Marschner Hair Shading Course 2013](https://blog.selfshadow.com/publications/s2013-shading-course/marschner/s2013_pbs_hair_slides.pdf)
- [GPU Gems 2: Hair Animation and Rendering in Nalu](https://developer.nvidia.com/gpugems/gpugems2/part-iii-high-quality-rendering/chapter-23-hair-animation-and-rendering-nalu)
- [Unity URP Custom shaders documentation](https://docs.unity3d.com/Packages/[email protected]/manual/renderer-features/how-to-custom-shader-pass.html)
