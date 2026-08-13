// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
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
        [SerializeField] bool installScreenLut;
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
                    "Creates the Endfield Color Adjustments/Color Curves/" +
                    "Neutral/Bloom/Vignette Volume profile. A genuine screen " +
                    "LUT can be installed explicitly; cloth and skin material " +
                    "LUTs are rejected."),
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            installScreenLut = EditorGUILayout.Toggle(
                MikuEditorLocalization.Tr("Install Screen LUT (Advanced)"),
                installScreenLut);
            rendererData = (ScriptableRendererData)EditorGUILayout.ObjectField(
                MikuEditorLocalization.Tr(
                    installScreenLut
                        ? "Renderer Data"
                        : "Renderer Data (Remove Legacy LUT Feature)"),
                rendererData,
                typeof(ScriptableRendererData),
                false);
            if (installScreenLut)
                lut = (Texture2D)EditorGUILayout.ObjectField(
                    MikuEditorLocalization.Tr("Flattened 32x32x32 Screen LUT"),
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
                    ? MikuEditorLocalization.Tr(
                        installScreenLut
                            ? "Choose Renderer Data and a genuine screen LUT asset."
                            : "Choose an output folder. Renderer Data is optional.")
                    : preview,
                MessageType.None);
            using (new EditorGUI.DisabledScope(
                       installScreenLut &&
                       (rendererData == null || lut == null)))
            {
                if (!GUILayout.Button(MikuEditorLocalization.Tr("Apply")))
                    return;
                VolumeProfile profile;
                if (installScreenLut)
                {
                    var result = Install(
                        rendererData,
                        lut,
                        outputFolder,
                        null);
                    profile = result.profile;
                    preview = result.createdFeature
                        ? MikuEditorLocalization.Tr(
                            "Installed the screen LUT feature and Endfield Volume profile.")
                        : MikuEditorLocalization.Tr(
                            "Updated the existing installation; no duplicate was added.");
                }
                else
                {
                    profile = InstallVolumeOnly(outputFolder, null);
                    var removed = rendererData == null
                        ? 0
                        : RemoveEndfieldScreenLutFeature(rendererData);
                    preview = removed == 0
                        ? MikuEditorLocalization.Tr(
                            "Installed the Volume-only Endfield profile.")
                        : MikuEditorLocalization.Tr(
                            "Installed the Volume-only profile and removed the legacy screen LUT feature.");
                }
                Selection.activeObject = profile;
                EditorGUIUtility.PingObject(profile);
            }
        }

        void RefreshPreview()
        {
            try
            {
                ValidateOutputFolder(outputFolder);
                if (!installScreenLut)
                {
                    preview = rendererData == null
                        ? MikuEditorLocalization.Tr(
                            "Apply will create or update the Volume-only profile.")
                        : MikuEditorLocalization.Tr(
                            "Apply will create or update the Volume-only profile " +
                            "and remove the Miku screen LUT feature if present.");
                    return;
                }
                if (rendererData == null || lut == null)
                {
                    preview = "";
                    return;
                }
                ValidateLutLayout(lut);
                ValidateScreenLutPurpose(lut);
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

        /// <summary>
        /// Creates or updates the Endfield Volume profile without requiring or
        /// installing a full-screen LUT renderer feature.
        /// </summary>
        public static void InstallEndfieldVolumeOnly(
            string projectOutputFolder = DefaultOutputFolder)
        {
            InstallVolumeOnly(projectOutputFolder, null);
        }

        internal static VolumeProfile InstallVolumeOnly(
            string projectOutputFolder,
            Action beforeCommit)
        {
            ValidateOutputFolder(projectOutputFolder);
            var profilePath = projectOutputFolder.TrimEnd('/') + "/" +
                ProfileFileName;
            if (AssetDatabase.LoadMainAssetAtPath(profilePath) is
                    VolumeProfile existing)
            {
                var dirty = new UnityEngine.Object[] { existing }
                    .Concat(existing.components)
                    .FirstOrDefault(item =>
                        item != null && EditorUtility.IsDirty(item));
                if (dirty != null)
                    throw new InvalidOperationException(
                        "MIKU_ENDFIELD_POST_DIRTY_ASSET:" + profilePath + ":" +
                        dirty.name);
            }
            var snapshot = AssetFileSnapshot.CaptureAsset(profilePath);
            var createdAssets = new List<string>();
            var createdFolders = new List<string>();
            var undoGroup = BeginUndoGroup();
            try
            {
                EnsureFolder(projectOutputFolder, createdFolders);
                var profile = MikuEndfieldPostVolumeProfileFactory.CreateOrUpdate(
                    profilePath,
                    createdAssets);
                beforeCommit?.Invoke();
                EditorUtility.SetDirty(profile);
                MarkProfileComponentsDirty(profile);
                AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.ImportAsset(
                    profilePath,
                    ImportAssetOptions.ForceSynchronousImport);
                Undo.CollapseUndoOperations(undoGroup);
                return AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath)
                    ?? throw new InvalidOperationException(
                        "MIKU_ENDFIELD_POST_PROFILE_RELOAD_FAILED:" + profilePath);
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                foreach (var path in createdAssets
                             .Distinct(StringComparer.Ordinal)
                             .OrderByDescending(item => item.Length))
                    AssetDatabase.DeleteAsset(path);
                if (snapshot != null)
                {
                    snapshot.RestoreBytes();
                    snapshot.ImportAsset();
                }
                foreach (var path in createdFolders
                             .OrderByDescending(item => item.Length))
                    AssetDatabase.DeleteAsset(path);
                throw;
            }
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
            Action beforeCommit,
            Action afterSave = null)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            ValidateOutputFolder(projectOutputFolder);
            ValidateLutLayout(texture);
            ValidateScreenLutPurpose(texture);
            ValidateRendererFeatureState(target);
            var matchingFeatureCount = CountFeatures(target);
            if (matchingFeatureCount > 1)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_FEATURE_REFERENCE_DUPLICATE:" +
                    AssetDatabase.GetAssetPath(target));
            var targetPath = AssetDatabase.GetAssetPath(target);
            var lutPath = AssetDatabase.GetAssetPath(texture);
            var materialPath = projectOutputFolder.TrimEnd('/') + "/" +
                MaterialFileName;
            var profilePath = projectOutputFolder.TrimEnd('/') + "/" +
                ProfileFileName;
            ValidateNoUnsavedTargetState(
                target,
                texture,
                materialPath,
                profilePath);
            var diskSnapshots = new[]
                {
                    AssetFileSnapshot.CaptureAsset(materialPath),
                    AssetFileSnapshot.CaptureAsset(profilePath),
                    AssetFileSnapshot.CaptureImporter(lutPath),
                    AssetFileSnapshot.CaptureAsset(targetPath),
                }
                .Where(item => item != null)
                .ToArray();
            var createdAssets = new List<string>();
            var createdFolders = new List<string>();
            FullScreenPassRendererFeature feature = null;
            Material material = null;
            VolumeProfile profile = null;
            var createdFeature = false;
            var undoGroup = BeginUndoGroup();
            try
            {
                texture = ConfigureLutImporter(texture, out _);
                EnsureFolder(projectOutputFolder, createdFolders);
                var shader = Shader.Find(LutShaderName)
                    ?? throw new InvalidOperationException(
                        "MIKU_ENDFIELD_LUT_SHADER_MISSING:" + LutShaderName);
                material = CreateOrUpdateMaterial(
                    materialPath,
                    shader,
                    texture,
                    createdAssets);
                profile = MikuEndfieldPostVolumeProfileFactory.CreateOrUpdate(
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
                MarkProfileComponentsDirty(profile);
                AssetDatabase.SaveAssetIfDirty(material);
                AssetDatabase.SaveAssetIfDirty(profile);
                AssetDatabase.SaveAssetIfDirty(feature);
                AssetDatabase.SaveAssetIfDirty(target);
                AssetDatabase.ImportAsset(
                    AssetDatabase.GetAssetPath(target),
                    ImportAssetOptions.ForceSynchronousImport);
                afterSave?.Invoke();
                feature = ValidatePersistedFeature(target, material);
                Undo.CollapseUndoOperations(undoGroup);
                return new InstallResult(
                    feature,
                    material,
                    profile,
                    texture,
                    createdFeature);
            }
            catch (Exception installError)
            {
                try
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
                    foreach (var snapshot in diskSnapshots)
                        snapshot.RestoreBytes();
                    foreach (var snapshot in diskSnapshots)
                        snapshot.ImportAsset();
                    foreach (var path in createdFolders
                                 .OrderByDescending(item => item.Length))
                        AssetDatabase.DeleteAsset(path);
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException(
                        "MIKU_ENDFIELD_POST_ROLLBACK_FAILED",
                        installError,
                        rollbackError);
                }
                throw;
            }
        }

        static void MarkProfileComponentsDirty(VolumeProfile profile)
        {
            if (profile == null)
                return;
            foreach (var component in profile.components)
            {
                if (component != null)
                    EditorUtility.SetDirty(component);
            }
        }

        static void ValidateNoUnsavedTargetState(
            ScriptableRendererData target,
            Texture2D texture,
            string materialPath,
            string profilePath)
        {
            var candidates = new List<UnityEngine.Object>
            {
                target,
                texture,
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)),
                AssetDatabase.LoadMainAssetAtPath(materialPath),
                AssetDatabase.LoadMainAssetAtPath(profilePath),
            };
            candidates.AddRange(target.rendererFeatures);
            if (AssetDatabase.LoadMainAssetAtPath(profilePath) is
                    VolumeProfile profile)
                candidates.AddRange(profile.components);
            foreach (var candidate in candidates.Where(item => item != null))
            {
                if (!EditorUtility.IsDirty(candidate))
                    continue;
                var path = AssetDatabase.GetAssetPath(candidate);
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_POST_DIRTY_ASSET:" + path + ":" +
                    candidate.name);
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

        internal static void ValidateScreenLutPurpose(Texture2D texture)
        {
            ValidateLutLayout(texture);
            var path = AssetDatabase.GetAssetPath(texture);
            var fileName = Path.GetFileNameWithoutExtension(path)
                .ToLowerInvariant();
            if (fileName.Contains("cloth_lut") ||
                (fileName.Contains("skincolor") && fileName.Contains("_lut")))
                throw MaterialLutRejected(path, "name");

            foreach (var guid in AssetDatabase.FindAssets("t:Material"))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (material == null ||
                    !material.HasProperty("_ColorLutTex") ||
                    !SameAsset(material.GetTexture("_ColorLutTex"), texture))
                    continue;
                throw MaterialLutRejected(
                    path,
                    "material=" + AssetDatabase.GetAssetPath(material));
            }

            foreach (var guid in AssetDatabase.FindAssets(
                         "t:MikuToonMaterialRecipe"))
            {
                var recipe = AssetDatabase.LoadAssetAtPath<
                    MikuToonMaterialRecipe>(AssetDatabase.GUIDToAssetPath(guid));
                var binding = recipe?.textureBindings?.FirstOrDefault(item =>
                    item != null &&
                    string.Equals(
                        item.role,
                        "ColorLut",
                        StringComparison.Ordinal) &&
                    SameAsset(item.texture, texture));
                if (binding == null)
                    continue;
                throw MaterialLutRejected(
                    path,
                    "recipe=" + AssetDatabase.GetAssetPath(recipe));
            }
        }

        static InvalidOperationException MaterialLutRejected(
            string path,
            string evidence)
        {
            return new InvalidOperationException(
                "MIKU_ENDFIELD_SCREEN_LUT_MATERIAL_ASSET_REJECTED:" + path +
                ":" + evidence);
        }

        /// <summary>
        /// Removes every Miku Endfield full-screen LUT feature from one
        /// renderer. Other renderer features and generated material assets are
        /// left untouched.
        /// </summary>
        public static void RemoveEndfieldScreenLut(
            ScriptableRendererData target)
        {
            RemoveEndfieldScreenLutFeature(target);
        }

        internal static int RemoveEndfieldScreenLutFeature(
            ScriptableRendererData target)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            ValidateRendererFeatureState(target);
            var matches = target.rendererFeatures
                .Where(FeatureMatches)
                .ToArray();
            if (matches.Length == 0)
                return 0;
            var dirty = new UnityEngine.Object[] { target }
                .Concat(target.rendererFeatures)
                .FirstOrDefault(item =>
                    item != null && EditorUtility.IsDirty(item));
            if (dirty != null)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_POST_DIRTY_ASSET:" +
                    AssetDatabase.GetAssetPath(target) + ":" + dirty.name);

            var path = AssetDatabase.GetAssetPath(target);
            var snapshot = AssetFileSnapshot.CaptureAsset(path);
            var undoGroup = BeginUndoGroup();
            try
            {
                Undo.RegisterCompleteObjectUndo(
                    target,
                    "Remove Miku Endfield Screen LUT");
                foreach (var feature in matches)
                {
                    RemoveFeatureReference(target, feature);
                    Undo.DestroyObjectImmediate(feature);
                }
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceSynchronousImport);
                var reloaded = AssetDatabase.LoadAssetAtPath<
                    ScriptableRendererData>(path);
                if (reloaded == null || CountFeatures(reloaded) != 0)
                    throw new InvalidOperationException(
                        "MIKU_ENDFIELD_LUT_FEATURE_REMOVE_PERSIST_FAILED:" + path);
                Undo.CollapseUndoOperations(undoGroup);
                return matches.Length;
            }
            catch
            {
                Undo.RevertAllDownToGroup(undoGroup);
                if (snapshot != null)
                {
                    snapshot.RestoreBytes();
                    snapshot.ImportAsset();
                }
                throw;
            }
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
                Undo.RegisterCompleteObjectUndo(
                    importer,
                    "Configure Miku Endfield LUT Importer");
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

        static FullScreenPassRendererFeature ValidatePersistedFeature(
            ScriptableRendererData target,
            Material material)
        {
            var path = AssetDatabase.GetAssetPath(target);
            var reloaded = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
                path);
            if (reloaded == null)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_RENDERER_RELOAD_FAILED:" + path);

            var matches = reloaded.rendererFeatures
                .OfType<FullScreenPassRendererFeature>()
                .Where(FeatureMatches)
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_FEATURE_PERSISTENCE_INVALID:" +
                    "expected=1:actual=" + matches.Length + ":" + path);

            var feature = matches[0];
            if (!string.Equals(
                    AssetDatabase.GetAssetPath(feature),
                    path,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_FEATURE_ASSET_MISMATCH:" + path);

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    feature,
                    out _,
                    out long localId))
                throw new InvalidOperationException(
                    "MIKU_RENDERER_FEATURE_LOCAL_ID_FAILED");

            ValidateRendererFeatureState(reloaded);
            var serializedRenderer = new SerializedObject(reloaded);
            var features = serializedRenderer.FindProperty("m_RendererFeatures");
            var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");

            var matchedIndex = -1;
            for (var index = 0; index < features.arraySize; index++)
            {
                if (features.GetArrayElementAtIndex(index)
                        .objectReferenceValue != feature)
                    continue;
                if (matchedIndex >= 0)
                    throw new InvalidOperationException(
                        "MIKU_ENDFIELD_LUT_FEATURE_REFERENCE_DUPLICATE:" + path);
                matchedIndex = index;
            }
            if (matchedIndex < 0 ||
                featureMap.GetArrayElementAtIndex(matchedIndex).longValue != localId)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_FEATURE_MAP_INVALID:" + path);

            var serializedFeature = new SerializedObject(feature);
            var injection = RequireProperty(
                serializedFeature,
                "injectionPoint",
                "m_InjectionPoint");
            var requirements = RequireProperty(
                serializedFeature,
                "requirements",
                "m_Requirements");
            var passMaterial = RequireProperty(
                serializedFeature,
                "passMaterial",
                "m_PassMaterial").objectReferenceValue;
            var passIndex = RequireProperty(
                serializedFeature,
                "passIndex",
                "m_PassIndex");
            var fetchColor = RequireProperty(
                serializedFeature,
                "fetchColorBuffer",
                "m_FetchColorBuffer");
            var bindDepth = RequireProperty(
                serializedFeature,
                "bindDepthStencilAttachment",
                "m_BindDepthStencilAttachment");
            if (!feature.isActive ||
                !string.Equals(
                    injection.enumNames[injection.enumValueIndex],
                    "BeforeRenderingPostProcessing",
                    StringComparison.Ordinal) ||
                requirements.intValue != 0 ||
                passIndex.intValue != 0 ||
                !fetchColor.boolValue ||
                bindDepth.boolValue)
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_FEATURE_CONFIGURATION_INVALID:" + path);
            if (!SameAsset(passMaterial, material))
                throw new InvalidOperationException(
                    "MIKU_ENDFIELD_LUT_FEATURE_MATERIAL_INVALID:" + path);
            return feature;
        }

        internal static void ValidateRendererFeatureState(
            ScriptableRendererData target)
        {
            if (target == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_DATA_SELECTION_REQUIRED");
            var path = AssetDatabase.GetAssetPath(target);
            var serialized = new SerializedObject(target);
            var features = serialized.FindProperty("m_RendererFeatures");
            var featureMap = serialized.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_FEATURE_SCHEMA_UNSUPPORTED:URP17");
            if (features.arraySize != featureMap.arraySize)
                throw new InvalidOperationException(
                    "MIKU_RENDERER_FEATURE_STATE_INVALID:count:" + path);

            var seen = new HashSet<long>();
            for (var index = 0; index < features.arraySize; index++)
            {
                var feature = features.GetArrayElementAtIndex(index)
                    .objectReferenceValue as ScriptableRendererFeature;
                if (feature == null)
                    throw new InvalidOperationException(
                        "MIKU_RENDERER_FEATURE_STATE_INVALID:null:" +
                        index + ":" + path);
                if (!string.Equals(
                        AssetDatabase.GetAssetPath(feature),
                        path,
                        StringComparison.Ordinal) ||
                    !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        feature,
                        out _,
                        out long localId) ||
                    featureMap.GetArrayElementAtIndex(index).longValue != localId ||
                    !seen.Add(localId))
                    throw new InvalidOperationException(
                        "MIKU_RENDERER_FEATURE_STATE_INVALID:map:" +
                        index + ":" + path);
            }
        }

        static bool SameAsset(UnityEngine.Object left, UnityEngine.Object right)
        {
            if (left == right)
                return true;
            if (left == null || right == null)
                return false;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    left,
                    out var leftGuid,
                    out long leftLocalId) ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    right,
                    out var rightGuid,
                    out long rightLocalId))
                return false;
            return string.Equals(leftGuid, rightGuid, StringComparison.Ordinal) &&
                leftLocalId == rightLocalId;
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

        sealed class AssetFileSnapshot
        {
            AssetFileSnapshot(
                string absolutePath,
                string assetPath,
                byte[] bytes,
                DateTime lastWriteTimeUtc)
            {
                this.absolutePath = absolutePath;
                this.assetPath = assetPath;
                this.bytes = bytes;
                this.lastWriteTimeUtc = lastWriteTimeUtc;
            }

            readonly string absolutePath;
            readonly string assetPath;
            readonly byte[] bytes;
            readonly DateTime lastWriteTimeUtc;

            internal static AssetFileSnapshot CaptureAsset(string assetPath)
            {
                return Capture(assetPath, assetPath, false);
            }

            internal static AssetFileSnapshot CaptureImporter(string assetPath)
            {
                return Capture(assetPath + ".meta", assetPath, true);
            }

            static AssetFileSnapshot Capture(
                string projectPath,
                string assetPath,
                bool isMeta)
            {
                if (string.IsNullOrEmpty(projectPath))
                    return null;
                var absolute = Path.GetFullPath(Path.Combine(
                    Application.dataPath,
                    "..",
                    projectPath));
                if (!File.Exists(absolute))
                {
                    if (isMeta)
                        throw new InvalidOperationException(
                            "MIKU_ENDFIELD_LUT_IMPORTER_REQUIRED:" + assetPath);
                    return null;
                }
                return new AssetFileSnapshot(
                    absolute,
                    assetPath,
                    File.ReadAllBytes(absolute),
                    File.GetLastWriteTimeUtc(absolute));
            }

            internal void RestoreBytes()
            {
                var temporary = absolutePath + ".miku-rollback-" +
                    Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllBytes(temporary, bytes);
                    File.Replace(temporary, absolutePath, null);
                    File.SetLastWriteTimeUtc(absolutePath, lastWriteTimeUtc);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }

            internal void ImportAsset()
            {
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
