// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    public sealed class MikuGameToonMaterialTemplateWindow : EditorWindow
    {
        static readonly string[] Workflows =
        {
            "genshin_toon", "wuwa_toon", "hsr_toon", "endfield_toon",
        };

        [SerializeField] int workflowIndex;
        [SerializeField] MikuGameMaterialPart part = MikuGameMaterialPart.Body;

        [MenuItem("Miku/Game Toon/Materials/Create Material Template")]
        static void Open() =>
            GetWindow<MikuGameToonMaterialTemplateWindow>(
                MikuEditorLocalization.Tr("Miku Material Template"));

        void OnGUI()
        {
            workflowIndex = EditorGUILayout.Popup(
                MikuEditorLocalization.Tr("Workflow"),
                workflowIndex,
                Workflows.Select(MikuEditorLocalization.Tr).ToArray());
            var workflow = Workflows[Mathf.Clamp(
                workflowIndex,
                0,
                Workflows.Length - 1)];
            part = (MikuGameMaterialPart)EditorGUILayout.EnumPopup(
                MikuEditorLocalization.Tr("Material Part"),
                part);
            string shaderName;
            try
            {
                shaderName = MikuFixedWorkflowTextureBindings.ShaderName(
                    workflow,
                    part.ToString());
                EditorGUILayout.HelpBox(
                    shaderName +
                    "\n" + MikuEditorLocalization.Tr(
                        "The created .mat is user-owned and is never rebound " +
                        "to a model automatically."),
                    MessageType.Info);
            }
            catch (ArgumentException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }
            if (!GUILayout.Button(MikuEditorLocalization.Tr(
                    "Create User-owned Material")))
                return;
            var path = EditorUtility.SaveFilePanelInProject(
                MikuEditorLocalization.Tr("Create Miku game Toon material"),
                workflow + "_" + part,
                "mat",
                MikuEditorLocalization.Tr(
                    "Choose the output location under Assets."));
            if (string.IsNullOrEmpty(path))
                return;
            CreateMaterialAsset(path, workflow, part);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        public static Material CreateMaterialAsset(
            string assetPath,
            string workflow,
            MikuGameMaterialPart materialPart)
        {
            var normalized = ValidateAssetPath(assetPath, ".mat");
            if (AssetDatabase.LoadMainAssetAtPath(normalized) != null)
                throw new InvalidOperationException(
                    "MIKU_MATERIAL_TEMPLATE_ALREADY_EXISTS:" + normalized);
            EnsureFolder(Path.GetDirectoryName(normalized)?.Replace('\\', '/'));
            var shaderName = MikuFixedWorkflowTextureBindings.ShaderName(
                workflow,
                materialPart.ToString());
            var shader = Shader.Find(shaderName)
                ?? throw new InvalidOperationException(
                    "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            var material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(normalized),
            };
            AssetDatabase.CreateAsset(material, normalized);
            AssetDatabase.SaveAssets();
            return material;
        }

        internal static string ValidateAssetPath(string value, string extension)
        {
            var path = (value ?? "").Replace('\\', '/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ||
                path.Split('/').Any(item => item == "." || item == ".."))
                throw new InvalidOperationException(
                    "MIKU_ASSET_OUTPUT_PATH_INVALID:" + value);
            return path;
        }

        internal static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) ||
                !folder.StartsWith("Assets", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MIKU_ASSET_OUTPUT_FOLDER_INVALID");
            var current = "Assets";
            foreach (var item in folder.Substring(6).TrimStart('/').Split('/'))
            {
                if (string.IsNullOrEmpty(item))
                    continue;
                var next = current + "/" + item;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, item);
                current = next;
            }
        }
    }

    public enum MikuEndfieldTextureProfile
    {
        Unrecognized,
        ColorRepeat,
        LinearRepeat,
        ColorClampNoMips,
        LinearClampNoMips,
    }

    public sealed class MikuGameToonTextureImportAuditWindow : EditorWindow
    {
        [SerializeField] DefaultAsset folder;

        [MenuItem("Miku/Game Toon/Textures/Import Audit")]
        static void Open() =>
            GetWindow<MikuGameToonTextureImportAuditWindow>(
                MikuEditorLocalization.Tr("Miku Texture Audit"));

        void OnGUI()
        {
            folder = (DefaultAsset)EditorGUILayout.ObjectField(
                MikuEditorLocalization.Tr("Texture Folder"),
                folder,
                typeof(DefaultAsset),
                false);
            var path = folder == null ? "" : AssetDatabase.GetAssetPath(folder);
            EditorGUILayout.HelpBox(
                MikuEditorLocalization.Tr(
                    "Only complete Endfield filename patterns are recognized. " +
                    "Ambiguous _M files are left unchanged."),
                MessageType.Info);
            using (new EditorGUI.DisabledScope(
                       string.IsNullOrEmpty(path) ||
                       !AssetDatabase.IsValidFolder(path)))
            {
                if (GUILayout.Button(MikuEditorLocalization.Tr(
                        "Apply Recognized Import Settings")))
                    ApplyEndfieldFolder(
                        path,
                        "Assets/Miku/Reports/endfield-texture-import-audit.json");
            }
        }

        public static MikuEndfieldTextureProfile ClassifyFileName(string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName ?? "")
                .ToLowerInvariant();
            if (string.IsNullOrEmpty(stem))
                return MikuEndfieldTextureProfile.Unrecognized;
            if (stem.Contains("common_hairst") &&
                stem.EndsWith("_st", StringComparison.Ordinal))
                return MikuEndfieldTextureProfile.LinearClampNoMips;
            if ((stem.Contains("hairst") &&
                 stem.EndsWith("_st", StringComparison.Ordinal)) ||
                (stem.Contains("hairline") &&
                 stem.EndsWith("_m", StringComparison.Ordinal)))
                return MikuEndfieldTextureProfile.LinearRepeat;
            if (stem.Contains("emotion") ||
                stem.Contains("matcap"))
                return MikuEndfieldTextureProfile.ColorClampNoMips;
            if (stem.Contains("hairshadow") ||
                stem.Contains("eyeshadow") ||
                stem.EndsWith("_sdf", StringComparison.Ordinal) ||
                stem.EndsWith("_st", StringComparison.Ordinal) ||
                stem.EndsWith("_cm_m", StringComparison.Ordinal) ||
                stem.EndsWith("_sw_m", StringComparison.Ordinal) ||
                stem.EndsWith("_hl_m", StringComparison.Ordinal) ||
                stem.Contains("fx_") && stem.EndsWith("_m", StringComparison.Ordinal))
                return MikuEndfieldTextureProfile.LinearClampNoMips;
            if (stem.EndsWith("_hn", StringComparison.Ordinal) ||
                stem.EndsWith("_n", StringComparison.Ordinal) ||
                stem.EndsWith("_p", StringComparison.Ordinal) ||
                stem.EndsWith("_cloth_01_m", StringComparison.Ordinal))
                return MikuEndfieldTextureProfile.LinearRepeat;
            if (stem.Contains("_rd") ||
                stem.Contains("_rs") ||
                stem.Contains("lut"))
                return MikuEndfieldTextureProfile.ColorClampNoMips;
            if (stem.EndsWith("_d", StringComparison.Ordinal) ||
                stem.EndsWith("_e", StringComparison.Ordinal))
                return MikuEndfieldTextureProfile.ColorRepeat;
            return MikuEndfieldTextureProfile.Unrecognized;
        }

        public static int ApplyEndfieldFolder(
            string assetFolder,
            string reportAssetPath)
        {
            if (!AssetDatabase.IsValidFolder(assetFolder))
                throw new InvalidOperationException(
                    "MIKU_TEXTURE_AUDIT_FOLDER_INVALID:" + assetFolder);
            var reportPath = MikuGameToonMaterialTemplateWindow.ValidateAssetPath(
                reportAssetPath,
                ".json");
            var changes = new JArray();
            var changedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets(
                         "t:Texture2D",
                         new[] { assetFolder }).OrderBy(item => item, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = ClassifyFileName(Path.GetFileName(path));
                if (profile == MikuEndfieldTextureProfile.Unrecognized)
                    continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;
                var before = Snapshot(importer);
                Undo.RecordObject(
                    importer,
                    MikuEditorLocalization.Tr(
                        "Apply Miku Endfield texture import profile"));
                Apply(importer, profile);
                var after = Snapshot(importer);
                var changed = !JToken.DeepEquals(before, after);
                if (changed)
                {
                    importer.SaveAndReimport();
                    changedCount++;
                }
                changes.Add(new JObject
                {
                    ["path"] = path,
                    ["profile"] = profile.ToString(),
                    ["changed"] = changed,
                    ["before"] = before,
                    ["after"] = after,
                });
            }
            var report = new JObject
            {
                ["schema"] = "miku-endfield-texture-import-audit-1.0",
                ["folder"] = assetFolder,
                ["changedCount"] = changedCount,
                ["textures"] = changes,
            };
            var absolute = Path.GetFullPath(Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? "",
                reportPath));
            var assetsRoot = Path.GetFullPath(Application.dataPath) +
                Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "MIKU_TEXTURE_AUDIT_REPORT_PATH_UNSAFE");
            MikuAtomicAssetWriter.WriteIfChanged(
                absolute,
                report.ToString(Newtonsoft.Json.Formatting.Indented) + "\n");
            AssetDatabase.ImportAsset(reportPath);
            return changedCount;
        }

        static JObject Snapshot(TextureImporter importer) => new JObject
        {
            ["sRGB"] = importer.sRGBTexture,
            ["wrapMode"] = importer.wrapMode.ToString(),
            ["mipmapEnabled"] = importer.mipmapEnabled,
            ["textureType"] = importer.textureType.ToString(),
        };

        static void Apply(
            TextureImporter importer,
            MikuEndfieldTextureProfile profile)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = profile == MikuEndfieldTextureProfile.ColorRepeat ||
                profile == MikuEndfieldTextureProfile.ColorClampNoMips;
            var clamp = profile == MikuEndfieldTextureProfile.ColorClampNoMips ||
                profile == MikuEndfieldTextureProfile.LinearClampNoMips;
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.mipmapEnabled = !clamp;
        }
    }
}
