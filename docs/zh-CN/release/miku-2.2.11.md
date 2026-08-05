# Miku 2.2.11

Miku 2.2.11 将严格的闭区间版本校验改为“仅校验大版本一致”的策略。

- Blender 允许任意 5.x；5.2.0 是唯一认证（无警告）版本。
- Unity 允许任意 6000.x（Unity 6），URP 与 Shader Graph 允许任意 17.x。
- 认证参考组合为 Blender 5.2.0、Unity 6000.5.4f1、URP 17.5.4 和 Shader Graph
  17.5.4；同大版本但非认证版本会显示“未经完整验证”诊断并继续运行。
- 大版本不符（如 Blender 4.x/6.x、Unity 5.x/7.x、URP 16.x/18.x）会在写入任何
  资产前失败。
- Unity 包清单最低要求升至 `unity: 6000.5`、URP 17.5.4；低于这些版本时
  Package Manager 不会安装该包。
- Shader Graph 会为 17.0–17.6 选择显式适配器，更高的小版本会回落到最高已知
  适配器，同时保持生成资产的身份命名空间稳定。

发布文件为 `miku_shader_converter-2.2.11.zip`、
`com.miku.shaderconverter-2.2.11.tgz` 和 `SHA256SUMS.txt`。
