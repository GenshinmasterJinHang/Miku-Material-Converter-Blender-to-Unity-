// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuEndfieldPostVolumeProfileFactory
    {
        internal static VolumeProfile CreateOrUpdate(string path)
        {
            return CreateOrUpdate(path, null);
        }

        internal static VolumeProfile CreateOrUpdate(
            string path,
            ICollection<string> createdAssetPaths)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is VolumeProfile))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_POST_ASSET_CONFLICT:" + path);

            var profile = existing as VolumeProfile;
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(profile, path);
                Undo.RegisterCreatedObjectUndo(
                    profile,
                    "Create Miku Endfield Volume Profile");
                createdAssetPaths?.Add(path);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(
                    profile,
                    "Update Miku Endfield Volume Profile");
            }

            var tonemapping = Reconcile<Tonemapping>(profile);
            Configure(tonemapping, component =>
            {
                Override(component.mode, TonemappingMode.Neutral);
            });
            var bloom = Reconcile<Bloom>(profile);
            Configure(bloom, component =>
            {
                Override(component.threshold, 0.85f);
                Override(component.intensity, 0.20f);
                Override(component.scatter, 0.65f);
                Override(component.clamp, 4f);
                Override(component.tint, Color.white);
                Override(component.highQualityFiltering, true);
            });
            var vignette = Reconcile<Vignette>(profile);
            Configure(vignette, component =>
            {
                Override(component.color, Color.black);
                Override(component.center, new Vector2(0.5f, 0.5f));
                Override(component.intensity, 0.04f);
                Override(component.smoothness, 0.50f);
                Override(component.rounded, false);
            });

            var keep = new HashSet<VolumeComponent>
            {
                tonemapping,
                bloom,
                vignette,
            };
            foreach (var component in profile.components.ToArray())
            {
                if (keep.Contains(component))
                    continue;
                profile.components.Remove(component);
                if (component != null)
                    Undo.DestroyObjectImmediate(component);
            }
            profile.components.Clear();
            profile.components.Add(tonemapping);
            profile.components.Add(bloom);
            profile.components.Add(vignette);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        static T Reconcile<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            var matches = profile.components
                .Where(item => item != null && item.GetType() == typeof(T))
                .Cast<T>()
                .ToArray();
            var component = matches.FirstOrDefault();
            foreach (var duplicate in matches.Skip(1))
            {
                profile.components.Remove(duplicate);
                Undo.DestroyObjectImmediate(duplicate);
            }
            if (component != null)
            {
                Undo.RegisterCompleteObjectUndo(
                    component,
                    "Update Miku Endfield Volume Profile");
                return component;
            }

            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            Undo.RegisterCreatedObjectUndo(
                component,
                "Create Miku Endfield Volume Component");
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void Configure<T>(T component, Action<T> configure)
            where T : VolumeComponent
        {
            component.active = true;
            component.name = typeof(T).Name;
            configure(component);
            EditorUtility.SetDirty(component);
        }

        static void Override<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }
    }
}
