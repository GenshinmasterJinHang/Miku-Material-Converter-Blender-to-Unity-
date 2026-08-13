// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace Miku.ShaderConverter.Runtime.Endfield
{
    /// <summary>
    /// Synchronizes Endfield outline properties with the serialized ShaderLab
    /// pass state used by Unity's renderer lists.
    /// </summary>
    public static class MikuEndfieldMaterialState
    {
        /// <summary>The ShaderLab pass name used by Endfield outlines.</summary>
        public const string OutlinePassName = "Outline";

        const string OutlineProperty = "_UseOutline";
        const string StateVersionProperty =
            "_MikuEndfieldMaterialStateVersion";
        const float CurrentStateVersion = 300f;

        /// <summary>
        /// Returns whether both the Endfield outline property and its ShaderLab
        /// pass are enabled.
        /// </summary>
        public static bool GetOutlineEnabled(Material material)
        {
            RequireMaterial(material);
            return SupportsOutline(material) &&
                   material.GetFloat(OutlineProperty) > 0.5f &&
                   material.GetShaderPassEnabled(OutlinePassName);
        }

        /// <summary>
        /// Enables or disables Endfield outline rendering without changing the
        /// material's lighting shader or width controls.
        /// </summary>
        public static void SetOutlineEnabled(Material material, bool enabled)
        {
            RequireMaterial(material);
            if (!SupportsOutline(material))
            {
                if (enabled)
                    throw new InvalidOperationException(
                        "MIKU_ENDFIELD_OUTLINE_UNSUPPORTED:" +
                        material.shader.name);
                return;
            }

            material.SetFloat(OutlineProperty, enabled ? 1f : 0f);
            material.SetShaderPassEnabled(OutlinePassName, enabled);
            if (material.HasProperty(StateVersionProperty))
                material.SetFloat(
                    StateVersionProperty,
                    CurrentStateVersion);
        }

        /// <summary>
        /// Migrates legacy Endfield materials and repairs property/pass state
        /// drift. An existing disabled Outline pass is preserved on first sync.
        /// </summary>
        public static void Synchronize(Material material)
        {
            RequireMaterial(material);
            if (material.shader == null ||
                !material.shader.name.StartsWith(
                    "MIKU/Endfield/",
                    StringComparison.Ordinal) ||
                !SupportsOutline(material))
                return;

            if (material.HasProperty(StateVersionProperty) &&
                material.GetFloat(StateVersionProperty) < CurrentStateVersion)
            {
                var legacyPassEnabled = material.GetShaderPassEnabled(
                    OutlinePassName);
                material.SetFloat(
                    OutlineProperty,
                    legacyPassEnabled ? 1f : 0f);
            }

            SetOutlineEnabled(
                material,
                material.GetFloat(OutlineProperty) > 0.5f);
        }

        static bool SupportsOutline(Material material) =>
            material.shader != null &&
            material.HasProperty(OutlineProperty) &&
            material.FindPass(OutlinePassName) >= 0;

        static void RequireMaterial(Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
        }
    }
}
