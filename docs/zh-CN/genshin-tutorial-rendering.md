# Miku 2.4.0 原神教程光照

Miku 2.4.0 依据用户提供的教程与本机固定参考提交，独立实现原神材质的
LightMap AO、半兰伯特、昼夜 Ramp、高光、金属 MatCap、五色描边和 Alpha
语义。参考仓库没有许可证，因此 Miku 不复制或发布其中的 Shader、脚本、
FBX、贴图、材质、场景、`.meta`、截图或派生 Mesh。

Body/Hair 的 NormalMap 使用原始 Mikk tangent；UV7 TangentSpaceV2 只用于描边。
`_DiffuseA` 仍是唯一序列化 Alpha 模式：0=None、1=Cutout、
2=DiffuseAlphaEmission。颜色、背面、描边、阴影、深度、DepthNormals 与角色
Mask 使用同一个裁剪函数。

在 Unity 中先打开 **Miku > Game Toon > Rendering > Game Toon Renderer Feature
Installer**。安装器会为所有活动 Universal Renderer Data 安装 Geometry 与
Screen Rim Feature。Geometry Feature 在 Opaque 后依次绘制
`MikuGenshinBackface` 和 `MikuToonOutline`；背面按材质通过
`_UseUv1Backface` 开启，描边宽度读取顶点色 G。

Unity 包不再附带芙宁娜场景创建器、私有模型/贴图 Fixture 或其他本机验收构建器；
第三方验收输入与生成场景必须保留在包和公开仓库之外。Windows GPU 验收必须使用
`-force-d3d12`，不能带 `-nographics`，并断言
`GraphicsDeviceType.Direct3D12`；Null Device 结果不能作为 GPU 兼容证据。
