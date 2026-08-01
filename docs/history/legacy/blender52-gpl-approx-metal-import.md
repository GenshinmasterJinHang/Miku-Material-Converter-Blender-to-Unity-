# Blender 5.2 GPL Custom Function 与金属材质批量导入

## Purpose and outcome

将 MiGR 的普通非严格材质导入从 Noise/Voronoi/White Noise 等节点默认烘焙，改为优先使用带来源记录的 Blender 5.2 GPL Custom Function 近似实现；失败时允许显式、可追踪的烘焙回退。最终使用 Blender 5.2.0 LTS 导入 `材质库/金属` 中 5 个 `.blend` 的 73 个已分配材质到 Unity 6000.4.5f1、URP/Shader Graph 17.4.0，并生成 5 个独立审阅场景。

## Context and constraints

- Blender 可执行文件：`C:/SteamLibrary/steamapps/common/Blender/blender.exe`。
- 测试集：`材质库/金属`，只纳入已分配材质；已核对数量为 8/12/19/14/20，共 73 个，排除未分配的 `Dots Stroke`。
- Unity 目标工程：`C:/Users/22687/Desktop/unity/test`；目标包路径必须使用工作区内 `unity/Packages/com.migr.shaderconverter`。
- Unity MCP 已运行在本地 8080；所有 Unity 资产导入、刷新、测试和场景生成通过 MCP 完成。
- 现有工作树有大量用户改动。不得 reset、checkout、清理或覆盖无关改动；旧 `MetalPhase1` 资产保持不变。
- Blender 来源锁定为 5.2.0 LTS，提交 `fbe6228777e7d9afefcd61a413844e790ae75db7`。GPU/EEVEE 实现是语义主来源，安装目录中的 Cycles 源码仅作补充，不可把补充代码伪称为 EEVEE 原实现。
- 全项目许可证迁移由维护者明确批准为 `GPL-2.0-or-later`；历史 MIT 版权和 `LICENSES/MIT.txt` 保留。

## Progress

- [x] 完成需求访谈：普通工作流优先近似、10 类 Custom Function、73 个材质、隔离 Unity 目录、5 个审阅场景、失败允许回退。
- [x] 核对 Blender 5.2.0、73 个材质及当前 Unity 工程/包版本。
- [x] 发现 Unity `Packages/manifest.json` 中本地包路径乱码；`packages-lock.json` 已有正确路径。
- [ ] 完成 GPL 来源审计和 10 类 HLSL。
- [ ] 完成 exporter、Unity Shader Graph 注册表、诊断与回退。
- [ ] 执行 Python/Blender/Unity 测试。
- [ ] 导入 73 个材质、创建 5 个场景和截图。
- [ ] 更新本节、Results and follow-up 及最终批次报告。

## Discoveries

- 现有 `b2u_mvp/hybrid_material_plan.py` 将 Noise、Voronoi、White Noise、Magic、Gabor、Wave 默认标为 Baked；需要改变普通非严格策略，但 Strict Exact/Parity 仍须拒绝 Approximate。
- 现有 Shader Graph 17 后端对 Custom Function 的 translation 硬编码为 `Exact`，且部分 HLSL 缺少维度包装、输出变体或 include；这些是本次必须修正的真实缺陷。
- 现有 HLSL 文件把 clean-room MIT 标记与 Blender 语义混用；新文件必须有 Blender 上游路径、提交和哈希。
- 当前 Unity 工程 `manifest.json` 指向乱码路径 `C:/Users/22687/Desktop/椤圭洰4/...`，会妨碍刷新；目标包版本为 Shader Graph 17.4.0。

## Decision log

1. **普通导入策略**：`Auto` 默认 `PureCF + Approximate`；Explicit Bake 仍烘焙；Strict Exact/Parity 不接受近似。这样保留严格审计语义，同时改变普通工作流的默认质量。
2. **移植范围**：重写 Math、VectorMath、ColorRamp、Noise、Voronoi、Wave，并新增 WhiteNoise、Brick、Magic、Gabor，共 10 类 Custom Function。
3. **失败处理**：导出时保留 bake fallback；Unity 预检或编译失败时最多重试一次，按节点或材质回退并发出结构化诊断，禁止静默全量烘焙。
4. **资产隔离**：输出到 `Assets/MiGRReview/MetalGPLApprox/`，不覆盖 `MetalPhase1` 或用户 wrapper graph。
5. **兼容策略**：不改变 MGIR schema 版本；新导出只写语义路由，旧文件中的历史 Unity 路由字段继续兼容读取。Unity 包和 Blender add-on 版本统一到 0.13.0。
6. **验收门槛**：73/73 材质无错误；10 类节点各有独立 Unity Custom Function 实测；允许的烘焙回退必须逐项报告。

## Implementation sequence

1. **许可证与来源**
   - 更新根 LICENSE、Unity package metadata、third-party notices、许可证审计、Blender source lock、节点来源文档、兼容矩阵、README/CHANGELOG。
   - 对 10 个 HLSL 文件添加 GPL SPDX、Blender Authors、MiGR 修改声明及固定提交/路径/哈希。

2. **HLSL 与 exporter**
   - 以固定 Blender 5.2 GPU/EEVEE 源码为主来源，生成 Shader Graph `_float` 入口和完整端口 ABI；显式处理 1D/2D/3D/4D 及已有节点模式。
   - 普通导出为 10 类节点生成 `Approximate/PureCF` 质量并保留 bake fallback；严格模式继续阻断 Approximate。
   - 保证语义 IR 不出现 Unity 内部类名、槽号、资源路径或随机 ID。

3. **Unity 后端**
   - 在 Shader Graph 17.4 后端增加 Blender 5.2 Custom Function registry，把 op/参数/维度映射到 HLSL、函数和端口 ABI。
   - 反射桥只使用 registry 描述；修正 `Exact` 硬编码，生成 `Approximate` 报告。
   - 增加 HLSL/函数/端口预检、编译失败诊断和一次性 bake fallback；回退原因写入 mapping/report。

4. **批量导出和 Unity 导入**
   - 将现有材质库脚本参数化为 Blender、LibraryRoot、UnityProject、OutputRoot；只扫描 5 个源 `.blend` 的 73 个已分配材质。
   - 原子写入隔离目录中的 MGIR、纹理、材质、shadergraph、subgraph、mapping、report。
   - 修复目标工程 manifest 的本地包路径，使用 Unity MCP refresh/import/run-tests/read-console。

5. **审阅场景与证据**
   - 为彩色金属、金、铁、铜、银分别创建场景；使用球体网格、相机、方向光和内置文本标签。
   - 保存场景和 PNG 截图，生成批次 manifest，列出每个材质的 Custom Function/回退/错误统计。

## Validation

- Python：运行现有 `tools/ci/run_checks.py` 以及新增/更新的 exporter、HLSL、hybrid-plan、许可证和确定性测试。
- Blender：使用固定 Blender 5.2.0 无界面运行 10 类节点夹具和 5 个材质库导出；验证源 `.blend` 哈希未改变、输出数量为 73。
- Unity EditMode：验证 10 类 Custom Function 节点的 HLSL GUID、函数名、精度和端口；验证 Strict 拒绝、Explicit Bake、预检失败回退、编译失败回退和无 fallback 报错。
- Determinism：相同输入生成两次，比较 MGIR、mapping、report 和语义 Shader Graph 输出；无关节点 ID 不变。
- MCP 批次验收：73/73 生成 `.mat`、wrapper `.shadergraph`、generated `.shadersubgraph` 和报告；5 个场景可打开并渲染，无粉色材质和本批次 shader 编译错误。
- UnityConnect token 异常若仍存在，单独标记为既有环境问题，不计入本批次失败。

## Results and follow-up

## Execution addendum (2026-07-26)

- Blender validation: Blender 5.2.0 LTS, commit `fbe6228777e7d9afefcd61a413844e790ae75db7`; 5/5 source files exported, 73/73 assigned materials discovered.
- Unity validation: Unity 6000.4.5f1, URP/Shader Graph 17.4.0, project `C:/Users/22687/Desktop/unity/test`.
- Import result: 56 generated Shader Graph materials plus 17 explicit URP Lit fallback materials; every scene renderer has a non-null material and a non-error shader. The fallback list and real asset paths are recorded in `Assets/MiGRReview/MetalGPLApprox/Fallback/fallback-summary.json`.
- The apparent empty materials were caused by two independent issues: generated graphs that stopped on critical `MIGR_SG_PORT_NOT_FOUND`/`MIGR_SG_UNSUPPORTED_OPERATION` diagnostics, and Custom Function ABI mismatches (output ordering and compact Noise overloads). The first group remains explicit fallback by design; the second group was repaired for Brick, Magic, Noise, Gabor, White Noise, and HLSL include metadata.
- A final distance-only Voronoi ABI overload was added after Unity exposed `MiGR_VoronoiCompat3D_float: no matching 7 parameter function` in `Material_1_cdb5f099`; all five review scenes now report `null=0` and `shaderErr=0`.
- Unity EditMode tests: 40/40 passed (`b5cff2f96b51470f9c5cab34837b0dae`). Targeted Python tests: 90 passed. Full CI remains blocked by missing `ruff`/`jsonschema` and the pre-existing documentation trailing-newline check.
- Final verified screenshots were captured for all five scenes under `Assets/MiGRReview/MetalGPLApprox/Screenshots/*_final_verified.png`.
- The Codex CLI portion of `grill-me-codex` was attempted but blocked by Windows `Access Denied`; plan hardening and implementation continued locally.

本节只在实际执行后更新。必须区分 `passed`、`failed`、`implemented but not executed` 和 `blocked`，记录真实命令和 Unity MCP 返回结果。若某些 Blender 5.2 节点模式只能烘焙，保留近似/回退诊断，不把它们标记为 Exact；严格一致性工作仍由既有 exact parity 计划负责。
