// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor
{
    internal static class MikuAnimeVolumeProfileFactory
    {
        internal const string ProfilePath =
            "Packages/com.miku.shaderconverter/Runtime/Profiles/" +
            "MikuAnimeGlobalVolumeProfile.asset";

        [MenuItem(
            "Miku/Game Toon/Rendering/Rebuild Anime Global Volume Profile",
            priority = 230)]
        static void RebuildStableProfile()
        {
            CreateOrReplace(ProfilePath);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                ProfilePath);
        }

        internal static VolumeProfile CreateOrReplace(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(profile, path);

            Add<Tonemapping>(profile, nameof(Tonemapping), component =>
            {
                Override(component.mode, TonemappingMode.Neutral);
            });
            Add<WhiteBalance>(profile, nameof(WhiteBalance), component =>
            {
                Override(component.temperature, 0f);
                Override(component.tint, 0f);
            });
            Add<ChannelMixer>(profile, nameof(ChannelMixer), component =>
            {
                Override(component.redOutRedIn, 100f);
                Override(component.redOutGreenIn, 0f);
                Override(component.redOutBlueIn, 0f);
                Override(component.greenOutRedIn, 0f);
                Override(component.greenOutGreenIn, 100f);
                Override(component.greenOutBlueIn, 0f);
                Override(component.blueOutRedIn, 0f);
                Override(component.blueOutGreenIn, 0f);
                Override(component.blueOutBlueIn, 100f);
            });
            Add<LiftGammaGain>(profile, nameof(LiftGammaGain), component =>
            {
                Override(component.lift, new Vector4(1f, 1f, 1f, 0f));
                Override(component.gamma, new Vector4(1f, 1f, 1f, 0f));
                Override(component.gain, new Vector4(1f, 1f, 1f, 0f));
            });
            Add<ShadowsMidtonesHighlights>(
                profile,
                nameof(ShadowsMidtonesHighlights),
                component =>
                {
                    Override(component.shadows, new Vector4(1f, 1f, 1f, 0f));
                    Override(component.midtones, new Vector4(1f, 1f, 1f, 0f));
                    Override(component.highlights, new Vector4(1f, 1f, 1f, 0f));
                    Override(component.shadowsStart, 0f);
                    Override(component.shadowsEnd, 0.35f);
                    Override(component.highlightsStart, 0.58f);
                    Override(component.highlightsEnd, 1f);
                });
            Add<SplitToning>(profile, nameof(SplitToning), component =>
            {
                Override(component.shadows, Color.gray);
                Override(component.highlights, Color.gray);
                Override(component.balance, 0f);
            });
            Add<ColorCurves>(profile, nameof(ColorCurves), component =>
            {
                SetCurve(
                    component.master,
                    Curve(
                        false,
                        0f,
                        new Vector2(0f, 0f),
                        new Vector2(0.12f, 0.10f),
                        new Vector2(0.28f, 0.32f),
                        new Vector2(0.50f, 0.59f),
                        new Vector2(0.75f, 0.84f),
                        new Vector2(1f, 1f)));
                SetCurve(
                    component.red,
                    Curve(
                        false,
                        0f,
                        new Vector2(0f, 0f),
                        new Vector2(0.25f, 0.25f),
                        new Vector2(0.50f, 0.50f),
                        new Vector2(0.75f, 0.75f),
                        new Vector2(1f, 1f)));
                SetCurve(
                    component.green,
                    Curve(
                        false,
                        0f,
                        new Vector2(0f, 0f),
                        new Vector2(0.25f, 0.25f),
                        new Vector2(0.50f, 0.50f),
                        new Vector2(0.75f, 0.75f),
                        new Vector2(1f, 1f)));
                SetCurve(
                    component.blue,
                    Curve(
                        false,
                        0f,
                        new Vector2(0f, 0f),
                        new Vector2(0.25f, 0.25f),
                        new Vector2(0.50f, 0.50f),
                        new Vector2(0.75f, 0.75f),
                        new Vector2(1f, 1f)));
                SetCurve(
                    component.hueVsHue,
                    Curve(
                        true,
                        0.5f,
                        new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(1f, 0.5f)));
                SetCurve(
                    component.hueVsSat,
                    Curve(
                        true,
                        0.5f,
                        new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(1f, 0.5f)));
                SetCurve(
                    component.satVsSat,
                    Curve(
                        false,
                        0.5f,
                        new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(1f, 0.5f)));
                SetCurve(
                    component.lumVsSat,
                    Curve(
                        false,
                        0.5f,
                        new Vector2(0f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new Vector2(1f, 0.5f)));
            });
            Add<ColorAdjustments>(profile, "Color Adjustments", component =>
            {
                Override(component.postExposure, 0.35f);
                Override(component.contrast, 16f);
                Override(component.saturation, 8f);
                Override(component.hueShift, 0f);
                Override(component.colorFilter, Color.white);
            });
            Add<Bloom>(profile, nameof(Bloom), component =>
            {
                Override(component.threshold, 0.85f);
                Override(component.intensity, 0.20f);
                Override(component.scatter, 0.65f);
                Override(component.clamp, 4f);
                Override(component.tint, Color.white);
                Override(component.highQualityFiltering, true);
            });
            Add<Vignette>(profile, nameof(Vignette), component =>
            {
                Override(component.color, Color.black);
                Override(component.center, new Vector2(0.5f, 0.5f));
                Override(component.intensity, 0.04f);
                Override(component.smoothness, 0.50f);
                Override(component.rounded, false);
            });

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        }

        static void AddSubAsset(VolumeComponent component, VolumeProfile profile)
        {
            EditorUtility.SetDirty(component);
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        static void Add<T>(
            VolumeProfile profile,
            string name,
            Action<T> configure)
            where T : VolumeComponent
        {
            var component = profile.Add<T>(false);
            component.name = name;
            configure(component);
            AddSubAsset(component, profile);
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
                          Mathf.Max(points[index].x - previous.x, Mathf.Epsilon)
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
