# Miku 2.2.11 安装与导出

Blender 扩展和 Unity 包版本均为 `2.2.11`。允许的版本为（仅校验大版本一致）：

- Blender 5.x；
- Unity 6（6000.x）；
- URP 与 Shader Graph 17.x。

认证参考环境为 Blender 5.2.0、Unity 6000.5.4f1、URP 17.5.4 和 Shader Graph
17.5.4。其他同大版本版本会显示未经完整验证诊断并继续运行；大版本不符的版本
会被拒绝。本仓库的 Blender headless 验证固定使用：

`C:\SteamLibrary\steamapps\common\Blender\blender.exe`

当前公开 Blender 工作流只导出 `standard_pbr`，新文件使用 MaterialIR 2.0。旧
`generic_toon` 保存值会显示退役提示并阻止导出，不会静默回退到 Standard PBR。

Unity 项目升级前请备份。包升级不会删除 `Assets/` 中的旧材质、recipe 或 wrapper；
旧 Generic Toon 材质可能显示 Missing Shader，需要按迁移文档手动处理。
