// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Collections;
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

    /// <summary>Source selected for one output vertex-color channel.</summary>
    public enum MikuVertexColorChannelSource
    {
        Red,
        Green,
        Blue,
        Alpha,
        Zero,
        One,
    }

    public abstract class MikuToonMeshToolWindow : EditorWindow
    {
        [SerializeField] Mesh sourceMesh;
        [SerializeField] string outputFolder = "Assets/Miku/ToonMeshes";
        [SerializeField] string outputName = "";
        [SerializeField, Range(0.000001f, 0.1f)]
        float positionTolerance = MikuToonMeshData.DefaultPositionTolerance;
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
                MikuEditorLocalization.Tr(
                    "Explicit Mesh input only. The source/importer and all " +
                    "Renderer references remain untouched."),
                MessageType.Info);
            sourceMesh = (Mesh)EditorGUILayout.ObjectField(
                MikuEditorLocalization.Tr("Source Mesh"),
                sourceMesh,
                typeof(Mesh),
                false);
            outputFolder = EditorGUILayout.TextField(
                MikuEditorLocalization.Tr("Output Folder"),
                outputFolder);
            outputName = EditorGUILayout.TextField(
                MikuEditorLocalization.Tr("Output Name"),
                outputName);

            var sourceReadable = sourceMesh == null || sourceMesh.isReadable;
            if (!sourceReadable)
                EditorGUILayout.HelpBox(
                    MikuEditorLocalization.Tr(
                        "The imported Mesh is not CPU-readable. Miku will use " +
                        "MeshUtility.AcquireReadOnlyMeshData and write only to " +
                        "the generated clone; importer settings stay unchanged."),
                    MessageType.Info);

            var hasUv7 = sourceMesh != null &&
                         MikuToonMeshAssetCreator.HasUv7(sourceMesh);
            if (ShowsSmoothNormals)
                DrawSmoothNormalSettings(hasUv7);
            if (ShowsVertexColors)
                DrawVertexColorSettings();

            using (new EditorGUI.DisabledScope(sourceMesh == null))
            {
                if (ShowsSmoothNormals && ShowsVertexColors)
                {
                    var preserveUv7 =
                        hasUv7 &&
                        uv7ConflictMode == MikuUv7ConflictMode.Preserve;
                    var label = preserveUv7
                        ? MikuEditorLocalization.Tr(
                            "Create Mesh (Preserve UV7 + Vertex Colors)")
                        : MikuEditorLocalization.Tr("Create Mesh with Both");
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
                                MikuEditorLocalization.Tr(
                                    "Create Mesh with Smooth Normals")) &&
                            ConfirmUv7Replacement(hasUv7))
                            Create(
                                writeNormals: true,
                                writeColors: false);
                    }
                }
                else if (GUILayout.Button(
                             MikuEditorLocalization.Tr(
                                 "Create Mesh with Neutral Vertex Colors")))
                {
                    Create(writeNormals: false, writeColors: true);
                }
            }
        }

        void DrawSmoothNormalSettings(bool hasUv7)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                MikuEditorLocalization.Tr(
                    "Smooth outline normal -> UV7 / TEXCOORD7"),
                EditorStyles.boldLabel);
            positionTolerance = EditorGUILayout.Slider(
                MikuEditorLocalization.Tr("Position Tolerance"),
                positionTolerance,
                0.000001f,
                0.1f);
            smoothingAngle = EditorGUILayout.Slider(
                MikuEditorLocalization.Tr("Smoothing Angle"),
                smoothingAngle,
                0f,
                180f);
            includeBoneWeightSignature = EditorGUILayout.Toggle(
                MikuEditorLocalization.Tr("Respect Bone Weights"),
                includeBoneWeightSignature);
            if (!hasUv7)
                return;

            EditorGUILayout.HelpBox(
                MikuEditorLocalization.Tr(
                    "The source Mesh already contains UV7 data. Preserve leaves " +
                    "that channel unchanged; Replace writes smooth normals only " +
                    "to the generated clone."),
                MessageType.Warning);
            uv7ConflictMode = (MikuUv7ConflictMode)
                EditorGUILayout.EnumPopup(
                    MikuEditorLocalization.Tr("Existing UV7"),
                    uv7ConflictMode);
            if (!ShowsVertexColors &&
                uv7ConflictMode == MikuUv7ConflictMode.Preserve)
                EditorGUILayout.HelpBox(
                    MikuEditorLocalization.Tr(
                        "Select Replace to generate smooth normals. Preserve " +
                        "performs no operation in the normals-only tool."),
                    MessageType.Info);
        }

        void DrawVertexColorSettings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                MikuEditorLocalization.Tr(
                    "Vertex colors - Miku_ToonMask_v1"),
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                MikuEditorLocalization.Tr(
                    "Neutral mask is RGBA (255,255,255,0): SSS, outline width, " +
                    "screen rim, face correction."),
                MessageType.None);
            colorMode = (MikuVertexColorWriteMode)
                EditorGUILayout.EnumPopup(
                    MikuEditorLocalization.Tr("Mode"),
                    colorMode);
            if (colorMode != MikuVertexColorWriteMode.Merge)
                return;
            mergeR = EditorGUILayout.Toggle(
                MikuEditorLocalization.Tr("Write R / SSS"),
                mergeR);
            mergeG = EditorGUILayout.Toggle(
                MikuEditorLocalization.Tr("Write G / Outline"),
                mergeG);
            mergeB = EditorGUILayout.Toggle(
                MikuEditorLocalization.Tr("Write B / Screen Rim"),
                mergeB);
            mergeA = EditorGUILayout.Toggle(
                MikuEditorLocalization.Tr("Write A / Face Correction"),
                mergeA);
        }

        bool ConfirmUv7Replacement(bool hasUv7)
        {
            if (!hasUv7 ||
                uv7ConflictMode != MikuUv7ConflictMode.Replace)
                return true;
            return EditorUtility.DisplayDialog(
                MikuEditorLocalization.Tr("Replace UV7 on generated Mesh?"),
                MikuEditorLocalization.Tr(
                    "The source Mesh remains untouched. UV7 will be replaced only " +
                    "on the newly generated Mesh asset."),
                MikuEditorLocalization.Tr("Replace on Clone"),
                MikuEditorLocalization.Tr("Cancel"));
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
            "Miku/Game Toon/Mesh/Smooth Normal Generator")]
        static void Open() =>
            OpenWindow<MikuSmoothNormalGeneratorWindow>(
                MikuEditorLocalization.Tr("Miku Smooth Normals"));
    }

    public sealed class MikuVertexColorInitializerWindow :
        MikuToonMeshToolWindow
    {
        protected override bool ShowsSmoothNormals => false;
        protected override bool ShowsVertexColors => true;
    }

    public sealed class MikuToonMeshDataTool : MikuToonMeshToolWindow
    {
        protected override bool ShowsSmoothNormals => true;
        protected override bool ShowsVertexColors => true;
    }

    public static class MikuToonMeshAssetCreator
    {
        public static bool HasUv7(Mesh mesh)
        {
            if (mesh == null)
                return false;
            using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
                return data.Length == 1 &&
                    data[0].HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.TexCoord7);
        }

        public static Mesh CreateAsset(
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
            if (!writeNormals && !writeColors)
                throw new InvalidOperationException(
                    "MIKU_TOON_MESH_OPERATION_MISSING");
            var folder = NormalizeFolder(outputFolder);
            var name = string.IsNullOrWhiteSpace(outputName)
                ? sourceMesh.name + "_MikuToon"
                : outputName.Trim();
            var clone = CloneForEditing(sourceMesh);
            clone.name = Sanitize(name);
            var createdFolders = new List<string>();
            string path = null;
            var assetCreated = false;
            try
            {
                if (writeNormals)
                    MikuToonMeshData.GenerateSmoothNormalsFromSource(
                        sourceMesh,
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
                EnsureFolder(folder, createdFolders);
                path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + Sanitize(name) + ".asset");
                clone.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(clone, path);
                assetCreated = true;
                AssetDatabase.SaveAssetIfDirty(clone);
                return clone;
            }
            catch
            {
                if (assetCreated && !string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    clone = null;
                }
                if (clone != null)
                    UnityEngine.Object.DestroyImmediate(clone);
                RollbackFolders(createdFolders);
                throw;
            }
        }

        /// <summary>
        /// Creates a non-destructive toon Mesh clone with UV7 smooth normals
        /// and an explicit per-channel vertex-color mapping.
        /// </summary>
        public static Mesh CreateAsset(
            Mesh sourceMesh,
            string outputFolder,
            string outputName,
            MikuVertexColorChannelSource red,
            MikuVertexColorChannelSource green,
            MikuVertexColorChannelSource blue,
            MikuVertexColorChannelSource alpha,
            float positionTolerance = MikuToonMeshData.DefaultPositionTolerance,
            float smoothingAngle = 60f,
            bool includeBoneWeightSignature = true)
        {
            if (sourceMesh == null)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MESH_MISSING");
            var folder = NormalizeFolder(outputFolder);
            var name = string.IsNullOrWhiteSpace(outputName)
                ? sourceMesh.name + "_MikuToon"
                : outputName.Trim();
            var clone = CloneForEditing(sourceMesh);
            clone.name = Sanitize(name);
            var createdFolders = new List<string>();
            string path = null;
            var assetCreated = false;
            try
            {
                MikuToonMeshData.GenerateSmoothNormalsFromSource(
                    sourceMesh,
                    clone,
                    positionTolerance,
                    smoothingAngle,
                    includeBoneWeightSignature,
                    true);
                MikuToonMeshData.InitializeVertexColors(
                    clone,
                    red,
                    green,
                    blue,
                    alpha);
                EnsureFolder(folder, createdFolders);
                path = AssetDatabase.GenerateUniqueAssetPath(
                    folder + "/" + Sanitize(name) + ".asset");
                clone.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(clone, path);
                assetCreated = true;
                AssetDatabase.SaveAssetIfDirty(clone);
                return clone;
            }
            catch
            {
                if (assetCreated && !string.IsNullOrEmpty(path))
                {
                    AssetDatabase.DeleteAsset(path);
                    clone = null;
                }
                if (clone != null)
                    UnityEngine.Object.DestroyImmediate(clone);
                RollbackFolders(createdFolders);
                throw;
            }
        }

        public static Mesh CreateOrUpdateSmoothNormalAsset(
            Mesh sourceMesh,
            string assetPath,
            float positionTolerance = MikuToonMeshData.DefaultPositionTolerance,
            float smoothingAngle = 60f,
            bool includeBoneWeightSignature = true)
        {
            return CreateOrUpdateSmoothNormalAsset(
                sourceMesh,
                assetPath,
                positionTolerance,
                smoothingAngle,
                includeBoneWeightSignature,
                null);
        }

        /// <summary>
        /// Atomically creates or updates a generated Mesh asset while keeping
        /// the imported source Mesh unchanged.
        /// </summary>
        public static Mesh CreateOrUpdateAsset(
            Mesh sourceMesh,
            string assetPath,
            MikuVertexColorChannelSource red,
            MikuVertexColorChannelSource green,
            MikuVertexColorChannelSource blue,
            MikuVertexColorChannelSource alpha,
            float positionTolerance = MikuToonMeshData.DefaultPositionTolerance,
            float smoothingAngle = 60f,
            bool includeBoneWeightSignature = true)
        {
            if (sourceMesh == null)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MESH_MISSING");
            var normalized = (assetPath ?? "").Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                normalized.Split('/').Any(part => part == "." || part == ".."))
                throw new InvalidOperationException(
                    "MIKU_TOON_OUTPUT_PATH_INVALID");
            var folder = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
                throw new InvalidOperationException(
                    "MIKU_TOON_OUTPUT_PATH_INVALID");
            var clone = CloneForEditing(sourceMesh);
            clone.name = Path.GetFileNameWithoutExtension(normalized);
            var createdFolders = new List<string>();
            Mesh existing = null;
            Mesh backup = null;
            var assetCreated = false;
            try
            {
                MikuToonMeshData.GenerateSmoothNormalsFromSource(
                    sourceMesh,
                    clone,
                    positionTolerance,
                    smoothingAngle,
                    includeBoneWeightSignature,
                    true);
                MikuToonMeshData.InitializeVertexColors(
                    clone,
                    red,
                    green,
                    blue,
                    alpha);
                EnsureFolder(folder, createdFolders);
                existing = AssetDatabase.LoadAssetAtPath<Mesh>(normalized);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(clone, normalized);
                    assetCreated = true;
                    clone = null;
                    existing = AssetDatabase.LoadAssetAtPath<Mesh>(normalized);
                    if (existing == null)
                        throw new InvalidOperationException(
                            "MIKU_TOON_ASSET_CREATE_FAILED:" + normalized);
                }
                else
                {
                    backup = UnityEngine.Object.Instantiate(existing);
                    backup.name = existing.name;
                    EditorUtility.CopySerialized(clone, existing);
                    EditorUtility.SetDirty(existing);
                }
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            catch
            {
                if (assetCreated)
                {
                    AssetDatabase.DeleteAsset(normalized);
                    existing = null;
                }
                else if (existing != null && backup != null)
                {
                    EditorUtility.CopySerialized(backup, existing);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                RollbackFolders(createdFolders);
                throw;
            }
            finally
            {
                if (clone != null)
                    UnityEngine.Object.DestroyImmediate(clone);
                if (backup != null)
                    UnityEngine.Object.DestroyImmediate(backup);
            }
        }

        internal static Mesh CreateOrUpdateSmoothNormalAsset(
            Mesh sourceMesh,
            string assetPath,
            float positionTolerance,
            float smoothingAngle,
            bool includeBoneWeightSignature,
            Action beforeCommit)
        {
            if (sourceMesh == null)
                throw new InvalidOperationException(
                    "MIKU_TOON_SOURCE_MESH_MISSING");
            var normalized = (assetPath ?? "").Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) ||
                !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                normalized.Split('/').Any(part => part == "." || part == ".."))
                throw new InvalidOperationException(
                    "MIKU_TOON_OUTPUT_PATH_INVALID");
            var folder = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
                throw new InvalidOperationException(
                    "MIKU_TOON_OUTPUT_PATH_INVALID");
            var clone = CloneForEditing(sourceMesh);
            clone.name = Path.GetFileNameWithoutExtension(normalized);
            var createdFolders = new List<string>();
            Mesh existing = null;
            Mesh backup = null;
            var assetCreated = false;
            try
            {
                MikuToonMeshData.GenerateSmoothNormalsFromSource(
                    sourceMesh,
                    clone,
                    positionTolerance,
                    smoothingAngle,
                    includeBoneWeightSignature,
                    true);
                EnsureFolder(folder, createdFolders);
                existing = AssetDatabase.LoadAssetAtPath<Mesh>(normalized);
                if (existing == null)
                {
                    AssetDatabase.CreateAsset(clone, normalized);
                    assetCreated = true;
                    clone = null;
                    existing = AssetDatabase.LoadAssetAtPath<Mesh>(normalized);
                    if (existing == null)
                        throw new InvalidOperationException(
                            "MIKU_TOON_ASSET_CREATE_FAILED:" + normalized);
                }
                else
                {
                    backup = UnityEngine.Object.Instantiate(existing);
                    backup.name = existing.name;
                    EditorUtility.CopySerialized(clone, existing);
                    EditorUtility.SetDirty(existing);
                }
                beforeCommit?.Invoke();
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            catch
            {
                if (assetCreated)
                {
                    AssetDatabase.DeleteAsset(normalized);
                    existing = null;
                }
                else if (existing != null && backup != null)
                {
                    EditorUtility.CopySerialized(backup, existing);
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssetIfDirty(existing);
                }
                RollbackFolders(createdFolders);
                throw;
            }
            finally
            {
                if (clone != null)
                    UnityEngine.Object.DestroyImmediate(clone);
                if (backup != null)
                    UnityEngine.Object.DestroyImmediate(backup);
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

        static Mesh CloneForEditing(Mesh sourceMesh)
        {
            if (sourceMesh.isReadable)
                return UnityEngine.Object.Instantiate(sourceMesh);
            if (sourceMesh.blendShapeCount != 0)
                throw new InvalidOperationException(
                    "MIKU_TOON_NON_READABLE_BLEND_SHAPES_UNSUPPORTED");

            using (var readArray =
                   MeshUtility.AcquireReadOnlyMeshData(sourceMesh))
            {
                if (readArray.Length != 1)
                    throw new InvalidOperationException(
                        "MIKU_TOON_MESH_DATA_UNAVAILABLE");
                var source = readArray[0];
                var writableArray = Mesh.AllocateWritableMeshData(1);
                var writableDisposed = false;
                try
                {
                    var destination = writableArray[0];
                    var attributes = sourceMesh.GetVertexAttributes();
                    destination.SetVertexBufferParams(
                        source.vertexCount,
                        attributes);
                    var streamCount = attributes.Length == 0
                        ? 0
                        : attributes.Max(value => value.stream) + 1;
                    for (var stream = 0; stream < streamCount; stream++)
                        destination.GetVertexData<byte>(stream).CopyFrom(
                            source.GetVertexData<byte>(stream));

                    var indexData = source.GetIndexData<byte>();
                    var indexSize = sourceMesh.indexFormat ==
                                    UnityEngine.Rendering.IndexFormat.UInt16
                        ? 2
                        : 4;
                    destination.SetIndexBufferParams(
                        indexData.Length / indexSize,
                        sourceMesh.indexFormat);
                    destination.GetIndexData<byte>().CopyFrom(indexData);
                    destination.subMeshCount = source.subMeshCount;
                    for (var index = 0;
                         index < source.subMeshCount;
                         index++)
                        destination.SetSubMesh(
                            index,
                            source.GetSubMesh(index),
                            UnityEngine.Rendering.MeshUpdateFlags
                                .DontRecalculateBounds |
                            UnityEngine.Rendering.MeshUpdateFlags
                                .DontValidateIndices);

                    var clone = new Mesh
                    {
                        name = sourceMesh.name,
                        indexFormat = sourceMesh.indexFormat,
                    };
                    Mesh.ApplyAndDisposeWritableMeshData(
                        writableArray,
                        clone,
                        UnityEngine.Rendering.MeshUpdateFlags
                            .DontRecalculateBounds |
                        UnityEngine.Rendering.MeshUpdateFlags
                            .DontValidateIndices);
                    writableDisposed = true;
                    clone.bounds = sourceMesh.bounds;
                    clone.bindposes = sourceMesh.bindposes;
                    return clone;
                }
                finally
                {
                    if (!writableDisposed)
                        writableArray.Dispose();
                }
            }
        }

        static void EnsureFolder(
            string folder,
            ICollection<string> createdFolders)
        {
            var current = "Assets";
            foreach (var part in folder.Substring(7).Split('/'))
            {
                var next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (string.IsNullOrEmpty(
                            AssetDatabase.CreateFolder(current, part)))
                        throw new InvalidOperationException(
                            "MIKU_TOON_OUTPUT_FOLDER_CREATE_FAILED:" + next);
                    createdFolders?.Add(next);
                }
                current = next;
            }
        }

        static void RollbackFolders(IEnumerable<string> createdFolders)
        {
            foreach (var folder in (createdFolders ?? Array.Empty<string>())
                         .OrderByDescending(item => item.Length))
                AssetDatabase.DeleteAsset(folder);
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
        /// <summary>
        /// Default seam-position tolerance used by the smooth-normal tool.
        /// </summary>
        public const float DefaultPositionTolerance = 0.000001f;

        const float DirectionLengthSquaredEpsilon = 0.000000000001f;
        const float TangentSpaceV2Marker = 2f;

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
            GenerateSmoothNormalsFromSource(
                mesh,
                mesh,
                positionTolerance,
                smoothingAngle,
                includeBoneWeightSignature,
                overwriteExistingUv7);
        }

        public static void GenerateSmoothNormalsFromSource(
            Mesh sourceMesh,
            Mesh destinationMesh,
            float positionTolerance,
            float smoothingAngle,
            bool includeBoneWeightSignature,
            bool overwriteExistingUv7)
        {
            if (sourceMesh == null)
                throw new ArgumentNullException(nameof(sourceMesh));
            if (destinationMesh == null)
                throw new ArgumentNullException(nameof(destinationMesh));
            if (float.IsNaN(positionTolerance) ||
                float.IsInfinity(positionTolerance) ||
                positionTolerance <= 0f)
                throw new ArgumentOutOfRangeException(
                    nameof(positionTolerance));
            if (MikuToonMeshAssetCreator.HasUv7(sourceMesh) &&
                !overwriteExistingUv7)
                throw new InvalidOperationException(
                    "MIKU_TOON_UV7_ALREADY_PRESENT");

            Vector3[] vertices;
            Vector3[] normals;
            Vector4[] tangents;
            var submeshIndices = new List<int[]>();
            using (var meshDataArray =
                   MeshUtility.AcquireReadOnlyMeshData(sourceMesh))
            {
                if (meshDataArray.Length != 1)
                    throw new InvalidOperationException(
                        "MIKU_TOON_MESH_DATA_UNAVAILABLE");
                var meshData = meshDataArray[0];
                if (!meshData.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.Position))
                    throw MeshDataInvalid("position");
                if (!meshData.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.Normal))
                    throw new InvalidOperationException(
                        "MIKU_TOON_NORMALS_MISSING");
                if (!meshData.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.Tangent))
                    throw TangentsRequired();
                using (var nativeVertices = new NativeArray<Vector3>(
                           meshData.vertexCount,
                           Allocator.Temp))
                using (var nativeNormals = new NativeArray<Vector3>(
                           meshData.vertexCount,
                           Allocator.Temp))
                using (var nativeTangents = new NativeArray<Vector4>(
                           meshData.vertexCount,
                           Allocator.Temp))
                {
                    meshData.GetVertices(nativeVertices);
                    meshData.GetNormals(nativeNormals);
                    meshData.GetTangents(nativeTangents);
                    vertices = nativeVertices.ToArray();
                    normals = nativeNormals.ToArray();
                    tangents = nativeTangents.ToArray();
                }
                for (var subMesh = 0;
                     subMesh < meshData.subMeshCount;
                     subMesh++)
                {
                    var descriptor = meshData.GetSubMesh(subMesh);
                    if (descriptor.topology != MeshTopology.Triangles)
                        throw new InvalidOperationException(
                            "MIKU_TOON_TRIANGLE_TOPOLOGY_REQUIRED");
                    using (var nativeIndices = new NativeArray<int>(
                               checked((int)descriptor.indexCount),
                               Allocator.Temp))
                    {
                        meshData.GetIndices(
                            nativeIndices,
                            subMesh,
                            true);
                        submeshIndices.Add(nativeIndices.ToArray());
                    }
                }
            }
            if (normals.Length != vertices.Length)
                throw new InvalidOperationException(
                    "MIKU_TOON_NORMALS_MISSING");
            if (tangents.Length != vertices.Length)
                throw TangentsRequired();
            if (destinationMesh.vertexCount != vertices.Length)
                throw MeshDataInvalid("destination-vertex-count");

            var sourceNormals = new Vector3[vertices.Length];
            var tangentAxes = new Vector3[vertices.Length];
            var bitangentAxes = new Vector3[vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
            {
                if (!IsFinite(vertices[index]))
                    throw MeshDataInvalid("position");
                if (!TryNormalizeDirection(
                        normals[index],
                        out sourceNormals[index]))
                    throw MeshDataInvalid("normal");
                if (!TryCreateTangentFrame(
                        sourceNormals[index],
                        tangents[index],
                        out tangentAxes[index],
                        out bitangentAxes[index]))
                    throw TangentsRequired();
            }
            foreach (var indices in submeshIndices)
            {
                if (indices.Length % 3 != 0)
                    throw MeshDataInvalid("index");
                foreach (var vertexIndex in indices)
                {
                    if (vertexIndex < 0 || vertexIndex >= vertices.Length)
                        throw MeshDataInvalid("index");
                }
            }

            var areaNormals = new Vector3[vertices.Length];
            foreach (var indices in submeshIndices)
            {
                for (var index = 0; index + 2 < indices.Length; index += 3)
                {
                    var a = indices[index];
                    var b = indices[index + 1];
                    var c = indices[index + 2];
                    var weighted = Vector3.Cross(
                        vertices[b] - vertices[a],
                        vertices[c] - vertices[a]);
                    if (!IsFinite(weighted) ||
                        weighted.sqrMagnitude <=
                        DirectionLengthSquaredEpsilon)
                        continue;
                    areaNormals[a] += AlignHemisphere(
                        weighted,
                        sourceNormals[a]);
                    areaNormals[b] += AlignHemisphere(
                        weighted,
                        sourceNormals[b]);
                    areaNormals[c] += AlignHemisphere(
                        weighted,
                        sourceNormals[c]);
                }
            }

            var boneWeights = sourceMesh.isReadable
                ? sourceMesh.boneWeights
                : Array.Empty<BoneWeight>();
            if (includeBoneWeightSignature &&
                !sourceMesh.isReadable &&
                sourceMesh.bindposes.Length != 0)
                throw new InvalidOperationException(
                    "MIKU_TOON_BONE_WEIGHTS_UNAVAILABLE");
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

            var result = new Vector4[vertices.Length];
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
                        var sourceNormalDot = Vector3.Dot(
                            sourceNormals[index],
                            sourceNormals[candidate]);
                        // A wide smoothing angle may intentionally bridge a
                        // hard fold. Only nearly opposite duplicated surfaces
                        // are isolated so front/back shells never cancel.
                        if (sourceNormalDot <= -0.99f ||
                            sourceNormalDot +
                            DefaultPositionTolerance <
                            threshold)
                            continue;
                        sum += AlignHemisphere(
                            areaNormals[candidate],
                            sourceNormals[index]);
                    }
                    var smoothNormalOS = NormalizeInHemisphere(
                        sum,
                        sourceNormals[index]);
                    result[index] = EncodeTangentSpaceV2(
                        smoothNormalOS,
                        sourceNormals[index],
                        tangentAxes[index],
                        bitangentAxes[index]);
                }
            }
            destinationMesh.SetUVs(7, result.ToList());
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
            if (!mesh.isReadable && mode == MikuVertexColorWriteMode.Merge)
                throw new InvalidOperationException(
                    "MIKU_TOON_VERTEX_COLOR_MERGE_REQUIRES_READABLE_MESH");
            var existing = mesh.isReadable
                ? mesh.colors32
                : Array.Empty<Color32>();
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

        /// <summary>
        /// Remaps each output channel from the mesh's existing vertex color or
        /// from a constant. Call this on a generated clone to keep an imported
        /// FBX immutable. For Furina the contract is One, Alpha, One, Zero.
        /// </summary>
        public static void InitializeVertexColors(
            Mesh mesh,
            MikuVertexColorChannelSource red,
            MikuVertexColorChannelSource green,
            MikuVertexColorChannelSource blue,
            MikuVertexColorChannelSource alpha)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));
            if (!mesh.isReadable)
                throw new InvalidOperationException(
                    "MIKU_TOON_VERTEX_COLOR_MAPPING_REQUIRES_READABLE_MESH");
            var source = mesh.colors32;
            if (source.Length != mesh.vertexCount)
                source = Enumerable.Repeat(
                    new Color32(255, 255, 255, 0),
                    mesh.vertexCount).ToArray();
            var mapped = new Color32[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var value = source[index];
                mapped[index] = new Color32(
                    Select(value, red),
                    Select(value, green),
                    Select(value, blue),
                    Select(value, alpha));
            }
            mesh.colors32 = mapped;
        }

        static byte Select(
            Color32 value,
            MikuVertexColorChannelSource source)
        {
            switch (source)
            {
                case MikuVertexColorChannelSource.Red:
                    return value.r;
                case MikuVertexColorChannelSource.Green:
                    return value.g;
                case MikuVertexColorChannelSource.Blue:
                    return value.b;
                case MikuVertexColorChannelSource.Alpha:
                    return value.a;
                case MikuVertexColorChannelSource.Zero:
                    return 0;
                case MikuVertexColorChannelSource.One:
                    return 255;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }
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

        static InvalidOperationException TangentsRequired() =>
            new InvalidOperationException("MIKU_TOON_TANGENTS_REQUIRED");

        static InvalidOperationException MeshDataInvalid(string semantic) =>
            new InvalidOperationException(
                "MIKU_TOON_MESH_DATA_INVALID:" + semantic);

        static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        static bool IsFinite(Vector4 value) =>
            IsFinite(value.x) && IsFinite(value.y) &&
            IsFinite(value.z) && IsFinite(value.w);

        static bool TryNormalizeDirection(
            Vector3 value,
            out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value))
                return false;
            var componentScale = Mathf.Max(
                Mathf.Max(Mathf.Abs(value.x), Mathf.Abs(value.y)),
                Mathf.Abs(value.z));
            if (componentScale <= 0f)
                return false;
            var scaled = value / componentScale;
            var scaledLength = Mathf.Sqrt(scaled.sqrMagnitude);
            if (!IsFinite(scaledLength) ||
                scaledLength <= 0f ||
                componentScale <=
                DefaultPositionTolerance / scaledLength)
                return false;
            normalized = scaled / scaledLength;
            return IsFinite(normalized) &&
                   Mathf.Abs(normalized.sqrMagnitude - 1f) <=
                   DefaultPositionTolerance;
        }

        static bool TryCreateTangentFrame(
            Vector3 normal,
            Vector4 tangent,
            out Vector3 tangentAxis,
            out Vector3 bitangentAxis)
        {
            tangentAxis = Vector3.zero;
            bitangentAxis = Vector3.zero;
            if (!IsFinite(tangent) ||
                Mathf.Abs(tangent.w) <= DefaultPositionTolerance)
                return false;

            if (!TryNormalizeDirection(
                    new Vector3(tangent.x, tangent.y, tangent.z),
                    out tangentAxis))
                return false;
            tangentAxis -= normal * Vector3.Dot(normal, tangentAxis);
            if (!TryNormalizeDirection(tangentAxis, out tangentAxis))
                return false;
            bitangentAxis = Vector3.Cross(normal, tangentAxis) *
                (tangent.w < 0f ? -1f : 1f);
            if (!TryNormalizeDirection(bitangentAxis, out bitangentAxis))
                return false;
            return true;
        }

        static Vector3 AlignHemisphere(Vector3 value, Vector3 reference) =>
            Vector3.Dot(value, reference) < 0f ? -value : value;

        static Vector3 NormalizeInHemisphere(
            Vector3 value,
            Vector3 fallbackNormal)
        {
            if (!TryNormalizeDirection(value, out value))
                return fallbackNormal;
            value = AlignHemisphere(value, fallbackNormal);
            return IsFinite(value) &&
                   value.sqrMagnitude > DirectionLengthSquaredEpsilon
                ? value
                : fallbackNormal;
        }

        static Vector4 EncodeTangentSpaceV2(
            Vector3 smoothNormalOS,
            Vector3 sourceNormalOS,
            Vector3 tangentAxisOS,
            Vector3 bitangentAxisOS)
        {
            var encoded = new Vector3(
                Vector3.Dot(smoothNormalOS, tangentAxisOS),
                Vector3.Dot(smoothNormalOS, bitangentAxisOS),
                Vector3.Dot(smoothNormalOS, sourceNormalOS));
            if (!TryNormalizeDirection(encoded, out encoded))
                return new Vector4(0f, 0f, 1f, TangentSpaceV2Marker);
            if (encoded.z < 0f)
                encoded = -encoded;
            if (!IsFinite(encoded) ||
                Mathf.Abs(encoded.sqrMagnitude - 1f) >
                DefaultPositionTolerance ||
                encoded.z < -DefaultPositionTolerance)
                return new Vector4(0f, 0f, 1f, TangentSpaceV2Marker);
            return new Vector4(
                encoded.x,
                encoded.y,
                encoded.z,
                TangentSpaceV2Marker);
        }
    }
}
