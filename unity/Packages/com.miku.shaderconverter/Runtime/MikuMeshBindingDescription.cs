using System;
using System.Collections.Generic;
using UnityEngine;

namespace Miku.ShaderConverter.Runtime
{
    /// <summary>
    /// Records the source-mesh contract for a mesh-bound Miku material.
    /// Mesh-bound materials are valid on the generated prefab or on a renderer
    /// whose geometry fingerprint matches one of these entries.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MikuMeshBinding",
        menuName = "Miku/Mesh Binding Description")]
    public sealed class MikuMeshBindingDescription : ScriptableObject
    {
        [Serializable]
        public sealed class RendererBinding
        {
            public string rendererPath = "";
            public string sourceObject = "";
            public string sourceMeshFingerprint = "";
            public string unityMeshFingerprint = "";
            public int vertexCount;
            public int indexCount;
            public int[] materialSlots = Array.Empty<int>();
        }

        public string sourceMeshSha256 = "";
        public string meshFingerprintSet = "";
        public GameObject generatedPrefab;
        public Material material;
        public List<RendererBinding> rendererBindings =
            new List<RendererBinding>();
    }
}
