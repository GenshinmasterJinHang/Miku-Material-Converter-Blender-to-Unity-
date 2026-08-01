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
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This material is mesh-bound. Use the generated prefab, or " +
                "apply it only to a renderer with an identical mesh.",
                MessageType.Warning);
            if (!GUILayout.Button("Apply to Selected Renderer"))
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
            Undo.RecordObject(renderer, "Apply Miku Mesh-Bound Material");
            foreach (var slot in binding.materialSlots)
                materials[slot] = description.material;
            renderer.sharedMaterials = materials;
            EditorUtility.SetDirty(renderer);
        }
    }
}
