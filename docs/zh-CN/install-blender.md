# Blender 5.0 至 5.2 扩展安装

安装 `miku_shader_converter-2.2.9.zip`。支持 Blender 5.0.0 至 5.2.0；5.0/5.1
会显示未经完整验证警告，5.2.1 及更高版本不会放行。不要手动复制到 Blender
Scripts 目录。
所有本地验证必须使用：

`C:\SteamLibrary\steamapps\common\Blender\blender.exe`

扩展导出 `standard_pbr`、`genshin_toon`、`wuwa_toon`、`hsr_toon`，新文件使用
MaterialIR 2.0。旧 `generic_toon` 值会返回
`MIKU_WORKFLOW_RETIRED:generic_toon`，不会自动迁移。

插件界面会跟随 Blender 的界面语言自动使用英文或简体中文。在着色器编辑器的
Miku 面板中展开“高级”，可选择烘焙贴图质量：低（512）、标准（1024，默认）、
高（2048）或超高（4096）。该设置仅影响实际生成的二维烘焙贴图；不需要烘焙的
材质不会因此生成新贴图。4096 档会显著增加烘焙时间和内存占用。

2.2.8 起，Advanced 面板不再显示“添加 Miku 时间节点”和“迁移旧版标识”。
新导出遇到有效输出链中的时间依赖会在写入输出前以
`MIKU_TIME_INPUT_UNSUPPORTED` 失败；断开的时间节点仍可存在。旧 MiGR 数据和
旧含时间 Bundle 继续支持静默读取。
