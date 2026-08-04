# Miku 2.0 安装与导出

Blender 扩展和 Unity 包版本均为 `2.0.0`。验证环境是 Blender 5.2.0、Unity
6000.4.5f1、URP 17.4.0、Shader Graph 17.4.0。安装和 headless 验证必须使用：

`C:\SteamLibrary\steamapps\common\Blender\blender.exe`

当前工作流只有 `standard_pbr`、`genshin_toon`、`wuwa_toon`、`hsr_toon`。
新导出写入 MaterialIR 2.0。旧 `generic_toon` 保存值会显示退休提示并阻止导出，
不会静默回退到 Standard PBR。

Unity 项目升级前请备份。包升级不会删除 `Assets/` 中的旧材质、recipe 或 wrapper；
Generic Toon 材质可能显示 Missing Shader。请为每个材质人工选择保留工作流，重新导出、
导入并绑定，详见 `docs/migrations/retire-generic-toon-2.0.md`。
