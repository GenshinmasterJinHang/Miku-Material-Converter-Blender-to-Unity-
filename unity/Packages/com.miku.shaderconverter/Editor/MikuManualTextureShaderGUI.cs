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
            foreach (var target in materialEditor.targets)
                if (target is Material material)
                    MikuManualTextureKeywordUtility.SyncKeywords(material);
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
                return;
            }
            if (shaderName.StartsWith("MIKU/Wuwa/", StringComparison.Ordinal))
            {
                foreach (var item in new[]
                {
                    new TextureKeyword("_NormalMap", "_WUWA_NORMAL_ON"),
                    new TextureKeyword("_IDMap", "_WUWA_ID_ON"),
                    new TextureKeyword("_HairHM", "_WUWA_HAIR_HM_ON"),
                    new TextureKeyword("_SkinRamp", "_WUWA_SKIN_RAMP_ON"),
                    new TextureKeyword("_FaceHET", "_WUWA_FACE_HET_ON"),
                    new TextureKeyword("_EyeHET", "_WUWA_EYE_HET_ON"),
                    new TextureKeyword("_EyeEG", "_WUWA_EYE_EG_ON"),
                    new TextureKeyword("_EmissionMap", "_WUWA_EMISSION_ON"),
                    new TextureKeyword("_MatCap", "_WUWA_MATCAP_ON"),
                })
                    SetKeyword(
                        material,
                        item.Keyword,
                        HasTexture(material, item.Property));
                SetKeyword(
                    material,
                    "_WUWA_HAIR_SHADOW_ON",
                    material.HasProperty("_UseHairShadow") &&
                    material.GetFloat("_UseHairShadow") > 0.5f);
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
    }
}
