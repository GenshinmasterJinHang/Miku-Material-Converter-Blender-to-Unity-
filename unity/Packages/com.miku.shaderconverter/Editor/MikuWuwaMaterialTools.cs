// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Public editor entry points for Wuwa material calibration. The
    /// recommended profile and keyword synchronization are internal helpers;
    /// this class exposes them for deterministic editor automation and
    /// validation-project setup without duplicating the tuning tables.
    /// </summary>
    public static class MikuWuwaMaterialTools
    {
        /// <summary>
        /// Applies the recommended Wuwa profile and synchronizes shader
        /// keywords for one material. Returns true when any property changed.
        /// </summary>
        public static bool ApplyRecommendedProfile(Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            var changed = MikuGameToonMaterialProfiles.ApplyRecommended(
                material);
            MikuManualTextureKeywordUtility.SyncKeywords(material);
            if (changed)
                EditorUtility.SetDirty(material);
            return changed;
        }

        /// <summary>
        /// Synchronizes texture-driven keywords for one Wuwa material without
        /// changing tuning values.
        /// </summary>
        public static void SyncKeywords(Material material)
        {
            if (material == null)
                throw new ArgumentNullException(nameof(material));
            MikuManualTextureKeywordUtility.SyncKeywords(material);
            EditorUtility.SetDirty(material);
        }
    }
}
