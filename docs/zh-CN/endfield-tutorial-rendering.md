# Miku 2.3.0 终末地教程渲染

> **3.0 后续清理（2026-08-13）：** 下文记录的 Hair Shadow 手工验证菜单及其专用
> 诊断测试已从发布包删除。既有执行结果仅作为历史证据保留；当前验证由持续维护的
> EditMode 测试和包外测试资产承担。

Miku 2.3.0 新增可选择启用的终末地教程光照、项目自有的 Volume 调色与可选屏幕 LUT
安装流程，以及四套 Game Toon 共用的描边实现。公开包不包含任何游戏模型、材质、
贴图、LUT、Logo 或场景资源。英文
[Endfield tutorial rendering](../features/endfield-tutorial-rendering.md) 是规范文档。

## 兼容行为

场景中存在一个有效的 `MikuEndfieldLightingController` 时，Endfield 受光材质才启用
教程光照贡献；不存在控制器时继续使用 2.2.12 旧路径。多个控制器同时启用时，
仅 Unity Instance ID 最小者生效，并报告
`MIKU_ENDFIELD_LIGHTING_CONTROLLER_DUPLICATE`。

公共路径包含连续 Day 0/Day 1、顶光、角色阴影 Sigmoid、三层漫反射、NoF、
Ramp Alpha/RGB、带俯仰抑制的背光补偿、相机 Forward 高光、有限安全的 DFG 与
多次散射、两类 Rim 和三种 Emission。Body 默认双面；法线贴图完成 TBN 合成后，
背面最终法线才翻转，再参与 NoL、NoV、SH、GGX、SSS 和 Rim。

Overlay 默认保持旧版 Unlit。其公开属性 `_LightingMode` 为
`0=LegacyUnlit`、`1=ToonLitTransparent`；控制器不会自动修改该材质属性。只有显式
选择 1 才进入受光透明路径，随后由控制器决定该路径使用教程贡献还是兼容旧光照。
部位切换会恢复 `_PartMode`、`_Cull` 和 `_DebugView=0`，未知部位不再回退 Body。

## 教程公式对齐（2026-08-10）

对照文章与参考仓库完成一轮忠实性修复：Body/Skin/Face 的直射高光改用文章的
D*V 响应与 Day 混合自阴影包络；Specular Refine F0 贴图按文章
`lerp(D*roughness2, NoV², _RefineF0U_lerp)` 与 `1-roughness*(1-AO)` 采样；
脸部 SDF 阴影改用宽度缩放 smoothstep，ramp 采样 `lerp(sdfNoL, NoL, Refine-G)`；
眼睛按脸平面投影光向着色且教程路径不接收场景阴影；ramp 颜色经
`rampColor_control` 保持亮度；NoF 支持 `_NoFPowStrength`，Skin/Face 按参考默认
关闭；主光/顶光在阴影带内去饱和；脸部 Rim 改用文章起止重映射与单侧 Mask；
脸部 SSS 使用 `NoV*0.85+0.15` 重映射；Body 漫反射能量使用
`0.96-0.96*metallic`。新增属性均为增量，legacy 路径保持不变。

## Volume 调色与可选屏幕 LUT

打开 **Miku > Game Toon > Rendering > Endfield LUT & Volume Installer**。默认
Volume-only 模式不需要 LUT 或 Renderer Data，会生成 Color Adjustments（曝光
`+0.35`、对比度 `+16`、饱和度 `+8`）、恒等 Color Curves、Neutral Tonemapping、
Bloom（0.85/0.20/0.65/Clamp 4/高质量过滤）和 Vignette 0.04。若选择 Renderer Data，
还会移除旧的 Miku Endfield 全屏 LUT Feature，而不改动其他 Feature。

角色资源中的 cloth LUT 和 female-skin LUT 是材质暗部颜色映射：前者供 Body/Cloth，
后者供 Face/Skin；它们不是全屏调色 LUT。安装器会依据命名、材质 `_ColorLutTex`
引用和 Recipe `ColorLut` 角色拒绝误用。高级模式仍保留显式屏幕 LUT API，仅供真实
1024×32、32³ 展平屏幕调色资源。Profile 不会自动绑定场景，
需要显式赋给目标全局 Volume。终末地验证相机使用 HDR、Post Processing、SMAA
High，并保持 MSAA 1x。

眼睛高光分为两层：动态角膜层采样 MatCap，`_MatCapUvScale` 默认 1 并只缩放
MatCap UV；固定 PMX `目HL` 几何采样 `iris_D`，使用 Opaque coverage、纹理 RGB、
Cull Off 和 Legacy Unlit。`face_01_hl_M` 仍是 Face Shader 的局部高光遮罩。

ShadowCaster 现使用 URP 标准世界空间 Bias/Clamping，并支持方向光和点/聚光；
DepthOnly 继续使用无 Bias 的相机深度顶点。Outline 仅通过 `MikuToonOutline` 绘制，
不参与投影。

LUT Shader 在 sRGB 坐标采样前保留线性 HDR 峰值，采样后恢复峰值，Alpha 不变；
强度为 0 时精确旁路。Importer 设置也支持 Undo；安装失败会原子恢复已有 Renderer
Data、Importer、材质与 Profile 的原始磁盘内容，并删除本次创建的 Feature 和资产。

写入前，安装器会拒绝空引用、外部引用、重复项或过期 Local ID 的 Renderer
Feature/Map 状态。定向保存并强制重导入 Renderer Data 后，只有在恰好一个有效 LUT
Feature 的执行点、资源映射和 Pass Material 均持久化时才报告成功。

## UV7 TangentSpaceV2 描边

新网格把 `float4(normalTS.xyz, 2.0)` 写入 UV7/TEXCOORD7；旧的未标记 object-space
float3 仍可读取。无效、零向量、非法切线或反半球数据会回退当前几何法线。

通过 **Miku > Game Toon > Mesh > Smooth Normal Generator** 生成新的
`*_SmoothOutline_v2.asset`，再显式绑定 MeshFilter/SkinnedMeshRenderer。工具不会覆盖
源 Mesh；缺失或退化切线在写入前报 `MIKU_TOON_TANGENTS_REQUIRED`。默认位置容差
为 `1e-6`，反向双面顶点不会互相平均。

13 个描边消费者统一使用屏幕/Clip 空间宽度与宽高比修正，Pass 为 `Cull Front /`
`ZTest LEqual / ZWrite Off`。`Miku_ToonMask_v1` 仍为 R=SSS、G=描边宽度、
B=屏幕 Rim、A=脸部修正。Genshin 和 Endfield 描边读取 G；Wuwa 和 HSR 为保持历史
材质行为，不读取 G 而使用中性宽度输入，HSR 还保留恒定的历史距离响应。

## 验证边界

在源码链接的隔离 Unity 6000.4.5f1 / URP 17.4.0 项目中，53/53 个定向
EditMode 测试已通过，覆盖 Shader 编译、光照数学、HairShadow 诊断、13 个描边消费者
和既有 Game Toon 回归。Python 262/262、Ruff、package identity 13/13、Blender
5.2.0 的 8/8 与最终 ZIP 安装烟测，以及 TGZ 两次字节一致构建也已通过。

上述结果不等于端口 8080 项目的场景/D3D11 视觉验收。最终 TGZ 安装与全套
EditMode、目标场景迁移、双面亮度、静态/蒙皮描边测量、LUT/Bloom 对比图和回滚截图
仍为 pending；不得把隔离或静态证据写成已完成的场景视觉证据。

迁移细节见[英文 UV7 迁移说明](../migrations/outline-tangent-space-v2.md)，当前验证状态见
[完整度审计](../audits/endfield-tutorial-completeness-2.3.0.md)。
