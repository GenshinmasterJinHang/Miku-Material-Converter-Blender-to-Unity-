// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuGameToonMaterialProfiles
    {
        internal const string MissingSkinMaskDiagnostic =
            "MIKU_SKIN_MASK_TEXTURE_MISSING";

        const string MenuPath =
            "Miku/Game Toon/Materials/Apply Recommended Skin & Highlight Profile";

        static readonly Color SkinTarget = new Color(1f, 0.93f, 0.90f, 1f);
        static readonly Color SssColor = new Color(1f, 0.5f, 0.4f, 1f);

        [MenuItem(MenuPath, priority = 220)]
        static void ApplyRecommendedToSelection()
        {
            var materials = Selection.GetFiltered<Material>(
                    SelectionMode.Assets | SelectionMode.DeepAssets)
                .Where(material => material != null)
                .Distinct()
                .ToArray();
            if (materials.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    MikuEditorLocalization.Tr("Miku Game Toon"),
                    MikuEditorLocalization.Tr(
                        "Select one or more material assets first."),
                    MikuEditorLocalization.Tr("OK"));
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    MikuEditorLocalization.Tr(
                        "Apply Recommended Skin & Highlight Profile"),
                    MikuEditorLocalization.Tr(
                        "This explicitly updates the selected materials. " +
                        "The operation supports Undo and does not change FBX " +
                        "or prefab assets."),
                    MikuEditorLocalization.Tr("Apply"),
                    MikuEditorLocalization.Tr("Cancel")))
                return;

            Undo.RecordObjects(
                materials,
                MikuEditorLocalization.Tr(
                    "Apply Miku skin and highlight profile"));
            var changed = 0;
            foreach (var material in materials)
            {
                if (!ApplyRecommended(material))
                    continue;
                EditorUtility.SetDirty(material);
                changed++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"MIKU_GAME_TOON_PROFILE_APPLIED:{changed}/{materials.Length}");
        }

        [MenuItem(MenuPath, validate = true)]
        static bool ValidateApplyRecommendedToSelection() =>
            Selection.GetFiltered<Material>(
                SelectionMode.Assets | SelectionMode.DeepAssets).Length > 0;

        internal static bool ApplyRecommended(
            Material material,
            bool logMissingMask = true)
        {
            if (material == null || material.shader == null)
                return false;

            var shaderName = material.shader.name;
            var changed = false;
            if (shaderName.StartsWith("MIKU/Genshin/", StringComparison.Ordinal))
            {
                changed |= SetFloat(material, "_MainLightColorUsage", 1f);
                changed |= SetFloat(material, "_MetalIntensity", 1f);
                changed |= SetFloat(material, "_Bright", 0.99f);
                changed |= SetFloat(material, "_Grey", 1.14f);
                changed |= SetFloat(material, "_Dark", 0.5f);
                changed |= SetFloat(material, "_Gloss", 16f);
                changed |= SetFloat(material, "_GlossStrength", 1f);
                changed |= SetFloat(material, "_FresnelStrength", 0f);
                changed |= SetFloat(material, "_HighlightCompression", 1f);
                changed |= SetFloat(material, "_HighlightKnee", 0.72f);
                changed |= SetFloat(material, "_HighlightCeiling", 0.98f);
                changed |= SetFloat(
                    material,
                    "_OutlineColorMode",
                    shaderName == "MIKU/Genshin/Face" ? 0f : 1f);
                if (shaderName == "MIKU/Genshin/Body")
                {
                    changed |= SetFloat(material, "_MainShadowInfluence", 0.25f);
                    changed |= ApplySkin(material, 0.12f, 0.30f, 1.02f, 0.10f, "_LightMap", logMissingMask);
                }
                else if (shaderName == "MIKU/Genshin/Hair")
                    changed |= SetFloat(material, "_MainShadowInfluence", 0.35f);
                else if (shaderName == "MIKU/Genshin/Face")
                {
                    changed |= SetFloat(material, "_MainShadowInfluence", 0f);
                    changed |= ApplySkin(material, 0.10f, 0.35f, 1.02f, 0.10f, null, logMissingMask);
                }
            }
            else if (shaderName.StartsWith("MIKU/HSR/", StringComparison.Ordinal))
            {
                if (shaderName == "MIKU/HSR/Body")
                    changed |= ApplySkin(material, 0.12f, 0.30f, 1.02f, 0.10f, "_LightMap", logMissingMask);
                else if (shaderName == "MIKU/HSR/Face")
                {
                    changed |= ApplySkin(material, 0.10f, 0.35f, 1.02f, 0.10f, null, logMissingMask);
                    changed |= SetFloat(material, "_FaceSpecularThresholdMask", 0.5f);
                    changed |= SetFloat(material, "_FaceSpecularExponent", 32f);
                    changed |= SetFloat(material, "_FaceSpecularSoftness", 0.1f);
                    changed |= SetFloat(material, "_FaceSpecularStrength", 0.12f);
                    changed |= SetColor(
                        material,
                        "_FaceSpecularColor",
                        new Color(1f, 0.82f, 0.74f, 1f));
                    changed |= SetFloat(material, "_NoseLinePower", 3f);
                    changed |= SetFloat(material, "_NoseLineStrength", 8f);
                    changed |= SetColor(
                        material,
                        "_NoseLineColor",
                        new Color(0.18f, 0.07f, 0.06f, 1f));
                }
            }
            else if (shaderName.StartsWith("MIKU/Wuwa/", StringComparison.Ordinal))
            {
                if (shaderName == "MIKU/Wuwa/Body")
                {
                    changed |= ApplySkin(material, 0.16f, 0.34f, 1.03f, 0.12f, "_IDMap", logMissingMask);
                    changed |= SetFloat(material, "_BodyEmissionStrength", 1f);
                    changed |= SetFloat(material, "_MatcapStrength", 0.15f);
                    changed |= SetFloat(material, "_MatcapSaturation", 0.1f);
                    var packedNormal = material.HasProperty(
                            "_NormalMapEncoding") &&
                        material.GetFloat("_NormalMapEncoding") > 0.5f;
                    changed |= SetFloat(
                        material,
                        "_Metallic",
                        packedNormal ? 1f : 0f);
                    changed |= SetFloat(material, "_Roughness", 0.6f);
                    changed |= SetFloat(material, "_NormalScale", 1f);
                    changed |= SetFloat(material, "_PackedMetallicScale", 1f);
                    changed |= SetFloat(material, "_PackedRoughnessScale", 1f);
                    changed |= SetFloat(
                        material,
                        "_OcclusionStrength",
                        packedNormal ? 1f : 0f);
                    changed |= SetFloat(material, "_SpecularStrength", 0.25f);
                    changed |= SetFloat(material, "_ReflectionStrength", 0.1f);
                    changed |= SetFloat(material, "_IndirectLightUsage", 1f);
                    changed |= SetFloat(material, "_MainLightColorUsage", 1f);
                    changed |= SetColor(
                        material,
                        "_VerticalGradientColor",
                        new Color(0.86f, 0.80f, 0.94f, 1f));
                    changed |= SetFloat(material, "_VerticalGradientStrength", 1f);
                    changed |= SetFloat(material, "_GradientUVIndex", 3f);
                    changed |= SetFloat(material, "_GradientInvert", 0f);
                    changed |= SetFloat(material, "_OutlineDistanceMode", 1f);
                    changed |= SetFloat(material, "_OutlineVertexColorMask", 1f);
                    changed |= SetFloat(material, "_StockingSheerness", 0.58f);
                    changed |= SetColor(
                        material,
                        "_StockingSkinTint",
                        new Color(1f, 0.78f, 0.74f, 1f));
                    changed |= SetColor(
                        material,
                        "_StockingEdgeTint",
                        new Color(0.58f, 0.56f, 0.70f, 1f));
                    changed |= SetFloat(material, "_StockingFresnelPower", 2.5f);
                    changed |= SetFloat(material, "_StockingReflectionStrength", 0.22f);
                }
                else if (shaderName == "MIKU/Wuwa/Face")
                {
                    changed |= ApplySkin(material, 0.12f, 0.35f, 1.00f, 0.08f, null, logMissingMask);
                    changed |= SetFloat(material, "_UseFaceBasis", 1f);
                    changed |= SetVector(
                        material,
                        "_FaceRight",
                        new Vector4(1f, 0f, 0f, 0f));
                    changed |= SetVector(
                        material,
                        "_FaceUp",
                        new Vector4(0f, 0f, 1f, 0f));
                    changed |= SetVector(
                        material,
                        "_FaceForward",
                        new Vector4(0f, -1f, 0f, 0f));
                    changed |= SetFloat(material, "_FaceFlatness", 1f);
                    changed |= SetFloat(material, "_FaceBaseCurvePower", 1.2f);
                    changed |= SetFloat(material, "_FaceBaseBrightness", 1.0f);
                    changed |= SetFloat(material, "_FaceFinalBrightness", 1.0f);
                    changed |= SetFloat(material, "_FaceShadowStrength", 0.72f);
                    changed |= SetFloat(material, "_MainShadowInfluence", 0f);
                    changed |= SetFloat(material, "_SkinRampBrightness", 1.0f);
                    changed |= SetFloat(material, "_SkinRampStrength", 0.35f);
                    changed |= SetFloat(material, "_FaceSoftChannelStrength", 1f);
                    changed |= SetFloat(material, "_UseHairShadow", 1f);
                    changed |= SetFloat(material, "_FaceSdfMainChannel", 3f);
                    changed |= SetFloat(material, "_FaceSdfSoftChannel", 2f);
                    changed |= SetFloat(
                        material,
                        "_FaceSdfMirrorBlendWidth",
                        0.10f);
                    changed |= SetFloat(material, "_FaceShadowOffsetX", 0.015f);
                    changed |= SetFloat(material, "_FaceShadowOffsetY", 0f);
                    changed |= SetFloat(material, "_Roughness", 0.72f);
                    changed |= SetFloat(material, "_SpecularStrength", 0.08f);
                    changed |= SetFloat(material, "_ReflectionStrength", 0.04f);
                    changed |= SetFloat(material, "_IndirectLightUsage", 1f);
                    changed |= SetFloat(material, "_MainLightColorUsage", 1f);
                    changed |= SetFloat(material, "_VerticalGradientStrength", 0f);
                    changed |= SetFloat(material, "_GradientUVIndex", 3f);
                    changed |= SetFloat(material, "_GradientInvert", 0f);
                    changed |= SetFloat(material, "_OutlineDistanceMode", 1f);
                    changed |= SetFloat(material, "_OutlineVertexColorMask", 1f);
                }
                else if (shaderName == "MIKU/Wuwa/Eye")
                {
                    var hasHdmf = material.HasProperty("_EyeHDMF") &&
                        material.GetTexture("_EyeHDMF") != null;
                    changed |= SetFloat(material, "_EyeBaseBrightness", 1.2f);
                    changed |= SetFloat(
                        material,
                        "_EyeParallaxStrength",
                        hasHdmf ? 0.02f : 0f);
                    changed |= SetVector(
                        material,
                        "_EyeUpperHighlightOffset",
                        Vector4.zero);
                    changed |= SetVector(
                        material,
                        "_EyeLowerHighlightOffset",
                        Vector4.zero);
                    changed |= SetVector(
                        material,
                        "_EyeUpperHighlightScale",
                        new Vector4(1f, 1f, 0f, 0f));
                    changed |= SetVector(
                        material,
                        "_EyeLowerHighlightScale",
                        new Vector4(1f, 1f, 0f, 0f));
                    changed |= SetFloat(material, "_EyeHighlightThreshold", 0.04000002f);
                    changed |= SetFloat(material, "_EyeHighlightSoftness", 0.001f);
                    changed |= SetFloat(material, "_EyeHighlightStrength", 1f);
                    changed |= SetFloat(material, "_EyeSecondHighlightStrength", 1f);
                    changed |= SetFloat(material, "_EyeHETScleraStrength", 1f);
                    changed |= SetFloat(material, "_EyeHETPupilStrength", 1f);
                    changed |= SetFloat(material, "_EyeHDMFHighlightStrength", 1f);
                    changed |= SetFloat(material, "_EyeEGStrength", 1f);
                    changed |= SetFloat(material, "_EyeEGFresnelPower", 3f);
                    changed |= SetFloat(material, "_EyeEGLightFollow", 0.08f);
                    changed |= SetFloat(material, "_EyeBaseEmissionStrength", 0f);
                    changed |= SetFloat(material, "_EmissionStrength", 1f);
                    changed |= SetFloat(material, "_EyeShadowStart", 0.25f);
                    changed |= SetFloat(material, "_EyeShadowEnd", 0.55f);
                    changed |= SetColor(
                        material,
                        "_EyeLitTint",
                        Color.white);
                    changed |= SetColor(
                        material,
                        "_EyeShadowTint",
                        new Color(0.82f, 0.82f, 0.82f, 1f));
                    changed |= SetFloat(material, "_Roughness", 0.52f);
                    changed |= SetFloat(material, "_SpecularStrength", 0.30f);
                    changed |= SetFloat(material, "_ReflectionStrength", 0.18f);
                    changed |= SetFloat(material, "_IndirectLightUsage", 1f);
                    changed |= SetFloat(material, "_MainLightColorUsage", 1f);
                }
                else if (shaderName == "MIKU/Wuwa/Hair")
                {
                    changed |= SetFloat(material, "_HairBaseBrightness", 1.20f);
                    changed |= SetFloat(material, "_HairLitMaskStrength", 0f);
                    changed |= SetFloat(material, "_HairSpecOffsetStrength", 0f);
                    changed |= SetFloat(material, "_HairSpecStrength", 0.16f);
                    changed |= SetFloat(material, "_Roughness", 0.65f);
                    changed |= SetFloat(material, "_SpecularStrength", 0.18f);
                    changed |= SetFloat(material, "_ReflectionStrength", 0.12f);
                    changed |= SetFloat(material, "_IndirectLightUsage", 1f);
                    changed |= SetFloat(material, "_MainLightColorUsage", 1f);
                    changed |= SetFloat(material, "_RimLightBrightness", 0.08f);
                    changed |= SetColor(
                        material,
                        "_VerticalGradientColor",
                        new Color(0.86f, 0.80f, 0.94f, 1f));
                    changed |= SetFloat(material, "_VerticalGradientStrength", 1f);
                    changed |= SetFloat(material, "_GradientUVIndex", 3f);
                    changed |= SetFloat(material, "_GradientInvert", 0f);
                    changed |= SetFloat(material, "_OutlineDistanceMode", 1f);
                    changed |= SetFloat(material, "_OutlineVertexColorMask", 1f);
                }
                else if (shaderName == "MIKU/Wuwa/Effect")
                {
                    changed |= SetFloat(material, "_EffectLayerBlend", 0f);
                    changed |= SetFloat(material, "_PrimaryEmissionStrength", 1.4f);
                }
            }

            if (changed)
                MikuManualTextureKeywordUtility.SyncKeywords(material);
            return changed;
        }

        internal static bool ApplyEndfieldEyeHighlight(
            Material material,
            Texture irisTexture)
        {
            if (material == null || material.shader == null ||
                !string.Equals(
                    material.shader.name,
                    "MIKU/Endfield/Overlay",
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_EYE_HIGHLIGHT_OVERLAY_REQUIRED");
            if (irisTexture == null)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_EYE_HIGHLIGHT_IRIS_REQUIRED");

            var changed = false;
            if (material.GetTexture("_BaseMap") != irisTexture)
            {
                material.SetTexture("_BaseMap", irisTexture);
                changed = true;
            }
            changed |= SetFloat(material, "_AlphaSource", 4f);
            changed |= SetFloat(material, "_AlphaClip", 0f);
            changed |= SetFloat(material, "_Cull", 0f);
            changed |= SetFloat(material, "_OverlayUseTintOnly", 0f);
            changed |= SetFloat(material, "_LightingMode", 0f);
            changed |= SetColor(material, "_BaseColorTint", Color.white);
            if (changed)
                MikuManualTextureKeywordUtility.SyncKeywords(material);
            return changed;
        }

        static bool ApplySkin(
            Material material,
            float intensity,
            float area,
            float brightness,
            float whitening,
            string requiredMaskProperty,
            bool logMissingMask)
        {
            var hasRequiredMask = string.IsNullOrEmpty(requiredMaskProperty) ||
                material.HasProperty(requiredMaskProperty) &&
                material.GetTexture(requiredMaskProperty) != null;
            if (!hasRequiredMask && logMissingMask)
            {
                Debug.LogWarning(
                    MissingSkinMaskDiagnostic + ":" + material.shader.name +
                    ":" + requiredMaskProperty,
                    material);
            }

            var changed = false;
            changed |= SetFloat(material, "_SkinSSSIntensity", hasRequiredMask ? intensity : 0f);
            changed |= SetFloat(material, "_SSSArea", area);
            // A missing authored Body mask must leave every surface untouched;
            // Genshin's default white LightMap would otherwise classify the
            // whole material as skin even though SSS itself is disabled.
            changed |= SetFloat(
                material,
                "_SkinToneBrightness",
                hasRequiredMask ? brightness : 1f);
            changed |= SetFloat(
                material,
                "_SkinToneWhitening",
                hasRequiredMask ? whitening : 0f);
            changed |= SetColor(material, "_SkinToneTarget", SkinTarget);
            changed |= SetColor(material, "_SSSColor", SssColor);
            changed |= SetFloat(material, "_SkinMaskDebugMode", 0f);
            return changed;
        }

        static bool SetFloat(Material material, string property, float value)
        {
            if (!material.HasProperty(property) ||
                Mathf.Approximately(material.GetFloat(property), value))
                return false;
            material.SetFloat(property, value);
            return true;
        }

        static bool SetColor(Material material, string property, Color value)
        {
            if (!material.HasProperty(property) ||
                material.GetColor(property) == value)
                return false;
            material.SetColor(property, value);
            return true;
        }

        static bool SetVector(Material material, string property, Vector4 value)
        {
            if (!material.HasProperty(property) ||
                material.GetVector(property) == value)
                return false;
            material.SetVector(property, value);
            return true;
        }
    }
}
