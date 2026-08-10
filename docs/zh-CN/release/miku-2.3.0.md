# Miku 2.3.0

Miku 2.3.0 补齐可选择启用的终末地教程光照，修复四套 Game Toon 的平滑法线描边，
并新增在 URP 后处理前执行的项目自有游戏 LUT 流程。

- `MikuEndfieldLightingController` 驱动 Day 0/Day 1、顶光、相机 Forward 高光、
  背光补偿、三层漫反射、DFG、多次散射和两类 Rim。
- Endfield Body 默认双面，并通过 Fragment Front Face 在法线贴图合成后翻转背面法线。
- Skin、Face、Eye、Hair、Lit Transparent Overlay、HairShadow 诊断和九部位不变量补齐；
  Overlay 默认 `_LightingMode=0` 保持 Unlit，显式设为 1 才进入受光透明路径。
- 新平滑法线网格使用带 `w=2` 标记的切线空间 UV7；旧 object-space UV7 保持兼容。
- 13 个描边消费者使用共用 Clip-space 外扩并统一 `ZWrite Off`；Genshin/Endfield
  读取绿色宽度遮罩，Wuwa/HSR 保留不读取 G 的历史行为，HSR 保留恒定历史距离响应。
- 安装器校验项目内 32³ 展平 LUT，安装一个后处理前 Full Screen Pass，并生成只含
  Neutral、Bloom、Vignette 的 Profile；游戏 LUT 不进入公开 TGZ。

本版本不升级 MaterialIR、Bundle、Blender/Miku IR 或 JSON Schema，不重命名已有
Shader/材质属性。没有 Lighting Controller 时保留旧 Endfield 光照路径。

正式验证环境为 Windows D3D11、Unity 6000.4.5f1
（`cc83ebd631f8`）、URP 17.4.0、Shader Graph 17.4.0，以及仓库固定路径的
Blender 5.2.0。

已执行（2026-08-10）：

- Python PR 与 release profile：268/268 测试通过，Ruff 干净，package
  identity 13/13。
- Blender 5.2.0：8/8 headless smoke；最终 2.3.0 ZIP 安装烟测通过
  （`MIKU_INSTALLED_COMPATIBILITY_SMOKE_OK`），证据保留在
  `artifacts/miku-2.3.0-blender-release-smoke.json`。
- Unity 6000.4.5f1 / URP 17.4.0（端口 8080 源链接工程）：全量 EditMode
  283 项，282 通过、0 失败、1 跳过。
- 两次独立构建字节一致。最终 SHA-256：
  - ZIP `miku_shader_converter-2.3.0.zip`：
    `f4557a134c7f34da3ebdc3feec2b18f0dd916dcf391699a2607f58da76c762c9`
  - TGZ `com.miku.shaderconverter-2.3.0.tgz`：
    `ad78ca6fbf376d94b6e50c5382fe7df72d07854e455e4df146a0ce2a12d8534b`

仍待执行且不宣称通过：最终 TGZ 在端口 8080 工程的安装与已安装包 hash 核对、
`Assets/endfield/终末地.unity` 事务式迁移与保存，以及所有 D3D11 双面、
静态/蒙皮描边、LUT/Bloom 和角色参考截图。
