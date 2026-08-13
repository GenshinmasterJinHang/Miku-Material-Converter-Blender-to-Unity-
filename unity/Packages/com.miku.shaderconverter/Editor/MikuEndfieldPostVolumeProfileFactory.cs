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

            var colorAdjustments = Reconcile<ColorAdjustments>(profile);
            Configure(colorAdjustments, component =>
            {
                Override(component.postExposure, 0.35f);
                Override(component.contrast, 16f);
                Override(component.saturation, 8f);
                Override(component.hueShift, 0f);
                Override(component.colorFilter, Color.white);
            });
            var colorCurves = Reconcile<ColorCurves>(profile);
            Configure(colorCurves, ConfigureIdentityCurves);
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
                colorAdjustments,
                colorCurves,
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
            profile.components.Add(colorAdjustments);
            profile.components.Add(colorCurves);
            profile.components.Add(tonemapping);
            profile.components.Add(bloom);
            profile.components.Add(vignette);
            EditorUtility.SetDirty(profile);
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

        static void ConfigureIdentityCurves(ColorCurves component)
        {
            SetCurve(component.master, LinearCurve());
            SetCurve(component.red, LinearCurve());
            SetCurve(component.green, LinearCurve());
            SetCurve(component.blue, LinearCurve());
            SetCurve(component.hueVsHue, FlatCurve(true));
            SetCurve(component.hueVsSat, FlatCurve(true));
            SetCurve(component.satVsSat, FlatCurve(false));
            SetCurve(component.lumVsSat, FlatCurve(false));
        }

        static TextureCurve LinearCurve()
        {
            return Curve(
                false,
                0f,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f));
        }

        static TextureCurve FlatCurve(bool loop)
        {
            return Curve(
                loop,
                0.5f,
                new Vector2(0f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f));
        }

        static void SetCurve(
            TextureCurveParameter parameter,
            TextureCurve curve)
        {
            parameter.value = curve;
            parameter.overrideState = true;
        }

        static TextureCurve Curve(
            bool loop,
            float zeroValue,
            params Vector2[] points)
        {
            if (points == null || points.Length < 2)
                throw new ArgumentException(
                    "At least two curve points are required.",
                    nameof(points));

            var keys = new Keyframe[points.Length];
            for (var index = 0; index < points.Length; index++)
            {
                var previous = points[Mathf.Max(0, index - 1)];
                var next = points[Mathf.Min(points.Length - 1, index + 1)];
                var tangent = index == 0
                    ? (next.y - points[index].y) /
                      Mathf.Max(next.x - points[index].x, Mathf.Epsilon)
                    : index == points.Length - 1
                        ? (points[index].y - previous.y) /
                          Mathf.Max(
                              points[index].x - previous.x,
                              Mathf.Epsilon)
                        : (next.y - previous.y) /
                          Mathf.Max(next.x - previous.x, Mathf.Epsilon);
                keys[index] = new Keyframe(
                    points[index].x,
                    points[index].y,
                    tangent,
                    tangent);
            }
            return new TextureCurve(
                keys,
                zeroValue,
                loop,
                new Vector2(0f, 1f));
        }

        static void Override<T>(VolumeParameter<T> parameter, T value)
        {
            parameter.value = value;
            parameter.overrideState = true;
        }
    }
}
