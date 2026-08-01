# Unity Bundle 最小集成样例

将本目录完整复制到 Unity 项目的 `Assets/` 下。glTFast 会导入 `Triangle.gltf`，B2U 会把 `Triangle.b2ubundle` 导入为可直接拖入场景的主对象，并用 `DemoMaterial.miku` 生成的可编辑 Standard PBR 材质替换 glTF 蓝色回退材质。

预期 `Triangle.b2ubundle` 子资源中包含：

- 一个带 MeshRenderer 的主 GameObject；
- 一个名为 `DemoMaterial` 的 B2U Material；
- 一个 `_ImportReport` TextAsset，且 `remappedSlots` 为 1、`fallbackSlots` 为 0。
