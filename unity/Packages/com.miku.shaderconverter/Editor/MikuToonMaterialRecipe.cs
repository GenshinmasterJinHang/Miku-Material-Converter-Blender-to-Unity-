// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    public enum MikuGameMaterialPart
    {
        Body,
        Hair,
        Face,
        Eye,
        Effect,
        Skin,
        Mouth,
        Overlay,
        HairShadow,
    }

    [Serializable]
    public sealed class MikuToonUvTransform
    {
        public string coordinateSpace = "UV0";
        public string operation = "Affine2D";
        public Vector3 row0 = new Vector3(1f, 0f, 0f);
        public Vector3 row1 = new Vector3(0f, 1f, 0f);

        public MikuToonUvTransform Clone() => new MikuToonUvTransform
        {
            coordinateSpace = coordinateSpace,
            operation = operation,
            row0 = row0,
            row1 = row1,
        };
    }

    [Serializable]
    public sealed class MikuToonTextureBinding
    {
        public string role = "";
        public Texture texture;
        [SerializeReference]
        public MikuToonUvTransform uvTransform;
    }

    /// <summary>
    /// Miku-owned synchronization metadata for one user-owned game Toon material.
    /// The recipe deliberately contains no retired semantic state.
    /// </summary>
    public sealed class MikuToonMaterialRecipe : ScriptableObject
    {
        public const string CurrentShaderFamilyVersion = "2.2.9";

        public Material generatedBaseMaterial;
        public Material userMaterial;
        public string workflowKind = "";
        public MikuGameMaterialPart gamePart = MikuGameMaterialPart.Body;
        public MikuToonTextureBinding[] textureBindings =
            Array.Empty<MikuToonTextureBinding>();
        public string sourceGuid = "";
        public string targetGuid = "";
        public string stableGuid = "";
        public string shaderFamilyVersion = CurrentShaderFamilyVersion;
    }

    [CustomEditor(typeof(MikuToonMaterialRecipe))]
    internal sealed class MikuToonMaterialRecipeEditor : UnityEditor.Editor
    {
        static readonly string[] PartValues =
        {
            "Body", "Hair", "Face", "Eye", "Effect", "Skin", "Mouth",
            "Overlay", "HairShadow",
        };

        SerializedProperty generatedBaseMaterial;
        SerializedProperty userMaterial;
        SerializedProperty workflowKind;
        SerializedProperty gamePart;
        SerializedProperty textureBindings;
        SerializedProperty sourceGuid;
        SerializedProperty targetGuid;
        SerializedProperty stableGuid;
        SerializedProperty shaderFamilyVersion;

        void OnEnable()
        {
            generatedBaseMaterial = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.generatedBaseMaterial));
            userMaterial = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.userMaterial));
            workflowKind = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.workflowKind));
            gamePart = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.gamePart));
            textureBindings = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.textureBindings));
            sourceGuid = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.sourceGuid));
            targetGuid = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.targetGuid));
            stableGuid = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.stableGuid));
            shaderFamilyVersion = serializedObject.FindProperty(
                nameof(MikuToonMaterialRecipe.shaderFamilyVersion));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            Draw(generatedBaseMaterial, "Generated Base Material");
            Draw(userMaterial, "User Material");
            Draw(workflowKind, "Workflow");
            DrawPartPopup();
            Draw(textureBindings, "Texture Bindings");
            Draw(sourceGuid, "Source GUID");
            Draw(targetGuid, "Target GUID");
            Draw(stableGuid, "Stable GUID");
            Draw(shaderFamilyVersion, "Shader Family Version");
            serializedObject.ApplyModifiedProperties();
        }

        static void Draw(SerializedProperty property, string label)
        {
            if (property != null)
                EditorGUILayout.PropertyField(
                    property,
                    MikuEditorLocalization.Content(label),
                    true);
        }

        void DrawPartPopup()
        {
            if (gamePart == null)
                return;
            var labels = PartValues
                .Select(MikuEditorLocalization.Tr)
                .ToArray();
            var selected = Mathf.Clamp(gamePart.enumValueIndex, 0, labels.Length - 1);
            var next = EditorGUILayout.Popup(
                MikuEditorLocalization.Tr("Material Part"),
                selected,
                labels);
            if (next != selected)
                gamePart.enumValueIndex = next;
        }
    }

    internal static class MikuToonRecipeUtility
    {
        internal static string SelectedShaderName(
            MikuToonMaterialRecipe recipe)
        {
            if (recipe == null)
                throw new ArgumentNullException(nameof(recipe));
            var workflow = recipe.workflowKind ?? "";
            if (!MikuFixedWorkflowTextureBindings.IsGame(workflow))
                throw new InvalidOperationException(
                    "MIKU_WORKFLOW_RETIRED:generic_toon");
            return MikuFixedWorkflowTextureBindings.ShaderName(
                workflow,
                recipe.gamePart.ToString());
        }

        internal static MikuToonMaterialRecipe FindForMaterial(
            Material material)
        {
            var path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path))
                return null;
            var recipePath = System.IO.Path.ChangeExtension(
                    path,
                    ".toon-recipe.asset")
                .Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<MikuToonMaterialRecipe>(
                recipePath);
        }

        internal static void ApplySelection(MikuToonMaterialRecipe recipe)
        {
            if (recipe == null || recipe.generatedBaseMaterial == null)
                throw new InvalidOperationException("MIKU_TOON_RECIPE_BASE_MISSING");
            var shaderName = SelectedShaderName(recipe);
            var shader = Shader.Find(shaderName)
                ?? throw new InvalidOperationException(
                    "MIKU_WORKFLOW_SHADER_MISSING:" + shaderName);
            Undo.RecordObjects(
                new UnityEngine.Object[]
                {
                    recipe.generatedBaseMaterial,
                    recipe,
                },
                MikuEditorLocalization.Tr(
                    "Change Miku game Toon material part"));
            recipe.generatedBaseMaterial.shader = shader;
            recipe.gamePart = ParsePart(recipe.workflowKind, recipe.gamePart.ToString());
            MikuFixedWorkflowTextureBindings.Bind(
                recipe.generatedBaseMaterial,
                recipe.workflowKind,
                recipe.textureBindings);
            MikuGameToonMaterialProfiles.ApplyRecommended(
                recipe.generatedBaseMaterial);
            EditorUtility.SetDirty(recipe.generatedBaseMaterial);
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }

        internal static MikuToonMaterialRecipe CreateOrUpdateImported(
            string path,
            Material generatedBase,
            Material userMaterial,
            string workflowKind,
            string initialPart,
            IDictionary<string, Texture2D> textures)
        {
            return CreateOrUpdateImported(
                path,
                generatedBase,
                userMaterial,
                workflowKind,
                initialPart,
                (textures ?? new Dictionary<string, Texture2D>())
                    .OrderBy(item => item.Key, StringComparer.Ordinal)
                    .Select(item => new MikuToonTextureBinding
                    {
                        role = item.Key,
                        texture = item.Value,
                    }));
        }

        internal static MikuToonMaterialRecipe CreateOrUpdateImported(
            string path,
            Material generatedBase,
            Material userMaterial,
            string workflowKind,
            string initialPart,
            IEnumerable<MikuToonTextureBinding> bindings)
        {
            if (!MikuFixedWorkflowTextureBindings.IsGame(workflowKind))
                throw new InvalidOperationException(
                    "MIKU_WORKFLOW_RETIRED:generic_toon");
            var recipe = AssetDatabase.LoadAssetAtPath<MikuToonMaterialRecipe>(
                path);
            var created = recipe == null;
            var previousVersion = recipe != null
                ? recipe.shaderFamilyVersion
                : "";
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<
                    MikuToonMaterialRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }
            recipe.generatedBaseMaterial = generatedBase;
            recipe.userMaterial = userMaterial;
            if (created || string.IsNullOrEmpty(recipe.workflowKind))
                recipe.workflowKind = workflowKind;
            if (created)
                recipe.gamePart = ParsePart(workflowKind, initialPart);
            recipe.textureBindings = (bindings ??
                    Array.Empty<MikuToonTextureBinding>())
                .Where(item => item != null)
                .OrderBy(item => item.role, StringComparer.Ordinal)
                .Select(item => new MikuToonTextureBinding
                {
                    role = item.role ?? "",
                    texture = item.texture,
                    uvTransform = item.uvTransform?.Clone(),
                })
                .ToArray();
            recipe.sourceGuid = "";
            recipe.targetGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(userMaterial));
            recipe.stableGuid = AssetDatabase.AssetPathToGUID(path);
            recipe.shaderFamilyVersion =
                MikuToonMaterialRecipe.CurrentShaderFamilyVersion;
            if (generatedBase != null &&
                (created || !string.Equals(
                    previousVersion,
                    MikuToonMaterialRecipe.CurrentShaderFamilyVersion,
                    StringComparison.Ordinal)))
            {
                MikuGameToonMaterialProfiles.ApplyRecommended(generatedBase);
                EditorUtility.SetDirty(generatedBase);
                AssetDatabase.SaveAssetIfDirty(generatedBase);
            }
            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssetIfDirty(recipe);
            return recipe;
        }

        static MikuGameMaterialPart ParsePart(string workflow, string value)
        {
            var normalized = MikuFixedWorkflowTextureBindings.NormalizePart(
                workflow,
                value ?? "");
            if (Enum.TryParse(normalized, out MikuGameMaterialPart part))
                return part;
            throw new InvalidOperationException(
                "MIKU_WORKFLOW_PART_INVALID:" + workflow + ":" + value);
        }
    }
}
