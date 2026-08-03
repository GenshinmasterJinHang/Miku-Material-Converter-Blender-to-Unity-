# Miku 2.2.8

Miku 将 Blender 5.2 EEVEE 材质转换为目标无关的 MaterialIR 2.0，再将确定性的、
可继续编辑的 Standard PBR Shader Graph 资源导入 Unity 6 URP。游戏专用卡通材质
仍可在 Unity 侧创建，但不再从 Blender 导出面板中选择。

![Miku 转换流程](../images/miku-workflow-zh-cn.svg)

[English README](../../README.md) · [中文完整手册](manual.md) ·
[English Manual](../manual.md)

## Miku 提供什么

- Blender 导出器：唯一可见的 Standard PBR 路线，支持可编辑语义转换、明确标注的
  近似，以及必要时的隔离贴图烘焙。
- Unity 导入器：确定性的 `.mikubundle` 导入、可编辑 Shader Graph 包装图、Miku
  管理的 Sub Graph、稳定 ID 和结构化报告。
- Unity 游戏卡通材质创建器：在 `Miku > Game Toon > Materials > Create Material`
  中为原神、鸣潮、崩坏：星穹铁道和明日方舟：终末地显式填写贴图。
- `Miku > Settings` 下独立的 Unity 界面语言设置，可选择 English 或简体中文；
  设置按用户保存，不进入生成资产。

## 安装已发布版本

1. 从 [v2.2.8 Release](https://github.com/GenshinmasterJinHang/Miku-Material-Converter-Blender-to-Unity-/releases/tag/v2.2.8)
   下载 `miku_shader_converter-2.2.8.zip` 和
   `com.miku.shaderconverter-2.2.8.tgz`。
2. 在 Blender 5.2 打开 **编辑 > 偏好设置 > 扩展**，选择 **从磁盘安装**，选中
   Blender ZIP 并启用 Miku。
3. 在 Unity 6000.4.5f1 打开 **Window > Package Manager > + > Add package from
   tarball**，选择 Unity TGZ。
4. 从 Blender 导出材质 Bundle，并将完整 Bundle 目录复制到 Unity 项目的
   `Assets/` 下。

源码安装时，在 Package Manager 中选择 **Add package from disk**，并指向
`unity/Packages/com.miku.shaderconverter/package.json`。完整的安装、资产所有权、
诊断和升级说明见[中文手册](manual.md)。

## 五分钟流程

1. 在 Blender Shader Editor 中选中带材质的对象。
2. 打开 **Miku** 侧栏，选择输出目录并确认 **Standard PBR** 路线。
3. 导出当前材质。若有效链路包含 `Input.Time.*`，导出会在任何输出或烘焙请求写入
   前停止；断开的时间节点不影响导出。
4. 将生成的 `.mikubundle` 完整目录复制到 Unity `Assets/`，等待导入器生成可编辑
   图和报告。
5. 要创建游戏材质时，使用 Unity 游戏卡通材质创建器，选择工作流和部位，显式填写
   可见贴图字段，然后保存为用户拥有的 `.mat`。

![Blender Standard PBR 面板](../images/blender-standard-pbr-zh-cn.png)

## 兼容性

已验证 Windows 组合为 Blender 5.2.0、Unity 6000.4.5f1、URP 17.4.0 和 Shader
Graph 17.4.0。项目当前状态为 **Experimental（实验性）**；不支持的版本组合必须
明确失败。详细格式和工作流说明见[兼容性矩阵](../compatibility.md)。

2.2.8 不修改 MaterialIR 2.0、Bundle 1.0、Conversion Plan、Bake Result 或公开
Shader property/reference 名称。历史 Bundle 仍可导入，但当前 Blender 界面不会新建
游戏工作流或含时间依赖的 Bundle。

## 开发

```powershell
py -3.13 -m unittest discover -s tests -p "test_*.py"
py -3.13 tools/ci/run_checks.py --profile pr
py -3.13 tools/release/build_release.py --output-dir artifacts
```

精确的 Blender/Unity 验证命令、截图来源、Schema 规则、安全说明和贡献规则见
[中文手册](manual.md)、[CONTRIBUTING.md](../../CONTRIBUTING.md)、
[SECURITY.md](../../SECURITY.md) 和 [SUPPORT.md](../../SUPPORT.md)。
