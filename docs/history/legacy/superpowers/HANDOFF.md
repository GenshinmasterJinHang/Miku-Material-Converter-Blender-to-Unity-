# 交接说明 — B2U NPR Shader Architecture

> 写于: 2026-07-21(上一会话末尾)
> 给下一会话的 Claude / 你本人用
>
> **写这份文档的原因**: 上一会话在 `.mcp.json` 用 stdio(错误)配置过 Unity MCP,
> Unity MCP 实际运行在 HTTP 8080。这个会话已经把 `.mcp.json` 改成 HTTP,
> 但 **Claude Code 工具列表是会话启动时锁定的**,必须完全退出 + 重新启动
> Claude Code 才能加载 `mcp__unity__*` 工具。Phase 6.2–6.5 视觉迭代因此被推到你
> 下一个会话。

---

## 1. 当前状态(已落地,23 个 commit)

```
748cdd1 test(delta-e): CI gate that fails when any material exceeds ΔE threshold
3f53a38 feat(delta-e): 16 Cycles refs for all Yvonne materials + 11 mgir v2 fixtures + regex fix
ed3e2df feat(delta-e): Cycles refs for 3 Yvonne materials + absolute-path fix in capture_cycles_ref
1f3ff1e feat(delta-e): batch + capture_cycles_ref + render_unity stub
44d5421 feat(delta-e): compare.py with heatmap + percentile summary
a1b7150 feat(delta-e): sRGB<->Lab conversions + CIEDE2000 vectorized
de5aefc feat(blender): generic_toon_bake emits schemaVersion=2 + nprFeatures
de5a750 feat(blender): npr_feature_detector for node-graph-driven mgir v2 features
178468b fix(codegen): drain stderr in B2UDirectiveRunner to surface Python errors
d78f8e5 feat(codegen): C# writer invokes Python directive builder for .mgir v2
b189af1 feat(codegen): Python directive builder + snapshot tests
77aadec feat(npr): NPR_MultiLayerSpec include (ramp-based two-lobe spec)
cd5381a feat(npr): NPR_AnisoSpec include (Ward anisotropic)
c65b560 feat(npr): NPR_SkinSSS include with back-light translucency)
0439b2a feat(npr): NPR_AlphaBlend include for Opaque/Cutout/Transparent/Fade
048df89 feat(npr): NPR_ColorRemap include for LUT-based color remapping
dbf9d39 feat(npr): NPR_MaskLayer include for RGBA channel picking
96e83bc feat(npr): add NPR_Common.hlsl with remap and saturate helpers
9eb5631 test: add 4 more Yvonne mgir v2 fixtures + parametrized validation
995fc25 feat(schemas): validator + v1 compat detection
dada035 feat(schemas): define .mgir v2 JSON Schema with nprFeatures
7fefb7d test: add failing tests for .mgir v2 schema validator
1f377ab chore: initial repo state + Unity/Python .gitignore
```

### 落地的能力

| 能力 | 位置 |
|---|---|
| `.mgir v2` schema(带 `nprFeatures`) | `schemas/mgir_v2.json` |
| Schema 验证器 | `schemas/validate_mgir.py` |
| 16 个 Yvonne 材质的 `.mgir v2` fixture | `tests/fixtures/mgir_v2/yvonne_*.json` |
| 21 个 schema 单元测试 + 1 个 fixture 校验 | `tests/test_mgir_v2_schema.py` |
| 7 个 NPR HLSL include(`NPR_Common` / `MaskLayer` / `ColorRemap` / `AlphaBlend` / `SkinSSS` / `AnisoSpec` / `MultiLayerSpec`) | `unity/Packages/com.migr.shaderconverter/Runtime/NPR/` |
| Python directive builder + 6 个 snapshot 测试 | `codegen/directive_builder.py` + `tests/test_codegen_snapshots.py` |
| C# writer 调用 Python 子进程 + stderr 排空 | `unity/.../Editor/B2UDirectiveRunner.cs` + `B2UEndfieldMaterialWriter.cs` |
| Goo-side NPR 特征检测器 | `b2u_mvp_blender/npr_feature_detector.py` |
| Goo 端 `__init__.py` 在导出循环里注入 `schemaVersion=2 + nprFeatures` | `b2u_mvp_blender/__init__.py:241(_inject_mgir_v2_fields)` |
| 完整 ΔE2000 工具链 | `tools/delta_e_tool/{cie_lab,compare,batch,capture_cycles_ref,render_unity}.py` |
| 16 张 Yvonne Cycles 参考 PNG(覆盖所有材质) | `tools/delta_e_tool/references/yvonne/` |
| CI 闸门测试(目前因无 URP 渲染跳过) | `tests/test_delta_e_gate.py` |

### 测试状态

```
$ python -m unittest discover -s tests
Ran 255 tests in 53.241s
OK (skipped=21)
```

21 个 skipped 全部是 `tests/test_delta_e_gate.DeltaEGateTests`,因为
`tools/delta_e_tool/renders/urp/` 不存在。

---

## 2. `.mcp.json` 已经修了

`unityproject/.mcp.json`:

```json
{
  "mcpServers": {
    "unityMCP": {
      "url": "http://127.0.0.1:8080",
      "type": "http"
    }
  }
}
```

(原 stdio 配置错误,用户已确认 Unity MCP 运行在 HTTP 8080。)

**但是 — 关键警告**: Claude Code 工具列表是会话启动时锁定的。
当前会话虽然已经把 `.mcp.json` 改对了,工具列表却仍没有 `mcp__unity__*`。
需要:

1. **完全退出当前 Claude Code 进程**
2. **重新启动 Claude Code**(同一个项目根 `c:/Users/22687/Desktop/项目4/`)
3. 启动后输入 `/mcp`,看到 `unityMCP` 状态是 connected,工具列表出现
   `mcp__unity__*_*`(具体名字取决于 unity-mcp-skill 版本)
4. 然后让新会话接续

如果重启后 `/mcp` 显示 `disconnected` 或 `failed`,检查:
- Unity Editor 是否在运行且 Window → MCP for Unity 窗口显示 "Running"
- `curl http://127.0.0.1:8080/health` 是否返回 `{"status":"healthy",...}`
- 防火墙/代理是否阻塞 localhost:8080

---

## 3. 重启后,接续该做什么

按优先级:

### P1 — 立即验证状态不退化(2 分钟)

```bash
cd "c:/Users/22687/Desktop/项目4"
git status                     # 应该干净
git log --oneline | head -5    # 应该看到 748cdd1
python -m unittest discover -s tests
# 期望: Ran 255 tests ... OK (skipped=21)
```

如果不通过,跑具体失败子集,不要全局重跑。

### P2 — Phase 6.2: URP 渲染(需要 MCP)

对每个材质(共 16 个):

```python
# 由新会话里的 Claude 驱动 mcp__unity__* 工具
1. mcp__unity__Material_Assign(...)       # 把材质挂到 sphere primitive
2. mcp__unity__Camera_RenderToPNG({size: 512x512, path: ".../renders/urp/<material>.png"})
3. 重复 16 次
```

URP 渲染输出应该放到:
```
c:/Users/22687/Desktop/项目4/tools/delta_e_tool/renders/urp/M_actor_yvonne_<material>.png
```

⚠ **命名要严格对齐**: 渲染的 PNG 文件名必须和 `references/yvonne/` 下的 PNG stem 一致
(`M_actor_yvonne_cloth_01.png` ↔ `M_actor_yvonne_cloth_01.png`)。否则 batch.py 跳过。
如果你用 `M_actor_yvonne_cloth_01.mat.png` 这种加后缀名,要么改文件、要么改 batch.py。

### P3 — Phase 6.3: 首次 ΔE 跑批

```bash
cd "c:/Users/22687/Desktop/项目4"
python -m tools.delta_e_tool.batch \
  --urp-dir    tools/delta_e_tool/renders/urp \
  --cycles-dir tools/delta_e_tool/references/yvonne \
  --out-dir    tools/delta_e_tool/baseline_v1
cat tools/delta_e_tool/baseline_v1/summary.md
```

期望: 大部分材料 ❌(因为当前 shader 是简化模板,大部分差距会很大)。
热图存到 `baseline_v1/<material>_heatmap.png`,这是诊断依据。

### P4 — Phase 6.3–6.5: 视觉迭代(人工驱动)

热图会显示:
- 衣物网格状伪影(Phase 2 的颜色重映射修复)
- 半透明袖子(Phase 2 的 AlphaBlend)
- 头发高光各向异性(Phase 2 的 AnisoSpec)
- 皮肤次表面(Phase 2 的 SkinSSS)

每次迭代:
1. 读热图 + 上一轮 ΔE 数据
2. 改一个参数(可能改 `codegen/directive_builder.py`、改 HLSL、调材质 Properties)
3. 提交,推回 P2 重渲受影响材质
4. 跑 P3 看新数据

**不要盲改**。每个材质至少 3 个迭代点:颜色、几何/边缘、Alpha/Blend。

### P5 — Phase 6.5 + 8: 闸门接入

每个材质 mean ΔE < 3.0 且 p99 < 8.0 之后:

```bash
python -m unittest tests.test_delta_e_gate -v
# 期望: 21 个 skipped 变成 OK,无 fail
```

闸门失败就回 P4。

---

## 4. Phase-boundary 已知项(下一会话需要处理)

### A. T3.2.I2 — C# wiring 处于休眠状态

`B2UEndfieldMaterialWriter.cs:30` 把 `graph` 通过 `JObject.FromObject(graph)` 序列化喂给
Python,但 `B2UMgirGraph` 类(`B2UModels.cs:9`)没有 `schemaVersion` 或 `nprFeatures` 字段。

Python `directive_builder.py` 的 v2 闸门:

```python
if not isinstance(mgir, dict) or mgir.get("schemaVersion", 1) < 2:
    return []
```

所以现在 `B2UDirectiveRunner.Run(...)` 永远返回空 list,**Phase 6 视觉迭代必须修这个**。

修复方式 (任选):
- (a) 给 `B2UMgirGraph` 加 `schemaVersion` 和 `nprFeatures` 字段(Unity 域代码)
- (b) 改 writer 走别的路径:不依赖 `B2UMgirGraph`,直接读 `.mgir v2` JSON 文件
- (c) 在 writer 里构造一个临时 dict `{schemaVersion: 2, nprFeatures: detect_via_bpy_walk(material)}`(最小改动)

我推荐 (c):改动最小、足够支持迭代,等到真正生产路径再做 (a)。

### B. 已知 minor 项(可忽略,也可批量清理)

- `schemas/mgir_v2.json`、`schemas/validate_mgir.py`、`codegen/directive_builder.py`、
  `tests/snapshots/yvonne_cloth_01_directives.json` 缺尾换行(虽然 Python 解析无影响)。
  如果你打算加 `.editorconfig` 强制 `eol-last`,一次性 `printf '\n' >> file` 即可。
- `B2UDirectiveRunner._pendingMaterialProps` 是 `static`,并发场景不安全(但 edit-time 同步调用,实际无影响)。
- `B2UEndfieldMaterialWriter.cs` 的 patch 偏离了 brief 原意 —— brief 假设 StringBuilder emitter,
  实际 writer 是 Material mutator。偏差已在 patch 注释中说明。

### C. 不在本次范围

- HDRP / Built-in 渲染管线
- 移动端 URP 变体策略
- 实时 Cycles 预览(off-line Cycles 是真值)
- 14+ 个材质之外的新角色

---

## 5. 给新会话的 Claude 的速查

### 项目根

```
c:/Users/22687/Desktop/项目4/
├── docs/superpowers/
│   ├── specs/2026-07-20-b2u-npr-shader-architecture-design.md  # 设计 spec
│   ├── plans/2026-07-20-b2u-npr-shader-architecture.md          # 实施 plan
│   └── HANDOFF.md                                              # ← 你正在读
├── schemas/                  # .mgir v2 schema + 验证器
├── codegen/                  # Python directive builder
├── b2u_mvp_blender/          # Goo-side 导出器
├── unity/Packages/com.migr.shaderconverter/
│   ├── Runtime/NPR/          # 7 个 NPR_*.hlsl
│   └── Editor/               # C# writer + 验证器 + B2UDirectiveRunner
├── unityproject/
│   ├── .mcp.json             # HTTP 8080 配置(已修)
│   └── ...
├── tests/                    # 21 个 schema 测试 + 5 个 detector 测试 + 6 个 snapshot 测试 + 4 个 ΔE 测试 + 2 个 CI 闸门测试
└── tools/delta_e_tool/       # ΔE2000 工具链
```

### 关键命令

```bash
# 全套测试
python -m unittest discover -s tests

# ΔE 跑批
python -m tools.delta_e_tool.batch \
  --urp-dir    tools/delta_e_tool/renders/urp \
  --cycles-dir tools/delta_e_tool/references/yvonne \
  --out-dir    tools/delta_e_tool/baseline_v1

# 单对比较(产出热图)
python -m tools.delta_e_tool.compare \
  --urp    path/to/urp.png \
  --cycles path/to/cycles.png \
  --out-heatmap /tmp/heat.png

# Cycles 参考捕获(重新跑)
python -m tools.delta_e_tool.capture_cycles_ref \
  --blender-exe "D:/Goo Engine 4.2/Goo-Engine 4.2/blender.exe" \
  --blend "陈_佩丽卡_莱万汀_伊冯_1.2.1_GooBlender4.2+(请阅读RM)_ By 新杨XIYAG/陈_佩丽卡_莱万汀_伊冯_1.2.1_GooBlender4.2+(请阅读RM)_ By 新杨XIYAG.blend" \
  --materials M_actor_yvonne_cloth_01 M_actor_yvonne_body_01 M_actor_yvonne_hair_01 \
  --out-dir "c:/Users/22687/Desktop/项目4/tools/delta_e_tool/references/yvonne"
```

### 测试预期

- 单元测试: 255 pass,21 skipped(`test_delta_e_gate`)
- ΔE 期望(每个材质):mean < 3.0, p99 < 8.0

---

## 6. 不需要做的事

- ❌ 重新设计 spec — 设计已批准,所有改动都在 spec 范围里
- ❌ 重新写 HLSL — 7 个 include 已经过最终 review
- ❌ 重做 schema — JSON Schema 已经过两次 review(初版 + regex 放宽版)
- ❌ 重新捕获 Cycles 参考 — 16 张已经覆盖所有 Yvonne 材质

---

## 7. 关键决定(回看时不要质疑)

| 决定 | 理由 |
|---|---|
| 模块化 Include + 分类模板 | 用户在 brainstorming 阶段明确选择 |
| `unittest` 替代 `pytest` | 环境的 pip proxy 阻塞,pytest 无法安装。代码本身不依赖 pytest 特性 |
| 7 个 fixture 在 Python directive builder 用 `if feature["enabled"]` | 让 v2 schema 简洁 |
| `B2UDirectiveRunner.Run()` 用 subprocess(不是 in-process) | 跨语言边界、Unity 进程内 import python 容易出 Unity Editor 崩溃 |
| `_pendingMaterialProps` static 而非 instance | writer 实际是 static class,这是必要的 adaption |
| 不用 JSON Schema Draft 2020-12 之前的版本 | Python `jsonschema` 4.x 要求 2019-09 或更新的 draft |

---

交接结束。如果有疑问,先读 `docs/superpowers/specs/2026-07-20-b2u-npr-shader-architecture-design.md` 和 `docs/superpowers/plans/2026-07-20-b2u-npr-shader-architecture.md`,这两个文件已经记录了所有的设计意图和为什么这么做。