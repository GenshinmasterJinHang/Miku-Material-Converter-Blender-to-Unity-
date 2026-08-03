using System;
using System.Linq;
using Miku.ShaderConverter.Runtime;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    [CustomEditor(typeof(MikuMeshBindingDescription))]
    sealed class MikuMeshBindingDescriptionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawProperty("sourceMeshSha256", "Source Mesh SHA-256");
            DrawProperty("meshFingerprintSet", "Mesh Fingerprint Set");
            DrawProperty("generatedPrefab", "Generated Prefab");
            DrawProperty("material", "Material");
            DrawProperty("rendererBindings", "Renderer Bindings", true);
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                MikuEditorLocalization.Tr(
                    "This material is mesh-bound. Use the generated prefab, or " +
                    "apply it only to a renderer with an identical mesh."),
                MessageType.Warning);
            if (!GUILayout.Button(MikuEditorLocalization.Tr(
                    "Apply to Selected Renderer")))
                return;
            var description =
                (MikuMeshBindingDescription)target;
            var selected = Selection.activeGameObject;
            var renderer = selected != null
                ? selected.GetComponent<Renderer>()
                : null;
            var filter = selected != null
                ? selected.GetComponent<MeshFilter>()
                : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (renderer == null || mesh == null)
            {
                Debug.LogError(
                    "MIKU_MESH_BINDING_MISMATCH:" +
                    "select a GameObject with MeshRenderer and MeshFilter");
                return;
            }
            var fingerprint =
                MikuBundleImporter.ComputeUnityMeshFingerprint(mesh);
            var binding = description.rendererBindings.SingleOrDefault(
                item => string.Equals(
                    item.unityMeshFingerprint,
                    fingerprint,
                    StringComparison.Ordinal));
            if (binding == null)
            {
                Debug.LogError(
                    "MIKU_MESH_BINDING_MISMATCH:" +
                    selected.name);
                return;
            }
            var materials = renderer.sharedMaterials;
            var requiredLength = Math.Max(
                materials.Length,
                binding.materialSlots.DefaultIfEmpty(-1).Max() + 1);
            if (requiredLength != materials.Length)
                Array.Resize(ref materials, requiredLength);
            Undo.RecordObject(
                renderer,
                MikuEditorLocalization.Tr("Apply Miku Mesh-Bound Material"));
            foreach (var slot in binding.materialSlots)
                materials[slot] = description.material;
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }

        void DrawProperty(
            string propertyName,
            string label,
            bool includeChildren = false)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property != null)
                EditorGUILayout.PropertyField(
                    property,
                    MikuEditorLocalization.Content(label),
                    includeChildren);
        }
    }
}
