# Miku 2.2.11

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
- 在 `Miku > Settings` 中按用户选择 English 或简体中文，不修改项目资产。

## 内置 Game Toon Shader/HLSL 预设

Unity 包直接附带四套由 Miku 独立编写的实验性 Shader/HLSL 代码；它们是可运行的
着色器实现，不只是贴图字段模板。

| 预设 | 可用材质部位 | 主要功能 |
| --- | --- | --- |
| 原神（Genshin） | Body、Hair、Face、Eye | Light Map/Ramp 卡通光照、Face SDF、头发、眼睛、描边和屏幕边缘光支持 |
| 崩坏：星穹铁道（HSR） | Body、Hair、Face、Eye | Light Map/Ramp、Face SDF、头发高光、眼睛和描边支持 |
| 鸣潮（Wuwa） | Body、Hair、Face、Eye、Effect | ID/Stockings 双绑定、脸部基向量、眼睛专用贴图、高光和自发光 |
| 明日方舟：终末地（Endfield） | Body、Skin、Hair、Face、Eye、Mouth、Overlay、Effect、HairShadow | 参数/Ramp/LUT 贴图、Face SDF、头发 refine/shift/line、Overlay、Effect 和 HairShadow |

创建器合计提供 22 个有效材质部位。这些预设处于 **Experimental（实验性）** 状态，
不承诺与任何游戏逐像素一致。Miku 不包含从游戏提取的 Shader 源码、模型、贴图、Logo
或其他游戏资产。完整贴图规则、角色效果示例和 Unity 编辑器工具教程见
[中文手册](manual.md)。

## 安装发布版本

1. 从 [v2.2.11 Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.11)
   下载 `miku_shader_converter-2.2.11.zip` 和
   `com.miku.shaderconverter-2.2.11.tgz`。
2. 在 Blender 5.x 打开 **编辑 > 偏好设置 > 扩展**，选择 **从磁盘安装**，选中 ZIP
   并启用 Miku。
3. 在 Unity 6（6000.x）打开 **Window > Package Manager > + > Add package from
   tarball**，选择 TGZ。
4. 从 Blender 导出材质 Bundle，并把完整 Bundle 目录复制到 Unity 项目的
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

认证参考组合为 Blender 5.2.0、Unity 6000.5.4f1、URP 17.5.4 和
Shader Graph 17.5.4。任意 Blender 5.x、Unity 6（6000.x）、URP/Shader Graph
17.x 均可接受；非认证版本会显示验证警告。Miku 当前为 **Experimental（实验性）**。
2.2.11 不修改
MaterialIR 2.0、Bundle 1.0、Conversion Plan、Bake Result 或公开 Shader
property/reference 名称。详见[兼容性矩阵](../compatibility.md)。

仓库中采用 MIT 的代码继续适用 [MIT License](../../LICENSE)；带有其他 SPDX 声明的
文件（包括 Blender Bake Worker）继续适用各自条款。手册中的四张角色渲染图是单独
管理的文档素材，仅供非商业学习和文档参考，禁止用于任何商业用途。相关角色、
设计及知识产权归各自权利人所有；Miku 不授予任何游戏资产使用权。详见
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
