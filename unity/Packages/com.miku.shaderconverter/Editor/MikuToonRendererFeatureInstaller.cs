// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Miku.ShaderConverter.Runtime.GenericToon;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor
{
    public sealed class MikuToonRendererFeatureInstaller : EditorWindow
    {
        [SerializeField] ScriptableRendererData rendererData;
        [SerializeField] string preview = "";

        [MenuItem(
            "Miku/Generic Toon/Rendering/Screen Rim Installer")]
        static void Open() => OpenWindow();

        internal static MikuToonRendererFeatureInstaller OpenWindow(
            ScriptableRendererData preferred = null)
        {
            var window = GetWindow<MikuToonRendererFeatureInstaller>(
                "Miku Toon Screen Rim");
            window.rendererData =
                preferred ??
                Selection.activeObject as ScriptableRendererData ??
                ResolveDefaultRendererData();
            window.RefreshPreview();
            window.Repaint();
            return window;
        }

        internal ScriptableRendererData SelectedRendererData =>
            rendererData;

        internal string PreviewText => preview;

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
                "Select exactly one Universal Renderer Data asset. Preview is " +
                "read-only; Apply changes only this asset.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            rendererData = (ScriptableRendererData)EditorGUILayout.ObjectField(
                "Renderer Data",
                rendererData,
                typeof(ScriptableRendererData),
                false);
            if (EditorGUI.EndChangeCheck())
                RefreshPreview();
            using (new EditorGUI.DisabledScope(rendererData == null))
            {
                if (GUILayout.Button("Preview"))
                    RefreshPreview();
                EditorGUILayout.HelpBox(
                    string.IsNullOrEmpty(preview)
                        ? "Choose a Renderer Data asset."
                        : preview,
                    MessageType.None);
                if (GUILayout.Button("Apply"))
                {
                    var result = Install(rendererData);
                    preview = result.created
                        ? "Installed one Miku Toon Screen Rim feature."
                        : "Already installed; no duplicate was added.";
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
                ? "Apply will add one feature. No other Renderer Data asset " +
                  "will be changed."
                : count == 1
                    ? "The feature is already installed. Apply is a no-op."
                    : "Multiple existing Miku features were found. Apply will " +
                      "not add another; remove unwanted duplicates manually.";
        }

        internal static void DrawStatusAndOpenButton()
        {
            EditorGUILayout.HelpBox(
                RendererFeatureStatus(),
                MessageType.None);
            if (GUILayout.Button("Open Screen Rim Installer"))
                OpenWindow();
        }

        internal static string RendererFeatureStatus()
        {
            var pipeline = ActivePipeline();
            if (pipeline == null)
                return "Screen Rim Renderer Feature: URP asset not active.";
            var rendererData = RendererData(pipeline).ToArray();
            var installed = rendererData.Count(
                item => CountFeatures(item) > 0);
            return "Screen Rim Renderer Feature: " +
                   installed + "/" + rendererData.Length +
                   " active Renderer Data assets installed. Use Miku > " +
                   "Generic Toon > Rendering > Screen Rim Installer for " +
                   "explicit Preview/Apply.";
        }

        internal static ScriptableRendererData ResolveDefaultRendererData()
        {
            var pipeline = ActivePipeline();
            if (pipeline == null)
                return null;
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            var defaultIndex =
                serialized.FindProperty("m_DefaultRendererIndex");
            if (list == null || list.arraySize == 0)
                return null;
            var index = defaultIndex != null
                ? defaultIndex.intValue
                : 0;
            if (index < 0 || index >= list.arraySize)
                return null;
            return list.GetArrayElementAtIndex(index)
                .objectReferenceValue as ScriptableRendererData;
        }

        static UniversalRenderPipelineAsset ActivePipeline() =>
            GraphicsSettings.currentRenderPipeline as
                UniversalRenderPipelineAsset ??
            QualitySettings.renderPipeline as
                UniversalRenderPipelineAsset ??
            GraphicsSettings.defaultRenderPipeline as
                UniversalRenderPipelineAsset;

        static System.Collections.Generic.IEnumerable<
            ScriptableRendererData> RendererData(
            UniversalRenderPipelineAsset pipeline)
        {
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            if (list == null)
                yield break;
            for (var index = 0; index < list.arraySize; index++)
                if (list.GetArrayElementAtIndex(index)
                        .objectReferenceValue is ScriptableRendererData data)
                    yield return data;
        }

        internal readonly struct InstallResult
        {
            internal InstallResult(
                MikuToonScreenRimRendererFeature feature,
                bool created)
            {
                this.feature = feature;
                this.created = created;
            }

            internal readonly MikuToonScreenRimRendererFeature feature;
            internal readonly bool created;
        }

        internal static int CountFeatures(ScriptableRendererData target) =>
            target == null
                ? 0
                : target.rendererFeatures.Count(
                    item => item is MikuToonScreenRimRendererFeature);

        internal static InstallResult Install(
            ScriptableRendererData target,
            Action beforeCommit = null)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            var existing = target.rendererFeatures
                .OfType<MikuToonScreenRimRendererFeature>()
                .FirstOrDefault();
            if (existing != null)
                return new InstallResult(existing, false);

            MikuToonScreenRimRendererFeature feature = null;
            var appended = false;
            try
            {
                Undo.RegisterCompleteObjectUndo(
                    target,
                    "Install Miku Toon Screen Rim");
                feature = CreateInstance<MikuToonScreenRimRendererFeature>();
                feature.name = nameof(MikuToonScreenRimRendererFeature);
                Undo.RegisterCreatedObjectUndo(
                    feature,
                    "Install Miku Toon Screen Rim");
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
