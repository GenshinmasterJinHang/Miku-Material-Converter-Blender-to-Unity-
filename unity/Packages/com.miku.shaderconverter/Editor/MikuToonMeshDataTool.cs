// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    public enum MikuVertexColorWriteMode
    {
        Preserve,
        Replace,
        Merge,
    }

    public enum MikuUv7ConflictMode
    {
        Preserve,
        Replace,
    }

    public abstract class MikuToonMeshToolWindow : EditorWindow
    {
        [SerializeField] Mesh sourceMesh;
        [SerializeField] string outputFolder = "Assets/Miku/ToonMeshes";
        [SerializeField] string outputName = "";
        [SerializeField, Range(0.000001f, 0.1f)]
        float positionTolerance = 0.0001f;
        [SerializeField, Range(0f, 180f)] float smoothingAngle = 60f;
        [SerializeField] bool includeBoneWeightSignature = true;
        [SerializeField] MikuUv7ConflictMode uv7ConflictMode =
            MikuUv7ConflictMode.Preserve;
        [SerializeField] MikuVertexColorWriteMode colorMode =
            MikuVertexColorWriteMode.Preserve;
        [SerializeField] bool mergeR = true;
        [SerializeField] bool mergeG = true;
        [SerializeField] bool mergeB = true;
        [SerializeField] bool mergeA = true;

        protected abstract bool ShowsSmoothNormals { get; }
        protected abstract bool ShowsVertexColors { get; }

        internal Mesh SourceMesh => sourceMesh;

        protected static T OpenWindow<T>(string title)
            where T : MikuToonMeshToolWindow
        {
            var window = GetWindow<T>(title);
            window.CaptureSelectedMesh();
            return window;
        }

        protected virtual void OnEnable()
        {
            if (sourceMesh == null)
                CaptureSelectedMesh();
        }

        void OnSelectionChange()
        {
            if (!(Selection.activeObject is Mesh))
                return;
            CaptureSelectedMesh();
            Repaint();
        }

        void CaptureSelectedMesh()
        {
            if (Selection.activeObject is Mesh selected)
                sourceMesh = selected;
        }

        protected virtual void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Explicit Mesh input only. The source/importer and all " +
                "Renderer references remain untouched.",
                MessageType.Info);
            sourceMesh = (Mesh)EditorGUILayout.ObjectField(
                "Source Mesh",
                sourceMesh,
                typeof(Mesh),
                false);
            outputFolder = EditorGUILayout.TextField(
                "Output Folder",
                outputFolder);
            outputName = EditorGUILayout.TextField(
                "Output Name",
                outputName);

            var sourceReadable = sourceMesh == null || sourceMesh.isReadable;
            if (!sourceReadable)
                EditorGUILayout.HelpBox(
                    "The selected Mesh is not readable. Create an explicit " +
                    "readable source asset before using this tool; importer " +
                    "settings are never changed automatically.",
                    MessageType.Error);

            var hasUv7 = sourceMesh != null &&
                         sourceReadable &&
                         MikuToonMeshAssetCreator.HasUv7(sourceMesh);
            if (ShowsSmoothNormals)
                DrawSmoothNormalSettings(hasUv7);
            if (ShowsVertexColors)
                DrawVertexColorSettings();

            using (new EditorGUI.DisabledScope(
                       sourceMesh == null || !sourceReadable))
            {
                if (ShowsSmoothNormals && ShowsVertexColors)
                {
                    var preserveUv7 =
                        hasUv7 &&
                        uv7ConflictMode == MikuUv7ConflictMode.Preserve;
                    var label = preserveUv7
                        ? "Create Mesh (Preserve UV7 + Vertex Colors)"
                        : "Create Mesh with Both";
                    if (GUILayout.Button(label) &&
                        ConfirmUv7Replacement(hasUv7))
                        Create(
                            writeNormals: !preserveUv7,
                            writeColors: true);
                }
                else if (ShowsSmoothNormals)
                {
                    var blocked =
                        hasUv7 &&
                        uv7ConflictMode == MikuUv7ConflictMode.Preserve;
                    using (new EditorGUI.DisabledScope(blocked))
                    {
                        if (GUILayout.Button(
                                "Create Mesh with Smooth Normals") &&
                            ConfirmUv7Replacement(hasUv7))
                            Create(
                                writeNormals: true,
                                writeColors: false);
                    }
                }
                else if (GUILayout.Button(
                             "Create Mesh with Neutral Vertex Colors"))
                {
                    Create(writeNormals: false, writeColors: true);
                }
            }
        }

        void DrawSmoothNormalSettings(bool hasUv7)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Smooth outline normal -> UV7 / TEXCOORD7",
                EditorStyles.boldLabel);
            positionTolerance = EditorGUILayout.Slider(
                "Position Tolerance",
                positionTolerance,
                0.000001f,
                0.1f);
            smoothingAngle = EditorGUILayout.Slider(
                "Smoothing Angle",
                smoothingAngle,
                0f,
                180f);
            includeBoneWeightSignature = EditorGUILayout.Toggle(
                "Respect Bone Weights",
                includeBoneWeightSignature);
            if (!hasUv7)
                return;

            EditorGUILayout.HelpBox(
                "The source Mesh already contains UV7 data. Preserve leaves " +
                "that channel unchanged; Replace writes smooth normals only " +
                "to the generated clone.",
                MessageType.Warning);
            uv7ConflictMode = (MikuUv7ConflictMode)
                EditorGUILayout.EnumPopup(
                    "Existing UV7",
                    uv7ConflictMode);
            if (!ShowsVertexColors &&
                uv7ConflictMode == MikuUv7ConflictMode.Preserve)
                EditorGUILayout.HelpBox(
                    "Select Replace to generate smooth normals. Preserve " +
                    "performs no operation in the normals-only tool.",
                    MessageType.Info);
        }

        void DrawVertexColorSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Vertex colors - Miku_ToonMask_v1",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Neutral mask is RGBA (255,255,255,0): SSS, outline width, " +
                "screen rim, face correction.",
                MessageType.None);
            colorMode = (MikuVertexColorWriteMode)
                EditorGUILayout.EnumPopup("Mode", colorMode);
            if (colorMode != MikuVertexColorWriteMode.Merge)
                return;
            mergeR = EditorGUILayout.Toggle("Write R / SSS", mergeR);
            mergeG = EditorGUILayout.Toggle(
                "Write G / Outline",
                mergeG);
            mergeB = EditorGUILayout.Toggle(
                "Write B / Screen Rim",
                mergeB);
            mergeA = EditorGUILayout.Toggle(
                "Write A / Face Correction",
                mergeA);
        }

        bool ConfirmUv7Replacement(bool hasUv7)
        {
            if (!hasUv7 ||
                uv7ConflictMode != MikuUv7ConflictMode.Replace)
                return true;
            return EditorUtility.DisplayDialog(
                "Replace UV7 on generated Mesh?",
                "The source Mesh remains untouched. UV7 will be replaced only " +
                "on the newly generated Mesh asset.",
                "Replace on Clone",
                "Cancel");
        }

        void Create(bool writeNormals, bool writeColors)
        {
            var clone = MikuToonMeshAssetCreator.CreateAsset(
                sourceMesh,
                outputFolder,
                outputName,
                writeNormals,
                positionTolerance,
                smoothingAngle,
                includeBoneWeightSignature,
                uv7ConflictMode == MikuUv7ConflictMode.Replace,
                writeColors,
                colorMode,
                mergeR,
                mergeG,
                mergeB,
                mergeA);
            Selection.activeObject = clone;
            EditorGUIUtility.PingObject(clone);
        }
    }

    public sealed class MikuSmoothNormalGeneratorWindow :
        MikuToonMeshToolWindow
    {
        protected override bool ShowsSmoothNormals => true;
        protected override bool ShowsVertexColors => false;

        [MenuItem(
            "Miku/Generic Toon/Mesh/Smooth Normal Generator")]
        static void Open() =>
            OpenWindow<MikuSmoothNormalGeneratorWindow>(
                "Miku Smooth Normals");
    }

    public sealed class MikuVertexColorInitializerWindow :
        MikuToonMeshToolWindow
    {
        protected override bool ShowsSmoothNormals => false;
        protected override bool ShowsVertexColors => true;

        [MenuItem(
            "Miku/Generic Toon/Mesh/Vertex Color Initializer")]
        static void Open() =>
            OpenWindow<MikuVertexColorInitializerWindow>(
                "Miku Vertex Colors");
    }

    public sealed class MikuToonMeshDataTool : MikuToonMeshToolWindow
    {
        protected override bool ShowsSmoothNormals => true;
        protected override bool ShowsVertexColors => true;

        [MenuItem("Miku/Generic Toon/Mesh/Combined Mesh Data")]
        static void Open() =>
            OpenWindow<MikuToonMeshDataTool>(
                "Miku Toon Mesh Data");
    }

    internal static class MikuToonMeshAssetCreator
    {
        internal static bool HasUv7(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable)
                return false;
            var existing = new List<Vector4>();
            mesh.GetUVs(7, existing);
            return existing.Count != 0;
        }

        internal static Mesh CreateAsset(
            Mesh sourceMesh,
            string outputFolder,
            string outputName,
            bool writeNormals,
            float positionTolerance,
            float smoothingAngle,
            bool includeBoneWeightSignature,
            bool overwriteExistingUv7,
            bool writeColors,
            MikuVertexColorWriteMode colorMode,
            bool mergeR,
            bool mergeG,
            bool mergeB,
            bool mergeA)
        {
            if (sourceMesh == null)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MESH_MISSING");
            if (!sourceMesh.isReadable)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MESH_NOT_READABLE");
            if (!writeNormals && !writeColors)
                throw new InvalidOperationException(
                    "MIKU_TOON_MESH_OPERATION_MISSING");
            var folder = NormalizeFolder(outputFolder);
            EnsureFolder(folder);
            var name = string.IsNullOrWhiteSpace(outputName)
                ? sourceMesh.name + "_MikuToon"
                : outputName.Trim();
            var path = AssetDatabase.GenerateUniqueAssetPath(
                folder + "/" + Sanitize(name) + ".asset");
            var clone = UnityEngine.Object.Instantiate(sourceMesh);
            clone.name = Path.GetFileNameWithoutExtension(path);
            try
            {
                if (writeNormals)
                    MikuToonMeshData.GenerateSmoothNormals(
                        clone,
                        positionTolerance,
                        smoothingAngle,
                        includeBoneWeightSignature,
                        overwriteExistingUv7);
                if (writeColors)
                    MikuToonMeshData.InitializeVertexColors(
                        clone,
                        colorMode,
                        mergeR,
                        mergeG,
                        mergeB,
                        mergeA);
                AssetDatabase.CreateAsset(clone, path);
                AssetDatabase.SaveAssets();
                return clone;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(clone);
                throw;
            }
        }

        static string NormalizeFolder(string value)
        {
            var folder = (value ?? "").Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) ||
                folder.Split('/').Any(part => part == "." || part == ".."))
                throw new InvalidOperationException(
                    "MIKU_TOON_OUTPUT_FOLDER_INVALID");
            return folder;
        }

        static void EnsureFolder(string folder)
        {
            var current = "Assets";
            foreach (var part in folder.Substring(7).Split('/'))
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
            return string.IsNullOrWhiteSpace(value)
                ? "MikuToonMesh"
                : value;
        }
    }

    public static class MikuToonMeshData
    {
        readonly struct PositionKey : IEquatable<PositionKey>
        {
            public PositionKey(
                Vector3 position,
                float tolerance,
                string boneSignature)
            {
                x = Mathf.RoundToInt(position.x / tolerance);
                y = Mathf.RoundToInt(position.y / tolerance);
                z = Mathf.RoundToInt(position.z / tolerance);
                bones = boneSignature ?? "";
            }

            readonly int x;
            readonly int y;
            readonly int z;
            readonly string bones;

            public bool Equals(PositionKey other) =>
                x == other.x && y == other.y && z == other.z &&
                string.Equals(bones, other.bones, StringComparison.Ordinal);

            public override bool Equals(object value) =>
                value is PositionKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = x;
                    hash = hash * 397 ^ y;
                    hash = hash * 397 ^ z;
                    hash = hash * 397 ^ bones.GetHashCode();
                    return hash;
                }
            }
        }

        public static void GenerateSmoothNormals(
            Mesh mesh,
            float positionTolerance,
            float smoothingAngle,
            bool includeBoneWeightSignature,
            bool overwriteExistingUv7)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            if (!mesh.isReadable)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MESH_NOT_READABLE");
            if (float.IsNaN(positionTolerance) ||
                float.IsInfinity(positionTolerance) ||
                positionTolerance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(positionTolerance));
            var existing = new List<Vector3>();
            mesh.GetUVs(7, existing);
            if (existing.Count != 0 && !overwriteExistingUv7)
                throw new InvalidOperationException(
                    "MIKU_TOON_UV7_ALREADY_PRESENT");

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals.Length != vertices.Length)
                throw new InvalidOperationException(
                    "MIKU_TOON_NORMALS_MISSING");
            var areaNormals = new Vector3[vertices.Length];
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                var indices = mesh.GetTriangles(subMesh, true);
                for (var index = 0; index + 2 < indices.Length; index += 3)
                {
                    var a = indices[index];
                    var b = indices[index + 1];
                    var c = indices[index + 2];
                    var weighted = Vector3.Cross(
                        vertices[b] - vertices[a],
                        vertices[c] - vertices[a]);
                    areaNormals[a] += weighted;
                    areaNormals[b] += weighted;
                    areaNormals[c] += weighted;
                }
            }

            var boneWeights = mesh.boneWeights;
            var groups =
                new Dictionary<PositionKey, List<int>>();
            for (var index = 0; index < vertices.Length; index++)
            {
                var signature = includeBoneWeightSignature &&
                                boneWeights.Length == vertices.Length
                    ? BoneSignature(boneWeights[index])
                    : "";
                var key = new PositionKey(
                    vertices[index],
                    positionTolerance,
                    signature);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new List<int>();
                    groups.Add(key, group);
                }
                group.Add(index);
            }

            var result = new Vector3[vertices.Length];
            var threshold = Mathf.Cos(
                Mathf.Clamp(smoothingAngle, 0f, 180f) *
                Mathf.Deg2Rad);
            foreach (var group in groups.Values)
            {
                group.Sort();
                foreach (var index in group)
                {
                    var sum = Vector3.zero;
                    foreach (var candidate in group)
                    {
                        if (Vector3.Dot(
                                normals[index].normalized,
                                normals[candidate].normalized) + 0.000001f <
                            threshold)
                            continue;
                        sum += areaNormals[candidate];
                    }
                    result[index] = sum.sqrMagnitude > 0.0000000001f
                        ? sum.normalized
                        : normals[index].normalized;
                }
            }
            mesh.SetUVs(7, result.ToList());
        }

        public static void InitializeVertexColors(
            Mesh mesh,
            MikuVertexColorWriteMode mode,
            bool mergeR = true,
            bool mergeG = true,
            bool mergeB = true,
            bool mergeA = true)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            if (mode == MikuVertexColorWriteMode.Preserve)
                return;
            var existing = mesh.colors32;
            if (existing.Length != mesh.vertexCount)
                existing = Enumerable.Repeat(
                    new Color32(255, 255, 255, 0),
                    mesh.vertexCount).ToArray();
            var neutral = new Color32(255, 255, 255, 0);
            for (var index = 0; index < existing.Length; index++)
            {
                if (mode == MikuVertexColorWriteMode.Replace)
                {
                    existing[index] = neutral;
                    continue;
                }
                var value = existing[index];
                existing[index] = new Color32(
                    mergeR ? neutral.r : value.r,
                    mergeG ? neutral.g : value.g,
                    mergeB ? neutral.b : value.b,
                    mergeA ? neutral.a : value.a);
            }
            mesh.colors32 = existing;
        }

        static string BoneSignature(BoneWeight weight) =>
            weight.boneIndex0 + ":" +
            weight.weight0.ToString("R", CultureInfo.InvariantCulture) + "|" +
            weight.boneIndex1 + ":" +
            weight.weight1.ToString("R", CultureInfo.InvariantCulture) + "|" +
            weight.boneIndex2 + ":" +
            weight.weight2.ToString("R", CultureInfo.InvariantCulture) + "|" +
            weight.boneIndex3 + ":" +
            weight.weight3.ToString("R", CultureInfo.InvariantCulture);
    }
}
