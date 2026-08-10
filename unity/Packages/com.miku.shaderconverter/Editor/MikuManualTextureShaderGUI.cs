// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    public sealed class MikuManualTextureShaderGUI : ShaderGUI
    {
        public override void OnGUI(
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            base.OnGUI(materialEditor, properties);
            var materials = materialEditor.targets
                .OfType<Material>()
                .ToArray();
            foreach (var material in materials)
                    MikuManualTextureKeywordUtility.SyncKeywords(material);
            if (materials.Length != 1 || materials[0].shader == null)
                return;
            var shaderName = materials[0].shader.name;
            if (!shaderName.StartsWith("MIKU/Genshin/", StringComparison.Ordinal) &&
                !shaderName.StartsWith("MIKU/Wuwa/", StringComparison.Ordinal) &&
                !shaderName.StartsWith("MIKU/HSR/", StringComparison.Ordinal) &&
                !shaderName.StartsWith("MIKU/Endfield/", StringComparison.Ordinal))
                return;

            DrawEndfieldDebugView(
                materialEditor,
                properties,
                shaderName);
            DrawWuwaEyeDebugView(
                materialEditor,
                properties,
                shaderName);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                MikuEditorLocalization.Tr("Miku Fixed Workflow"),
                EditorStyles.boldLabel);
            var recipe = MikuToonRecipeUtility.FindForMaterial(materials[0]);
            if (recipe != null)
            {
                if (string.Equals(
                        shaderName,
                        "MIKU/Wuwa/Eye",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        recipe.shaderFamilyVersion,
                        "2.2.6",
                        StringComparison.Ordinal))
                    EditorGUILayout.HelpBox(
                        "MIKU_WUWA_EYE_2_2_6_REIMPORT_REQUIRED: " +
                        MikuEditorLocalization.Tr(
                            "EyeHET is now an emission mask. Re-import this " +
                            "bundle to bind HDMF and authored highlight textures."),
                        MessageType.Warning);
                var values = string.Equals(
                        recipe.workflowKind,
                        "endfield_toon",
                        StringComparison.Ordinal)
                    ? new[]
                    {
                        MikuGameMaterialPart.Body,
                        MikuGameMaterialPart.Skin,
                        MikuGameMaterialPart.Hair,
                        MikuGameMaterialPart.Face,
                        MikuGameMaterialPart.Eye,
                        MikuGameMaterialPart.Mouth,
                        MikuGameMaterialPart.Overlay,
                        MikuGameMaterialPart.Effect,
                        MikuGameMaterialPart.HairShadow,
                    }
                    : string.Equals(
                        recipe.workflowKind,
                        "wuwa_toon",
                        StringComparison.Ordinal)
                        ? new[]
                    {
                        MikuGameMaterialPart.Body,
                        MikuGameMaterialPart.Hair,
                        MikuGameMaterialPart.Face,
                        MikuGameMaterialPart.Eye,
                        MikuGameMaterialPart.Effect,
                    }
                    : new[]
                    {
                        MikuGameMaterialPart.Body,
                        MikuGameMaterialPart.Hair,
                        MikuGameMaterialPart.Face,
                        MikuGameMaterialPart.Eye,
                    };
                var current = Math.Max(0, Array.IndexOf(values, recipe.gamePart));
                var labels = values
                    .Select(value => MikuEditorLocalization.Tr(value.ToString()))
                    .ToArray();
                EditorGUI.BeginChangeCheck();
                var selected = EditorGUILayout.Popup(
                    MikuEditorLocalization.Tr("Material Part"),
                    current,
                    labels);
                if (EditorGUI.EndChangeCheck())
                {
                    recipe.gamePart = values[selected];
                    MikuToonRecipeUtility.ApplySelection(recipe);
                }
            }
            MikuToonRendererFeatureInstaller.DrawStatusAndOpenButton();
        }

        static void DrawWuwaEyeDebugView(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string shaderName)
        {
            if (!string.Equals(
                    shaderName,
                    "MIKU/Wuwa/Eye",
                    StringComparison.Ordinal))
                return;
            var debugView = FindProperty("_EyeDebugView", properties, false);
            if (debugView == null)
                return;
            var labels = new[]
            {
                "Final",
                "Base Alpha",
                "HET",
                "HDMF R",
                "HDMF G",
                "HDMF B",
                "HDMF A",
                "Pupil Mask",
                "EG",
            };
            labels = labels
                .Select(MikuEditorLocalization.Tr)
                .ToArray();
            EditorGUI.showMixedValue = debugView.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var current = Mathf.Clamp(
                Mathf.RoundToInt(debugView.floatValue),
                0,
                labels.Length - 1);
            var selected = EditorGUILayout.Popup(
                MikuEditorLocalization.Tr(debugView.displayName),
                current,
                labels);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(
                    debugView.displayName);
                debugView.floatValue = selected;
            }
            EditorGUI.showMixedValue = false;
        }

        static void DrawEndfieldDebugView(
            MaterialEditor materialEditor,
            MaterialProperty[] properties,
            string shaderName)
        {
            string[] labels;
            if (string.Equals(
                shaderName,
                "MIKU/Endfield/Body",
                StringComparison.Ordinal))
            {
                labels = new[]
                {
                    "Final",
                    "Albedo",
                    "Material Params",
                    "Normal",
                    "Ramp",
                    "LUT",
                    "Metal Mask",
                    "Metal Specular",
                    "Main Light Color",
                    "Raw Distance Attenuation",
                    "Shadow Attenuation",
                    "Direct Diffuse Only",
                    "Direct Specular Only",
                    "SH Only",
                };
            }
            else if (string.Equals(
                shaderName,
                "MIKU/Endfield/Hair",
                StringComparison.Ordinal))
            {
                labels = new[]
                {
                    "Final",
                    "Albedo",
                    "Hair Params",
                    "Highlight Normal",
                    "Ramp",
                    "Lobes",
                    "Accessory Mask",
                    "Accessory Specular",
                    "Main Light Color",
                    "Raw Distance Attenuation",
                    "Shadow Attenuation",
                    "Direct Diffuse Only",
                    "Direct Specular Only",
                    "SH Only",
                };
            }
            else if (string.Equals(
                shaderName,
                "MIKU/Endfield/Face",
                StringComparison.Ordinal))
            {
                labels = new[]
                {
                    "Final",
                    "Albedo",
                    "Area Mask",
                    "Normal",
                    "Ramp",
                    "SDF",
                    "Blush Mask",
                    "Blush Mask (Legacy)",
                    "Main Light Color",
                    "Raw Distance Attenuation",
                    "Shadow Attenuation",
                    "Direct Diffuse Only",
                    "Direct Specular Only",
                    "SH Only",
                };
            }
            else if (string.Equals(
                shaderName,
                "MIKU/Endfield/Skin",
                StringComparison.Ordinal))
            {
                labels = new[]
                {
                    "Final",
                    "Albedo",
                    "AO",
                    "Normal",
                    "Ramp",
                    "LUT",
                    "LUT (Legacy)",
                    "LUT (Legacy)",
                    "Main Light Color",
                    "Raw Distance Attenuation",
                    "Shadow Attenuation",
                    "Direct Diffuse Only",
                    "Direct Specular Only",
                    "SH Only",
                };
            }
            else
                return;

            var debugView = FindProperty(
                "_DebugView",
                properties,
                false);
            if (debugView == null)
                return;
            labels = labels
                .Select(MikuEditorLocalization.Tr)
                .ToArray();

            EditorGUI.showMixedValue = debugView.hasMixedValue;
            EditorGUI.BeginChangeCheck();
            var current = Mathf.Clamp(
                Mathf.RoundToInt(debugView.floatValue),
                0,
                labels.Length - 1);
            var selected = EditorGUILayout.Popup(
                MikuEditorLocalization.Tr(debugView.displayName),
                current,
                labels);
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(
                    debugView.displayName);
                debugView.floatValue = selected;
            }
            EditorGUI.showMixedValue = false;
        }
    }

    internal static class MikuManualTextureKeywordUtility
    {
        static readonly TextureKeyword[] StandardPbrTextureKeywords =
        {
            new TextureKeyword("_BaseMap", "_MIKU_BASECOLOR_MAP"),
            new TextureKeyword("_AOMap", "_MIKU_AO_MAP"),
            new TextureKeyword("_MetallicMap", "_MIKU_METALLIC_MAP"),
            new TextureKeyword("_RoughnessMap", "_MIKU_ROUGHNESS_MAP"),
            new TextureKeyword("_GlossinessMap", "_MIKU_GLOSSINESS_MAP"),
            new TextureKeyword("_SpecularMap", "_MIKU_SPECULAR_MAP"),
            new TextureKeyword("_ReflectionMap", "_MIKU_REFLECTION_MAP"),
            new TextureKeyword("_ReflectionCube", "_MIKU_REFLECTION_CUBE"),
            new TextureKeyword("_NormalMap", "_MIKU_NORMAL_MAP"),
            new TextureKeyword("_BumpHeightMap", "_MIKU_BUMP_MAP"),
            new TextureKeyword("_HeightMap", "_MIKU_HEIGHT_MAP"),
            new TextureKeyword("_DisplacementMap", "_MIKU_DISPLACEMENT_MAP"),
            new TextureKeyword("_EmissionMap", "_MIKU_EMISSION_MAP"),
            new TextureKeyword("_AlphaMap", "_MIKU_ALPHA_MAP"),
        };

        public static void SyncKeywords(Material material)
        {
            var shaderName = material?.shader != null ? material.shader.name : "";
            if (shaderName == "MIKU/StandardPBR/SemanticLit")
            {
                foreach (var item in StandardPbrTextureKeywords)
                    SetKeyword(
                        material,
                        item.Keyword,
                        HasTexture(material, item.Property));
                SetKeyword(
                    material,
                    "_MIKU_PARALLAX",
                    HasTexture(material, "_HeightMap") ||
                    HasTexture(material, "_DisplacementMap"));
                return;
            }
            if (shaderName.StartsWith("MIKU/HSR/", StringComparison.Ordinal))
            {
                var stockings = HasTexture(material, "_StockingsMap");
                SetKeyword(material, "_HSR_STOCKINGS_ON", stockings);
                SetFloat(material, "_UseStockings", stockings ? 1f : 0f);
                SetKeyword(
                    material,
                    "_HSR_EMISSION_ON",
                    HasTexture(material, "_EmissionMap"));
                SetKeyword(
                    material,
                    "_HSR_DOUBLE_SIDED",
                    material.HasProperty("_DoubleSided") &&
                    material.GetFloat("_DoubleSided") > 0.5f);
                return;
            }
            if (shaderName.StartsWith("MIKU/Genshin/", StringComparison.Ordinal))
            {
                SetKeyword(
                    material,
                    "_GENSHIN_METALMAP_ON",
                    HasTexture(material, "_MetalMap"));
                SetKeyword(
                    material,
                    "_GENSHIN_EMISSION_ON",
                    HasTexture(material, "_EmissionMap"));
                SetKeyword(
                    material,
                    "_GENSHIN_NORMALMAP_ON",
                    HasTexture(material, "_NormalMap"));
                if (material.HasProperty("_DoubleSided") &&
                    material.HasProperty("_Cull"))
                {
                    SetFloat(
                        material,
                        "_Cull",
                        material.GetFloat("_DoubleSided") > 0.5f ? 0f : 2f);
                }
                return;
            }
            if (shaderName.StartsWith("MIKU/Wuwa/", StringComparison.Ordinal))
            {
                foreach (var item in new[]
                {
                    new TextureKeyword("_NormalMap", "_WUWA_NORMAL_ON"),
                    new TextureKeyword("_HairHM", "_WUWA_HAIR_HM_ON"),
                    new TextureKeyword("_SkinRamp", "_WUWA_SKIN_RAMP_ON"),
                    new TextureKeyword("_FaceHET", "_WUWA_FACE_HET_ON"),
                    new TextureKeyword("_EyeHET", "_WUWA_EYE_HET_ON"),
                    new TextureKeyword("_EyeHDMF", "_WUWA_EYE_HDMF_ON"),
                    new TextureKeyword(
                        "_EyeUpperHighlight",
                        "_WUWA_EYE_UPPER_HIGHLIGHT_ON"),
                    new TextureKeyword(
                        "_EyeLowerHighlight",
                        "_WUWA_EYE_LOWER_HIGHLIGHT_ON"),
                    new TextureKeyword("_EyeEG", "_WUWA_EYE_EG_ON"),
                    new TextureKeyword("_EmissionMap", "_WUWA_EMISSION_ON"),
                    new TextureKeyword("_MatCap", "_WUWA_MATCAP_ON"),
                })
                    SetKeyword(
                        material,
                        item.Keyword,
                        HasTexture(material, item.Property));
                if (string.Equals(
                    shaderName,
                    "MIKU/Wuwa/Eye",
                    StringComparison.Ordinal))
                {
                    SetKeyword(material, "_WUWA_EMISSION_ON", false);
                }
                SetKeyword(
                    material,
                    "_WUWA_ID_ON",
                    HasTexture(material, "_IDMap") ||
                    HasTexture(material, "_FaceID"));
                SetKeyword(
                    material,
                    "_WUWA_HAIR_SHADOW_ON",
                    material.HasProperty("_UseHairShadow") &&
                    material.GetFloat("_UseHairShadow") > 0.5f);
                var stockings = HasTexture(material, "_StockingsMap") &&
                    HasTexture(material, "_IDMap") &&
                    material.GetTexture("_StockingsMap") ==
                    material.GetTexture("_IDMap");
                SetKeyword(material, "_WUWA_STOCKINGS_ON", stockings);
                SetFloat(material, "_UseStockings", stockings ? 1f : 0f);
                return;
            }
            if (shaderName.StartsWith("MIKU/Endfield/", StringComparison.Ordinal))
            {
                foreach (var item in new[]
                {
                    new TextureKeyword("_NormalMap", "_ENDFIELD_NORMAL_ON"),
                    new TextureKeyword(
                        "_MaterialParamMap",
                        "_ENDFIELD_MATERIAL_PARAMS_ON"),
                    new TextureKeyword(
                        "_SplitNormalMap",
                        "_ENDFIELD_SPLIT_NORMAL_ON"),
                    new TextureKeyword(
                        "_EmissionMap",
                        "_ENDFIELD_EMISSION_ON"),
                    new TextureKeyword(
                        "_SDFLightmap",
                        "_ENDFIELD_FACE_SDF_ON"),
                })
                    SetKeyword(
                        material,
                        item.Keyword,
                        HasTexture(material, item.Property));
                foreach (var pair in new[]
                {
                    new TextureToggle("_NormalMap", "_UseNormalMap"),
                    new TextureToggle(
                        "_MaterialParamMap",
                        "_UseMaterialParamMap"),
                    new TextureToggle("_DiffRampMap", "_UseDiffRampMap"),
                    new TextureToggle("_SpecRampMap", "_UseSpecRampMap"),
                    new TextureToggle("_ColorLutTex", "_UseColorLut"),
                    new TextureToggle("_ShadowLutTex", "_UseShadowLut"),
                    new TextureToggle("_EmissionMap", "_UseEmissionMap"),
                    new TextureToggle("_MatCap", "_UseMatCap"),
                    new TextureToggle(
                        "_SplitNormalMap",
                        "_UseSplitNormalMap"),
                    new TextureToggle("_OutlineMask", "_UseOutlineMask"),
                    new TextureToggle(
                        "_SpecularMask",
                        "_UseSpecularMask"),
                    new TextureToggle("_LineMap", "_UseLineMap"),
                    new TextureToggle("_StrokeMap", "_UseStrokeMap"),
                    new TextureToggle("_HairLineMap", "_UseHairLineMap"),
                    new TextureToggle("_HairShiftMap", "_UseHairShiftMap"),
                    new TextureToggle("_HairRefineMap", "_UseHairRefineMap"),
                    new TextureToggle("_SDFLightmap", "_UseFaceSDF"),
                    new TextureToggle("_SDFMask", "_UseSDFMask"),
                    new TextureToggle("_FaceAreaMap", "_UseFaceAreaMap"),
                    new TextureToggle("_FaceRefineMap", "_UseFaceRefineMap"),
                    new TextureToggle("_HighlightMap", "_UseHighlightMap"),
                    new TextureToggle("_EffectMask", "_UseEffectMask"),
                })
                    SetFloat(
                        material,
                        pair.Toggle,
                        HasTexture(material, pair.Texture) ? 1f : 0f);
                SetFloat(
                    material,
                    "_UseSpecularRefine",
                    HasTexture(material, "_SpecularRefineF0Tex") ||
                    HasTexture(material, "_SpecularRefineColorTex") ? 1f : 0f);
            }
        }

        static bool HasTexture(Material material, string property)
        {
            return material != null &&
                material.HasProperty(property) &&
                material.GetTexture(property) != null;
        }

        static void SetKeyword(Material material, string keyword, bool enabled)
        {
            var changed = material.IsKeywordEnabled(keyword) != enabled;
            if (enabled)
                material.EnableKeyword(keyword);
            else
            {
                material.DisableKeyword(keyword);
                var keywords = material.shaderKeywords;
                if (keywords != null && Array.IndexOf(keywords, keyword) >= 0)
                {
                    material.shaderKeywords = keywords
                        .Where(item => !string.Equals(
                            item,
                            keyword,
                            StringComparison.Ordinal))
                        .ToArray();
                    changed = true;
                }
            }
            if (changed)
                EditorUtility.SetDirty(material);
        }

        static void SetFloat(Material material, string property, float value)
        {
            if (!material.HasProperty(property) ||
                Mathf.Approximately(material.GetFloat(property), value))
                return;
            material.SetFloat(property, value);
            EditorUtility.SetDirty(material);
        }

        readonly struct TextureKeyword
        {
            public readonly string Property;
            public readonly string Keyword;

            public TextureKeyword(string property, string keyword)
            {
                Property = property;
                Keyword = keyword;
            }
        }

        readonly struct TextureToggle
        {
            public readonly string Texture;
            public readonly string Toggle;

            public TextureToggle(string texture, string toggle)
            {
                Texture = texture;
                Toggle = toggle;
            }
        }
    }
}
