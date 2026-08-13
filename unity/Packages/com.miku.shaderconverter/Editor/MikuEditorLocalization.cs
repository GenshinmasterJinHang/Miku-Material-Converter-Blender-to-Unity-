// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Per-user localization for Miku's Unity Editor UI.
    ///
    /// The package deliberately does not use Unity's project Localization
    /// package: this preference is an editor-tool setting and must not be
    /// serialized into a project or affect generated assets.
    /// </summary>
    internal static class MikuEditorLocalization
    {
        internal const string English = "en_US";
        internal const string SimplifiedChinese = "zh_HANS";
        internal const string PreferenceKey =
            "com.miku.shaderconverter.editorLanguage";

        static readonly string[] LanguageValues =
        {
            English,
            SimplifiedChinese,
        };

        static readonly string[] LanguageLabels =
        {
            "English",
            "简体中文",
        };

        static readonly Dictionary<string, string> SimplifiedChineseMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Miku"] = "Miku",
                ["Miku Game Toon"] = "Miku 游戏卡通",
                ["Language"] = "语言",
                ["English"] = "English",
                ["Simplified Chinese"] = "简体中文",
                ["Miku Settings"] = "Miku 设置",
                ["This language setting only affects Miku's Unity Editor UI and is saved for the current user."] =
                    "此语言设置仅影响 Miku 的 Unity 编辑器界面，并保存到当前用户。",
                ["genshin_toon"] = "原神卡通",
                ["wuwa_toon"] = "鸣潮卡通",
                ["hsr_toon"] = "崩坏：星穹铁道卡通",
                ["endfield_toon"] = "明日方舟：终末地卡通",
                ["Body"] = "身体",
                ["Skin"] = "皮肤",
                ["Hair"] = "头发",
                ["Face"] = "面部",
                ["Eye"] = "眼睛",
                ["Mouth"] = "口腔",
                ["Overlay"] = "叠加层",
                ["Effect"] = "特效",
                ["HairShadow"] = "头发阴影",
                ["Select one or more material assets first."] =
                    "请先选择一个或多个材质资产。",
                ["Apply Recommended Skin & Highlight Profile"] =
                    "应用推荐的皮肤与高光配置",
                ["This explicitly updates the selected materials. The operation supports Undo and does not change FBX or prefab assets."] =
                    "此操作会明确更新选中的材质。支持撤销，不会修改 FBX 或 Prefab 资产。",
                ["Apply"] = "应用",
                ["Cancel"] = "取消",
                ["OK"] = "确定",
                ["Apply Miku skin and highlight profile"] =
                    "应用 Miku 皮肤与高光配置",
                ["Create Material Template"] = "创建材质模板",
                ["Create Material"] = "创建材质",
                ["Miku Material Template"] = "Miku 材质模板",
                ["Miku Material Creator"] = "Miku 材质创建器",
                ["Workflow"] = "工作流",
                ["Material Part"] = "材质部位",
                ["Texture Inputs"] = "贴图输入",
                ["(Required)"] = "（必填）",
                ["(Optional)"] = "（可选）",
                ["Shader: {0}\nThe created .mat is user-owned and is never rebound to a model automatically."] =
                    "Shader：{0}\n创建的 .mat 由用户拥有，不会自动重新绑定到模型。",
                ["Required textures missing: {0}"] = "缺少必填贴图：{0}",
                ["Base Map"] = "基础贴图",
                ["Normal Map"] = "法线贴图",
                ["Light Map"] = "光照贴图",
                ["Shadow Ramp Map"] = "阴影渐变贴图",
                ["Metal Map"] = "金属贴图",
                ["Emission Map"] = "发光贴图",
                ["Hair Ramp Map"] = "头发渐变贴图",
                ["Hair Specular Map"] = "头发高光贴图",
                ["Body Cool Ramp"] = "身体冷色渐变",
                ["Body Warm Ramp"] = "身体暖色渐变",
                ["Stockings Map"] = "丝袜贴图",
                ["Face Map"] = "面部贴图",
                ["Hair Cool Ramp"] = "头发冷色渐变",
                ["Hair Warm Ramp"] = "头发暖色渐变",
                ["ID / Stockings Map"] = "ID / 丝袜贴图",
                ["MatCap"] = "MatCap",
                ["Face SDF"] = "面部 SDF",
                ["Face ID"] = "面部 ID",
                ["Face HET"] = "面部 HET",
                ["Skin Ramp"] = "皮肤渐变",
                ["Hair HM"] = "头发 HM",
                ["Eye HET"] = "眼睛 HET",
                ["Eye HDMF"] = "眼睛 HDMF",
                ["Eye Upper Highlight"] = "眼睛上高光",
                ["Eye Lower Highlight"] = "眼睛下高光",
                ["Eye EG"] = "眼睛 EG",
                ["Material Parameter Map"] = "材质参数贴图",
                ["Diffuse Ramp Map"] = "漫反射渐变贴图",
                ["Specular Ramp Map"] = "高光渐变贴图",
                ["Shadow LUT"] = "阴影 LUT",
                ["Color LUT"] = "颜色 LUT",
                ["Split Normal Map"] = "拆分法线贴图",
                ["Specular Mask"] = "高光遮罩",
                ["Specular Refine F0"] = "高光 F0 修正",
                ["Specular Refine Color"] = "高光颜色修正",
                ["Hair Line Map"] = "发丝线条贴图",
                ["Hair Shift Map"] = "头发偏移贴图",
                ["Hair Refine Map"] = "头发修正贴图",
                ["Face Area Map"] = "面部区域贴图",
                ["Face Refine Map"] = "面部修正贴图",
                ["Emotion Map"] = "表情贴图",
                ["Highlight Map"] = "高光贴图",
                ["Outline Mask"] = "轮廓遮罩",
                ["Effect Mask"] = "特效遮罩",
                ["The created .mat is user-owned and is never rebound to a model automatically."] =
                    "创建的 .mat 由用户拥有，不会自动重新绑定到模型。",
                ["Create User-owned Material"] = "创建用户材质",
                ["Create Miku game Toon material"] =
                    "创建 Miku 游戏卡通材质",
                ["Choose the output location under Assets."] =
                    "选择 Assets 下的输出位置。",
                ["Import Audit"] = "导入审计",
                ["Miku Texture Audit"] = "Miku 贴图审计",
                ["Texture Folder"] = "贴图文件夹",
                ["Only complete Endfield filename patterns are recognized. Ambiguous _M files are left unchanged."] =
                    "仅识别完整的 Endfield 文件名模式。含义不明确的 _M 文件保持不变。",
                ["Apply Recognized Import Settings"] =
                    "应用已识别的导入设置",
                ["Preview"] = "预览",
                ["Renderer Data"] = "Renderer Data",
                ["Apply will add one feature. No other Renderer Data asset will be changed."] =
                    "应用将添加一个功能，不会修改其他 Renderer Data 资产。",
                ["The feature is already installed. Apply is a no-op."] =
                    "该功能已安装，应用操作不会产生变化。",
                ["Miku Game Toon Renderer Features"] =
                    "Miku 游戏卡通渲染功能",
                ["Open Game Toon Renderer Feature Installer"] =
                    "打开游戏卡通渲染功能安装器",
                ["Preview is read-only. Apply installs the Geometry and Screen Rim features into every active Universal Renderer Data asset as one Undo transaction."] =
                    "预览为只读。应用会在一个撤销事务中，为每个活动的 Universal Renderer Data 资产安装 Geometry 与 Screen Rim 功能。",
                ["Choose a Renderer Data asset."] = "请选择 Renderer Data 资产。",
                ["Installed Game Toon renderer features in {0} active Renderer Data asset(s); {1} subasset(s) created."] =
                    "已在 {0} 个活动 Renderer Data 资产中安装游戏卡通渲染功能；创建了 {1} 个子资产。",
                ["Apply will install missing Geometry and Screen Rim features in all active Universal Renderer Data assets."] =
                    "应用将为所有活动的 Universal Renderer Data 资产安装缺少的 Geometry 与 Screen Rim 功能。",
                ["Both features are installed on this Renderer Data. Apply remains idempotent across all active renderers."] =
                    "此 Renderer Data 已安装两项功能；对所有活动渲染器重复应用仍保持幂等。",
                ["Duplicate Miku renderer features were found. Apply is blocked until duplicates are resolved."] =
                    "发现重复的 Miku 渲染功能；请先解决重复项再应用。",
                ["Already installed; no duplicate was added."] =
                    "已安装；未添加重复功能。",
                ["Game Toon Renderer Features: URP asset not active."] =
                    "游戏卡通渲染功能：URP 资产未启用。",
                ["Game Toon Geometry + Screen Rim Renderer Features: {0}/{1} active Renderer Data assets installed."] =
                    "游戏卡通 Geometry + Screen Rim 渲染功能：已在 {0}/{1} 个活动 Renderer Data 资产中安装。",
                ["Install Miku Game Toon Renderer Features"] =
                    "安装 Miku 游戏卡通渲染功能",
                ["Write R / SSS"] = "写入 R / SSS",
                ["Write G / SDF"] = "写入 G / SDF",
                ["Write B / Matcap"] = "写入 B / Matcap",
                ["Write A / Mask"] = "写入 A / Mask",
                ["Explicit Mesh input only. The source/importer and all Renderer references remain untouched."] =
                    "仅使用明确的网格输入。源资产/导入器和所有 Renderer 引用保持不变。",
                ["The imported Mesh is not CPU-readable. Miku will use MeshUtility.AcquireReadOnlyMeshData and write only to the generated clone; importer settings stay unchanged."] =
                    "导入的网格不可由 CPU 读取。Miku 将使用 MeshUtility.AcquireReadOnlyMeshData，并只写入生成的克隆；导入器设置保持不变。",
                ["Create Mesh (Preserve UV7 + Vertex Colors)"] =
                    "创建网格（保留 UV7 + 顶点色）",
                ["Create Mesh with Both"] = "创建网格（两者都写入）",
                ["Create Mesh with Smooth Normals"] = "创建带平滑法线的网格",
                ["Create Mesh with Neutral Vertex Colors"] =
                    "创建带中性顶点色的网格",
                ["Smooth outline normal -> UV7 / TEXCOORD7"] =
                    "平滑轮廓法线 -> UV7 / TEXCOORD7",
                ["Respect Bone Weights"] = "遵循骨骼权重",
                ["The source Mesh already contains UV7 data. Preserve leaves that channel unchanged; Replace writes smooth normals only to the generated clone."] =
                    "源网格已包含 UV7 数据。保留会维持该通道不变；替换只会将平滑法线写入生成的克隆。",
                ["Existing UV7"] = "现有 UV7",
                ["Select Replace to generate smooth normals. Preserve performs no operation in the normals-only tool."] =
                    "请选择替换以生成平滑法线。在仅法线工具中选择保留不会执行操作。",
                ["Vertex colors - Miku_ToonMask_v1"] =
                    "顶点色 - Miku_ToonMask_v1",
                ["Neutral mask is RGBA (255,255,255,0): SSS, outline width, screen rim, face correction."] =
                    "中性遮罩为 RGBA (255,255,255,0)：SSS、轮廓宽度、屏幕边缘光、面部校正。",
                ["Write G / Outline"] = "写入 G / 轮廓",
                ["Write B / Screen Rim"] = "写入 B / 屏幕边缘光",
                ["Write A / Face Correction"] = "写入 A / 面部校正",
                ["Replace UV7 on generated Mesh?"] = "替换生成网格上的 UV7？",
                ["The source Mesh remains untouched. UV7 will be replaced only on the newly generated Mesh asset."] =
                    "源网格保持不变。UV7 只会在新生成的网格资产上被替换。",
                ["Replace on Clone"] = "在克隆上替换",
                ["Miku Smooth Normals"] = "Miku 平滑法线",
                ["Source Mesh"] = "源网格",
                ["Output Folder"] = "输出文件夹",
                ["Output Name"] = "输出名称",
                ["Generate smooth normals and optionally write them to UV7."] =
                    "生成平滑法线，并可选择写入 UV7。",
                ["Choose a source mesh."] = "请选择源网格。",
                ["Choose an output folder under Assets."] =
                    "请选择 Assets 下的输出文件夹。",
                ["Generate"] = "生成",
                ["Already exists"] = "已存在",
                ["Mode"] = "模式",
                ["Position Tolerance"] = "位置容差",
                ["Smoothing Angle"] = "平滑角度",
                ["Include Bone Weight Signature"] = "包含骨骼权重签名",
                ["UV7 Conflict"] = "UV7 冲突",
                ["Vertex Color Channels"] = "顶点色通道",
                ["Miku Toon Mesh Data"] = "Miku 卡通网格数据",
                ["Apply to Selected Renderer"] = "应用到选中渲染器",
                ["This material is mesh-bound. Use the generated prefab, or apply it only to a renderer with an identical mesh."] =
                    "此材质绑定到网格。请使用生成的 Prefab，或仅将其应用到使用相同网格的渲染器。",
                ["Apply Miku Mesh-Bound Material"] =
                    "应用 Miku 网格绑定材质",
                ["Source Mesh SHA-256"] = "源网格 SHA-256",
                ["Mesh Fingerprint Set"] = "网格指纹集合",
                ["Generated Prefab"] = "生成的 Prefab",
                ["Renderer Bindings"] = "渲染器绑定",
                ["Upgrade MiGR material data"] = "升级 MiGR 材质数据",
                ["Upgrade MiGR animation curves"] = "升级 MiGR 动画曲线",
                ["Change Miku game Toon material part"] =
                    "更改 Miku 游戏卡通材质部位",
                ["Apply Miku Endfield texture import profile"] =
                    "应用 Miku Endfield 贴图导入配置",
                ["Install Miku Toon Screen Rim"] =
                    "安装 Miku 卡通屏幕边缘光",
                ["Material"] = "材质",
                ["Generated Base Material"] = "生成的基础材质",
                ["User Material"] = "用户材质",
                ["Texture Bindings"] = "贴图绑定",
                ["Source GUID"] = "源 GUID",
                ["Target GUID"] = "目标 GUID",
                ["Stable GUID"] = "稳定 GUID",
                ["Shader Family Version"] = "Shader 系列版本",
                ["Texture Role"] = "贴图角色",
                ["Miku Fixed Workflow"] = "Miku 固定工作流",
                ["EyeHET is now an emission mask. Re-import this bundle to bind HDMF and authored highlight textures."] =
                    "EyeHET 现在是发光遮罩。请重新导入此 Bundle 以绑定 HDMF 和已制作的高光贴图。",
                ["Endfield iris materials require an authored MatCap for the tutorial cornea highlight."] =
                    "Endfield 虹膜材质需要已制作的 MatCap 才能呈现教程中的角膜高光。",
                ["Assign an authored Face SDF texture; the built-in white fallback cannot reproduce directional facial shadows."] =
                    "请指定已制作的面部 SDF 贴图；内置白色回退贴图无法产生随方向变化的面部阴影。",
                ["Face SDF Shadow Strength is zero, so the computed mask cannot affect final direct lighting."] =
                    "面部 SDF 阴影强度为零，因此计算出的遮罩不会影响最终直接光照。",
                ["The material face basis is zero, non-finite, or collinear. Supply an orthogonal Right/Up/Forward basis or disable the material basis."] =
                    "材质面部基向量为零、非有限值或共线。请提供正交的右/上/前基向量，或禁用材质基向量。",
                ["Import Face SDF as Linear with mipmaps disabled; Clamp and Repeat wrapping are both supported."] =
                    "请将面部 SDF 按 Linear 且禁用 Mipmap 的方式导入；Clamp 与 Repeat 均受支持。",
                ["Main and soft SDF channels are identical. This is allowed, but disables the authored two-channel refinement."] =
                    "主 SDF 与柔化 SDF 通道相同。该设置允许使用，但会停用已制作的双通道细化。",
                ["Lit Tint and Shadow Tint are identical, so the SDF mask has no visible diffuse contrast."] =
                    "亮部色与阴影色相同，因此 SDF 遮罩不会产生可见的漫反射对比。",
                ["A Face SDF debug view is active; set Debug Mode to 0 to inspect final shading."] =
                    "面部 SDF 调试视图已启用；请将调试模式设为 0 以检查最终着色。",
                ["Face SDF Shadow Softness or Mirror Blend Width is very wide and can flatten the authored light/shadow regions in final shading."] =
                    "面部 SDF 阴影柔化范围过宽，可能在最终着色中抹平已制作的明暗区域。",
                ["Face skin SSS is strong enough to fill the SDF shadow region and reduce its visible contrast."] =
                    "面部皮肤 SSS 强度较高，可能填充 SDF 阴影区域并降低可见对比度。",
                ["Final"] = "最终",
                ["Base Alpha"] = "基础 Alpha",
                ["HET"] = "HET",
                ["HDMF R"] = "HDMF R",
                ["HDMF G"] = "HDMF G",
                ["HDMF B"] = "HDMF B",
                ["HDMF A"] = "HDMF A",
                ["Pupil Mask"] = "瞳孔遮罩",
                ["EG"] = "EG",
                ["Albedo"] = "反照率",
                ["Material Params"] = "材质参数",
                ["Normal"] = "法线",
                ["Ramp"] = "渐变",
                ["LUT"] = "LUT",
                ["Metal Mask"] = "金属遮罩",
                ["Metal Specular"] = "金属高光",
                ["Main Light Color"] = "主光颜色",
                ["Raw Distance Attenuation"] = "原始距离衰减",
                ["Shadow Attenuation"] = "阴影衰减",
                ["Direct Diffuse Only"] = "仅直接漫反射",
                ["Direct Specular Only"] = "仅直接高光",
                ["SH Only"] = "仅球谐光照",
                ["Hair Params"] = "头发参数",
                ["Highlight Normal"] = "高光法线",
                ["Lobes"] = "光瓣",
                ["Accessory Mask"] = "配件遮罩",
                ["Accessory Specular"] = "配件高光",
                ["Area Mask"] = "区域遮罩",
                ["SDF"] = "SDF",
                ["Blush Mask"] = "腮红遮罩",
                ["Blush Mask (Legacy)"] = "腮红遮罩（旧版）",
                ["AO"] = "AO",
                ["LUT (Legacy)"] = "LUT（旧版）",
                ["Normal Map"] = "法线贴图",
                ["Displacement"] = "置换",
            };

        internal static string Language
        {
            get
            {
                var value = EditorPrefs.GetString(PreferenceKey, English);
                return Array.IndexOf(LanguageValues, value) >= 0
                    ? value
                    : English;
            }
        }

        internal static IReadOnlyCollection<string> KnownMessageIds =>
            SimplifiedChineseMap.Keys;

        internal static string Tr(string english)
        {
            if (!string.Equals(Language, SimplifiedChinese,
                    StringComparison.Ordinal))
                return english;
            return SimplifiedChineseMap.TryGetValue(english, out var value)
                ? value
                : english;
        }

        internal static string Format(string english, params object[] args) =>
            string.Format(Tr(english), args);

        internal static GUIContent Content(
            string english,
            string tooltip = null) =>
            new GUIContent(Tr(english), Tr(tooltip ?? ""));

        internal static void SetLanguage(string value)
        {
            var normalized = Array.IndexOf(LanguageValues, value) >= 0
                ? value
                : English;
            if (string.Equals(Language, normalized, StringComparison.Ordinal))
                return;
            EditorPrefs.SetString(PreferenceKey, normalized);
            RepaintMikuUi();
        }

        static void RepaintMikuUi()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                window.Repaint();
            SceneView.RepaintAll();
        }

        [MenuItem("Miku/Settings", priority = 10)]
        static void OpenSettings() =>
            SettingsService.OpenUserPreferences("Preferences/Miku");

        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider(
                "Preferences/Miku",
                SettingsScope.User)
            {
                label = "Miku",
                guiHandler = _ => DrawSettingsGui(),
                keywords = new HashSet<string>(new[]
                {
                    "Miku", "Language", "English", "Chinese",
                }),
            };
            return provider;
        }

        static void DrawSettingsGui()
        {
            EditorGUILayout.LabelField(
                Tr("Miku Settings"),
                EditorStyles.boldLabel);
            var current = Array.IndexOf(LanguageValues, Language);
            var next = EditorGUILayout.Popup(
                Tr("Language"),
                current < 0 ? 0 : current,
                LanguageLabels);
            if (next != current && next >= 0 && next < LanguageValues.Length)
                SetLanguage(LanguageValues[next]);
            EditorGUILayout.HelpBox(
                Tr("This language setting only affects Miku's Unity Editor UI and is saved for the current user."),
                MessageType.Info);
        }
    }
}
