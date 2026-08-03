# Miku 2.2.8

发布日期：2026-08-03

- Blender 当前材质面板仅显示 Standard PBR；当前导出固定写入
  `standard_pbr`，但保留旧属性、显式底层工作流调用和历史 Bundle 兼容，且不改写
  `.blend` 中已有属性。
- Unity 菜单改为 `Miku > Game Toon > Materials > Create Material`。向导支持原神、鸣潮、
  HSR 和 Endfield，按工作流过滤 22 个部位，按 Shader 声明顺序显示公开 `Texture2D`
  输入；除 Endfield Mouth 外 `_BaseMap` 必填，鸣潮 Body 的 ID / Stockings Map 同时绑定
  `_IDMap` 与 `_StockingsMap`。
- 新增中英双语 README、Manual、工作流图、界面图片、可复现构建脚本和文档回归测试。
- Unity `Miku/Settings` 提供按用户保存的 English / 简体中文界面设置；它不改变 Unity
  全局语言，也不写入生成资产。
- 有效输出链使用 `Input.Time.*` 时，Blender 在写入输出或烘焙请求前以
  `MIKU_TIME_INPUT_UNSUPPORTED` 失败；断开的时间节点仍可存在。

MaterialIR 2.0、Bundle 1.0、Conversion Plan、烘焙结果和 Unity Shader 属性/引用名不变。

验证版本：Blender 5.2.0、Unity 6000.4.5f1、URP 17.4.0、Shader Graph 17.4.0（Windows）。

最终 ZIP/TGZ 哈希见同目录的 `miku-2.2.8-sha256.txt`。
