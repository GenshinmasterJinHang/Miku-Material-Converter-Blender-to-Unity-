# Miku 2.2.8 Unity 独立双语与 Blender 时间策略 ExecPlan

## Purpose and outcome

Miku 2.2.8 将 Unity 编辑器界面提供为每个用户独立选择的 English / 简体中文，
并收紧 Blender 新导出边界：Advanced 面板不再显示时间节点和旧 MiGR 标识迁移
按钮；有效输出链包含时间依赖时，在任何输出文件、烘焙请求或暂存目录写入前明确
失败。旧 MiGR 数据、旧含时间 Bundle 和既有 MaterialIR/Bundle/Shader 公共接口继续
可读取。

## Context and constraints

- Canonical source roots are `miku/`, `miku_blender/`,
  `extensions/miku_shader_converter/`, and
  `unity/Packages/com.miku.shaderconverter/`; retired B2U roots、安装副本和
  `dist/` 源码副本不修改。
- 当前工作区在实施前已存在大量用户改动；只修改本任务相关文件，不使用 reset、
  checkout 或覆盖无关文件。
- 目标版本为 Blender 5.2.0、Unity 6000.4.5f1、URP/Shader Graph 17.4.0。
- Unity 语言偏好使用 `EditorPrefs`，值为 `en_US` 或 `zh_HANS`，默认英文；它不写入
  项目，不影响生成资产确定性。
- Unity 静态 `MenuItem` 路径保持英文稳定；打开后的 Miku 自绘界面、窗口、Inspector、
  ShaderGUI、对话框和友好状态文本按偏好翻译。品牌名、工作流/属性/引用名、诊断代码、
  结构化日志和 JSON 内容保持英文稳定。
- Blender 两个操作器 ID 和底层迁移/时间辅助函数继续保留并可由脚本调用，但操作器
  标记为内部且从 Advanced 面板移除；新 Blender 导出不提供时间重新启用开关。
- 版本统一提升到 2.2.8；不升级任何交换 schema 或烘焙协议。

## Progress

- [x] 2026-08-03：核对 canonical source markers、Unity/Blender 版本、现有 UI、
  时间 IR 链路、旧 MiGR 兼容入口和 dirty worktree。
- [x] 2026-08-03：确认语言开关为 per-user 独立设置，Unity 静态菜单保持英文。
- [x] 2026-08-03：实现 Blender 时间策略和入口隐藏，保留脚本/旧 Bundle 兼容。
- [x] 2026-08-03：实现 Unity 翻译目录、`Miku/Settings` 和自绘界面重绘。
- [x] 2026-08-03：更新 2.2.8 版本、文档、Changelog、测试和发布记录。
- [x] 2026-08-03：执行 Python/Blender/Unity 测试、确定性构建和哈希核对；
  因 GUI 可能有未保存工作，未覆盖安装目标。

## Discoveries

- 现有 Blender Advanced 在 `miku_blender/__init__.py` 绘制 `miku.add_time_node` 和
  `miku.migrate_legacy_identities`；两类操作器同时注册，底层迁移函数被单元测试和
  旧资产兼容路径使用。
- `export_material_bundle` 当前先创建输出根和 staging，再在
  `_export_material_bundle_to_directory` 中 snapshot/build IR；时间策略必须把 IR
  preflight 提前到文件系统写入之前。
- `build_material_ir` 已将可达时间节点降低为 `Input.Time.*`，而 Unity backend
  已支持旧含时间 Bundle；因此拒绝逻辑应放在 Blender exporter 边界，而不是删除
  Core/Unity 读取能力。
- Unity package 当前无 asmdef；由于语言开关独立于 Unity Editor locale，本实现采用
  package 内部翻译表和 `EditorPrefs`，不引入 Unity PO/Localization 依赖。

## Decision log

- 2026-08-03：仅隐藏 Blender 用户入口并保留底层兼容；避免旧脚本和旧资产失效。
- 2026-08-03：所有新 Blender 导出统一拒绝有效时间依赖；断开的时间节点允许，旧
  Bundle 仍由 Unity 读取。
- 2026-08-03：Unity 使用 `Miku/Settings` 下的独立语言选择，实时重绘已打开的
  Miku 编辑器窗口和 Inspector；设置按用户保存。
- 2026-08-03：不改变 MaterialIR、Bundle、Conversion Plan、烘焙协议、Shader
  property/reference names 或 JSON 诊断内容。

## Implementation sequence

1. 在 Blender exporter 中增加 reachable-time preflight，在 `root.mkdir`、staging、
   bake request 和 artifact write 前检查 IR 表达式；增加稳定诊断码和中英文友好消息。
2. 从 Advanced 移除两个按钮，将两个保留操作器标记为内部；更新 Blender UI/translation
   smoke 和 Python 回归测试。
3. 在 Unity Editor 中增加集中翻译服务、固定语言值、`EditorPrefs` 设置页、语言变更
   重绘，并将 package-authored custom windows、ShaderGUI、custom inspectors、按钮、
   help boxes、dialogs、undo/status 文本改为通过服务取文案。
4. 为需要用户编辑的 Miku 资产/组件补充不改序列化字段名的本地化 Inspector 绘制；
   保留 generated diagnostics/IDs 的英文稳定显示。
5. 将 Blender manifest、Unity package version 和文档更新到 2.2.8；增加 changelog、
   英文 canonical 文档、`docs/zh-CN/` 使用说明和兼容性记录。
6. 扩展 Python/Blender/Unity 测试，运行完整 checks，连续构建 Blender ZIP 与 Unity
   TGZ 并比较字节和 SHA-256。

## Validation

- Python：面板源码不再绘制两个按钮；操作器仍可通过 API 调用；旧 MiGR 迁移读取不变；
  直接时间节点、仿射 frame driver、各转换模式拒绝；断开时间节点允许；失败不创建
  输出根/staging/bake request。
- Blender：使用固定 `C:\SteamLibrary\steamapps\common\Blender\blender.exe`，
  先断言 `bpy.app.version == (5, 2, 0)`；分别验证 `en_US`/`zh_HANS` 面板和错误文本。
- Unity：使用 `C:\Program Files\Unity\Hub\Editor\6000.4.5f1\Editor\Unity.exe`
  的临时项目运行 EditMode；覆盖默认英文、持久化/非法值回退、翻译目录完整性、格式
  占位符、重绘、旧含时间 Bundle 导入和公开 Shader 属性稳定性。
- 构建：两次构建每种包，确认 ZIP/TGZ 字节一致并记录 SHA-256；若 GUI 有未保存工作，
  不覆盖安装目标并将安装验证标为未执行。

## Results and follow-up

完成后追加实际命令、通过/失败/未执行状态、生成物哈希、兼容性影响和已知限制。简体
中文是唯一非英文语言；Unity 静态顶部菜单和 Package Manager metadata 保持英文；
Blender 不提供重新启用新时间导出的 UI 或隐藏开关。

## Implementation results (2026-08-03)

- Blender exporter preflights lowered `Input.Time.*` expressions before the
  output root, staging directory, bake request, or bundle files are created.
  Direct time inputs and `TimeAffine` frame drivers fail with
  `MIKU_TIME_INPUT_UNSUPPORTED`; disconnected nodes remain exportable.
- The two legacy operators remain registered with `INTERNAL`/`UNDO` options for
  script compatibility, while the Advanced panel draws only the fork identity
  action. Legacy MiGR and historical time Bundle readers were not removed.
- Unity localization is implemented in `MikuEditorLocalization.cs`, with the
  `Miku/Settings` user preference, `EditorPrefs` persistence, invalid-value
  fallback, repaint, custom recipe inspector, ShaderGUI labels, package-authored
  windows, dialogs, help boxes, Undo labels, and status messages.
- Python regression: `python -m unittest discover -s tests -p "test_*.py"` —
  245 tests passed. PR checks passed with 11 schemas and 89 Python files.
- Blender 5.2.0 smoke passed for `en_US`/`zh_HANS`, hidden buttons, direct and
  frame-driven time rejection across conversion modes, disconnected time, and
  pre-write output safety.
- Unity 6000.4.5f1 EditMode passed: 163 total, 161 passed, 0 failed, 2 skipped.
- Deterministic builds passed twice for each package. SHA-256 values are in
  `docs/release/miku-2.2.8-sha256.txt`.
- Installation verification was not run: existing Blender/Unity GUI processes
  were left untouched, as required for unsaved-work safety.
