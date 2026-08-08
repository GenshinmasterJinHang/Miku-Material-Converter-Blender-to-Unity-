# Miku 2.2.12

本次候选构建修复 Source Mesh PBR 的 Height 绑定：当原始 Height 资源已被最终
烘焙通道取代时，纹理仍保留用于来源追踪，但不会再要求不存在的
`_MIKU_HeightMap`。真正可达的 Height/位移仍执行严格的缺失属性检查。

Miku 2.2.12 修复旧版 Unity 6 因 URP 17.5.4 强制依赖而无法安装的问题，同时不再
猜测未来技术线的兼容性。本仓库生成一个 Unity TGZ 和一个 Blender ZIP，并将通过
人工门禁发布为公开的 GitHub Release `v2.2.12`。

## 发布文件

- `miku_shader_converter-2.2.12.zip`
- `com.miku.shaderconverter-2.2.12.tgz`
- `SHA256SUMS.txt`

最终确定性 SHA-256：

- ZIP：`3344a2e7fc93e08412b6929511bdd26d814309cc5f2ce834864f63db1cb518c4`
- TGZ：`9d03d5b0cac5da6dbfa81e7f3b34f12a26de1fc226607353bb8fd173f2fe971d`

Blender 通过“扩展 > 从磁盘安装”选择 ZIP；Unity 通过 Package Manager 的
“Add package from tarball”选择同一个 TGZ。

## 兼容策略

- Blender 支持 5.0–5.2，Windows 代表版本为 5.0.1、5.1.2、5.2.0。区间内未
  记录的补丁会警告并执行 `bpy` 能力预检；5.3+ 明确拒绝。
- Unity 6000.0–6000.5 分别使用 Shader Graph 17.0–17.5 显式适配器。Unity
  6000.N 必须对应 URP/Shader Graph 17.N，且两个包的精确版本必须相同。
- 稳定 `f`/`p` 补丁在能力预检后放行；Alpha、Beta、RC、6000.6+ 和 17.6+
  会在资产写入前拒绝。
- 无警告认证目标为 Blender 5.2.0、Unity 6000.5.7f1、URP/Shader Graph
  17.5.4。本次只正式验证 Windows；实际执行状态以兼容矩阵为准。

Unity 六条新增技术线依据 Unity 官方包清单与 Shader Graph 17.0–17.5 文档
实现。按发布负责人最新要求，不在本机额外安装这些 Unity 编辑器；只对现有
6000.4.5f1 / 17.4.0 执行最终 TGZ 回归。因此其余精确 Unity 行保持
Experimental，不表述为已经完成运行时验证。

Unity 包清单使用最低版本 `unity: 6000.0` 与 URP `17.0.0`。UPM 不支持依赖范围，
因此项目必须直接锁定与编辑器技术线匹配的 URP 和 Shader Graph。

## 安全与兼容影响

版本错配通过 `MIKU_UNITY_PACKAGE_VERSION_MISMATCH` 在任何资产事务前失败。
Shader Graph 预检覆盖属性、节点、端口、连接、Custom Function、序列化、全部表面
输出，以及五类包装图的导入和哈希。未知 17.x 小版本不再回落到最高已知适配器。

MaterialIR、Bundle、Bake Request 1.2、Bake Result 等 Schema 均不升级。稳定 ID、
公开 Shader 属性名、Blender 操作器、用户包装图所有权和 Full Regeneration 规则
保持不变。2.2.12 Target Profile 哈希会更新，导入器继续接受 2.2.11 Profile。

## 已记录验证

- Python/Core：262 项测试通过；Ruff 与 release 检查配置通过。
- Blender 5.0.1、5.1.2、5.2.0：最终 ZIP 的安装、UI、Standard PBR、
  Bake Worker、TARGA 转换、稳定身份和确定性验证全部通过；三者的规范化
  IR SHA-256 均为
  `bf20e49c08b960ce8bd6945445850723c4d35ce990718c90f410a2a5a6da9c97`。
- Unity 6000.4.5f1 / URP 与 Shader Graph 17.4.0：最终 TGZ 共执行 218 项
  EditMode 测试，216 项通过、0 项失败、2 项跳过，并覆盖 Source Mesh
  Height 的已取代资源与真实可达属性回归。
- 两次独立构建的 ZIP、TGZ 与 `SHA256SUMS.txt` 字节完全一致。
