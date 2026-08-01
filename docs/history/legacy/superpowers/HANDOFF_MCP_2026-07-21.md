# MCP 阶段验收报告 — 2026-07-21

> 接续 `HANDOFF.md`(7f0d7c1 之前的 23 个 commit)
> 本会话完成的 MCP 验收 + 基线测量

---

## 0. 状态

| 项 | 状态 | 备注 |
|---|---|---|
| `.mcp.json` | HTTP 127.0.0.1:8080 ✓ | `mcp__unity__*` 工具仍需 Claude Code 完整重启才能加载 |
| `mcpforunity://editor/state` | `is_compiling=false` `ready_for_tools=true` | Unity 6000.4.5f1,SampleScene 加载,idle |
| Console 错误 | 1 条 Exception | `UnityConnectWebRequestException`(Token Exchange,良性,Unity 服务未登录) |
| Shader 编译 | 0 错误 | — |
| C# 编译 | 0 错误 | — |
| Python 测试 | 274 pass, 21 skipped(ΔE 闸门) | `python -m unittest discover -s tests` |

---

## 1. 场景层级(SampleScene)

9 个根对象,9 个 active:

```
Main Camera            active=True    (重新启用)
Directional Light      active=True
Global Volume          active=True
伊冯 (Yvonne)          active=True    ← 目标角色
布洛尼亚 (Bronya)      active=True
菲比渲染 (Phoebe)      active=True
胡桃 (Hu Tao)          active=True
Wuwa Toon Background   active=True
B2U_DeltaE_Cam         active=True
```

### Yvonne 层级(11 个子节点)

| instanceID | name | 组件 | mat_count | material | active |
|---|---|---|---|---|---|
| -2664 | Root | T | — | — | ✓ |
| -3728 | S_actor_yvonne_body_01_lod0 | T+SMR | 2 | body_01_Accessory / body_02_Accessory | ✓ |
| -3734 | S_actor_yvonne_cloth_01_lod0 | T+SMR | 1 | cloth_01_Cloth | ✓ |
| -3740 | S_actor_yvonne_cloth_02_lod0 | T+SMR | 1 | cloth_02_Cloth | ✓ |
| -3746 | S_actor_yvonne_cloth_03_lod0 | T+SMR | 1 | cloth_03_Cloth | ✓ |
| -3752 | S_actor_yvonne_cloth_04_lod0 | T+SMR | 1 | cloth_04_Cloth | ✓ |
| -3758 | S_actor_yvonne_eyeshadow_01_lod0 | T+SMR | 1 | eyeshadow_common_01_001 | ✓ |
| -3764 | S_actor_yvonne_face_01_lod0 | T+SMR | 2 | face_01_Face / brow_01_Accessory | ✓ |
| **-3770** | **S_actor_yvonne_face_01_lod0.001** | T+SMR | 1 | **iris_01_Eye** | **✓ (modifier 重复)** |
| -3776 | S_actor_yvonne_hair_01_lod0 | T+SMR | 1 | hair_01_Hair | ✓ |
| -3782 | S_actor_yvonne_hairshadow_01_lod0 | T+SMR | 1 | hairshadow_common_03 (URP/Lit) | ✗ (inactive) |

**所有 Renderer 材质槽非空 ✓**

### 重复 modifier 网格(已确认)

`S_actor_yvonne_face_01_lod0.001` 携带 `iris_01_Eye` 材质,这是 Blender 端 modifier 导出残留
(face + iris 在原始 .blend 是同一网格 + 不同 vertex group,FBX `use_mesh_modifiers=true` 会把
modifier 应用后的几何拆成 `.001` 命名)。

需要按 step 1 修复 `b2u_mvp_blender/__init__.py:_export_model`,关掉会导致重复的 modifier
求值。

---

## 2. 渲染(整角色 / 透明背景 / 512×512)

实施流程:
1. 创建 `B2U_DeltaE_RT_512` RenderTexture(512×512 ARGB32)
2. 绑定到 `B2U_DeltaE_Cam.targetTexture`
3. `camera.transform.LookAt(yvonne.bounds.center)`,按 bounds.max.z + dist 放
   (vFov=26°,aspect=1,distH / distW 取 max × 1.05 padding)
4. 临时 `SetActive(false)` 其他角色(Bronya/Phoebe/HuTao/Wuwa BG/Main Camera)
5. `Camera.Render()` → `Texture2D.ReadPixels` → `EncodeToPNG`
6. 恢复场景

输出:
- `unityproject/Assets/Screenshots/yvonne_urp_512.png`(58,121 字节,512×512 RGBA)
- 拷贝到 `tools/delta_e_tool/renders/urp_combined/yvonne_full.png`

### 视觉观察

| 元素 | URP 当前 | Cycles 参考 |
|---|---|---|
| 头发(双马尾 + 角) | 粉红,无高光,无 aniso | 粉红,亮高光,分层 specular |
| 衣服粉色袖子 | 实色,不透明 | 半透明,可见 "X" 缝线,内部轮廓 |
| 上衣黑色胸衣 | 灰黑实色 | 黑色 + 白色薄纱,胸带纹理 |
| 白色长裤 | 纯白,无纹理 | 白色 + 黑色 thigh 渐变 + 织带纹理 |
| 黑色腰带 | 实色 | 黑色 + 金属扣 + 紫色扣带 |
| 鞋子(黑色 + 粉) | 实色 | 黑色 + 红色高光,鞋底有金属感 |
| 尾巴 | 粉 + 黑色尖 | 粉 + 黑色尖,半透明(从衣服下面透出) |

---

## 3. ΔE 评估(基线)

参考(临时使用):`tools/delta_e_tool/references/yvonne_combined/yvonne_full.png`
(用户指定为「误捕获的暗色 Cycles 图」,但目前是仓库里唯一可用的全角色 Cycles 真值)

```
$ python -m tools.delta_e_tool.batch \
    --urp    tools/delta_e_tool/renders/urp_combined/yvonne_full.png \
    --cycles tools/delta_e_tool/references/yvonne_combined/yvonne_full.png \
    --out-dir tools/delta_e_tool/baseline_v2_20260721 \
    --alpha-mask --align

| Material     | mean ΔE2000 | p99   | max   | Pass |
| yvonne_full  |       82.96 | 100.0 | 100.0 | ❌   |
```

| 指标 | 当前 | 目标 | 差距 |
|---|---|---|---|
| mean ΔE2000 | 82.96 | < 3.0 | -79.96 |
| p99 ΔE2000 | 100.00 | < 8.0 | -92.0 |
| pct_above_1.0 | 99.99% | — | — |
| pct_above_3.0 | 99.61% | — | — |
| pct_above_5.0 | 98.61% | — | — |
| pct_above_8.0 | 96.99% | — | — |
| mask | aligned_foreground | — | — |
| 前景像素 | 668,995 | — | — |

### 热图诊断

`tools/delta_e_tool/baseline_v2_20260721/yvonne_full_heatmap.png`:
- 整图几乎全红(ΔE > 8.0):URP 简化 shader 跟 Cycles NPR 差太大
- 裤腿/鞋部出现 0~3 的偏暗区域(URP 这部分接近实色白,刚好和 Cycles 类似)
- 头/胸部分差异最大:NPR 颜色重映射、半透明、aniso 都没有

### 解读

此基线数字含义:
- **mean 83 几乎满** = 现在 URP 跟 Cycles 在视觉上完全是两个东西 — 意料之中
  (step 4「补齐 NPR Shader 与材质接线」还没做)
- **透明背景 vs 实色背景** 也被算进前景外环,放大 ΔE
- **比例/位置** 因为 align 步骤已矫正,贡献有限
- 真实可比较的基线要等 step 1~4 完成后才有意义

---

## 4. 用户原计划 6 步 vs 当前进度

| 步 | 计划内容 | 当前状态 |
|---|---|---|
| 1 | 修 FBX 重复 modifier 导出 | **未做** — `-3770 face_01_lod0.001` 仍是 iris modifier 重复 |
| 2 | 用第二张 Blender 图替换全角色参考 | **未做** — 仍在用「误捕获」旧图,等用户提供新参考 |
| 3 | 真实 MGIR v2 完整字段 | 部分 — Goo 端 `_inject_mgir_v2_fields` 已写但 v2 字段可能不全;Unity `B2UMgirGraph` 还没 schemaVersion/nprFeatures |
| 4 | 补齐 NPR shader 接线 | **未做** — shaders 仍在 Endfield_{Body,Skin,Face,Hair,Eye}.shader 内固定 include 但 `B2UEndfieldMaterialWriter` 还在只缓存 `_pendingMaterialProps`,没有真正落到开关/数值 |
| 5 | 重生 + MCP 验收 | **本会话完成** |
| 6 | 数据驱动迭代 | 未开始 |

---

## 5. 接下来要做的(优先级)

### 必须先做(否则 ΔE 没法收敛)

1. **step 1 修 FBX**:`b2u_mvp_blender/__init__.py:_export_model` 显式设
   `bpy.ops.export_scene.fbx(use_mesh_modifiers=False)`(或仅对个别 modifier 设
   `use_mesh_modifiers_render=True`),并删掉场景里旧的 `S_actor_yvonne_face_01_lod0.001`
2. **step 2 替换参考**:等用户提供第二张 Blender 图(整角色)
3. **step 3 接 v2**:把 `B2UMgirGraph` 加 `schemaVersion`/`nprFeatures` 字段,或
   在 `B2UEndfieldMaterialWriter` 用 (c) 路径(临时 dict + bpy walk)
4. **step 4 真接线**:`B2UEndfieldMaterialWriter` 从「只缓存」改为「写 material props」
   (设 `_B2U_NPR_*_ENABLED` 浮点开关,以及颜色/alpha/specular)

### 顺序

按用户原计划「几何/轮廓 → 纹理与颜色 → 透明 → 高光/SSS」逐项迭代,每轮
只改一个可归因参数,跑 ΔE,记录 mean/p99。

### 闸门

mean ΔE2000 < 3.0 且 p99 < 8.0 同时满足才进闸门。
`python -m unittest tests.test_delta_e_gate` 应在 URP 渲染都齐后从 21 skipped 变 0 skipped。

---

## 6. 给下一会话的速查

### 关键文件

```
unityproject/.mcp.json                            # HTTP 8080
unityproject/Assets/Screenshots/yvonne_urp_512.png   # 512×512 整角色基线
tools/delta_e_tool/baseline_v2_20260721/         # ΔE 报告 + 热图
b2u_mvp_blender/__init__.py:_export_model          # step 1
unity/Packages/com.migr.shaderconverter/Editor/
  B2UEndfieldMaterialWriter.cs                    # step 4
  B2UModels.cs                                    # step 3
```

### MCP 工具(本会话已用过的)

- `mcpforunity://editor/state` — 编译状态
- `mcpforunity://scene/gameobject/{id}` — GameObject 元数据
- `tools/call` 内的:
  - `find_gameobjects` (by_name)
  - `manage_scene` (get_hierarchy)
  - `manage_camera` (screenshot)
  - `manage_components` (注意:本 Unity 版本只有 add/remove/set_property,无 get)
  - `read_console` (get / types=[error,warning])
  - `execute_code` (action=execute, code=...)  — 最灵活,直接执行 C# 编辑器代码
  - `resources/read` (mcpforunity://...)

### 已知坑

- `mcp__unity__*` 工具因为 Claude Code 工具列表启动时锁定,即使 `.mcp.json` 配对了也不会
  自动出现;此会话一直走 `curl http://127.0.0.1:8080/mcp` + JSON-RPC 路径
- GameObject.Find("中文名") 在 execute_code 里返回 null(中文字符通过 JSON-RPC 转义后
  C# 编译器识别不出),需改用 `EditorUtility.InstanceIDToObject(iid)` 按 instanceID 找
- B2U_DeltaE_Cam 默认 solidColor + alpha=0 已经透明;但 Game 窗口分辨率 3840×2160,
  screenshot 默认抓 Game 窗口,需要给 camera 显式 `targetTexture = 512×512 RT` 才能拿到
  准确的 512×512 PNG
- 角色 Root 节点的 `childCount=11` 里有 1 个 Root(empty)+ 1 个 face_01.001(modifier
  duplicate)+ 1 个 hairshadow_01_lod0(inactive)+ 8 个正常 LOD0 网格
- 当前 URP shader 还没接 NPR 字段(NPR_ColorRemap / AlphaBlend / SkinSSS / AnisoSpec /
  MultiLayerSpec 都没启用),所以基线 ΔE ~83 几乎满;视觉差异主要在「粉色半透明袖子、
  头发高光、皮肤次表面、白裤腿渐变」上
