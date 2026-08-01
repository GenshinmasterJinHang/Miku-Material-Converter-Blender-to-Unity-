# Blender 5.2 扩展安装

Miku 2.1.0 使用 Blender 扩展 ZIP，不需要、也不允许手动修改 Blender 的
Scripts 目录。仓库中的规范源码是 `miku/`、`miku_blender/` 和
`extensions/`，`dist/` 与 Blender 已安装目录都只是构建产物。

## 安装

1. 保存并关闭所有 Blender GUI 窗口。存在未保存文件或正在运行的
   Blender 时，禁止覆盖扩展。
2. 在 Blender 5.2 中打开 **编辑 → 偏好设置 → 扩展**。
3. 选择 **从磁盘安装**，安装
   `miku_semantic_exporter-2.1.0.zip`。
4. 仅当材质确实需要烘焙时，再安装
   `miku_gpl_bake_worker-1.2.0.zip`。
5. 不要解压 ZIP，也不要把仓库源码复制到 Blender Scripts 目录。

本仓库的本机验证只允许直接调用：

```text
C:\SteamLibrary\steamapps\common\Blender\blender.exe
```

每次启动、headless 测试、扩展安装和导出前都必须确认
`bpy.app.version == (5, 2, 0)`。不得使用 `PATH`、Steam launcher、
`.tools`、Program Files 或其他 Blender 副本作为回退。

## 使用

1. 打开 Shader Editor，并显示右侧 Miku 面板。
2. 选择物体和要导出的活动材质槽。
3. 设置 `Output Folder` 和材质 `Workflow`。
4. 需要 View/Camera/Time 动态效果时，在 **Advanced** 中选择
   **Add Miku Time Node**，或连接 Blender 的 Camera Data、Geometry
   Incoming、Fresnel、Layer Weight 节点。
5. 点击 **Export Current Material**。

Miku Time v1 输出 Seconds、Frame、Sine 和 Cosine。Unity 默认使用运行时
Time，并可通过 `_MIKU_EffectTimeScale`、`_MIKU_EffectTimeOffset`、
`_MIKU_EffectTimeOverride` 与 `_MIKU_EffectUseTimeOverride` 暂停、跳转
或同步。

支持的 View、Camera、Time、Fresnel 与 Layer Weight 链会报告
`MIKU_RUNTIME_INPUT_PRESERVED`，不会再进入整材质 UV 烘焙。Light Path
仍不属于这一精确支持范围。

输出是以 `.migrbundle` 为入口的完整目录。导入 Unity 时复制整个目录，
不要只复制入口文件，也不要将其改名为 `.json`。

## 从源码构建

```powershell
python tools/build_miku_blender_extensions.py
```

构建结果位于 `dist/`。完整范围见[节点支持矩阵](../node-support-matrix.md)。
