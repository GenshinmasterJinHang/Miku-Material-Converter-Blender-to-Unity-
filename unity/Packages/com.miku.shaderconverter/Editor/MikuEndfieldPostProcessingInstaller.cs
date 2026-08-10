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
    public sealed class MikuEndfieldPostProcessingInstaller : EditorWindow
    {
        internal const string DefaultOutputFolder = "Assets/Miku/Endfield";
        internal const string FeatureName = "Miku Endfield Full Screen LUT";
        internal const string LutShaderName =
            "Hidden/MIKU/Endfield/FullScreenColorLut";
        internal const string MaterialFileName =
            "MikuEndfieldFullScreenColorLut.mat";
        internal const string ProfileFileName =
            "MikuEndfieldPostVolumeProfile.asset";

        [SerializeField] ScriptableRendererData rendererData;
        [SerializeField] Texture2D lut;
        [SerializeField] string outputFolder = DefaultOutputFolder;
        [SerializeField] string preview = "";

        [MenuItem(
            "Miku/Game Toon/Rendering/Endfield LUT & Volume Installer",
            priority = 231)]
        static void Open()
        {
            var window = GetWindow<MikuEndfieldPostProcessingInstaller>(
                MikuEditorLocalization.Tr("Miku Endfield Post Processing"));
            window.rendererData =
                Selection.activeObject as ScriptableRendererData ??
                MikuToonRendererFeatureInstaller.ResolveDefaultRendererData();
            window.RefreshPreview();
            window.Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                MikuEditorLocalization.Tr(
                    "Installs one pre-post-process full-screen 32-cube LUT " +
                    "feature and creates a Neutral/Bloom/Vignette profile. " +
                    "The game LUT remains a project-owned asset."),
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            rendererData = (ScriptableRendererData)EditorGUILayout.ObjectField(
                MikuEditorLocalization.Tr("Renderer Data"),
                rendererData,
                typeof(ScriptableRendererData),
                false);
            lut = (Texture2D)EditorGUILayout.ObjectField(
                MikuEditorLocalization.Tr("Flattened 32x32x32 LUT"),
                lut,
                typeof(Texture2D),
                false);
            outputFolder = EditorGUILayout.TextField(
                MikuEditorLocalization.Tr("Output Folder"),
                outputFolder);
            if (EditorGUI.EndChangeCheck())
                RefreshPreview();

            if (GUILayout.Button(MikuEditorLocalization.Tr("Preview")))
                RefreshPreview();
            EditorGUILayout.HelpBox(
                string.IsNullOrEmpty(preview)
                    ? MikuEditorLocalization.Tr("Choose Renderer Data and LUT assets.")
                    : preview,
                MessageType.None);
            using (new EditorGUI.DisabledScope(
                       rendererData == null || lut == null))
            {
                if (!GUILayout.Button(MikuEditorLocalization.Tr("Apply")))
                    return;
                var result = Install(
                    rendererData,
                    lut,
                    outputFolder,
                    null);
                preview = result.createdFeature
                    ? MikuEditorLocalization.Tr(
                        "Installed the LUT feature and Endfield Volume profile.")
                    : MikuEditorLocalization.Tr(
                        "Updated the existing installation; no duplicate was added.");
                Selection.activeObject = result.profile;
                EditorGUIUtility.PingObject(result.profile);
            }
        }

        void RefreshPreview()
        {
            try
            {
                ValidateOutputFolder(outputFolder);
                if (rendererData == null || lut == null)
                {
                    preview = "";
                    return;
                }
                ValidateLutLayout(lut);
                var count = CountFeatures(rendererData);
                preview = count == 0
                    ? MikuEditorLocalization.Tr(
                        "Apply will configure the LUT import, add one feature, " +
                        "and create project-owned Material/Profile assets.")
                    : MikuEditorLocalization.Tr(
                        "Apply will update the existing Miku LUT feature and " +
                        "assets without adding a duplicate.");
            }
            catch (Exception error)
            {
                preview = error.Message;
            }
        }

        /// <summary>
        /// Installs the Endfield full-screen LUT and post profile into one
        /// explicitly selected Universal Renderer Data asset.
        /// </summary>
        public static void InstallEndfieldPostProcessing(
            ScriptableRendererData target,
            Texture2D texture,
            string projectOutputFolder = DefaultOutputFolder)
        {
            Install(target, texture, projectOutputFolder, null);
        }

        internal readonly struct InstallResult
        {
            internal InstallResult(
                FullScreenPassRendererFeature feature,
                Material material,
                VolumeProfile profile,
                Texture2D texture,
                bool createdFeature)
            {
                this.feature = feature;
                this.material = material;
                this.profile = profile;
                lut = texture;
                this.createdFeature = createdFeature;
            }

            internal readonly FullScreenPassRendererFeature feature;
            internal readonly Material material;
            internal readonly VolumeProfile profile;
            internal readonly Texture2D lut;
            internal readonly bool createdFeature;
        }

        internal static InstallResult Install(
            ScriptableRendererData target,
            Texture2D texture,
            string projectOutputFolder,
            Action beforeCommit)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            ValidateOutputFolder(projectOutputFolder);
            ValidateLutLayout(texture);
            var importerState = LutImporterState.Capture(texture);
            var importerChanged = false;
            var createdAssets = new List<string>();
            var createdFolders = new List<string>();
            FullScreenPassRendererFeature feature = null;
            var createdFeature = false;
            var undoGroup = BeginUndoGroup();
            try
            {
                texture = ConfigureLutImporter(texture, out importerChanged);
                EnsureFolder(projectOutputFolder, createdFolders);
                var shader = Shader.Find(LutShaderName)
                    ?? throw new InvalidOperationException(
                        "MIKU_ENDFIELD_LUT_SHADER_MISSING:" + LutShaderName);
                var materialPath = projectOutputFolder.TrimEnd('/') + "/" +
                    MaterialFileName;
                var profilePath = projectOutputFolder.TrimEnd('/') + "/" +
                    ProfileFileName;
                var material = CreateOrUpdateMaterial(
                    materialPath,
                    shader,
                    texture,
                    createdAssets);
                var profile = MikuEndfieldPostVolumeProfileFactory.CreateOrUpdate(
                    profilePath,
                    createdAssets);
                feature = FindFeature(target);
                if (feature == null)
                {
                    feature = CreateFeature(target);
                    createdFeature = true;
                }
                else
                {
                    Undo.RegisterCompleteObjectUndo(
                        feature,
                        "Update Miku Endfield Full Screen LUT");
                }
                ConfigureFeature(feature, material);
                beforeCommit?.Invoke();

                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.SaveAssetIfDirty(feature);
                AssetDatabase.SaveAssetIfDirty(target);
                AssetDatabase.ImportAsset(
                    AssetDatabase.GetAssetPath(target),
                    ImportAssetOptions.ForceSynchronousImport);
                Undo.CollapseUndoOperations(undoGroup);
                return new InstallResult(
                    feature,
                    material,
                    profile,
                    texture,
                    createdFeature);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (createdFeature && feature != null)
                {
                    RemoveFeatureReference(target, feature);
                    DestroyImmediate(feature, true);
                }
                foreach (var path in createdAssets
                             .Distinct(StringComparer.Ordinal)
                             .OrderByDescending(item => item.Length))
                    AssetDatabase.DeleteAsset(path);
                if (importerChanged)
                    importerState.Restore();
                foreach (var path in createdFolders
                             .OrderByDescending(item => item.Length))
                    AssetDatabase.DeleteAsset(path);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                throw;
            }
        }

        internal static int CountFeatures(ScriptableRendererData target)
        {
            return target == null
                ? 0
                : target.rendererFeatures.Count(FeatureMatches);
        }

        internal static void ValidateLutLayout(Texture2D texture)
        {
            if (texture == null)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_MISSING");
            if (texture.width != 1024 || texture.height != 32)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_LAYOUT_INVALID:expected=1024x32:" +
                    "actual=" + texture.width + "x" + texture.height);
            var path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_PROJECT_ASSET_REQUIRED");
            if (!(AssetImporter.GetAtPath(path) is TextureImporter))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_IMPORTER_REQUIRED:" + path);
        }

        static Texture2D ConfigureLutImporter(
            Texture2D texture,
            out bool changed)
        {
            var path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_IMPORTER_REQUIRED:" + path);
            changed = !importer.sRGBTexture || importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.textureCompression !=
                TextureImporterCompression.Uncompressed ||
                importer.textureType != TextureImporterType.Default ||
                importer.GetPlatformTextureSettings("Standalone").overridden;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;
                importer.ClearPlatformTextureSettings("Standalone");
                importer.SaveAndReimport();
            }
            var reloaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path)
                ?? throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_REIMPORT_FAILED:" + path);
            ValidateLutLayout(reloaded);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !importer.sRGBTexture ||
                importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.textureCompression !=
                TextureImporterCompression.Uncompressed ||
                importer.textureType != TextureImporterType.Default ||
                importer.GetPlatformTextureSettings("Standalone").overridden)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_IMPORT_INVALID:" + path);
            return reloaded;
        }

        static Material CreateOrUpdateMaterial(
            string path,
            Shader shader,
            Texture2D texture,
            ICollection<string> createdAssetPaths)
        {
            var existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null && !(existing is Material))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_POST_ASSET_CONFLICT:" + path);
            var material = existing as Material;
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                };
                AssetDatabase.CreateAsset(material, path);
                Undo.RegisterCreatedObjectUndo(
                    material,
                    "Create Miku Endfield LUT Material");
                createdAssetPaths.Add(path);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(
                    material,
                    "Update Miku Endfield LUT Material");
                material.shader = shader;
            }
            material.SetTexture("_LutTex", texture);
            material.SetFloat("_Intensity", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static FullScreenPassRendererFeature FindFeature(
            ScriptableRendererData target)
        {
            return target.rendererFeatures
                .OfType<FullScreenPassRendererFeature>()
                .FirstOrDefault(FeatureMatches);
        }

        static bool FeatureMatches(ScriptableRendererFeature candidate)
        {
            if (!(candidate is FullScreenPassRendererFeature feature))
                return false;
            if (string.Equals(feature.name, FeatureName, StringComparison.Ordinal))
                return true;
            var serialized = new SerializedObject(feature);
            var materialProperty = FindProperty(
                serialized,
                "passMaterial",
                "m_PassMaterial");
            var material = materialProperty?.objectReferenceValue as Material;
            return material?.shader != null && string.Equals(
                material.shader.name,
                LutShaderName,
                StringComparison.Ordinal);
        }

        static FullScreenPassRendererFeature CreateFeature(
            ScriptableRendererData target)
        {
            Undo.RegisterCompleteObjectUndo(
                target,
                "Install Miku Endfield Full Screen LUT");
            var feature = CreateInstance<FullScreenPassRendererFeature>();
            try
            {
                feature.name = FeatureName;
                Undo.RegisterCreatedObjectUndo(
                    feature,
                    "Install Miku Endfield Full Screen LUT");
                AssetDatabase.AddObjectToAsset(feature, target);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature,
                        out _,
                        out long localId))
                    throw new InvalidOperationException(
                        "MIKU_RENDERER_FEATURE_LOCAL_ID_FAILED");
                AppendFeature(target, feature, localId);
                return feature;
            }
            catch
            {
                RemoveFeatureReference(target, feature);
                DestroyImmediate(feature, true);
                throw;
            }
        }

        static void ConfigureFeature(
            FullScreenPassRendererFeature feature,
            Material material)
        {
            feature.name = FeatureName;
            feature.SetActive(true);
            var serialized = new SerializedObject(feature);
            serialized.Update();
            SetEnum(
                RequireProperty(
                    serialized,
                    "injectionPoint",
                    "m_InjectionPoint"),
                "BeforeRenderingPostProcessing");
            RequireProperty(serialized, "requirements", "m_Requirements")
                .intValue = 0;
            RequireProperty(serialized, "passMaterial", "m_PassMaterial")
                .objectReferenceValue = material;
            RequireProperty(serialized, "passIndex", "m_PassIndex")
                .intValue = 0;
            RequireProperty(
                    serialized,
                    "fetchColorBuffer",
                    "m_FetchColorBuffer")
                .boolValue = true;
            RequireProperty(
                    serialized,
                    "bindDepthStencilAttachment",
                    "m_BindDepthStencilAttachment")
                .boolValue = false;
            serialized.ApplyModifiedProperties();
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
                    "MIKU_RENDERER_FEATURE_SCHEMA_UNSUPPORTED:URP17");
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
                for (var move = index; move + 1 < features.arraySize; move++)
                {
                    features.GetArrayElementAtIndex(move).objectReferenceValue =
                        features.GetArrayElementAtIndex(move + 1)
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

        static SerializedProperty RequireProperty(
            SerializedObject serialized,
            params string[] names)
        {
            var property = FindProperty(serialized, names);
            return property ?? throw new InvalidOperationException(
                "MIKU_FULLSCREEN_PASS_SCHEMA_UNSUPPORTED:" +
                string.Join("|", names));
        }

        static SerializedProperty FindProperty(
            SerializedObject serialized,
            params string[] names)
        {
            foreach (var name in names)
            {
                var property = serialized.FindProperty(name);
                if (property != null)
                    return property;
            }
            return null;
        }

        static void SetEnum(SerializedProperty property, string value)
        {
            var index = Array.IndexOf(property.enumNames, value);
            if (index < 0)
                throw new InvalidOperationException(
                    "MIKU_FULLSCREEN_PASS_ENUM_UNSUPPORTED:" + value);
            property.enumValueIndex = index;
        }

        static int BeginUndoGroup()
        {
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(
                "Install Miku Endfield LUT and Volume");
            return group;
        }

        static void ValidateOutputFolder(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.IndexOf('\\') >= 0 ||
                value.Contains("..") ||
                value.EndsWith("/", StringComparison.Ordinal) ||
                value.Contains("//") ||
                !(string.Equals(value, "Assets", StringComparison.Ordinal) ||
                  value.StartsWith("Assets/", StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_POST_OUTPUT_PATH_INVALID:" + value);
        }

        static void EnsureFolder(
            string path,
            ICollection<string> createdFolders)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (string.IsNullOrEmpty(
                            AssetDatabase.CreateFolder(current, segments[index])))
                        throw new InvalidOperationException(
                            "MIKU_ENDFIELD_POST_OUTPUT_CREATE_FAILED:" + next);
                    createdFolders.Add(next);
                }
                current = next;
            }
        }

        readonly struct LutImporterState
        {
            LutImporterState(
                string path,
                TextureImporterType textureType,
                bool sRgb,
                bool mipmap,
                TextureWrapMode wrap,
                FilterMode filter,
                TextureImporterCompression compression,
                TextureImporterPlatformSettings standalone)
            {
                this.path = path;
                this.textureType = textureType;
                this.sRgb = sRgb;
                this.mipmap = mipmap;
                this.wrap = wrap;
                this.filter = filter;
                this.compression = compression;
                this.standalone = standalone;
            }

            readonly string path;
            readonly TextureImporterType textureType;
            readonly bool sRgb;
            readonly bool mipmap;
            readonly TextureWrapMode wrap;
            readonly FilterMode filter;
            readonly TextureImporterCompression compression;
            readonly TextureImporterPlatformSettings standalone;

            internal static LutImporterState Capture(Texture2D texture)
            {
                var path = AssetDatabase.GetAssetPath(texture);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter
                    ?? throw new InvalidOperationException(
                        "MIKU_ENDFIELD_LUT_IMPORTER_REQUIRED:" + path);
                return new LutImporterState(
                    path,
                    importer.textureType,
                    importer.sRGBTexture,
                    importer.mipmapEnabled,
                    importer.wrapMode,
                    importer.filterMode,
                    importer.textureCompression,
                    importer.GetPlatformTextureSettings("Standalone"));
            }

            internal void Restore()
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    return;
                importer.textureType = textureType;
                importer.sRGBTexture = sRgb;
                importer.mipmapEnabled = mipmap;
                importer.wrapMode = wrap;
                importer.filterMode = filter;
                importer.textureCompression = compression;
                if (standalone.overridden)
                    importer.SetPlatformTextureSettings(standalone);
                else
                    importer.ClearPlatformTextureSettings("Standalone");
                importer.SaveAndReimport();
            }
        }
    }
}
