# Unity 包安装与最小流程

已验证组合为 Unity 6000.4.5f1、URP 17.4.0、Shader Graph 17.4.0。

## 使用 TGZ

1. 获取 `com.miku.shaderconverter-0.11.0.tgz`。
2. 打开 **Window > Package Manager**。
3. 选择 **Add package from tarball**。
4. 选中 TGZ，等待 Unity 完成脚本编译。

## 从源码安装

在 Package Manager 中选择 **Add package from disk**，然后选择：

```text
unity/Packages/com.miku.shaderconverter/package.json
```

## 最小流程

1. 把 Blender 导出的 `.b2ubundle`、同级 `Materials/` 文件夹和可选 `.glb`
   一起放入 Unity `Assets/`。
2. 等待脚本化导入器生成材质、`.shadergraph`、`.generated.shadersubgraph`
   和诊断报告。
3. 检查每个 Bundle 的 Import Report。
4. 对 `Approximate`、`Baked` 或 `RequiresProjectSetup` 材质进行人工预览。

阻塞错误表示该材质没有安全生成，不能忽略。玻璃/折射报告如果要求 Opaque
Texture，需要在 URP 项目设置中启用后再审核。
