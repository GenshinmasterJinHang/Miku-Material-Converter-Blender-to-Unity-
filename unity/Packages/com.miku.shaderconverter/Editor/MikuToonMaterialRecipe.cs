// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    [Serializable]
    public sealed class MikuToonPresetSnapshot
    {
        public float toonSteps;
        public float shadowSoftness;
        public float sssStrength;
        public float rimIntensity;
        public float outlineWidth;

        internal static MikuToonPresetSnapshot Capture(Material material)
        {
            float Read(string property) =>
                material != null && material.HasProperty(property)
                    ? material.GetFloat(property)
                    : 0f;
            return new MikuToonPresetSnapshot
            {
                toonSteps = Read("_MIKU_ToonSteps"),
                shadowSoftness = Read("_MIKU_ShadowSoftness"),
                sssStrength = Read("_MIKU_SSSStrength"),
                rimIntensity = Read("_MIKU_RimIntensity"),
                outlineWidth = Read("_MIKU_OutlineWidth"),
            };
        }
    }

    public enum MikuToonSemantic
    {
        Face,
        BodySkin,
        Hair,
        Eye,
        Mouth,
        Cloth,
        MetalAccessory,
        GenericOpaque,
    }

    public enum MikuToonAlbedoMode
    {
        Auto,
        Override,
        Solid,
    }

    /// <summary>
    /// Miku-owned synchronization metadata for one user-owned Toon material.
    /// It contains no timestamps or machine-specific absolute paths.
    /// </summary>
    public sealed class MikuToonMaterialRecipe : ScriptableObject
    {
        public const string CurrentShaderFamilyVersion = "1.0.0";

        public Material sourceMaterial;
        public Material generatedBaseMaterial;
        public Material userMaterial;
        public MikuToonSemantic semantic = MikuToonSemantic.GenericOpaque;
        public MikuToonAlbedoMode albedoMode = MikuToonAlbedoMode.Auto;
        public string sourceGuid = "";
        public string targetGuid = "";
        public string stableGuid = "";
        public string shaderFamilyVersion = CurrentShaderFamilyVersion;
        public MikuToonPresetSnapshot initialPreset =
            new MikuToonPresetSnapshot();
        public Texture sourceTexture;
        public Color sourceColor = Color.white;
        public Texture lastSyncedTexture;
        public Color lastSyncedColor = Color.white;
        public float lastSyncedCutoff = 0.5f;
    }

    internal static class MikuToonRecipeUtility
    {
        internal const string BaseMap = "_MIKU_BaseMap";
        internal const string BaseColor = "_MIKU_BaseColor";
        internal const string Cutoff = "_MIKU_Cutoff";

        internal static string ShaderName(MikuToonSemantic semantic) =>
            "Miku/GenericToon/" + semantic;

        internal static void ApplySemanticPreset(
            Material material,
            MikuToonSemantic semantic)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            Undo.RecordObject(material, "Reset Miku Toon semantic preset");
            Set(material, "_MIKU_ToonSteps", semantic == MikuToonSemantic.Face ? 3f : 2f);
            Set(material, "_MIKU_ShadowSoftness", semantic == MikuToonSemantic.MetalAccessory ? 0.08f : 0.16f);
            Set(material, "_MIKU_SSSStrength",
                semantic == MikuToonSemantic.Face ||
                semantic == MikuToonSemantic.BodySkin ? 0.35f : 0f);
            Set(material, "_MIKU_RimIntensity",
                semantic == MikuToonSemantic.Eye ? 0.75f : 0.25f);
            Set(material, "_MIKU_OutlineWidth",
                semantic == MikuToonSemantic.Eye ||
                semantic == MikuToonSemantic.Mouth ? 0.5f : 1.25f);
            EditorUtility.SetDirty(material);
        }

        internal static void Rebuild(MikuToonMaterialRecipe recipe)
        {
            if (recipe == null || recipe.userMaterial == null)
                throw new InvalidOperationException("MIKU_TOON_RECIPE_TARGET_MISSING");
            var target = recipe.userMaterial;
            var nextTexture = recipe.sourceTexture;
            var nextColor = recipe.sourceColor;
            if (recipe.sourceMaterial != null)
                ResolveSource(
                    recipe.sourceMaterial,
                    recipe.albedoMode,
                    recipe.sourceTexture,
                    recipe.sourceColor,
                    out nextTexture,
                    out nextColor);

            Undo.RecordObjects(
                new UnityEngine.Object[] { target, recipe },
                "Rebuild Miku Toon material");
            if (target.HasProperty(BaseMap) &&
                target.GetTexture(BaseMap) == recipe.lastSyncedTexture)
                target.SetTexture(BaseMap, nextTexture);
            if (target.HasProperty(BaseColor) &&
                Approximately(target.GetColor(BaseColor), recipe.lastSyncedColor))
                target.SetColor(BaseColor, nextColor);
            recipe.sourceTexture = nextTexture;
            recipe.sourceColor = nextColor;
            recipe.lastSyncedTexture = nextTexture;
            recipe.lastSyncedColor = nextColor;
            recipe.shaderFamilyVersion =
                MikuToonMaterialRecipe.CurrentShaderFamilyVersion;
            EditorUtility.SetDirty(target);
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }

        internal static void RestoreSourceValues(MikuToonMaterialRecipe recipe)
        {
            if (recipe == null || recipe.userMaterial == null)
                return;
            ResolveSource(
                recipe.sourceMaterial,
                recipe.albedoMode,
                recipe.sourceTexture,
                recipe.sourceColor,
                out var texture,
                out var color);
            Undo.RecordObjects(
                new UnityEngine.Object[] { recipe.userMaterial, recipe },
                "Restore Miku Toon source values");
            if (recipe.userMaterial.HasProperty(BaseMap))
                recipe.userMaterial.SetTexture(BaseMap, texture);
            if (recipe.userMaterial.HasProperty(BaseColor))
                recipe.userMaterial.SetColor(BaseColor, color);
            recipe.sourceTexture = texture;
            recipe.sourceColor = color;
            recipe.lastSyncedTexture = texture;
            recipe.lastSyncedColor = color;
            EditorUtility.SetDirty(recipe.userMaterial);
            EditorUtility.SetDirty(recipe);
        }

        internal static void ResolveSource(
            Material source,
            MikuToonAlbedoMode mode,
            Texture overrideTexture,
            Color overrideColor,
            out Texture texture,
            out Color color)
        {
            if (mode == MikuToonAlbedoMode.Override)
            {
                texture = overrideTexture;
                color = overrideColor;
                return;
            }
            if (mode == MikuToonAlbedoMode.Solid)
            {
                texture = null;
                color = overrideColor;
                return;
            }
            if (source == null)
                throw new InvalidOperationException("MIKU_TOON_SOURCE_MATERIAL_MISSING");

            var baseMap = source.HasProperty("_BaseMap")
                ? source.GetTexture("_BaseMap")
                : null;
            var mainTex = source.HasProperty("_MainTex")
                ? source.GetTexture("_MainTex")
                : null;
            if (baseMap != null && mainTex != null && baseMap != mainTex)
                throw new InvalidOperationException(
                    "MIKU_TOON_ALBEDO_AMBIGUOUS:_BaseMap,_MainTex");
            texture = baseMap != null ? baseMap : mainTex;
            var baseColor = source.HasProperty("_BaseColor")
                ? source.GetColor("_BaseColor")
                : Color.white;
            var mainColor = source.HasProperty("_Color")
                ? source.GetColor("_Color")
                : baseColor;
            if (source.HasProperty("_BaseColor") &&
                source.HasProperty("_Color") &&
                !Approximately(baseColor, mainColor))
                throw new InvalidOperationException(
                    "MIKU_TOON_COLOR_AMBIGUOUS:_BaseColor,_Color");
            color = source.HasProperty("_BaseColor") ? baseColor : mainColor;
        }

        internal static MikuToonMaterialRecipe CreateOrUpdateImported(
            string path,
            Material generatedBase,
            Material userMaterial)
        {
            var recipe =
                AssetDatabase.LoadAssetAtPath<MikuToonMaterialRecipe>(path);
            var created = recipe == null;
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<
                    MikuToonMaterialRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }
            recipe.generatedBaseMaterial = generatedBase;
            recipe.userMaterial = userMaterial;
            recipe.semantic = MikuToonSemantic.GenericOpaque;
            recipe.sourceGuid = "";
            recipe.targetGuid =
                AssetDatabase.AssetPathToGUID(
                    AssetDatabase.GetAssetPath(userMaterial));
            recipe.stableGuid = AssetDatabase.AssetPathToGUID(path);
            recipe.shaderFamilyVersion =
                MikuToonMaterialRecipe.CurrentShaderFamilyVersion;
            if (created || recipe.initialPreset == null)
                recipe.initialPreset =
                    MikuToonPresetSnapshot.Capture(generatedBase);
            if (userMaterial != null)
            {
                recipe.lastSyncedTexture = userMaterial.HasProperty(BaseMap)
                    ? userMaterial.GetTexture(BaseMap)
                    : null;
                recipe.lastSyncedColor = userMaterial.HasProperty(BaseColor)
                    ? userMaterial.GetColor(BaseColor)
                    : Color.white;
            }
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssetIfDirty(recipe);
            return recipe;
        }

        static void Set(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        static bool Approximately(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.00001f &&
            Mathf.Abs(a.g - b.g) < 0.00001f &&
            Mathf.Abs(a.b - b.b) < 0.00001f &&
            Mathf.Abs(a.a - b.a) < 0.00001f;
    }
}
