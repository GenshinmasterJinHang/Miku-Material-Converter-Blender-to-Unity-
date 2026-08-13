// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using Miku.ShaderConverter.Runtime.GameToon;
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
            "Miku/Game Toon/Rendering/Game Toon Renderer Feature Installer")]
        static void OpenGameToonInstaller() => OpenWindow();

        internal static MikuToonRendererFeatureInstaller OpenWindow(
            ScriptableRendererData preferred = null)
        {
            var window = GetWindow<MikuToonRendererFeatureInstaller>(
                MikuEditorLocalization.Tr(
                    "Miku Game Toon Renderer Features"));
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
                MikuEditorLocalization.Tr(
                    "Preview is read-only. Apply installs the Geometry and " +
                    "Screen Rim features into every active Universal Renderer " +
                    "Data asset as one Undo transaction."),
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
                    var result = InstallAllActive();
                    preview = MikuEditorLocalization.Format(
                        "Installed Game Toon renderer features in {0} active Renderer Data asset(s); {1} subasset(s) created.",
                        result.rendererDataCount,
                        result.createdCount);
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
            var screenCount = CountFeatures(rendererData);
            var geometryCount = CountGeometryFeatures(rendererData);
            preview = screenCount == 0 || geometryCount == 0
                ? MikuEditorLocalization.Tr(
                    "Apply will install missing Geometry and Screen Rim " +
                    "features in all active Universal Renderer Data assets.")
                : screenCount == 1 && geometryCount == 1
                    ? MikuEditorLocalization.Tr(
                        "Both features are installed on this Renderer Data. " +
                        "Apply remains idempotent across all active renderers.")
                    : MikuEditorLocalization.Tr(
                        "Duplicate Miku renderer features were found. Apply is " +
                        "blocked until duplicates are resolved.");
        }

        internal static void DrawStatusAndOpenButton()
        {
            EditorGUILayout.HelpBox(
                RendererFeatureStatus(),
                MessageType.None);
            if (GUILayout.Button(MikuEditorLocalization.Tr(
                    "Open Game Toon Renderer Feature Installer")))
                OpenWindow();
        }

        internal static string RendererFeatureStatus()
        {
            var pipeline = ActivePipeline();
            if (pipeline == null)
                return MikuEditorLocalization.Tr(
                    "Game Toon Renderer Features: URP asset not active.");
            var rendererData = RendererData(pipeline).ToArray();
            var installed = rendererData.Count(
                item => CountFeatures(item) == 1 &&
                        CountGeometryFeatures(item) == 1);
            return MikuEditorLocalization.Format(
                "Game Toon Geometry + Screen Rim Renderer Features: {0}/{1} active Renderer Data assets installed.",
                installed,
                rendererData.Length);
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

        internal static int CountGeometryFeatures(
            ScriptableRendererData target) =>
            target == null
                ? 0
                : target.rendererFeatures.Count(
                    item => item is MikuGameToonGeometryRendererFeature);

        internal static bool AllActiveUniversalRenderersHaveGeometry()
        {
            var pipeline = ActivePipeline();
            if (pipeline == null)
                return false;
            var targets = RendererData(pipeline)
                .OfType<UniversalRendererData>()
                .Distinct()
                .ToArray();
            return targets.Length > 0 && targets.All(target =>
                target.rendererFeatures
                    .OfType<MikuGameToonGeometryRendererFeature>()
                    .Count(feature => feature != null && feature.isActive) == 1);
        }

        internal readonly struct InstallAllResult
        {
            internal InstallAllResult(int rendererDataCount, int createdCount)
            {
                this.rendererDataCount = rendererDataCount;
                this.createdCount = createdCount;
            }

            internal readonly int rendererDataCount;
            internal readonly int createdCount;
        }

        internal static InstallAllResult InstallAllActive(
            Action beforeCommit = null)
        {
            var pipeline = ActivePipeline();
            if (pipeline == null)
                throw new InvalidOperationException(
                    "MIKU_URP_ASSET_REQUIRED");
            var targets = RendererData(pipeline)
                .OfType<UniversalRendererData>()
                .Distinct()
                .ToArray();
            if (targets.Length == 0)
                throw new InvalidOperationException(
                    "MIKU_UNIVERSAL_RENDERER_DATA_REQUIRED");
            if (targets.Any(target =>
                    CountFeatures(target) > 1 ||
                    CountGeometryFeatures(target) > 1))
                throw new InvalidOperationException(
                    "MIKU_RENDERER_FEATURE_DUPLICATE");

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(
                MikuEditorLocalization.Tr(
                    "Install Miku Game Toon Renderer Features"));
            var created = 0;
            try
            {
                foreach (var target in targets)
                {
                    created += InstallGeometry(target).created ? 1 : 0;
                    created += Install(target).created ? 1 : 0;
                }
                beforeCommit?.Invoke();
                Undo.CollapseUndoOperations(undoGroup);
                return new InstallAllResult(targets.Length, created);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                throw;
            }
        }

        internal readonly struct GeometryInstallResult
        {
            internal GeometryInstallResult(
                MikuGameToonGeometryRendererFeature feature,
                bool created)
            {
                this.feature = feature;
                this.created = created;
            }

            internal readonly MikuGameToonGeometryRendererFeature feature;
            internal readonly bool created;
        }

        internal static GeometryInstallResult InstallGeometry(
            ScriptableRendererData target)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            var existing = target.rendererFeatures
                .OfType<MikuGameToonGeometryRendererFeature>()
                .FirstOrDefault();
            if (existing != null)
            {
                if (!existing.isActive)
                {
                    Undo.RecordObject(existing, "Activate Miku Game Toon Geometry");
                    existing.SetActive(true);
                    EditorUtility.SetDirty(existing);
                }
                return new GeometryInstallResult(existing, false);
            }

            MikuGameToonGeometryRendererFeature feature = null;
            try
            {
                Undo.RegisterCompleteObjectUndo(
                    target,
                    "Install Miku Game Toon Geometry");
                feature = CreateInstance<MikuGameToonGeometryRendererFeature>();
                feature.name = nameof(MikuGameToonGeometryRendererFeature);
                Undo.RegisterCreatedObjectUndo(
                    feature,
                    "Install Miku Game Toon Geometry");
                AssetDatabase.AddObjectToAsset(feature, target);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature,
                        out _,
                        out long localId))
                    throw new InvalidOperationException(
                        "MIKU_RENDERER_FEATURE_LOCAL_ID_FAILED");
                AppendFeature(target, feature, localId);
                feature.Create();
                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(feature);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(
                    AssetDatabase.GetAssetPath(target),
                    ImportAssetOptions.ForceSynchronousImport);
                return new GeometryInstallResult(feature, true);
            }
            catch
            {
                if (feature != null)
                {
                    RemoveFeatureReference(target, feature);
                    DestroyImmediate(feature, true);
                }
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
                throw;
            }
        }

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
                    MikuEditorLocalization.Tr(
                        "Install Miku Toon Screen Rim"));
                feature = CreateInstance<MikuToonScreenRimRendererFeature>();
                feature.name = nameof(MikuToonScreenRimRendererFeature);
                Undo.RegisterCreatedObjectUndo(
                    feature,
                    MikuEditorLocalization.Tr(
                        "Install Miku Toon Screen Rim"));
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

        internal static void RemoveInstalledFeature(
            ScriptableRendererData target,
            MikuToonScreenRimRendererFeature feature)
        {
            if (target == null || feature == null)
                return;
            RemoveFeatureReference(target, feature);
            DestroyImmediate(feature, true);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        internal static void RemoveInstalledGeometryFeature(
            ScriptableRendererData target,
            MikuGameToonGeometryRendererFeature feature)
        {
            if (target == null || feature == null)
                return;
            RemoveFeatureReference(target, feature);
            DestroyImmediate(feature, true);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
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
