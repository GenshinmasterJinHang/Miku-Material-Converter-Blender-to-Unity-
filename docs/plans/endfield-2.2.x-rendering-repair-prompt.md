# Miku Endfield 渲染修复提示词（2026-08-02）

> 用法：把本文件整段喂给 AI 助手，或自己逐条对照修改。每条问题都标注了**根因**、**代码位置**、**原版行为参考**（`MyZmdShaders`）、**修复目标**。不抄原版源码，只描述行为差异。

---

## -1. P0 紧急修复：Visibility 吞 main light

**症状**：除了头发其他部位（body/skin/face/eye/mouth）都"完全不受主光"。

**根因**（`EndfieldCommon.hlsl`）：

`EndfieldMainVisibility`（line 363-366）：
```hlsl
float EndfieldMainVisibility(Light mainLight)
{
    return saturate(mainLight.distanceAttenuation * mainLight.shadowAttenuation);
}
```

5 个 evaluator 的 `direct` 都乘 visibility，**只有 Hair 不乘**：
- Hair line 733：`diffuse = ... * mainLight.color * lerp(0.72, 1.0, lit)` — 不乘
- Body line 428-429：`diffuse = ... * mainLight.color * visibility * ...` — 乘
- Skin line 501-502：乘
- Face line 608-609：乘
- Eye line 843-844：乘
- Mouth line 892-893：乘

**触发条件**：URP 主光 `shadowAttenuation=0`（场景 main light shadows=Off 或场景无 shadow caster）→ `visibility=0` → 5 个乘 visibility 的 evaluator 全黑；Hair 因为不乘所以仍然显示 baseColor。

**修复目标**：把 `EndfieldMainVisibility` 改成 shadow 关闭时 fallback 到 atten：

```hlsl
float EndfieldMainVisibility(Light mainLight)
{
    float atten = mainLight.distanceAttenuation;
    float shadow = mainLight.shadowAttenuation;
    // shadow 贴图未激活（shadow=0 但 atten>0）时 fallback 到 atten
    float shadowActive = step(0.0001, shadow);
    return saturate(atten * lerp(1.0, shadow, shadowActive));
}
```

---

## 0. 项目背景与边界

- **项目**：Miku 1.x（Blender 5.2 Shader Nodes → Unity 6 URP Shader Graph 转换工具）
- **目标**：终末地（Endfield）风格角色渲染
- **实现位置**：`unity/Packages/com.miku.shaderconverter/Runtime/Endfield/EndfieldCommon.hlsl` + 4 个 Part shader
- **参考实现（仅行为，不抄代码）**：`https://github.com/qiudashu233/MyZmdShaders`
  - `Assets/MyZmd/ZmdFaceToonMain.hlsl`
  - `Assets/MyZmd/ZmdEyeToonMain.hlsl`
  - `Assets/MyZmd/ZmdHairToonMain.hlsl`
  - `Assets/MyZmd/ZmdSkinToonMain.hlsl`
  - `Assets/MyZmd/ZmdToonMain.hlsl`（body/cloth）
- **资源位置**：`C:\Users\22687\Downloads\【明日方舟：终末地】洁尔佩塔_by_茶叶味香皂_aef698bce31d4c14f77a7b92451bd129\other tex\`
- **约束**：Miku 是公开开源项目，MyZmdShaders 是行为参考（无 license，不能复制源码）。所有修复必须**重写**或**改写**原版逻辑到 Miku 的代码风格里。
- **测试位置**：`unity/Packages/com.miku.shaderconverter/Tests/Editor/`（EditMode 回归测试）

---

## 1. 六个症状总览

| # | 症状 | 根本原因类别 | 优先级 |
|---|---|---|---|
| 1 | 脸部只有眼睛周围吃光 | 脸空间基用 mesh 物体轴推算 + faceLight 单点依赖 SDF | **P0** |
| 2 | 眼睛虹膜没修复（眼白显示橙红） | `irisMask` 方向反了 + 法线没分区域 | **P0** |
| 3 | 腮红没有 | emotion atlas 的 mask 在 RGB 不在 alpha，代码读 alpha | **P0** |
| 4 | 毛发一片一片 | strandTangent 用世界全局方向 + 球面混合方向反了 + ao 压 spec | **P0** |
| 5 | 皮肤没有白里透红 | `_UseColorLut=0` 默认 + SSS 缺颜色控制 + emotion 无 blush 路径 | **P1** |
| 6 | 金属部分全黑 | ao 直接乘 spec 三项 + 缺 SpecularRefine 通道 | **P0** |

---

## 2. 逐症状详细分析与修复

### 症状 1：脸部只有眼睛周围吃光

**实锤数据**：
- `T_actor_common_female_face_01_SDF.png` (1024×1024)：R 通道从中心 255 强径向衰减到边缘 0；G 通道几乎全 255；B 中等；A 全 255
- 名字直接是"SDF"——`_SDFLightmap` 绑定正确

**根因**（在 `EndfieldCommon.hlsl`）：

| 位置 | 问题 |
|---|---|
| `EndfieldGetHeadBasis()` line 253-275 | 从 `GetObjectToWorldMatrix()` 推算 right/forward/up——**假设脸 mesh 跟头是同一个 GameObject**，子 mesh 失效 |
| `EndfieldEvaluateFace` line 567-569 | `projected = lightDirWS - dot(lightDirWS, head.upWS) * head.upWS` —— 用全局 up 投影，**没背光补偿** |
| `EndfieldEvaluateFace` line 570-572 | `sdfUv.x = lerp(uv.x, 1-uv.x, step(0, rightAmount))` —— **flag=1 时翻成 1-x，跟原版相反**（原版 flag=0 翻，flag=1 不翻） |
| `EndfieldEvaluateFace` line 575-580 | `forwardAmount = dot(projected, head.forwardWS)` —— 单点 forward，无 backLight |
| `EndfieldEvaluateFace` line 588 | `faceLight = lerp(sdfLight, geometricLight, refine.g)` —— **默认 refine.g=0，永远走 sdfLight**，SDF 边缘=0 时脸部其他区域主光被吞掉 |
| `EndfieldEvaluateFace` line 616-619 | `sdfNormal = rightWS*sdf.b + forwardWS*(1-abs(...))` —— **忽略 head.upWS，矩阵不正交** |

**原版行为**（`ZmdFaceToonMain.hlsl`）：
- 脸空间基 = 材质属性 `_FaceRight / _FaceUp / _FaceForward`（line 109），**不是从 mesh 推算**
- `faceNoL = mainLightDir_xz_faceDir.z + backLight * compensation`（line 159-164），**有 backLight 补偿**
- `sdf_smoothVar` 用 faceNoL 决定 sdf_min/max/width（line 168-170），**不是固定 threshold**
- `ramp_NoL = lerp(sdf_NoL, NoL, sdfRefineTex_var.y)`（line 177）—— **SDF 默认走，但通过 sdfRefine 切换到几何光**

**修复目标**：
1. 新增材质属性 `_FaceRight / _FaceUp / _FaceForward`（Vector，object space 三轴），用 `TransformObjectToWorldNormal` 转世界空间，**完全替代 `EndfieldGetHeadBasis`**
2. 加 backLight 项：`faceNoL += backLight * (0.5 - 0.5*faceNoL_z*faceNoL_z)`
3. 改 SDF 阴影：`sdf_smoothVar = saturate((0.5*(sdf.r+sdf.g) - sdf_min) / sdf_width)`，其中 `sdf_min/max/width` 由 faceNoL 决定
4. 加 SDF 退化 fallback：`faceLight = max(geometricLight, sdfLight * step(0.05, margin))`——SDF 无值时退回几何光
5. UV 镜像公式改：`sdf_u = step(0, lightDir_faceDir.x) * (2*uv.x - 1) + (1 - uv.x)`（跟原版一致）

---

### 症状 2：眼睛虹膜没修复

**实锤数据**：
- `T_actor_aglina_iris_01_D.png` (512×512)：**5×5 grid 抽样全是橙红色**（顶行 (164,77,75) 等），右下角突然出现 (215,221,252) 蓝色高光
- 这张图是**单根虹膜特写**（橙红 + 蓝色高光），不是整只眼球的分层贴图

**根因**（在 `EndfieldCommon.hlsl`）：

| 位置 | 问题 |
|---|---|
| `EndfieldEvaluateEye` line 818 | `irisMask = 1 - smoothstep(0.72, 1.0, radiusSquared)` —— **外圈=0（眼白），内圈=1（虹膜），符号反了**。原版用 `step(0.25, distSq)` 是**外圈=1（眼白），内圈=0（虹膜）** |
| `EndfieldEvaluateEye` line 820-822 | `parallaxOffset = -viewDirTS.xy / safeViewZ * irisMask` —— **整张眼睛都在视差**，应该是圆周附近 |
| `EndfieldEvaluateEye` line 828-830 | `corneaNormalWS = ...` —— 法线全程用 corneaNormalWS，**眼白区也被强行 cornea** |
| `EndfieldEvaluateEye` line 837 | `scleraMode = step(0.5, _EyeMode)` —— `_EyeMode=0` 时 `eyeColor = irisSample.rgb`，**整只眼都用 BaseMap 颜色** |
| `EndfieldEvaluateEye` line 842 | `eyeColor = lerp(irisSample.rgb, scleraColor, scleraMode)` —— **scleraMode 是全局开关**而不是按 irisMask 区分 |

**原版行为**（`ZmdEyeToonMain.hlsl`）：
- `eyeCenterArea = step(0.25, distSq)`（line 80）—— **外圈=1（眼白）**
- `parallaxMask = smoothstep(-5*(distSq-0.25))`（line 93-94）—— **圆周附近亮，圆内圆外暗**
- `corneaNormalTS = lerp(corneaNormalTS, float3(0,0,1), eyeCenterArea)`（line 113）—— **外圈 lerp 回几何法线**
- `#ifdef _FACIAL_ON` 切换：眼部在 face 表情里时禁用视差/cornea，用 face LUT 染色

**修复目标**：
1. `irisMask = step(0.25, distSq)`——**外圈=1**（眼白），**内圈=0**（虹膜区域）
2. `parallaxMask = smoothstep(saturate(-5*(distSq-0.25)))`——**圆周附近 mask**
3. `corneaNormalWS = lerp(corneaNormalTS_world, input.normalWS, irisMask)`——**眼白区 lerp 回几何法线**
4. `eyeColor = lerp(irisSample.rgb, scleraColor, irisMask)`——**按 irisMask 区分眼白/虹膜**，丢掉 `_EyeMode` 全局开关
5. 资源提示：这张 BaseMap 本身就是单根虹膜特写，要让眼白显示成白色，得用 _SDFMask 或额外 _ScleraTex；或者修改 BaseMap 资源

---

### 症状 3：腮红没有

**实锤数据**（从 2×2 atlas 实图）：
- **左上 tile 0**：两个粉红脸颊腮红，RGB=(192,116,102)，**A=0**
- **右上 tile 1**：纯白空白，A=0
- **左下 tile 2**：两个粉红脸颊腮红（位置略不同），RGB=(192,116,102)，**A=0**
- **右下 tile 3**：黑色眉毛/上睫毛 mask，RGB=(34,30,30)，A=**219**

**根因**（在 `EndfieldCommon.hlsl:543-559`）：
- 代码用 `blush.a * _BlushStrength` 当 mask
- 实际腮红的 mask **在 RGB 粉红 vs 白色背景的对比**，**A 通道全是 0**
- 结果：无论 `_BlushTileIndex=0/1/2/3`、`_BlushStrength` 设多少，`blushMask=0` → 永远没腮红

**Plan 错误**（`docs/plans/endfield-2.2.1-eye-face-metal-regressions.md:60-62`）：
- 写"tile zero alpha contains a symmetric authored cheek mask"——**与实际数据相反**
- 实际 tile 0 alpha=0，mask 在 RGB

**原版行为**（`ZmdFaceToonMain.hlsl:55-63`）：
- `albedo = lerp(albedo, trickColor, trickTex_var.w * _TrickStrength)` —— 原版也用 alpha，但**原版 emotion atlas 的腮红 alpha 是 >0 的**（跟 Miku 资源不一致）
- 没有 `_BlushTileIndex`/`_BlushStrength` 拆分，只用 `_TrickType` 选 tile

**修复目标**（因为 Miku 资源 alpha=0，必须用 RGB 通道当 mask）：
1. 把 mask 来源从 alpha 改成 RGB：
   ```hlsl
   float whiteness = max(max(blush.r, blush.g), blush.b);  // 1=白, 0=纯黑
   float blushMask = saturate((1.0 - whiteness) * 2.5 * _BlushStrength);
   ```
   或者用饱和度：
   ```hlsl
   float maxC = max(max(blush.r, blush.g), blush.b);
   float minC = min(min(blush.r, blush.g), blush.b);
   float saturation = saturate((maxC - minC) * 3.0);
   float blushMask = saturation * _BlushStrength;
   ```
2. 材质上把 `_BlushStrength` 设到 0.6（plan 写的值）
3. `_BlushTileIndex=0` 指向左上 tile（有腮红）—— 默认值已对
4. **修改 plan 文档**（`docs/plans/endfield-2.2.1-eye-face-metal-regressions.md:60-62`），纠正成"tile 0/2 是 RGB 粉红腮红（alpha=0），mask 走 RGB-vs-白"

---

### 症状 4：毛发一片一片

**实锤数据**：
- `T_actor_aglina_hair_01_P.png` (1024×1024)：R 通道 median=255（大量=1），mean=190 → `material.r` 接近 1
- `T_actor_aglina_hair_01_HN.png` (1024×1024)：G/A 通道 std=8.4（极小）→ split normal 几乎中性
- `T_actor_aglina_hair_01_sw_M.png` (512×512)：全 1 → accessory mask 算出来 = 0

**根因**（在 `EndfieldCommon.hlsl`）：

| 位置 | 问题 |
|---|---|
| `EndfieldEvaluateHair` line 723-725 | `sphereBlend = material.r * _UseHeadSphereNormal` —— `material.r` 接近 1 + `_UseHeadSphereNormal` 默认 1 → **sphereBlend=1，highlightNormal 完全是 sphereNormal** |
| 尾巴：`sphereNormal = positionWS - headCenterWS`，`_HeadCenterOS=(0,0,0,1)` 默认 → 尾巴每根毛发的 sphereNormal 几乎平行 → **尾巴高光"成片"** |
| `EndfieldEvaluateHair` line 691-700 | `_UseSplitNormalMap=0` 默认 + HN 贴图 G/A std 极小 → **diffuseNormal ≈ normalWS**，无切线扰动 |
| `EndfieldEvaluateHair` line 736-744 | `stableStrand = head.upWS 投影` —— **世界空间全局方向**，同一切平面所有像素的 strandTangent 相同 |
| `EndfieldEvaluateHair` line 746 | `meshStrand = cross(highlightNormal, input.tangentWS)` —— **算成 binormal，不是发丝方向** |
| `EndfieldEvaluateHair` line 748 | `strandTangent = lerp(stableStrand, meshStrand, material.r)` —— material.r=1 时走错的 meshStrand |
| `EndfieldEvaluateHair` line 776 | `accessoryMask = (1 - specMask) * _UseSpecularMask` —— specMask=1 → **配饰区域 = 0** |
| `EndfieldEvaluateHair` line 446, 450 | Body `directSpecular *= ao`、`indirectSpecular *= ao` —— **ao 压 spec** |

**原版行为**（`ZmdHairToonMain.hlsl`）：
- `HNormalWS = lerp(sphereNormal, HNormalWS, ormTex_var.x)`（line 87）—— **R=0 时全 sphere，R=1 时不带**（**跟 Miku 方向相反**）
- `cylinderNormal = HNormalWS - dot(H, cameraRight) * cameraRight`（line 242）—— **圆柱体近似**算 strand
- `fakeTangent = cross(worldUp, flatHNormal)`（line 244）—— **世界 up 兜底**
- 高光用 **2D LUT**（`SpecularRefineF0Tex`，line 263）—— 不依赖精确的 strandTangent
- AO 不参与 spec 主体

**修复目标**：
1. **球面混合方向反过来**：`HNormalWS = lerp(sphereNormal, HNormalWS, material.r)`（R=0 不带 sphere=尾巴正确，R=1 全 sphere=头部正确）
2. **strand 改用 cylinderNormal 投影**：
   ```hlsl
   float3 cylinderNormal = highlightNormal - dot(highlightNormal, cameraRightWS) * cameraRightWS;
   cylinderNormal = normalize(cylinderNormal);
   float3 strandTangent = normalize(cross(worldUp, cylinderNormal));
   ```
3. **ao 不参与 spec**：参考原版 `ZmdToonMain.hlsl:270-272`：
   ```hlsl
   float3 specularLight = mainLightColor * selfAoShadowEffect * (ao_shadow*0.5+0.5);
   ```
   把 `directSpecular *= visibility * ao` 改成 `directSpecular *= specularLight` 模式
4. **可选改进**：把 Kajiya-Kay 高光改成 2D LUT 模型（参考 `ZmdHairToonMain.hlsl:248-263`），用 `SpecularRefineF0Tex` LUT + `ToH_lut + VoHN_horizontal` 算 UV
5. `_HairRefineMap` / `_HairLineMap` / `_HairShiftMap` 材质上启用（plan 里没要求但能显著改善细节）

---

### 症状 5：皮肤没有白里透红

**实锤数据**：
- `T_actor_common_femaleskincolor03_lut_D.png` (1024×32)：LUT 范围 R 0~211, G 0~197, B 0~195 → 偏红
- 默认 `_UseColorLut=0`（Face shader 第 20 行）→ **LUT 完全没启用**
- `complexion` 计算：`baseSample.rgb * (1.06, 1.015, 1.0) + (0.018, 0.006, 0.005)` —— 增量太小

**根因**（在 `EndfieldCommon.hlsl`）：
| 位置 | 问题 |
|---|---|
| `EndfieldEvaluateFace` line 560-562 | `complexion` 增量只有 +0.018，看不出"透红" |
| `EndfieldEvaluateFace` line 494-499 | `useLut = max(_UseColorLut, _UseShadowLut)` 默认 0 → **LUT 完全不用** |
| `EndfieldEvaluateSkin` line 509-512 | `_SkinSSSIntensity` 只有强度，**没 SSSColor 颜色控制** |
| emotion atlas 无 blush mask（症状 3）—— 透红路径也不通 |

**原版行为**（`ZmdFaceToonMain.hlsl:113-122`）：
- `view_sssStrength = lerp(saturate(headFDotCamerF + 0.5), 1, sdfRefineTex_var.y) * sdfRefineTex_var.x`（line 113）—— 视角 + SDF refine 控制
- `sss_NoV = saturate(NoV) * 0.85 + 0.15`（line 118）—— 视角衰减
- `sss_area = saturate(_SSSArea * view_sssStrength * (1 - sss_NoV))`（line 119）—— 边缘 SSS
- `albedo_sssRefine = albedo * lerp(1, _SSSColor, sss_area)`（line 121-122）—— albedo 被 SSS 染色
- **`_SSSColor` + `_SSSArea` 是新属性**

**修复目标**：
1. 新增材质属性 `_SSSColor`（Color，默认 (1, 0.5, 0.4) 微红）和 `_SSSArea`（Range 0~1，默认 0.3）
2. 加 SSS 视角计算：`view_sssStrength = sdfRefine.r * sdfRefine.y`（简化版）
3. `complexion = baseSample.rgb * lerp(1, _SSSColor, sss_area)`
4. 材质上把 `_UseColorLut=1` 启用 LUT
5. 配合症状 3 修 emotion atlas + `_BlushStrength=0.6`

---

### 症状 6：金属部分全黑

**实锤数据**：
- `T_actor_aglina_cloth_01_M.png` (2048×2048)：R 通道 mean=55.5 median=0（22.3% 像素 R>200 是金属区）
- **金属区 R>200 的像素** B 通道 min=0 max=255 mean=162.1（6% 的金属区 B=0）—— 多数金属区 B>0，但有 6% 直接被 ao=0 压黑

**根因**（在 `EndfieldCommon.hlsl`）：

| 位置 | 问题 |
|---|---|
| `EndfieldEvaluateBody` line 444-446 | `directSpecular = GGX(...) * specRefine * mainLight * visibility * ao * _SpecularIntensity` —— **ao 直接乘 spec** |
| `EndfieldEvaluateBody` line 450 | `indirectSpecular = environment * f0 * ao * _IndirectIntensity` —— **ao 直接乘 spec** |
| `EndfieldEvaluateBody` line 453-456 | `metalVisibility = metallic * f0 * mainLight * lerp(0.22, 0.38, smoothness) * reflectivity` —— 系数 0.22~0.38 太小，金属区基础色不显示 |
| `EndfieldEvaluateBody` line 437 | `roughness = max(1 - smoothness, 0.06)` —— smoothness=0.45（fallback）时 roughness=0.55 |
| **没有 SpecularRefineF0Tex / SpecularRefineColorTex 通道**（参考原版 `ZmdToonMain.hlsl:254-262, 373-378`） |

**原版行为**（`ZmdToonMain.hlsl:115-131, 254-262, 270-272`）：
- `metallic = ormTex_var.r`, `reflectivity = ormTex_var.g`, `ao = ormTex_var.b`（**Ao 不参与 spec**）
- `F0 = 0.04 * reflectivity.xxx + metallic * (baseColor - reflectivity.xxx * 0.04)` —— 标准 PBR
- `mainLightSpeuclarResult = mainLightColor_final * selfAoShadowEffect * specular_brdf`（line 270-272）—— **specularLight 用 selfAoShadowEffect** 而非 `ao`
  ```hlsl
  float ao_shadow_lowLight = lerp(ao_shadow_NoFRamp, min_shadowEffect, _DayStrength);
  float selfAoShadowEffect = lerp(_SelfAoShadowStrength, 1, ao_shadow_lowLight);
  ```
- `_SPECULARREFINE_ON` keyword 启用 `SpecularRefineF0Tex` LUT refine F0
- IBL 用多项式拟合 + env rotation + LOD（line 285-341）

**修复目标**：
1. **ao 不参与 spec 三项**：把 `directSpecular *= ao` 和 `indirectSpecular *= ao` 改成：
   ```hlsl
   float ao_shadow = ao * shadowEffect;
   float selfAoShadowEffect = lerp(_SelfAoShadowStrength, 1, ao_shadow);
   float3 specularLight = mainLight.color * visibility * selfAoShadowEffect * (ao_shadow * 0.5 + 0.5);
   float3 directSpecular = GGX(...) * specRefine * specularLight * _SpecularIntensity;
   ```
2. **加 SpecularRefineF0Tex 和 SpecularRefineColorTex 通道**（新增 Properties 和 Sampler）：
   - `_SpecularRefineF0Tex` 用于 F0 refine LUT
   - `_SpecularRefineColorTex` 用于高光颜色 refine
   - 加 `_UseSpecularRefine` Toggle keyword 控制
3. **加 env map rotation 和 LOD 控制**（参考 `ZmdToonMain.hlsl:326-337`）
4. **提高 metalVisibility 系数**：把 `lerp(0.22, 0.38, smoothness)` 改成 `lerp(0.5, 0.85, smoothness)`
5. **加 IBL 多项式拟合**（参考 `ZmdToonMain.hlsl:285-317`）

---

## 3. 修复优先级与依赖关系

```
P0 优先级（一次性修 4 个症状）：
├── 症状 3 腮红（emotion atlas mask 改成 RGB）— 独立
├── 症状 6 金属 ao 不压 spec — 独立
├── 症状 4 球面混合方向反过来 + strand 改 cylinderNormal — 独立
└── 症状 2 眼睛 irisMask 方向反过来 + 视差改圆周附近 — 独立

P1 优先级（依赖 P0）：
├── 症状 1 脸空间基改成材质属性 — 依赖材质层支持
├── 症状 5 加 SSSColor/SSSArea — 依赖 LUT 启用
└── 症状 4 改 K-K 为 2D LUT 模型（可选改进）

P2 优先级（增强）：
├── 症状 6 加 SpecularRefineF0Tex/ColorTex + env rotation
└── 症状 1 加 backLight 补偿
```

---

## 4. 关键差异对照表（MyZmd 原版 vs Miku 端）

| 维度 | MyZmd 端 | Miku 端（Endfield） |
|---|---|---|
| **脸空间基** | 材质上指定 `_FaceRight/Up/Forward` | 从 `GetObjectToWorldMatrix()` 推算 |
| **SDF 阴影模型** | `sdf_smoothVar` 用 faceNoL 决定 sdf_min/max/width | `margin * (sdf.r + sdf.g) * 0.5` + threshold/smoothstep |
| **faceLight fallback** | `ramp_NoL = lerp(sdf_NoL, NoL, refine.y)` | `faceLight = lerp(sdfLight, geometricLight, refine.g)` |
| **眼睛 mask 方向** | `step(0.25, distSq)` 外圈=1 | `1 - smoothstep(0.72, 1.0, r²)` 内圈=1 |
| **眼睛视差** | 圆周附近 mask | 整张眼睛 mask |
| **眼睛法线分区** | 外圈 lerp 回几何法线 | 全程用 cornea |
| **头发高光模型** | 2D LUT | Kajiya-Kay |
| **strandTangent 来源** | cylinderNormal 投影 + worldUp 兜底 | head.upWS 投影（世界全局） |
| **球面混合方向** | `R=0` 全 sphere，`R=1` 不带 | `R=1` 全 sphere，`R=0` 不带（**反**） |
| **ao 影响 spec** | ao 只调 diffuse 暗度 | ao 直接乘 spec 三项 |
| **SSS 控制** | `_SSSColor` + `_SSSArea` + 视角 fade | 只有 `_SkinSSSIntensity` 强度 |
| **Rim light** | 视角 rim + NoLxz rim 两路 | 无 |
| **IBL 模型** | 多项式拟合 + env rotation + LOD | URP GlossyEnvironmentReflection |
| **emotion mask 通道** | 资源 alpha（跟原版一致） | 资源 alpha=0（设计不一样！） |

---

## 5. 验证标准

每修一个症状需要：

1. **代码改动**：`EndfieldCommon.hlsl` 和对应 Part shader
2. **Properties 改动**：新增属性要在对应 `.shader` 文件的 `Properties{}` 块里声明
3. **EditMode 回归测试**（参考 `unity/Packages/com.miku.shaderconverter/Tests/Editor/MikuEndfieldShaderTests.cs` 之类）：
   - 测新属性的默认值
   - 测 mask 计算的方向（RGB vs alpha）
   - 测 fallback 行为（SDF 无值时退回几何光）
4. **视觉验证**：用 Jiege 2.2.1 材质在 Unity 里截图，对比参考图
   - 脸部光照均匀（不再只有眼睛周围有光）
   - 眼睛虹膜橙色 + 眼白白色
   - 脸颊有粉色腮红
   - 头发一根根有方向
   - 皮肤白里透红
   - 金属反光

---

## 6. 不要做的事

- **不要**直接复制 MyZmdShaders 的源码到 Miku 项目（plan 里写了"no implementation source is copied"）
- **不要**改 `b2u_mvp/` 或 `unity/Packages/com.b2u.shaderconverter/`（已废弃的旧架构）
- **不要**只改 HLSL 不改 Properties 块（新增属性不声明 = 编译失败）
- **不要**改 Jiege_2.2 rollback 材质（plan 里 2.2 是回滚资产，2.2.1 才是新版本）
- **不要**改 `docs/plans/endfield-2.2.1-eye-face-metal-regressions.md` 里 1.x 范围内的描述——只改 60-62 行关于 emotion atlas 描述错的部分

---

## 7. 完成定义

修复完成 = 全部 6 个症状在 Unity 截图里**视觉上对齐参考图**，且：
- EditMode 测试全过
- `tools/ci/run_checks.py --profile pr` 全过
- 连续两次 `tools/build_miku_unity_package.py` SHA-256 哈希一致
- 文档 / changelog / plan 同步更新
