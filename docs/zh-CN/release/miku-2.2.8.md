# Miku 2.2.8

发布日期：2026-08-03

- Unity 在 `Miku/Settings` 提供独立的 English / 简体中文编辑器界面开关，
  按用户保存，不跟随 Unity Editor 语言，也不写入生成资产。
- Blender Advanced 不再显示时间节点和旧版标识迁移按钮，但保留操作器 ID 与旧数据
  兼容读取。
- Blender 新导出在有效输出链使用 `Input.Time.*` 时，在写入输出或烘焙请求前以
  `MIKU_TIME_INPUT_UNSUPPORTED` 失败；断开的时间节点仍可存在，旧 MiGR/含时间 Bundle
  仍可导入 Unity。

MaterialIR、Bundle、Conversion Plan、烘焙结果和 Unity Shader 公共接口不变。

验证版本：Blender 5.2.0、Unity 6000.4.5f1、URP 17.4.0、Shader Graph 17.4.0（Windows）。
