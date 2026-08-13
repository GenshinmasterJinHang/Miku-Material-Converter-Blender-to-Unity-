# Documentation image provenance

This record covers the public UI captures and character-render examples used by
the Miku documentation. The two UI captures retain their approved 2026-08-03
source bytes. The five character renders were captured again on 2026-08-13 at
their final 1920x1080 size; no crop, rescale, or recompression was applied after
Unity wrote the PNGs.

## UI captures

| Source filename | Repository path | Subject | SHA-256 |
| --- | --- | --- | --- |
| `codex-clipboard-7257066c-eaf6-420d-ae61-c1a7fb1d0438.png` | `docs/images/blender-standard-pbr-en.png` | Blender 5.2.0 Standard PBR export panel | `24aa37f677bf9e51d1d35259b8e284e144c0b08ae040edc510a575609a48915c` |
| `codex-clipboard-0efdff1c-d778-4a7d-ab67-12b148e5e70e.png` | `docs/images/unity-game-material-wizard-en.png` | Unity 6000.4.5f1 material creator | `c82e3325b05626c1a8772b59aa6a099397baa4336ea07af697e7b6fc40e3f118` |

The UI captures are documentation evidence of the named tools. Third-party
application names and interface elements remain subject to their respective
owners' terms.

## Character-render examples

Capture environment: Unity 6000.4.5f1 (`cc83ebd631f8`), Universal Render
Pipeline 17.4.0, Shader Graph 17.4.0, Windows Editor, Direct3D 12. Each source
scene was copied to a temporary Unity scene. A `HideAndDontSave` clone of its
existing `Main Camera` preserved FOV 30, URP Additional Camera Data,
post-processing, antialiasing, and renderer selection while rendering directly
to a 1920x1080 target. The original scenes were never saved by the capture
operation, and all temporary scenes, cameras, render textures, and visibility
overrides were removed afterward.

For Genshin, the other top-level character root's renderers were temporarily
isolated with `Renderer.forceRenderingOff` and restored in `finally`. The saved
scene present at capture time placed the Hu Tao and Furina renderer-bound
centers at x=0.75 and x=3.28 respectively, so those observed centers—not stale
pre-save estimates—were used for the two temporary camera clones.

| Capture file | Private source scene | Repository path | Documentation mapping | SHA-256 |
| --- | --- | --- | --- | --- |
| `Captures/Miku-3.0.0/preset-genshin-hu-tao.png` | `Assets/原神/原神.unity` | `docs/images/preset-genshin-hu-tao.png` | Genshin — Hu Tao / 原神—胡桃 | `95a7812d501d27557cafd0ab7a15052ad67eec29d3422996d85b86e448e7e022` |
| `Captures/Miku-3.0.0/preset-genshin-furina.png` | `Assets/原神/原神.unity` | `docs/images/preset-genshin-furina.png` | Genshin — Furina / 原神—芙宁娜 | `1abfde9f6bf844de2503c8694df5c423e6650366d2d8e2bac0af6dca6b36f5d9` |
| `Captures/Miku-3.0.0/preset-hsr-bronya.png` | `Assets/星穹铁道/布洛妮娅.unity` | `docs/images/preset-hsr-bronya.png` | Honkai: Star Rail — Bronya / 崩坏：星穹铁道—布洛妮娅 | `85464c9fdce286b3c51bcf341901642019e95248cbdf66c7b786eef74ed6fcfe` |
| `Captures/Miku-3.0.0/preset-wuwa-phoebe.png` | `Assets/鸣潮/鸣潮.unity` | `docs/images/preset-wuwa-phoebe.png` | Wuthering Waves — Phoebe / 鸣潮—菲比 | `f178220fddc059886db4cb3bd4af67b633590b4ad48144fe664fc47b0c74de3e` |
| `Captures/Miku-3.0.0/preset-endfield-jierpeta.png` | `Assets/endfield/终末地.unity` | `docs/images/preset-endfield-jierpeta.png` | Arknights: Endfield — 洁尔佩塔 / 明日方舟：终末地—洁尔佩塔 (scene object: `杰哥`) | `7200e0f50d905370d977db3981d166f09bb51fcf4d29483973e3f8c922ee185f` |

The pre/post capture SHA-256 values of the private source scenes were:

| Scene | SHA-256 |
| --- | --- |
| `Assets/鸣潮/鸣潮.unity` | `356d8284183a9901590a2f16fccaed842e48e68c5bc0f2089bff3975e2fa1c62` |
| `Assets/endfield/终末地.unity` | `339721df338abc30512b57c11cf606b0613babda42fa6cce4664be03a507f233` |
| `Assets/星穹铁道/布洛妮娅.unity` | `559d719012b004aa5a607bacf4e9a2c6dfa7bd5115784689b22e9894acbc14e3` |
| `Assets/原神/原神.unity` | `d6f082ac222a408eec6c047833f73512c6ca20453a52c2f8de4d1b4c37f2bab0` |

### Restricted image terms

These five character renders are provided solely for non-commercial learning
and documentation reference. Commercial use is prohibited. All related
characters, designs, and intellectual property belong to their respective
rights holders; Miku grants no rights to game assets.

The five files are excluded from Miku's MIT source-code license. They are
tracked under `docs/images/` and therefore appear in GitHub's automatically
generated source-code archives, but they are not included in either installable
ZIP/TGZ package and are not uploaded as standalone v3.0.0 Release assets. This
restriction changes no code license: existing MIT and separately identified GPL
code retain their current terms, and the first-party Game Toon Shader/HLSL
implementations remain MIT.
