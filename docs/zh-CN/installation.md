# Miku 2.2.9 安装与导出

Blender 扩展和 Unity 包版本均为 `2.2.9`。允许的严格版本区间是：

- Blender 5.0.0 至 5.2.0；
- Unity 6000.0.0f1 至 6000.4.5f1；
- URP 与 Shader Graph 17.0.0 至 17.4.0。

已验证环境仍是 Blender 5.2.0、Unity 6000.4.5f1、URP 17.4.0 和 Shader Graph
17.4.0。其他区间内版本会显示未经完整验证诊断并继续运行；高于或低于区间的版本
会被拒绝。本仓库的 Blender headless 验证固定使用：

`C:\SteamLibrary\steamapps\common\Blender\blender.exe`

当前公开 Blender 工作流只导出 `standard_pbr`，新文件使用 MaterialIR 2.0。旧
`generic_toon` 保存值会显示退役提示并阻止导出，不会静默回退到 Standard PBR。

Unity 项目升级前请备份。包升级不会删除 `Assets/` 中的旧材质、recipe 或 wrapper；
旧 Generic Toon 材质可能显示 Missing Shader，需要按迁移文档手动处理。
