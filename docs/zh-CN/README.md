# Miku 3.0.0

Miku 将 Blender 5.x EEVEE 材质转换为目标无关的 MaterialIR 2.0，再将确定性、
可继续编辑的 Standard PBR Shader Graph 资产导入 Unity 6 URP。Blender 只显示一条
Standard PBR 路线；Unity 编辑器内提供四套随包附带的 Game Toon Shader/HLSL 预设。

![Miku 转换流程](../images/miku-workflow-zh-cn.svg)

[English README](../../README.md) · [中文完整手册](manual.md) ·
[English Manual](../manual.md)

## 主要能力

- Blender 只显示 Standard PBR 导出路线，支持明确标注的近似、隔离贴图烘焙、
  确定性 Bundle 和时间输入预检。
- Unity 生成可编辑 Shader Graph 包装图、由 Miku 管理的 Sub Graph、稳定 ID 和
  结构化诊断，并继续读取历史 Bundle 1.0。
- Unity 材质创建器位于 `Miku > Game Toon > Materials > Create Material`，提供
  显式贴图字段和必填校验，不覆盖现有资产，也不自动绑定 Renderer、FBX 或 Prefab。
- 可选择启用的终末地教程光照、支持蒙皮的切线空间描边，以及项目自有的后处理前
  游戏 LUT 安装器。详见[终末地 2.3.0 指南](endfield-tutorial-rendering.md)。
- 最初为未发布 2.4.0 候选开发、最终随 3.0.0 交付的原神教程公式、独立 UV1
  背面/UV7 描边调度，以及组合式 Geometry/Screen Rim 渲染功能安装器。详见
  [原神渲染指南](genshin-tutorial-rendering.md)。
- 在 `Miku > Settings` 中按用户选择 English 或简体中文，不修改项目资产。

## 内置 Game Toon Shader/HLSL 预设

Unity 包直接附带四套由 Miku 独立编写的实验性 Shader/HLSL 代码；它们是可运行的
着色器实现，不只是贴图字段模板。

| 预设 | 可用材质部位 | 主要功能 |
| --- | --- | --- |
| 原神（Genshin） | Body、Hair、Face、Eye | Light Map/Ramp 卡通光照、Face SDF、头发、眼睛、描边和屏幕边缘光支持 |
| 崩坏：星穹铁道（HSR） | Body、Hair、Face、Eye | Light Map/Ramp、Face SDF、头发高光、眼睛和描边支持 |
| 鸣潮（Wuwa） | Body、Hair、Face、Eye、Effect | ID/Stockings 双绑定、脸部基向量、眼睛专用贴图、高光和自发光 |
| 明日方舟：终末地（Endfield） | Body、Skin、Hair、Face、Eye、Mouth、Overlay、Effect、HairShadow | 连续昼夜/顶光、三层 Ramp、DFG、Face SDF、皮肤/眼睛/头发、受光 Overlay、阴影、描边和项目 LUT |

创建器合计提供 22 个有效材质部位。这些预设处于 **Experimental（实验性）** 状态，
不承诺与任何游戏逐像素一致。Miku 不包含从游戏提取的 Shader 源码、模型、贴图、Logo
或其他游戏资产。完整贴图规则、角色效果示例和 Unity 编辑器工具教程见
[中文手册](manual.md)。

## 安装发布版本

1. 从 [v3.0.0 Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v3.0.0)
   下载 `miku_shader_converter-3.0.0.zip`、`com.miku.shaderconverter-3.0.0.tgz`
   和 `SHA256SUMS.txt`。
2. 安装前对 ZIP 与 TGZ 运行 `Get-FileHash -Algorithm SHA256`，并逐项与
   `SHA256SUMS.txt` 比对。
3. 在 Blender 5.0–5.2 打开 **编辑 > 偏好设置 > 扩展**，选择 **从磁盘安装**，选中 ZIP
   并启用 Miku。
4. 在 Unity 6000.0–6000.5 打开 **Window > Package Manager > + > Add package
   from tarball**，选择同一个 TGZ。
5. 从 Blender 导出材质 Bundle，并把完整 Bundle 目录复制到 Unity 项目的
   `Assets/` 下。

源码开发时，在 Package Manager 中选择 **Add package from disk**，并指向
`unity/Packages/com.miku.shaderconverter/package.json`。完整安装、资产所有权、诊断和
升级说明见[中文手册](manual.md)。

## 五分钟流程

1. 在 Blender Shader Editor 中选中带材质的对象。
2. 打开 **Miku** 侧栏，选择输出目录并确认 **Standard PBR**。
3. 导出当前材质。有效链路中的 `Input.Time.*` 会在写入输出或烘焙请求前停止导出；
   断开的时间节点不会影响导出。
4. 将完整 `.mikubundle` 目录复制到 Unity `Assets/`，等待导入器生成可编辑图和报告。
5. 创建游戏卡通材质时，在 Unity 创建器中选择预设和部位、填写所显示的贴图，然后
   保存为用户拥有的 `.mat`。

![Blender Standard PBR 面板](../images/blender-standard-pbr-en.png)

## 兼容性与许可范围

已验证参考组合为 Blender 5.2.0、Unity 6000.4.5f1、URP 17.4.0 和
Shader Graph 17.4.0，并使用 Windows Direct3D 12。正式范围为 Blender 5.0–5.2，以及严格匹配的
Unity 6000.0/URP 17.0 到 Unity 6000.5/URP 17.5 技术线；URP 与 Shader Graph
版本必须完全相同。区间内未记录的稳定补丁会先执行能力预检并显示警告。
Blender 5.3+、Unity 6000.6+、17.6+ 和预发布版本会在写资产前拒绝。
3.0.0 不修改
MaterialIR 2.0、Bundle 1.0、Conversion Plan、Bake Result 或公开 Shader
property/reference 名称。详见[兼容性矩阵](../compatibility.md)。

仓库中采用 MIT 的代码继续适用 [MIT License](../../LICENSE)；带有其他 SPDX 声明的
文件（包括 Blender Bake Worker）继续适用各自条款。手册中的五张角色渲染图是单独
管理的文档素材，仅供非商业学习和文档参考，禁止用于任何商业用途。相关角色、
设计及知识产权归各自权利人所有；Miku 不授予任何游戏资产使用权。PNG 随源码文档
跟踪，不适用 Miku 的 MIT 许可，也不进入可安装 ZIP/TGZ。详见
[第三方声明](../../THIRD_PARTY_NOTICES.md)和
[文档图片来源记录](../provenance/documentation-images.md)。

## 开发

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 tools/release/build_release.py --output-dir artifacts
```

贡献或分发构建前，请阅读 [CONTRIBUTING.md](../../CONTRIBUTING.md)、
[SECURITY.md](../../SECURITY.md) 和 [SUPPORT.md](../../SUPPORT.md)。
