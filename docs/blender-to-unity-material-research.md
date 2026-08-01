# Blender 材质导入 Unity：方案调研与 Miku 融合结论

调研时间：2026-07-21 至 2026-07-22

## 结论

不存在一种通用交换格式可以无损表达任意 Blender Shader Editor 节点图并
直接生成 Unity URP Shader Graph。可维护的工程方案是分层处理：标准 PBR
材质走语义映射，能实时执行的节点图走 Miku 目标中立 IR，超出实时能力的
内容走受诊断约束的烘焙，并始终保留源节点身份和诊断信息。

Miku 2.2.0 的策略为：

1. `SEMANTIC_PBR`：将 Principled/PBR 语义映射到 URP PBR 模板。
2. `NODE_GRAPH`：保留 Blender DAG，在 Unity Shader Graph 17.4 后端中生成
   可编辑的 Sub Graph 和包装图。
3. `BAKED_PBR`：对 Cycles 或实时能力之外的图烘焙 Base Color、Metalness、
   Roughness、Normal、Emission 和 Alpha，并在报告中记录质量。
4. `SOURCE_MATERIAL`：只作为故障保护和审计来源，不作为隐藏的运行时回退。

## 外部方案的边界

- Unity Model Importer 适合 FBX 标准材质槽和属性传递，但不能执行任意
  Blender 节点图。
- Blender glTF 2.0 适合网格、纹理和标准 PBR 载荷；超出 glTF PBR 的逻辑需要
  Miku IR 或烘焙结果。
- Khronos UnityGLTF 的 `KHR_materials_*` 扩展覆盖更多材质属性，但不等价于
  Blender Cycles 行为，也不能取代节点语义验证。
- 节点图导入项目证明了 JSON/DAG、拓扑排序和不支持节点诊断的价值；Miku
  使用独立 schema、强类型端口、坐标空间和 Shader Graph 版本适配器实现这些
  能力，不复制第三方 GPL HLSL。

## Miku 设计要求

- 节点组按实例路径展开，源节点和源 socket 身份稳定且可追踪。
- 标量、向量、颜色、纹理、坐标空间和 Shader stage 必须显式保留，禁止隐式
  把 Object space 转成 World space。
- Roughness 必须转换为 `smoothness = 1 - roughness`；透明、裁剪、阴影和深度
  行为必须保持诊断可见。
- 生成资产采用确定性 ID 和稳定排序；Miku 拥有生成的 Sub Graph，用户拥有包装图。
- 不支持的关键节点必须报错；近似或烘焙必须注明视觉/语义差异。

## 验证范围

当前兼容性矩阵以 Blender 5.2.0、Unity 6000.4.5f1、URP/Shader Graph 17.4.0
为目标，状态仍为 `Experimental`。实际支持以 `docs/node-support-matrix.md`、
schema 校验、Blender smoke 和一次性 Unity EditMode runner 的结果为准。
