# Miku 2.2.9

Miku 2.2.9 取消严格兼容区间内由补丁版本差异造成的安装和运行阻断。

- Blender 允许 5.0.0 至 5.2.0；5.2.0 是唯一已验证版本。
- Unity 允许 6000.0.0f1 至 6000.4.5f1，URP 与 Shader Graph 允许
  17.0.0 至 17.4.0。
- 区间内较低版本会显示“未经完整验证”诊断并继续运行；高于当前严格上限的版本
  会在写入资产前失败。
- 新的 `miku-bake-request-1.2` 记录实际 Blender 版本和构建哈希；旧 1.0/1.1
  请求仍只按认证的 Blender 5.2.0 构建执行。

发布文件为 `miku_shader_converter-2.2.9.zip`、
`com.miku.shaderconverter-2.2.9.tgz` 和 `SHA256SUMS.txt`。
