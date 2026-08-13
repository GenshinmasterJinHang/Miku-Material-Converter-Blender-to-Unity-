// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Describes the assets produced by a non-destructive WuWa 3.0 scene clone.
    /// </summary>
    public sealed class MikuWuwa3MigrationResult
    {
        internal MikuWuwa3MigrationResult(
            string scenePath,
            string materialFolder,
            IReadOnlyList<string> materialPaths)
        {
            ScenePath = scenePath;
            MaterialFolder = materialFolder;
            MaterialPaths = materialPaths;
        }

        public string ScenePath { get; }
        public string MaterialFolder { get; }
        public IReadOnlyList<string> MaterialPaths { get; }
    }

    /// <summary>
    /// Creates a WuWa 3.0 scene and material set without changing the source
    /// scene, source materials, FBX assets, or prefabs.
    /// </summary>
    public static class MikuWuwa3Migration
    {
        const string SceneExtension = ".unity";

        /// <summary>
        /// Clones a scene, clones every MIKU/Wuwa material referenced by that
        /// scene, and applies the 3.0 recommended profile to the clones.
        /// </summary>
        public static MikuWuwa3MigrationResult CloneAndUpgradeScene(
            string sourceScenePath,
            string destinationScenePath,
            string destinationMaterialFolder)
        {
            return CloneAndUpgradeScene(
                sourceScenePath,
                destinationScenePath,
                destinationMaterialFolder,
                null);
        }

        internal static MikuWuwa3MigrationResult CloneAndUpgradeScene(
            string sourceScenePath,
            string destinationScenePath,
            string destinationMaterialFolder,
            Action beforeCommit)
        {
            var source = ValidateAssetPath(sourceScenePath, SceneExtension);
            var destination = ValidateAssetPath(
                destinationScenePath,
                SceneExtension);
            var materialFolder = ValidateAssetFolder(destinationMaterialFolder);
            if (string.Equals(source, destination, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MIKU_WUWA3_SOURCE_DESTINATION_IDENTICAL");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(source) == null)
                throw new InvalidOperationException(
                    "MIKU_WUWA3_SOURCE_SCENE_MISSING:" + source);
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null)
                throw new InvalidOperationException(
                    "MIKU_WUWA3_DESTINATION_SCENE_EXISTS:" + destination);
            if (AssetDatabase.IsValidFolder(materialFolder))
                throw new InvalidOperationException(
                    "MIKU_WUWA3_DESTINATION_MATERIAL_FOLDER_EXISTS:" +
                    materialFolder);

            var setup = EditorSceneManager.GetSceneManagerSetup();
            var createdFolders = new List<string>();
            var createdAssets = new List<string>();
            Scene clonedScene = default;
            try
            {
                EnsureFolder(
                    Path.GetDirectoryName(destination)?.Replace('\\', '/'),
                    createdFolders);
                EnsureFolder(materialFolder, createdFolders);
                if (!AssetDatabase.CopyAsset(source, destination))
                    throw new InvalidOperationException(
                        "MIKU_WUWA3_SCENE_CLONE_FAILED:" + destination);
                createdAssets.Add(destination);

                clonedScene = EditorSceneManager.OpenScene(
                    destination,
                    OpenSceneMode.Additive);
                var materialMap = new Dictionary<Material, Material>();
                foreach (var renderer in clonedScene.GetRootGameObjects()
                             .SelectMany(root =>
                                 root.GetComponentsInChildren<Renderer>(true)))
                {
                    var materials = renderer.sharedMaterials;
                    var changed = false;
                    for (var index = 0; index < materials.Length; index++)
                    {
                        var sourceMaterial = materials[index];
                        if (!IsWuwaMaterial(sourceMaterial))
                            continue;
                        if (!materialMap.TryGetValue(
                                sourceMaterial,
                                out var clonedMaterial))
                        {
                            var materialPath = UniqueMaterialPath(
                                materialFolder,
                                sourceMaterial.name,
                                createdAssets);
                            clonedMaterial = new Material(sourceMaterial)
                            {
                                name = Path.GetFileNameWithoutExtension(
                                    materialPath),
                            };
                            MikuGameToonMaterialProfiles.ApplyRecommended(
                                clonedMaterial,
                                false);
                            AssetDatabase.CreateAsset(
                                clonedMaterial,
                                materialPath);
                            createdAssets.Add(materialPath);
                            materialMap.Add(sourceMaterial, clonedMaterial);
                        }
                        materials[index] = clonedMaterial;
                        changed = true;
                    }
                    if (changed)
                    {
                        Undo.RecordObject(
                            renderer,
                            "Clone and upgrade WuWa materials to 3.0");
                        renderer.sharedMaterials = materials;
                        EditorUtility.SetDirty(renderer);
                    }
                }

                beforeCommit?.Invoke();
                if (!EditorSceneManager.SaveScene(clonedScene, destination))
                    throw new InvalidOperationException(
                        "MIKU_WUWA3_SCENE_SAVE_FAILED:" + destination);
                AssetDatabase.SaveAssets();
                var materialPaths = createdAssets
                    .Where(path => path.EndsWith(
                        ".mat",
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray();
                return new MikuWuwa3MigrationResult(
                    destination,
                    materialFolder,
                    materialPaths);
            }
            catch
            {
                if (clonedScene.IsValid() && clonedScene.isLoaded)
                    EditorSceneManager.CloseScene(clonedScene, true);
                foreach (var asset in createdAssets
                             .OrderByDescending(path => path.Length))
                    AssetDatabase.DeleteAsset(asset);
                RollbackFolders(createdFolders);
                AssetDatabase.Refresh();
                throw;
            }
            finally
            {
                if (clonedScene.IsValid() && clonedScene.isLoaded)
                    EditorSceneManager.CloseScene(clonedScene, true);
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static bool IsWuwaMaterial(Material material) =>
            material != null && material.shader != null &&
            material.shader.name.StartsWith(
                "MIKU/Wuwa/",
                StringComparison.Ordinal);

        static string UniqueMaterialPath(
            string folder,
            string sourceName,
            ICollection<string> reserved)
        {
            var name = Sanitize(sourceName);
            var suffix = 0;
            while (true)
            {
                var candidate = folder + "/" + name +
                    (suffix == 0 ? "" : "_" + suffix) + ".mat";
                if (AssetDatabase.LoadMainAssetAtPath(candidate) == null &&
                    !reserved.Contains(candidate))
                    return candidate;
                suffix++;
            }
        }

        static string ValidateAssetPath(string value, string extension)
        {
            var path = (value ?? "").Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                path.Split('/').Any(part => part == "." || part == ".."))
                throw new InvalidOperationException(
                    "MIKU_WUWA3_ASSET_PATH_INVALID:" + value);
            return path;
        }

        static string ValidateAssetFolder(string value)
        {
            var path = (value ?? "").Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                path.Split('/').Any(part => part == "." || part == ".."))
                throw new InvalidOperationException(
                    "MIKU_WUWA3_ASSET_FOLDER_INVALID:" + value);
            return path;
        }

        static void EnsureFolder(
            string folder,
            ICollection<string> createdFolders)
        {
            if (string.IsNullOrEmpty(folder) || folder == "Assets")
                return;
            var current = "Assets";
            foreach (var part in folder.Substring(7).Split('/'))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (string.IsNullOrEmpty(
                            AssetDatabase.CreateFolder(current, part)))
                        throw new InvalidOperationException(
                            "MIKU_WUWA3_FOLDER_CREATE_FAILED:" + next);
                    createdFolders.Add(next);
                }
                current = next;
            }
        }

        static void RollbackFolders(IEnumerable<string> folders)
        {
            foreach (var folder in folders.OrderByDescending(
                         path => path.Length))
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
        }

        static string Sanitize(string value)
        {
            var name = string.IsNullOrWhiteSpace(value)
                ? "WuWaMaterial"
                : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '_');
            return name;
        }
    }
}
