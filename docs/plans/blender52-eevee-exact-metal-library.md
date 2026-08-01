# Blender 5.2 EEVEE Exact 金属材质库执行记录

## Purpose and outcome

将 `材质库/金属` 中 5 个 Blender 5.2.0 文件的 73 个已绑定材质转换为
Unity 6000.4.5f1、URP/Shader Graph 17.4.0 可验证的 Exact/no-bake 资产。
Equivalent、Approximate、Conditional、Baked、Deferred 和 Unsupported 都不
得进入正式 Unity 项目。

## Context and constraints

- 源文件保持只读；5 个未绑定的 `Dots Stroke` 数据块不属于本批导入范围。
- 目标环境固定为 Blender 5.2.0 LTS、Unity 6000.4.5f1、URP/Shader Graph
  17.4.0、Windows DX12 Linear。
- 旧 `mgir-3/4 + AUTO_HYBRID` 路径仍服务历史兼容；本计划新增的严格门禁
  不得把旧近似结果重新标记为 Exact。
- 主项目工作区已有大量用户改动，不执行 reset、clean 或批量覆盖。

## Progress

- [x] 2026-07-26：完成 `grill-me-codex` Act 1/Act 2，审查结论 PASS。
- [x] 2026-07-26：新增 `b2u_mvp.exact_contract`，严格拒绝旧版本、烘焙、条件节点、近似诊断和未验证纹理路线。
- [x] 2026-07-26：新增只读 Blender 审计器和五文件批处理器。
- [x] 2026-07-26：5/5 文件打开成功，73/73 已绑定材质解析成功，0/73 Exact，73/73 阻断。
- [x] 2026-07-26：批处理返回 `importAllowed=false`；未执行 Unity 材质导入。
- [x] 2026-07-26：Unity 6000.4.5f1 资产刷新和脚本编译完成；无本次新增 C# 编译错误。
- [x] 2026-07-26：Unity EditMode `B2UExactContractTests` 3/3 通过。
- [x] 2026-07-26：Python 全量测试 380 项执行，377 通过、3 个既有兼容性断言失败、44 跳过。
- [ ] 完成 mgir-5.0 闭集节点合同、闭包 ABI、URP 17.4 Custom Target/Pass 合同和全节点 oracle。
- [ ] 在独立 Unity 认证项目通过最坏闭包、采样、Pass、Player Build 和显存压力测试。
- [ ] 73/73 重新审计为 Exact/no-bake 后，执行 Unity 两阶段事务导入和人工目测。

## Discoveries

本次真实 Blender 审计输出在：

- `outputs/material-library-exact-audit-20260726/summary.json`
- 同目录下每个 `.blend` 对应的 JSON 报告和 Blender 日志。

当前阻断证据包括：

- 所有文档仍为 `mgir-4.0`，严格合同要求 `mgir-5.0`。
- 多数材质选择 `BAKED_PBR`，并报告 `requiresTextureBake=true`。
- `Texture.Noise`、`Texture.Voronoi`、`Texture.Magic`、`Vector.Bump`、
  `Shader.Mix`、`Shader.AnisotropicBSDF` 等进入 conditional/baked 路线。
- `MIGR_EEVEE_GLOSSY_ANISOTROPY_IGNORED`、`surface_ir_limited`、
  `normal_passthrough` 等不能通过 Exact 门禁。
- 73 个记录的汇总阻断证据为：`exact_schema_version_required=73`、
  `conditional_operation=73`、`material_texture_bake_required=69`、
  `exact_backend_required=69`、`non_exact_texture_route=114`、
  `non_exact_diagnostic=113`。所有记录仍为 `mgir-4.0`；69 个选择了
  `BAKED_PBR`，其余 4 个虽选择 `NODE_GRAPH` 也仍受版本、条件节点和非 Exact
  诊断阻断。

## Decision log

- 根项目许可证已改为 MIT；历史 MIT 文本保存在
  `LICENSES/MIT.txt`。第三方 Unity/Blender/模型/纹理权利不随根许可证转移。
- 不为了让批处理“成功”而把烘焙或近似路线改名为 Exact。
- 只有全节点合同和 73/73 Exact/no-bake 通过后，才允许连接 Unity MCP 执行正式写入。

## Implementation sequence

1. 固化 Blender 5.2 EEVEE 节点清单、模式/端口/空间/阶段和源码哈希。
2. 完成 mgir-5.0、闭包 ABI、Shader Graph 17.4 版本适配和运行时 Pass 合同。
3. 将 Unity 导入器接入批次门禁与崩溃可恢复的两阶段事务。
4. 在临时认证 Unity 项目中通过全节点 oracle、确定性、图像阈值和旧预设回归。
5. 重新执行本审计；只有 `exactCount=73` 且 `blockedCount=0` 才提交正式项目。

## Validation

已执行：

- `python -m unittest tests.test_exact_contract -v`：4/4 通过。
- `python -m unittest tests.test_exact_contract tests.test_exact_schema tests.test_eevee_semantics tests.test_mgir4_contract tests.test_mgir_v3_schema -v`：17/17 通过。
- Unity MCP EditMode：`B2U.ShaderConverter.Editor.Tests.B2UExactContractTests`：3/3 通过。
- `python -m unittest discover -s tests -p 'test_*.py'`：380 项执行，377 通过、3 失败、44 跳过。失败为既有 0.11.0/0.12.0 版本断言和旧静态路由断言；不是新增 Exact 门禁失败。
- Blender 5.2 五文件只读批审计：5/5 进程退出码 0，73/73 材质解析成功，
  0/73 Exact，73/73 阻断。

尚未执行：正式 Unity 导入、Shader Graph 17.4 目标图生成、DX12 Player Build、渲染差异和人工目测；
原因是 Exact/no-bake 前置门禁尚未通过，继续导入会违反本计划。

## Results and follow-up

当前结果是“审计完成、导入安全阻断”，不是材质导入完成。下一步必须完成
mgir-5.0 全节点 Exact 实现和对应 Unity 后端；在此之前不得生成或提交 73 个正式
Unity 材质资产。
