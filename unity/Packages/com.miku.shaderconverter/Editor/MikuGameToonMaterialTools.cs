// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField] List<Texture2D> textureValues = new List<Texture2D>();
        [SerializeField] string textureSlotKey = "";

        [MenuItem("Miku/Game Toon/Materials/Create Material")]
        static void Open() =>
            GetWindow<MikuGameToonMaterialTemplateWindow>(
                MikuEditorLocalization.Tr("Miku Material Creator"));

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
            var allowedParts = MikuFixedWorkflowTextureBindings
                .AllowedParts(workflow);
            var partIndex = Array.IndexOf(
                allowedParts.ToArray(),
                part);
            if (partIndex < 0)
            {
                partIndex = 0;
                part = allowedParts[0];
            }
            var selectedPartIndex = EditorGUILayout.Popup(
                MikuEditorLocalization.Tr("Material Part"),
                partIndex,
                allowedParts.Select(
                    item => MikuEditorLocalization.Tr(item.ToString()))
                    .ToArray());
            if (selectedPartIndex >= 0 &&
                selectedPartIndex < allowedParts.Count)
                part = allowedParts[selectedPartIndex];

            var slotKey = workflow + ":" + part;
            if (!string.Equals(textureSlotKey, slotKey, StringComparison.Ordinal))
            {
                textureValues.Clear();
                textureSlotKey = slotKey;
            }

            try
            {
                var shaderName = MikuFixedWorkflowTextureBindings.ShaderName(
                    workflow,
                    part.ToString());
                var slots = GetTextureSlots(shaderName, workflow);
                EnsureTextureValueCount(slots.Count);
                EditorGUILayout.HelpBox(
                    MikuEditorLocalization.Format(
                        "Shader: {0}\\nThe created .mat is user-owned and is never rebound to a model automatically.",
                        shaderName),
                    MessageType.Info);

                EditorGUILayout.LabelField(
                    MikuEditorLocalization.Tr("Texture Inputs"),
                    EditorStyles.boldLabel);
                for (var index = 0; index < slots.Count; index++)
                {
                    var slot = slots[index];
                    var label = slot.Label +
                        (slot.Required
                            ? " " + MikuEditorLocalization.Tr("(Required)")
                            : " " + MikuEditorLocalization.Tr("(Optional)"));
                    textureValues[index] = (Texture2D)EditorGUILayout.ObjectField(
                        label,
                        textureValues[index],
                        typeof(Texture2D),
                        false);
                }

                var missing = slots
                    .Select((slot, index) => new { slot, index })
                    .Where(item => item.slot.Required &&
                                   textureValues[item.index] == null)
                    .Select(item => item.slot.Label)
                    .ToArray();
                if (missing.Length > 0)
                    EditorGUILayout.HelpBox(
                        MikuEditorLocalization.Format(
                            "Required textures missing: {0}",
                            string.Join(", ", missing)),
                        MessageType.Warning);

                using (new EditorGUI.DisabledScope(missing.Length > 0))
                {
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
                    CreateConfiguredMaterialAsset(
                        path,
                        workflow,
                        part,
                        slots,
                        textureValues);
                    Selection.activeObject =
                        AssetDatabase.LoadAssetAtPath<Material>(path);
                }
            }
            catch (ArgumentException exception)
            {
                EditorGUILayout.HelpBox(
                    MikuEditorLocalization.Tr(exception.Message),
                    MessageType.Error);
            }
            catch (InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(
                    MikuEditorLocalization.Tr(exception.Message),
                    MessageType.Error);
            }
        }

        internal readonly struct MikuGameMaterialTextureSlot
        {
            internal readonly string Property;
            internal readonly string Label;
            internal readonly bool Required;

            internal MikuGameMaterialTextureSlot(
                string property,
                string label,
                bool required)
            {
                Property = property;
                Label = label;
                Required = required;
            }
        }

        internal static IReadOnlyList<MikuGameMaterialTextureSlot> GetTextureSlots(
            string shaderName,
            string workflow)
        {
            var shader = Shader.Find(shaderName)
                ?? throw new InvalidOperationException(
                    "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            var slots = new List<MikuGameMaterialTextureSlot>();
            for (var index = 0; index < shader.GetPropertyCount(); index++)
            {
                var property = shader.GetPropertyName(index);
                if (shader.GetPropertyType(index) != ShaderPropertyType.Texture ||
                    (shader.GetPropertyFlags(index) &
                     ShaderPropertyFlags.HideInInspector) != 0 ||
                    shader.GetPropertyTextureDimension(index) !=
                    UnityEngine.Rendering.TextureDimension.Tex2D)
                    continue;
                if (property == "_MainTex" ||
                    (workflow == "wuwa_toon" &&
                     shaderName == "MIKU/Wuwa/Body" &&
                     property == "_StockingsMap"))
                    continue;
                var required = property == "_BaseMap" &&
                    !(workflow == "endfield_toon" &&
                      shaderName == "MIKU/Endfield/Mouth");
                slots.Add(new MikuGameMaterialTextureSlot(
                    property,
                    LocalizedTextureLabel(property),
                    required));
            }
            if (slots.Count == 0)
                throw new InvalidOperationException(
                    "MIKU_WORKFLOW_TEXTURE_SLOTS_EMPTY:" + shaderName);
            return slots;
        }

        static string LocalizedTextureLabel(string property)
        {
            var english = property switch
            {
                "_BaseMap" => "Base Map",
                "_NormalMap" => "Normal Map",
                "_LightMap" => "Light Map",
                "_ShadowRampMap" => "Shadow Ramp Map",
                "_MetalMap" => "Metal Map",
                "_EmissionMap" => "Emission Map",
                "_HairRampMap" => "Hair Ramp Map",
                "_HairSpecMap" => "Hair Specular Map",
                "_BodyCoolRamp" => "Body Cool Ramp",
                "_BodyWarmRamp" => "Body Warm Ramp",
                "_StockingsMap" => "Stockings Map",
                "_FaceMap" => "Face Map",
                "_HairCoolRamp" => "Hair Cool Ramp",
                "_HairWarmRamp" => "Hair Warm Ramp",
                "_IDMap" => "ID / Stockings Map",
                "_MatCap" => "MatCap",
                "_FaceSDF" => "Face SDF",
                "_FaceID" => "Face ID",
                "_FaceHET" => "Face HET",
                "_SkinRamp" => "Skin Ramp",
                "_HairHM" => "Hair HM",
                "_EyeHET" => "Eye HET",
                "_EyeHDMF" => "Eye HDMF",
                "_EyeUpperHighlight" => "Eye Upper Highlight",
                "_EyeLowerHighlight" => "Eye Lower Highlight",
                "_EyeEG" => "Eye EG",
                "_MaterialParamMap" => "Material Parameter Map",
                "_DiffRampMap" => "Diffuse Ramp Map",
                "_SpecRampMap" => "Specular Ramp Map",
                "_ShadowLutTex" => "Shadow LUT",
                "_ColorLutTex" => "Color LUT",
                "_SplitNormalMap" => "Split Normal Map",
                "_SpecularMask" => "Specular Mask",
                "_SpecularRefineF0Tex" => "Specular Refine F0",
                "_SpecularRefineColorTex" => "Specular Refine Color",
                "_HairLineMap" => "Hair Line Map",
                "_HairShiftMap" => "Hair Shift Map",
                "_HairRefineMap" => "Hair Refine Map",
                "_FaceAreaMap" => "Face Area Map",
                "_FaceRefineMap" => "Face Refine Map",
                "_SDFLightmap" => "Face SDF",
                "_EmotionMap" => "Emotion Map",
                "_HighlightMap" => "Highlight Map",
                "_OutlineMask" => "Outline Mask",
                "_EffectMask" => "Effect Mask",
                _ => property,
            };
            return MikuEditorLocalization.Tr(english);
        }

        void EnsureTextureValueCount(int count)
        {
            while (textureValues.Count < count)
                textureValues.Add(null);
            if (textureValues.Count > count)
                textureValues.RemoveRange(count, textureValues.Count - count);
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

        internal static Material CreateConfiguredMaterialAsset(
            string assetPath,
            string workflow,
            MikuGameMaterialPart materialPart,
            IReadOnlyList<MikuGameMaterialTextureSlot> slots,
            IReadOnlyList<Texture2D> textures)
        {
            var normalized = ValidateAssetPath(assetPath, ".mat");
            if (AssetDatabase.LoadMainAssetAtPath(normalized) != null)
                throw new InvalidOperationException(
                    "MIKU_MATERIAL_ALREADY_EXISTS:" + normalized);
            if (slots == null || textures == null || slots.Count != textures.Count)
                throw new InvalidOperationException(
                    "MIKU_TEXTURE_SLOT_VALUE_COUNT_MISMATCH");
            var shaderName = MikuFixedWorkflowTextureBindings.ShaderName(
                workflow,
                materialPart.ToString());
            var shader = Shader.Find(shaderName)
                ?? throw new InvalidOperationException(
                    "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            // Validate every requested value against the shader before creating
            // a UnityEngine.Object. A failed wizard run must not leave even an
            // unsaved half-configured material behind.
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var texture = textures[index];
                if (slot.Required && texture == null)
                    throw new InvalidOperationException(
                        "MIKU_REQUIRED_TEXTURE_MISSING:" + slot.Property);
                if (texture != null && !ShaderHasProperty(shader, slot.Property))
                    throw new InvalidOperationException(
                        "MIKU_TEXTURE_PROPERTY_MISSING:" + slot.Property);
            }
            var material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(normalized),
            };
            for (var index = 0; index < slots.Count; index++)
            {
                var slot = slots[index];
                var texture = textures[index];
                if (texture == null)
                    continue;
                material.SetTexture(slot.Property, texture);
                if (workflow == "wuwa_toon" &&
                    materialPart == MikuGameMaterialPart.Body &&
                    slot.Property == "_IDMap" &&
                    material.HasProperty("_StockingsMap"))
                    material.SetTexture("_StockingsMap", texture);
            }
            MikuGameToonMaterialProfiles.ApplyRecommended(
                material,
                logMissingMask: false);
            MikuManualTextureKeywordUtility.SyncKeywords(material);
            EnsureFolder(Path.GetDirectoryName(normalized)?.Replace('\\', '/'));
            AssetDatabase.CreateAsset(material, normalized);
            AssetDatabase.SaveAssets();
            return material;
        }

        static bool ShaderHasProperty(Shader shader, string propertyName)
        {
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var index = 0; index < count; index++)
            {
                if (string.Equals(
                        ShaderUtil.GetPropertyName(shader, index),
                        propertyName,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
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

    public enum MikuGenshinTextureProfile
    {
        Unrecognized,
        ColorRepeat,
        LinearRepeat,
        RampColorClampNoMips,
        FaceSdfLinearRepeatNoMips,
        NormalMap,
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
                        "Apply Endfield Import Settings")))
                    ApplyEndfieldFolder(
                        path,
                        "Assets/Miku/Reports/endfield-texture-import-audit.json");
                if (GUILayout.Button(MikuEditorLocalization.Tr(
                        "Apply Genshin Import Settings")))
                    ApplyGenshinFolder(
                        path,
                        "Assets/Miku/Reports/genshin-texture-import-audit.json");
            }
        }

        public static MikuGenshinTextureProfile ClassifyGenshinFileName(
            string fileName)
        {
            var stem = Path.GetFileNameWithoutExtension(fileName ?? "")
                .ToLowerInvariant();
            var compact = new string(stem
                .Where(char.IsLetterOrDigit)
                .ToArray());
            if (compact.EndsWith("facelightmap", StringComparison.Ordinal) ||
                compact.EndsWith("facesdf", StringComparison.Ordinal) ||
                compact.Contains("facesdf"))
                return MikuGenshinTextureProfile.FaceSdfLinearRepeatNoMips;
            if (compact.EndsWith("bodyshadowramp", StringComparison.Ordinal) ||
                compact.EndsWith("hairshadowramp", StringComparison.Ordinal) ||
                compact.EndsWith("shadowramp", StringComparison.Ordinal) ||
                compact.EndsWith("skinramp", StringComparison.Ordinal))
                return MikuGenshinTextureProfile.RampColorClampNoMips;
            if (compact.EndsWith("normalmap", StringComparison.Ordinal) ||
                compact.EndsWith("normal", StringComparison.Ordinal))
                return MikuGenshinTextureProfile.NormalMap;
            if (compact.EndsWith("lightmap", StringComparison.Ordinal) ||
                compact.EndsWith("metalmap", StringComparison.Ordinal))
                return MikuGenshinTextureProfile.LinearRepeat;
            if (compact.EndsWith("diffuse", StringComparison.Ordinal) ||
                compact.EndsWith("emission", StringComparison.Ordinal) ||
                compact.EndsWith("emissive", StringComparison.Ordinal))
                return MikuGenshinTextureProfile.ColorRepeat;
            return MikuGenshinTextureProfile.Unrecognized;
        }

        public static int ApplyGenshinFolder(
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
                         new[] { assetFolder })
                     .OrderBy(item => item, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = ClassifyGenshinFileName(Path.GetFileName(path));
                if (profile == MikuGenshinTextureProfile.Unrecognized)
                    continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    continue;
                var before = Snapshot(importer);
                Undo.RecordObject(importer, "Apply Miku Genshin texture import profile");
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
                ["schema"] = "miku-genshin-texture-import-audit-1.0",
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
            ["alphaSource"] = importer.alphaSource.ToString(),
            ["alphaIsTransparency"] = importer.alphaIsTransparency,
            ["npotScale"] = importer.npotScale.ToString(),
            ["textureCompression"] = importer.textureCompression.ToString(),
            ["crunchedCompression"] = importer.crunchedCompression,
            ["standaloneOverridden"] = importer
                .GetPlatformTextureSettings("Standalone").overridden,
        };

        static void Apply(
            TextureImporter importer,
            MikuGenshinTextureProfile profile)
        {
            importer.textureType = profile == MikuGenshinTextureProfile.NormalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture =
                profile == MikuGenshinTextureProfile.ColorRepeat ||
                profile == MikuGenshinTextureProfile.RampColorClampNoMips;
            importer.mipmapEnabled =
                profile != MikuGenshinTextureProfile.RampColorClampNoMips &&
                profile != MikuGenshinTextureProfile.FaceSdfLinearRepeatNoMips;
            importer.wrapMode =
                profile == MikuGenshinTextureProfile.RampColorClampNoMips
                    ? TextureWrapMode.Clamp
                    : TextureWrapMode.Repeat;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.filterMode = FilterMode.Bilinear;
            var controlData =
                profile == MikuGenshinTextureProfile.LinearRepeat ||
                profile == MikuGenshinTextureProfile.RampColorClampNoMips ||
                profile == MikuGenshinTextureProfile.FaceSdfLinearRepeatNoMips;
            if (controlData)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression =
                    TextureImporterCompression.Uncompressed;
                importer.crunchedCompression = false;
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                if (standalone.overridden)
                {
                    standalone.overridden = false;
                    importer.SetPlatformTextureSettings(standalone);
                }
            }
        }

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
