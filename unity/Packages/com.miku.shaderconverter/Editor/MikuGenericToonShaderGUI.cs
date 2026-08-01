// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    public sealed class MikuGenericToonShaderGUI : ShaderGUI
    {
        public override void OnGUI(
            MaterialEditor materialEditor,
            MaterialProperty[] properties)
        {
            EditorGUILayout.LabelField(
                "Miku Generic Toon",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The outline and character-mask passes are embedded in this " +
                "semantic shader. Renderer references are never changed here.",
                MessageType.Info);

            var materials = materialEditor.targets
                .OfType<Material>()
                .ToArray();
            var semantics = materials
                .Select(material => ParseSemantic(material.shader).ToString())
                .Distinct()
                .OrderBy(value => value)
                .ToArray();
            EditorGUILayout.HelpBox(
                "Semantic: " + string.Join(", ", semantics),
                MessageType.None);
            MikuToonRendererFeatureInstaller.DrawStatusAndOpenButton();

            DrawGroup(
                materialEditor,
                properties,
                "Surface",
                "_MIKU_BaseMap",
                "_MIKU_BaseColor",
                "_MIKU_ShadowColor",
                "_MIKU_AlphaClip",
                "_MIKU_Cutoff");
            DrawGroup(
                materialEditor,
                properties,
                "Toon Lighting",
                "_MIKU_ToonSteps",
                "_MIKU_ShadowSoftness",
                "_MIKU_SSSStrength",
                "_MIKU_MetallicAccent");
            DrawGroup(
                materialEditor,
                properties,
                "Embedded Geometry Outline",
                "_MIKU_OutlineEnabled",
                "_MIKU_OutlineColor",
                "_MIKU_OutlineWidth",
                "_MIKU_OutlineDepthBias",
                "_MIKU_OutlineMinPixels",
                "_MIKU_OutlineMaxPixels");
            DrawGroup(
                materialEditor,
                properties,
                "Screen Rim",
                "_MIKU_RimColor",
                "_MIKU_RimIntensity",
                "_MIKU_RimWidth");
            DrawGroup(
                materialEditor,
                properties,
                "Face Object Space",
                "_MIKU_FaceCenterOS",
                "_MIKU_FaceExtentOS",
                "_MIKU_FaceRembrandt",
                "_MIKU_FaceBlush");
            EditorGUILayout.Space();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
            materialEditor.RenderQueueField();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Semantic Preset"))
                {
                    foreach (var material in materials)
                    {
                        var semantic = ParseSemantic(material.shader);
                        MikuToonRecipeUtility.ApplySemanticPreset(
                            material,
                            semantic);
                    }
                }
                if (GUILayout.Button("Restore Source Values"))
                {
                    foreach (var material in materials)
                    {
                        var path = AssetDatabase.GetAssetPath(material);
                        var recipePath = System.IO.Path.ChangeExtension(
                                path,
                                ".toon-recipe.asset")
                            .Replace('\\', '/');
                        MikuToonRecipeUtility.RestoreSourceValues(
                            AssetDatabase.LoadAssetAtPath<
                                MikuToonMaterialRecipe>(recipePath));
                    }
                }
            }
        }

        static void DrawGroup(
            MaterialEditor editor,
            MaterialProperty[] properties,
            string title,
            params string[] names)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var name in names)
            {
                var property = FindProperty(name, properties, false);
                if (property != null)
                    editor.ShaderProperty(property, property.displayName);
            }
        }

        static MikuToonSemantic ParseSemantic(Shader shader)
        {
            var name = shader != null ? shader.name : "";
            var tail = name.Substring(name.LastIndexOf('/') + 1);
            return System.Enum.TryParse(tail, out MikuToonSemantic semantic)
                ? semantic
                : MikuToonSemantic.GenericOpaque;
        }
    }
}
