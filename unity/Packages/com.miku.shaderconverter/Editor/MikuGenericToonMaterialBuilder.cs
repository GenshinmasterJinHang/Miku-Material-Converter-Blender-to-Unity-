// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    public sealed class MikuGenericToonMaterialBuilder : EditorWindow
    {
        [Serializable]
        sealed class Row
        {
            public Material source;
            public MikuToonSemantic semantic =
                MikuToonSemantic.GenericOpaque;
            public MikuToonAlbedoMode albedoMode =
                MikuToonAlbedoMode.Auto;
            public Texture overrideTexture;
            public Color overrideColor = Color.white;
            public string outputFolder = "Assets/Miku/ToonMaterials";
            public string outputName = "";
        }

        [SerializeField] List<Row> rows = new List<Row>();
        Vector2 scroll;

        [MenuItem("Miku/Generic Toon/Material Builder")]
        static void Open() =>
            GetWindow<MikuGenericToonMaterialBuilder>(
                "Miku Toon Materials");

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Material-asset workflow only. This tool never scans a Model " +
                "Root and never changes Renderer references.",
                MessageType.Info);
            MikuToonRendererFeatureInstaller.DrawStatusAndOpenButton();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Selected Materials"))
                    AddSelected();
                if (GUILayout.Button("Add Empty Row"))
                    rows.Add(new Row());
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            "Material " + (index + 1),
                            EditorStyles.boldLabel);
                        if (GUILayout.Button("Remove", GUILayout.Width(70)))
                        {
                            rows.RemoveAt(index--);
                            continue;
                        }
                    }
                    row.source = (Material)EditorGUILayout.ObjectField(
                        "Source",
                        row.source,
                        typeof(Material),
                        false);
                    row.semantic = (MikuToonSemantic)
                        EditorGUILayout.EnumPopup("Semantic", row.semantic);
                    row.albedoMode = (MikuToonAlbedoMode)
                        EditorGUILayout.EnumPopup(
                            "Albedo",
                            row.albedoMode);
                    if (row.albedoMode != MikuToonAlbedoMode.Auto)
                    {
                        if (row.albedoMode == MikuToonAlbedoMode.Override)
                            row.overrideTexture = (Texture)
                                EditorGUILayout.ObjectField(
                                    "Texture",
                                    row.overrideTexture,
                                    typeof(Texture),
                                    false);
                        row.overrideColor = EditorGUILayout.ColorField(
                            "Tint",
                            row.overrideColor);
                    }
                    row.outputFolder = EditorGUILayout.TextField(
                        "Output Folder",
                        row.outputFolder);
                    row.outputName = EditorGUILayout.TextField(
                        "Output Name",
                        row.outputName);
                    if (GUILayout.Button("Create Derived Material"))
                        Create(row);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void AddSelected()
        {
            foreach (var material in Selection.GetFiltered<Material>(
                         SelectionMode.Assets))
                rows.Add(new Row
                {
                    source = material,
                    outputName = material.name + "_" +
                                 MikuToonSemantic.GenericOpaque,
                });
        }

        internal readonly struct BuildResult
        {
            internal BuildResult(
                Material generatedBase,
                Material userMaterial,
                MikuToonMaterialRecipe recipe)
            {
                this.generatedBase = generatedBase;
                this.userMaterial = userMaterial;
                this.recipe = recipe;
            }

            internal readonly Material generatedBase;
            internal readonly Material userMaterial;
            internal readonly MikuToonMaterialRecipe recipe;
        }

        static void Create(Row row)
        {
            var result = CreateAssets(
                row.source,
                row.semantic,
                row.albedoMode,
                row.overrideTexture,
                row.overrideColor,
                row.outputFolder,
                row.outputName);
            Selection.activeObject = result.userMaterial;
            EditorGUIUtility.PingObject(result.userMaterial);
        }

        internal static BuildResult CreateAssets(
            Material source,
            MikuToonSemantic semantic,
            MikuToonAlbedoMode albedoMode,
            Texture overrideTexture,
            Color overrideColor,
            string outputFolder,
            string outputName)
        {
            if (source == null)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MATERIAL_MISSING");
            var folder = NormalizeFolder(outputFolder);
            EnsureFolder(folder);
            var name = string.IsNullOrWhiteSpace(outputName)
                ? source.name + "_" + semantic
                : outputName.Trim();
            name = Sanitize(name);
            ResolveOutputPaths(
                folder,
                name,
                out var baseMaterialPath,
                out var materialPath,
                out var recipePath);
            var shader = Shader.Find(
                MikuToonRecipeUtility.ShaderName(semantic));
            if (shader == null)
                throw new InvalidOperationException(
                    "MIKU_TOON_SHADER_MISSING:" + semantic);
            MikuToonRecipeUtility.ResolveSource(
                source,
                albedoMode,
                overrideTexture,
                overrideColor,
                out var texture,
                out var color);

            var generatedBase = new Material(shader)
            {
                name = name + ".generated",
            };
            if (generatedBase.HasProperty(MikuToonRecipeUtility.BaseMap))
                generatedBase.SetTexture(
                    MikuToonRecipeUtility.BaseMap,
                    texture);
            if (generatedBase.HasProperty(MikuToonRecipeUtility.BaseColor))
                generatedBase.SetColor(
                    MikuToonRecipeUtility.BaseColor,
                    color);
            MikuToonRecipeUtility.ApplySemanticPreset(
                generatedBase,
                semantic);
            var derived = new Material(generatedBase)
            {
                name = name,
            };

            var recipe =
                ScriptableObject.CreateInstance<MikuToonMaterialRecipe>();
            recipe.sourceMaterial = source;
            recipe.generatedBaseMaterial = generatedBase;
            recipe.userMaterial = derived;
            recipe.semantic = semantic;
            recipe.albedoMode = albedoMode;
            recipe.sourceGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(source));
            recipe.sourceTexture = texture;
            recipe.sourceColor = color;
            recipe.lastSyncedTexture = texture;
            recipe.lastSyncedColor = color;
            recipe.lastSyncedCutoff =
                generatedBase.HasProperty(MikuToonRecipeUtility.Cutoff)
                    ? generatedBase.GetFloat(MikuToonRecipeUtility.Cutoff)
                    : 0.5f;
            recipe.initialPreset =
                MikuToonPresetSnapshot.Capture(generatedBase);
            try
            {
                AssetDatabase.CreateAsset(
                    generatedBase,
                    baseMaterialPath);
                AssetDatabase.CreateAsset(derived, materialPath);
                recipe.targetGuid =
                    AssetDatabase.AssetPathToGUID(materialPath);
                AssetDatabase.CreateAsset(recipe, recipePath);
                recipe.stableGuid =
                    AssetDatabase.AssetPathToGUID(recipePath);
                EditorUtility.SetDirty(recipe);
                Undo.RegisterCreatedObjectUndo(
                    generatedBase,
                    "Create Miku Toon materials");
                Undo.RegisterCreatedObjectUndo(
                    derived,
                    "Create Miku Toon materials");
                Undo.RegisterCreatedObjectUndo(
                    recipe,
                    "Create Miku Toon materials");
                AssetDatabase.SaveAssets();
                return new BuildResult(
                    generatedBase,
                    derived,
                    recipe);
            }
            catch
            {
                foreach (var path in new[]
                         {
                             recipePath,
                             materialPath,
                             baseMaterialPath,
                         })
                {
                    if (!string.IsNullOrEmpty(
                            AssetDatabase.AssetPathToGUID(path)))
                        AssetDatabase.DeleteAsset(path);
                }
                if (!AssetDatabase.Contains(recipe))
                    DestroyImmediate(recipe);
                if (!AssetDatabase.Contains(derived))
                    DestroyImmediate(derived);
                if (!AssetDatabase.Contains(generatedBase))
                    DestroyImmediate(generatedBase);
                throw;
            }
        }

        static void ResolveOutputPaths(
            string folder,
            string requestedName,
            out string baseMaterialPath,
            out string materialPath,
            out string recipePath)
        {
            for (var suffix = 0; suffix < 10000; suffix++)
            {
                var stem = suffix == 0
                    ? requestedName
                    : requestedName + "_" + suffix;
                baseMaterialPath =
                    folder + "/" + stem + ".generated.mat";
                materialPath = folder + "/" + stem + ".mat";
                recipePath =
                    folder + "/" + stem + ".toon-recipe.asset";
                if (string.IsNullOrEmpty(
                        AssetDatabase.AssetPathToGUID(baseMaterialPath)) &&
                    string.IsNullOrEmpty(
                        AssetDatabase.AssetPathToGUID(materialPath)) &&
                    string.IsNullOrEmpty(
                        AssetDatabase.AssetPathToGUID(recipePath)))
                    return;
            }
            throw new InvalidOperationException(
                "MIKU_TOON_OUTPUT_NAME_EXHAUSTED");
        }

        static string NormalizeFolder(string value)
        {
            var folder = (value ?? "").Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                folder.Contains("/../", StringComparison.Ordinal) ||
                folder.EndsWith("/..", StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "MIKU_TOON_OUTPUT_FOLDER_INVALID");
            return folder;
        }

        static void EnsureFolder(string folder)
        {
            var current = "Assets";
            foreach (var part in folder.Substring("Assets/".Length)
                         .Split('/'))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        static string Sanitize(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');
            return string.IsNullOrWhiteSpace(value) ? "ToonMaterial" : value;
        }
    }
}
