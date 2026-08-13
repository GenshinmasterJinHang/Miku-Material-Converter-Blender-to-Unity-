// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Miku.ShaderConverter.Runtime.GameToon;
using Miku.ShaderConverter.Runtime.Wuwa;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Idempotent installer for the Wuwa hair-shadow renderer feature.
    /// Mirrors the Screen Rim installer contract: at most one instance per
    /// Renderer Data asset, Undo-safe, and deterministic on repeated runs.
    /// </summary>
    public sealed class MikuWuwaRendererFeatureInstaller : EditorWindow
    {
        [SerializeField] ScriptableRendererData rendererData;
        [SerializeField] string preview = "";

        [MenuItem("Miku/Game Toon/Rendering/Wuwa Hair Shadow Installer")]
        static void Open() => OpenWindow();

        internal static MikuWuwaRendererFeatureInstaller OpenWindow(
            ScriptableRendererData preferred = null)
        {
            var window = GetWindow<MikuWuwaRendererFeatureInstaller>(
                MikuEditorLocalization.Tr("Wuwa Hair Shadow"));
            window.rendererData =
                preferred ??
                Selection.activeObject as ScriptableRendererData ??
                MikuToonRendererFeatureInstaller.ResolveDefaultRendererData();
            window.RefreshPreview();
            window.Repaint();
            return window;
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is ScriptableRendererData selected)
            {
                rendererData = selected;
                RefreshPreview();
                Repaint();
            }
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                MikuEditorLocalization.Tr(
                    "Select one Universal Renderer Data asset. Preview is " +
                    "read-only; Apply adds one Wuwa hair-shadow feature when " +
                    "none exists yet."),
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            rendererData = (ScriptableRendererData)EditorGUILayout.ObjectField(
                MikuEditorLocalization.Tr("Renderer Data"),
                rendererData,
                typeof(ScriptableRendererData),
                false);
            if (EditorGUI.EndChangeCheck())
                RefreshPreview();
            using (new EditorGUI.DisabledScope(rendererData == null))
            {
                if (GUILayout.Button(MikuEditorLocalization.Tr("Preview")))
                    RefreshPreview();
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(preview)
                        ? MikuEditorLocalization.Tr(
                            "Choose a Renderer Data asset.")
                        : preview,
                    MessageType.None);
                if (GUILayout.Button(MikuEditorLocalization.Tr("Apply")))
                {
                    var result = Install(rendererData);
                    preview = result.created
                        ? MikuEditorLocalization.Tr(
                            "Installed one Wuwa hair-shadow feature.")
                        : MikuEditorLocalization.Tr(
                            "Already installed; no duplicate was added.");
                    EditorGUIUtility.PingObject(rendererData);
                }
            }
        }

        void RefreshPreview()
        {
            if (rendererData == null)
            {
                preview = "";
                return;
            }
            var count = CountFeatures(rendererData);
            preview = count == 0
                ? MikuEditorLocalization.Tr(
                    "Apply will add one feature. No other Renderer Data asset " +
                    "will be changed.")
                : count == 1
                    ? MikuEditorLocalization.Tr(
                        "The feature is already installed. Apply is a no-op.")
                    : MikuEditorLocalization.Tr(
                        "Multiple existing Wuwa hair-shadow features were " +
                        "found. Apply will not add another; remove unwanted " +
                        "duplicates manually.");
        }

        /// <summary>
        /// Outcome of an Install call: the installed (or pre-existing)
        /// feature and whether this call created it.
        /// </summary>
        public readonly struct InstallResult
        {
            public InstallResult(
                MikuWuwaHairShadowRendererFeature feature,
                bool created)
                : this(
                    feature,
                    created,
                    null,
                    false,
                    null,
                    false,
                    MikuToonScreenRimRendererFeature.RimAlgorithm.LegacyFourTap,
                    false)
            {
            }

            public InstallResult(
                MikuWuwaHairShadowRendererFeature feature,
                bool created,
                MikuToonScreenRimRendererFeature screenRimFeature,
                bool screenRimCreated)
                : this(
                    feature,
                    created,
                    screenRimFeature,
                    screenRimCreated,
                    null,
                    false,
                    MikuToonScreenRimRendererFeature.RimAlgorithm.LegacyFourTap,
                    false)
            {
            }

            internal InstallResult(
                MikuWuwaHairShadowRendererFeature feature,
                bool created,
                MikuToonScreenRimRendererFeature screenRimFeature,
                bool screenRimCreated,
                MikuGameToonGeometryRendererFeature geometryFeature,
                bool geometryCreated,
                MikuToonScreenRimRendererFeature.RimAlgorithm
                    previousScreenRimAlgorithm,
                bool screenRimAlgorithmChanged)
            {
                this.feature = feature;
                this.created = created;
                this.screenRimFeature = screenRimFeature;
                this.screenRimCreated = screenRimCreated;
                this.geometryFeature = geometryFeature;
                this.geometryCreated = geometryCreated;
                this.previousScreenRimAlgorithm =
                    previousScreenRimAlgorithm;
                this.screenRimAlgorithmChanged =
                    screenRimAlgorithmChanged;
            }

            public readonly MikuWuwaHairShadowRendererFeature feature;
            public readonly bool created;
            public readonly MikuToonScreenRimRendererFeature screenRimFeature;
            public readonly bool screenRimCreated;
            public readonly MikuGameToonGeometryRendererFeature geometryFeature;
            public readonly bool geometryCreated;
            internal readonly MikuToonScreenRimRendererFeature.RimAlgorithm
                previousScreenRimAlgorithm;
            internal readonly bool screenRimAlgorithmChanged;
        }

        /// <summary>
        /// Returns how many Wuwa hair-shadow features already exist on the
        /// target Renderer Data asset (0 or 1 after Install).
        /// </summary>
        public static int CountFeatures(ScriptableRendererData target) =>
            target == null
                ? 0
                : target.rendererFeatures.Count(
                    item => item is MikuWuwaHairShadowRendererFeature);

        /// <summary>
        /// Installs one Wuwa hair-shadow feature on the target Renderer Data
        /// asset. Idempotent: repeated calls return the existing feature.
        /// </summary>
        public static InstallResult Install(
            ScriptableRendererData target,
            Action beforeCommit = null)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            var existing = target.rendererFeatures
                .OfType<MikuWuwaHairShadowRendererFeature>()
                .FirstOrDefault();
            var feature = existing;
            var created = false;
            var appended = false;
            MikuToonRendererFeatureInstaller.GeometryInstallResult geometry =
                default;
            MikuToonRendererFeatureInstaller.InstallResult screenRim = default;
            var screenRimConfigured = false;
            var previousRimAlgorithm =
                MikuToonScreenRimRendererFeature.RimAlgorithm.LegacyFourTap;
            try
            {
                if (feature == null)
                {
                    Undo.RegisterCompleteObjectUndo(
                        target,
                        MikuEditorLocalization.Tr(
                            "Install Wuwa Hair Shadow"));
                    feature = CreateInstance<
                        MikuWuwaHairShadowRendererFeature>();
                    feature.name = nameof(
                        MikuWuwaHairShadowRendererFeature);
                    Undo.RegisterCreatedObjectUndo(
                        feature,
                        MikuEditorLocalization.Tr(
                            "Install Wuwa Hair Shadow"));
                    AssetDatabase.AddObjectToAsset(feature, target);
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            feature,
                            out _,
                            out long localId))
                        throw new InvalidOperationException(
                            "MIKU_RENDERER_FEATURE_LOCAL_ID_FAILED");

                    AppendFeature(target, feature, localId);
                    appended = true;
                    created = true;
                    feature.Create();
                }
                else if (!feature.isActive)
                {
                    Undo.RecordObject(feature, "Activate Wuwa Hair Shadow");
                    feature.SetActive(true);
                    EditorUtility.SetDirty(feature);
                }

                geometry = MikuToonRendererFeatureInstaller.InstallGeometry(
                    target);
                screenRim = MikuToonRendererFeatureInstaller.Install(target);
                previousRimAlgorithm = screenRim.feature.settings.algorithm;
                SetTutorialScreenRim(screenRim.feature);
                screenRimConfigured = previousRimAlgorithm !=
                    MikuToonScreenRimRendererFeature.RimAlgorithm.WuwaTutorial;
                beforeCommit?.Invoke();
                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(feature);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    AssetDatabase.GetAssetPath(target),
                    ImportAssetOptions.ForceSynchronousImport);
                return new InstallResult(
                    feature,
                    created,
                    screenRim.feature,
                    screenRim.created,
                    geometry.feature,
                    geometry.created,
                    previousRimAlgorithm,
                    screenRimConfigured);
            }
            catch
            {
                if (screenRimConfigured && screenRim.feature != null)
                {
                    if (screenRim.created)
                        MikuToonRendererFeatureInstaller.RemoveInstalledFeature(
                            target,
                            screenRim.feature);
                    else
                    {
                        screenRim.feature.settings.algorithm =
                            previousRimAlgorithm;
                        screenRim.feature.Create();
                        EditorUtility.SetDirty(screenRim.feature);
                    }
                }
                if (geometry.created && geometry.feature != null)
                    MikuToonRendererFeatureInstaller
                        .RemoveInstalledGeometryFeature(
                            target,
                            geometry.feature);
                if (appended && feature != null)
                    RemoveFeatureReference(target, feature);
                if (created && feature != null)
                    DestroyImmediate(feature, true);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                throw;
            }
        }

        internal static void RollbackInstall(
            ScriptableRendererData target,
            InstallResult result)
        {
            if (target == null)
                return;
            if (result.screenRimFeature != null)
            {
                if (result.screenRimCreated)
                    MikuToonRendererFeatureInstaller.RemoveInstalledFeature(
                        target,
                        result.screenRimFeature);
                else if (result.screenRimAlgorithmChanged)
                {
                    result.screenRimFeature.settings.algorithm =
                        result.previousScreenRimAlgorithm;
                    result.screenRimFeature.Create();
                    EditorUtility.SetDirty(result.screenRimFeature);
                }
            }
            if (result.geometryCreated && result.geometryFeature != null)
                MikuToonRendererFeatureInstaller
                    .RemoveInstalledGeometryFeature(
                        target,
                        result.geometryFeature);
            if (result.created && result.feature != null)
            {
                RemoveFeatureReference(target, result.feature);
                DestroyImmediate(result.feature, true);
            }
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        static void SetTutorialScreenRim(
            MikuToonScreenRimRendererFeature feature)
        {
            if (feature == null)
                throw new InvalidOperationException(
                    "MIKU_WUWA_SCREEN_RIM_INSTALL_FAILED");
            feature.settings.algorithm =
                MikuToonScreenRimRendererFeature.RimAlgorithm.WuwaTutorial;
            feature.Create();
        }

        static void AppendFeature(
            ScriptableRendererData target,
            ScriptableRendererFeature feature,
            long localId)
        {
            var serialized = new SerializedObject(target);
            serialized.Update();
            var features = serialized.FindProperty("m_RendererFeatures");
            var featureMap = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_FEATURE_SCHEMA_UNSUPPORTED:URP17.4");
            var index = features.arraySize;
            features.arraySize = index + 1;
            features.GetArrayElementAtIndex(index).objectReferenceValue =
                feature;
            featureMap.arraySize = index + 1;
            featureMap.GetArrayElementAtIndex(index).longValue = localId;
            serialized.ApplyModifiedProperties();
        }

        static void RemoveFeatureReference(
            ScriptableRendererData target,
            ScriptableRendererFeature feature)
        {
            var serialized = new SerializedObject(target);
            serialized.Update();
            var features = serialized.FindProperty("m_RendererFeatures");
            var featureMap = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
                return;
            for (var index = features.arraySize - 1; index >= 0; index--)
            {
                if (features.GetArrayElementAtIndex(index)
                        .objectReferenceValue != feature)
                    continue;
                for (var move = index;
                     move + 1 < features.arraySize;
                     move++)
                {
                    features.GetArrayElementAtIndex(move)
                        .objectReferenceValue = features
                        .GetArrayElementAtIndex(move + 1)
                        .objectReferenceValue;
                    if (move + 1 < featureMap.arraySize)
                        featureMap.GetArrayElementAtIndex(move).longValue =
                            featureMap.GetArrayElementAtIndex(move + 1)
                                .longValue;
                }
                features.arraySize--;
                if (featureMap.arraySize > features.arraySize)
                    featureMap.arraySize = features.arraySize;
                break;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
