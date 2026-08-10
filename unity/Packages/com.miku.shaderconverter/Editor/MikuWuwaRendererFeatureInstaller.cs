// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
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
            {
                this.feature = feature;
                this.created = created;
            }

            public readonly MikuWuwaHairShadowRendererFeature feature;
            public readonly bool created;
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
            if (existing != null)
                return new InstallResult(existing, false);

            MikuWuwaHairShadowRendererFeature feature = null;
            var appended = false;
            try
            {
                Undo.RegisterCompleteObjectUndo(
                    target,
                    MikuEditorLocalization.Tr(
                        "Install Wuwa Hair Shadow"));
                feature = CreateInstance<MikuWuwaHairShadowRendererFeature>();
                feature.name = nameof(MikuWuwaHairShadowRendererFeature);
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
                feature.Create();
                beforeCommit?.Invoke();
                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(feature);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    AssetDatabase.GetAssetPath(target),
                    ImportAssetOptions.ForceSynchronousImport);
                return new InstallResult(feature, true);
            }
            catch
            {
                if (appended && feature != null)
                    RemoveFeatureReference(target, feature);
                if (feature != null)
                    DestroyImmediate(feature, true);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                throw;
            }
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
