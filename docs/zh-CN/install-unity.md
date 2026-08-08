# Unity 包安装

认证参考组合为 Unity `6000.5.7f1`、URP `17.5.4`、Shader Graph `17.5.4`。
正式技术线为 Unity 6000.0–6000.5，分别对应 URP/Shader Graph 17.0–17.5；
URP 与 Shader Graph 的精确版本必须相同。区间内未记录的稳定补丁会显示警告并
执行完整能力预检。6000.6+、17.6+ 和预发布版本会在写入资产前拒绝。通过
Package Manager 安装单一
`com.miku.shaderconverter-2.2.12.tgz`，或从
`unity/Packages/com.miku.shaderconverter/package.json` 添加本地包。

将完整 `.mikubundle` 目录放入 `Assets/`。Standard PBR 生成 Shader Graph；
Genshin、WuWa、HSR 使用 Game Toon 材质与共享 Screen Rim 工具。

升级前备份项目。包不会删除 `Assets/` 中的旧 Generic Toon 材质、recipe 或 wrapper，
但旧材质可能显示 Missing Shader；请按迁移文档手动重新选择工作流并绑定。

## Miku 独立语言设置

在 Unity 菜单打开 `Miku/Settings`，可以选择 English 或简体中文。设置保存为
当前用户的 EditorPrefs（`com.miku.shaderconverter.editorLanguage`），不写入项目，
也不跟随 Unity Editor 的语言设置；菜单路径、Shader 属性名、诊断代码和 JSON 内容
仍保持英文稳定。
