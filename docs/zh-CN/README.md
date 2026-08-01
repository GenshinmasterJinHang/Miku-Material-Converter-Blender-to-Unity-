# Miku — Blender EEVEE 材质转 Unity Shader Graph

Miku 0.11.0 用于把 Blender 5.2 的 EEVEE 材质节点转换成 Unity 6 URP
中可继续编辑的 Shader Graph。

```text
Blender 5.2 EEVEE 节点
        ↓
与目标平台无关的 Miku 3.0
        ↓
Unity 6000.4 / URP 17.4 / Shader Graph 17.4
        ↓
可编辑 .shadergraph + Miku 管理的 .generated.shadersubgraph
```

转换器按“可编辑节点图 → 明确标注的近似 → 必要通道烘焙”的顺序工作。对
必需链路中的未知节点，不会静默替换成黑色、白色、零值或直通值。

[English README](../../README.md) ·
[完整节点矩阵](../node-support-matrix.md) ·
[兼容性矩阵](../compatibility.md)

## 已验证版本

| 组件 | 已验证版本 | 状态 |
| --- | --- | --- |
| Miku Blender 插件 | 0.11.0 | 发布候选版 |
| Blender | 5.2.0 LTS，构建 `fbe6228777e7` | Windows 11 已测试 |
| Miku 交换格式 | `miku-3.0`、`b2u-bundle-1.2`；Exact 为 `miku-5.0`、`b2u-bundle-2.0` | Exact 尚未通过 73/73 门禁 |
| Miku Unity 包 | 0.11.0 | 发布候选版 |
| Unity Editor | 6000.4.5f1 | Windows 11 已测试 |
| Universal Render Pipeline | 17.4.0 | 已测试 |
| Shader Graph | 17.4.0 | 版本专用后端 |

节点支持结论以 Blender 官方 `blender-v5.2-release` 文档分支的
[`e74f0a2b`](https://projects.blender.org/blender/blender-manual/commit/e74f0a2b4c5475fe8bc50434d869b07ea7adfa4f)
提交以及 Blender 5.2.0 LTS 中真实创建节点的结果为依据。Blender 官方列出的
[EEVEE 节点限制](https://docs.blender.org/manual/en/5.2/render/eevee/limitations/nodes_support.html)
仍然成立。

## 当前范围

- 材质输出按 EEVEE 优先选择：先选启用的 `EEVEE` 输出，再选 `ALL` 输出。
  如果必需表面链只连接到 Cycles 专用输出，则明确报错。
- 当前只做表面材质。原理化体积、体积吸收、体积散射和体积系数暂不实现。
- 普通材质生成可编辑 Shader Graph；已有的游戏专用预设继续作为独立兼容后端。
- 公开格式中不再包含 Cycles 光学管线或动态特效协议。由于 Blender 的材质烘焙
  API 通过 Cycles 执行，插件在确实需要通道烘焙时会在隔离的临时数据中短暂使用
  Cycles。它只是内部求值器：插件会恢复场景状态，也不会把 Cycles 着色语义写入
  Miku。

## 程序化纹理是否都要烘焙？

不需要。

| 路线 | 节点 |
| --- | --- |
| 原生或等价的可编辑 Shader Graph | 棋盘格、全部渐变模式、静态平面图像、无扭曲波浪、受支持的环境纹理采样 |
| 可编辑近似，并可带烘焙对照分支 | 噪波和部分波浪配置 |
| 为保证外观，默认烘焙 | 砖墙、Gabor、迷幻、噪波、带扭曲波浪、沃罗诺伊、白噪波 |
| 方向查找纹理 | IES、天空，以及不能直接采样的环境纹理模式 |
| Blender 5.2 中不存在 | 点密度 |

空间程序纹理通常生成 Texture3D；依赖 UV 或网格的数据生成 Texture2D。若 4D
纹理的 `W` 输入是实时连接，转换器不会把它错误冻结成 3D 快照，而是保留带诊断
的可编辑运行时近似。每个节点的具体路线见
[纹理节点矩阵](../node-support-matrix.md#texture-nodes)。

## BSDF / 表面着色器支持

活动 EEVEE 表面链上的以下节点会被接受：

原理化 BSDF、漫射 BSDF、自发光、相加着色器、混合着色器、玻璃 BSDF、
光泽 BSDF、阻隔、金属 BSDF、折射 BSDF、高光 BSDF、次表面散射、卡通 BSDF、
半透明 BSDF、透明 BSDF、光泽层（Sheen）BSDF。

不同节点的转换质量不同：

- 原理化、漫射、自发光，以及常用数学、颜色、矢量、映射、法线和色带节点，
  优先生成可编辑的语义节点或展开后的 Shader Graph。
- 玻璃、折射、阻隔、SSS、半透明、Sheen、金属、卡通和任意闭包混合属于明确
  标注的 URP 近似。
- 粗糙度始终按 `smoothness = 1 - roughness` 转换，绝不直接接到 Smoothness。
- 必需链上的不支持设置会产生结构化诊断，不会静默丢弃。

Blender 官方文档本身也说明：Diffuse Roughness、若干各向异性/IOR 参数、Toon
和 Sheen 在 EEVEE 5.2 中不可用或不完整。因此 Miku 会尽量保留作者意图，但不
宣称达到 Cycles 光线传输一致性。

## 安装

### Blender

1. 下载 `b2u-blender-to-unity-0.11.0.zip`。
2. 在 Blender 5.2 中打开 **编辑 > 偏好设置 > 插件**。
3. 选择 **从磁盘安装**，选中 ZIP，然后启用
   **B2U Blender to Unity Bridge**。

不要先把 ZIP 解压后再交给插件安装器。

### Unity

1. 下载 `com.miku.shaderconverter-0.11.0.tgz`。
2. 在 Unity 6000.4.5f1 中打开 **Window > Package Manager**。
3. 选择 **Add package from tarball** 并选中 TGZ。
4. 确认项目使用 URP 17.4.0 和 Shader Graph 17.4.0。

从源码安装时，选择 **Add package from disk**，并选中
`unity/Packages/com.miku.shaderconverter/package.json`。

## 最简导出流程

1. 在 Blender 中选中含节点材质的一个或多个对象。
2. 打开 Miku 材质面板并选择输出目录。
3. 执行导出。插件会按材质自动选择可编辑图、近似或通道烘焙。
4. 把导出的 `.b2ubundle`、`Materials/` 文件夹和可选 `.glb` 一起复制到
   Unity 项目的 `Assets/` 下。
5. Unity 自动导入材质、Shader Graph、绑定报告；只有烘焙资源与几何绑定时
   才会使用 GLB。

Blender 面板只保留真正会改变转换结果的选项，不再提供 FBX/模型格式选择：

- 没有生成资源依赖网格时，只导出材质。
- 任意烘焙资源依赖 UV 或几何时，只导出所需绑定对象的 GLB。

## 材质库验证结果

本次 Windows 验证从项目材质库中排除了“玻璃”“宝石”“魔法球”目录和名为
`玻璃雾岩` 的材质。

- 18/18 个 Blender 文件成功生成 Bundle。
- 253/253 个已分配材质完成导出。
- 阻塞性材质诊断为 0。
- 242 个材质因为生成资源依赖网格而要求绑定 GLB。
- Blender 5.2 中目标范围内的 16 种非体积表面闭包节点，以及纹理菜单中实际
  存在的 13 种纹理节点，都完成了真实构造与导出冒烟测试。

其中 6 个源文件已经完整写出 Bundle 与 GLB，但 Blender 5.2 在后台进程退出时
报告 `EXCEPTION_ACCESS_VIOLATION`。批处理将它们标记为
`completed-with-blender-exit-error` 并保留产物；插件不会保存或修改源
`.blend` 文件。

## 已知限制

- 体积着色器暂不支持。
- URP 无法复现 Blender 的完整 BSDF 光线传输；玻璃/折射基于屏幕或反射探针，
  SSS、半透明、Sheen、阻隔和卡通着色都是明确诊断的近似。
- 烘焙资源是导出时状态的快照；带动画的程序输入需要重新导出或另写运行时实现。
- Shader Graph 序列化目前只验证了 17.4.0。
- 当前操作系统证据仅覆盖 Windows 11。
- 游戏专用预设被保留，但不计入通用 EEVEE Shader Graph 支持范围。
- 仓库中存在不会进入发布包的第三方或受限素材，详见
  [第三方声明](../../THIRD_PARTY_NOTICES.md)。

## 开发与测试

```powershell
python -m unittest discover -s tests -p "test_*.py"
dotnet build tests/dotnet/B2U.ShaderConverter.Tests.csproj
blender --background --python tests/blender/all_surface_nodes_smoke.py
blender --background --python tests/blender/all_texture_nodes_smoke.py
python tools/release/build_release.py --output-dir dist
```

英文 [`README.md`](../../README.md) 是公开兼容性与发布行为的规范文档；中文
版本与其保持同步。安全、支持和许可证信息见
[SECURITY.md](../../SECURITY.md)、[SUPPORT.md](../../SUPPORT.md) 和
[GPL-2.0-or-later](../../LICENSE)。历史 MIT 授权保存在 [`LICENSES/MIT.txt`](../../LICENSES/MIT.txt)。
