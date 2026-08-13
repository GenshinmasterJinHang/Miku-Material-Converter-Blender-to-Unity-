// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Miku.ShaderConverter.Runtime.Genshin
{
    /// <summary>Serialized modes backed by the legacy <c>_DiffuseA</c> field.</summary>
    public enum MikuGenshinAlphaMode
    {
        None = 0,
        Cutout = 1,
        DiffuseAlphaEmission = 2,
    }

    /// <summary>Synchronizes Genshin material properties, keywords, and passes.</summary>
    public static class MikuGenshinMaterialState
    {
        public const string BackfacePassName = "MikuGenshinBackface";
        public const string OutlinePassName = "MikuToonOutline";
        const string AlphaModeProperty = "_DiffuseA";
        const string BackfaceProperty = "_UseUv1Backface";
        const string StateVersionProperty = "_MikuGenshinMaterialStateVersion";
        const float CurrentStateVersion = 240f;

        public static MikuGenshinAlphaMode GetAlphaMode(Material material)
        {
            RequireMaterial(material);
            if (!material.HasProperty(AlphaModeProperty))
                return MikuGenshinAlphaMode.None;
            return (MikuGenshinAlphaMode)Mathf.Clamp(
                Mathf.RoundToInt(material.GetFloat(AlphaModeProperty)),
                0,
                2);
        }

        public static void SetAlphaMode(
            Material material,
            MikuGenshinAlphaMode mode)
        {
            RequireMaterial(material);
            if (mode < MikuGenshinAlphaMode.None ||
                mode > MikuGenshinAlphaMode.DiffuseAlphaEmission)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (!material.HasProperty(AlphaModeProperty))
                throw new InvalidOperationException(
                    "MIKU_GENSHIN_ALPHA_MODE_UNSUPPORTED:" +
                    material.shader.name);
            material.SetFloat(AlphaModeProperty, (float)mode);
        }

        public static bool GetUv1Backface(Material material)
        {
            RequireMaterial(material);
            return material.HasProperty(BackfaceProperty) &&
                   material.GetFloat(BackfaceProperty) > 0.5f;
        }

        public static void SetUv1Backface(Material material, bool enabled)
        {
            RequireMaterial(material);
            if (!material.HasProperty(BackfaceProperty) ||
                material.FindPass(BackfacePassName) < 0)
            {
                if (enabled)
                    throw new InvalidOperationException(
                        "MIKU_GENSHIN_UV1_BACKFACE_UNSUPPORTED:" +
                        material.shader.name);
                return;
            }
            material.SetFloat(BackfaceProperty, enabled ? 1f : 0f);
            if (material.HasProperty("_DoubleSided"))
                material.SetFloat("_DoubleSided", enabled ? 1f : 0f);
            if (material.HasProperty("_BackUV1"))
                material.SetFloat("_BackUV1", enabled ? 1f : 0f);
            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Back);
            material.DisableKeyword("_GENSHIN_DOUBLE_SIDED");
            material.SetShaderPassEnabled(BackfacePassName, enabled);
        }

        public static void Synchronize(Material material)
        {
            RequireMaterial(material);
            if (material.shader == null ||
                !material.shader.name.StartsWith(
                    "MIKU/Genshin/",
                    StringComparison.Ordinal))
                return;

            if (material.HasProperty(StateVersionProperty) &&
                material.GetFloat(StateVersionProperty) < CurrentStateVersion)
            {
                var legacyDoubleSided = material.HasProperty("_DoubleSided") &&
                                        material.GetFloat("_DoubleSided") > 0.5f;
                var legacyUv1 = legacyDoubleSided &&
                                material.HasProperty("_BackUV1") &&
                                material.GetFloat("_BackUV1") > 0.5f;
                if (material.HasProperty(BackfaceProperty))
                    material.SetFloat(BackfaceProperty, legacyUv1 ? 1f : 0f);
                material.SetFloat(StateVersionProperty, CurrentStateVersion);
                if (legacyDoubleSided && !legacyUv1)
                {
                    Debug.LogWarning(
                        "MIKU_GENSHIN_LEGACY_UV0_DOUBLE_SIDED_UPDATED:" +
                        material.name);
                }
            }

            if (material.HasProperty(AlphaModeProperty))
                SetAlphaMode(material, GetAlphaMode(material));
            if (material.HasProperty(BackfaceProperty))
                SetUv1Backface(material, GetUv1Backface(material));
        }

        static void RequireMaterial(Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
        }
    }
}
