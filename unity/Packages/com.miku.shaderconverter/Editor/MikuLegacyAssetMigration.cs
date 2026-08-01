// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    /// <summary>
    /// Explicit selected-asset migration. It never traverses scene objects or
    /// modifies Renderer material assignments.
    /// </summary>
    internal static class MikuLegacyAssetMigration
    {
        [MenuItem("Miku/Migration/Dry Run Selected MiGR Assets")]
        static void DryRun() => Run(false);

        [MenuItem("Miku/Migration/Upgrade Selected MiGR Assets")]
        static void Apply() => Run(true);

        static void Run(bool apply)
        {
            var paths = SelectedAssetPaths();
            var materialPaths = Expand(paths, "t:Material");
            var clipPaths = Expand(paths, "t:AnimationClip");
            var metadataPaths = Expand(paths, "t:TextAsset")
                .Where(IsLegacyMetadataPath)
                .ToArray();
            var materialChanges = materialPaths.Sum(
                path => MigrateMaterial(path, apply));
            var curveChanges = clipPaths.Sum(
                path => MigrateClip(path, apply));
            var metadataChanges = metadataPaths.Sum(
                path => MigrateGeneratedMetadata(path, apply));
            if (apply)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
            }
            Debug.Log(
                (apply ? "MIKU_LEGACY_MIGRATION_APPLIED:" :
                    "MIKU_LEGACY_MIGRATION_DRY_RUN:") +
                $"materials={materialChanges}:curves={curveChanges}:" +
                $"metadata={metadataChanges}");
        }

        static string[] SelectedAssetPaths()
        {
            var paths = Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                throw new InvalidOperationException(
                    "MIKU_MIGRATION_ASSET_SELECTION_REQUIRED");
            return paths;
        }

        static string[] Expand(
            IEnumerable<string> selected,
            string filter)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in selected)
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    foreach (var guid in AssetDatabase.FindAssets(
                                 filter,
                                 new[] { path }))
                        result.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
                else
                {
                    result.Add(path);
                }
            }
            return result.OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        internal static int MigrateMaterial(string path, bool apply)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                return 0;
            if (apply)
                Undo.RecordObject(material, "Upgrade MiGR material data");
            var serialized = new SerializedObject(material);
            serialized.Update();
            var changed = 0;
            foreach (var propertyPath in new[]
                     {
                         "m_SavedProperties.m_TexEnvs",
                         "m_SavedProperties.m_Floats",
                         "m_SavedProperties.m_Colors",
                     })
            {
                var values = serialized.FindProperty(propertyPath);
                if (values == null || !values.isArray)
                    continue;
                for (var index = 0; index < values.arraySize; index++)
                {
                    var key = values.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("first");
                    if (key == null)
                        continue;
                    var next = UpgradePropertyName(key.stringValue);
                    if (next == key.stringValue)
                        continue;
                    changed++;
                    if (apply)
                        key.stringValue = next;
                }
            }
            if (material.shader != null &&
                string.Equals(
                    material.shader.name,
                    "MGIR/GenericToon/Lit",
                    StringComparison.Ordinal))
            {
                changed++;
                if (apply)
                    material.shader = Shader.Find(
                        "Miku/GenericToon/GenericOpaque");
            }
            if (apply && changed > 0)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(material);
            }
            return changed;
        }

        internal static int MigrateClip(string path, bool apply)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
                return 0;
            if (apply)
                Undo.RecordObject(clip, "Upgrade MiGR animation curves");
            var changed = 0;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var nextName = UpgradePropertyName(binding.propertyName);
                if (nextName == binding.propertyName)
                    continue;
                changed++;
                if (!apply)
                    continue;
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                var upgraded = binding;
                upgraded.propertyName = nextName;
                AnimationUtility.SetEditorCurve(clip, upgraded, curve);
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
            if (apply && changed > 0)
                EditorUtility.SetDirty(clip);
            return changed;
        }

        internal static int MigrateGeneratedMetadata(
            string path,
            bool apply)
        {
            if (!IsLegacyMetadataPath(path))
                return 0;
            var absolute = ToAbsolute(path);
            JObject document;
            try
            {
                document = JObject.Parse(File.ReadAllText(absolute));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(
                    "MIKU_LEGACY_METADATA_JSON_INVALID:" + path,
                    ex);
            }
            document = (JObject)UpgradeMetadataToken(document);
            if (path.EndsWith(
                    ".migr-assets.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                document["schema"] =
                    "miku-generated-asset-identity-1.0";
            }
            else if (path.EndsWith(
                         ".migr-unity-receipt.json",
                         StringComparison.OrdinalIgnoreCase))
            {
                document["documentKind"] =
                    "miku-unity-import-receipt-1.0";
                document["schemaVersion"] = "1.0";
                document["toolVersion"] = "1.0.0";
            }
            else
            {
                document["documentKind"] =
                    "miku-conversion-manifest-1.0";
                document["schemaVersion"] = "1.0";
                document["toolVersion"] = "1.0.0";
            }
            if (document["canonicalHash"] != null)
                document["canonicalHash"] =
                    MikuBundleImporter.CanonicalHash(
                        document,
                        "canonicalHash");
            if (!apply)
                return 1;

            var nextPath = UpgradeMetadataPath(path);
            if (!string.IsNullOrEmpty(
                    AssetDatabase.AssetPathToGUID(nextPath)))
                throw new InvalidOperationException(
                    "MIKU_LEGACY_METADATA_TARGET_EXISTS:" + nextPath);
            var moveError = AssetDatabase.MoveAsset(path, nextPath);
            if (!string.IsNullOrEmpty(moveError))
                throw new InvalidOperationException(
                    "MIKU_LEGACY_METADATA_MOVE_FAILED:" + moveError);
            try
            {
                MikuAtomicAssetWriter.WriteIfChanged(
                    ToAbsolute(nextPath),
                    document.ToString(Formatting.Indented) + "\n");
                AssetDatabase.ImportAsset(
                    nextPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            catch
            {
                var rollbackError =
                    AssetDatabase.MoveAsset(nextPath, path);
                if (!string.IsNullOrEmpty(rollbackError))
                    throw new InvalidOperationException(
                        "MIKU_LEGACY_METADATA_ROLLBACK_FAILED:" +
                        rollbackError);
                throw;
            }
            return 1;
        }

        static bool IsLegacyMetadataPath(string path) =>
            path.EndsWith(
                ".migr-assets.json",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                ".migr-unity-receipt.json",
                StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(
                ".migrmanifest.json",
                StringComparison.OrdinalIgnoreCase);

        static string UpgradeMetadataPath(string path)
        {
            if (path.EndsWith(
                    ".migr-assets.json",
                    StringComparison.OrdinalIgnoreCase))
                return path.Substring(
                           0,
                           path.Length - ".migr-assets.json".Length) +
                       ".miku-assets.json";
            if (path.EndsWith(
                    ".migr-unity-receipt.json",
                    StringComparison.OrdinalIgnoreCase))
                return path.Substring(
                           0,
                           path.Length -
                           ".migr-unity-receipt.json".Length) +
                       ".miku-unity-receipt.json";
            return path.Substring(
                       0,
                       path.Length - ".migrmanifest.json".Length) +
                   ".miku-manifest.json";
        }

        static JToken UpgradeMetadataToken(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (var property in obj.Properties())
                    result[UpgradeMetadataString(property.Name)] =
                        UpgradeMetadataToken(property.Value);
                return result;
            }
            if (token is JArray array)
                return new JArray(array.Select(UpgradeMetadataToken));
            if (token.Type == JTokenType.String)
                return new JValue(
                    UpgradeMetadataString(token.Value<string>()));
            return token.DeepClone();
        }

        static string UpgradeMetadataString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            if (string.Equals(value, "MiGR", StringComparison.Ordinal))
                return "Miku";
            return value
                .Replace(
                    "migr-generated-asset-identity-1.0",
                    "miku-generated-asset-identity-1.0")
                .Replace(
                    "migr-unity-import-receipt-1.0",
                    "miku-unity-import-receipt-1.0")
                .Replace(".migr-assets.json", ".miku-assets.json")
                .Replace(
                    ".migr-unity-receipt.json",
                    ".miku-unity-receipt.json")
                .Replace(".migrmanifest.json", ".miku-manifest.json")
                .Replace("MGIR/GenericToon/Lit",
                    "Miku/GenericToon/GenericOpaque")
                .Replace("MiGR Generated", "Miku Generated")
                .Replace("_MIGR_", "_MIKU_")
                .Replace("_MGIR_", "_MIKU_");
        }

        static string ToAbsolute(string assetPath)
        {
            var projectRoot = Directory.GetParent(
                Application.dataPath)?.FullName ??
                throw new InvalidOperationException(
                    "MIKU_PROJECT_ROOT_MISSING");
            var absolute = Path.GetFullPath(Path.Combine(
                projectRoot,
                assetPath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
            var allowed = Path.GetFullPath(projectRoot)
                              .TrimEnd(
                                  Path.DirectorySeparatorChar,
                                  Path.AltDirectorySeparatorChar) +
                          Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(
                    allowed,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "MIKU_LEGACY_METADATA_PATH_UNSAFE");
            return absolute;
        }

        internal static string UpgradePropertyName(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return value
                .Replace("_MIGR_", "_MIKU_")
                .Replace("_MGIR_", "_MIKU_");
        }
    }
}
