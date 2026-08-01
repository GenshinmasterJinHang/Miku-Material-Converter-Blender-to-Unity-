using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Miku.ShaderConverter.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Miku.ShaderConverter.Editor
{
    [Serializable]
    public sealed class MikuImportRequest
    {
        public string bundlePath = "";
        public string outputRoot = "Assets/Miku/Generated/MetalLibrary";
        public bool fullRegeneration;
        public bool createMaterialVariant = true;
    }

    [Serializable]
    public sealed class MikuImportResult
    {
        public bool success;
        public string transactionId = "";
        public string receiptPath = "";
        public List<string> assetPaths = new List<string>();
        public List<string> diagnostics = new List<string>();
    }

    /// <summary>
    /// Imports a sealed Miku bundle from verified bytes. Generated assets keep
    /// stable GUIDs and a receipt is committed only after Shader Graph,
    /// textures, material bindings, and the active URP profile are validated.
    /// </summary>
    public static class MikuBundleImporter
    {
        const string ExpectedKindV1 = "miku-bundle-1.0";
        const string LegacyKindV1 = "migr-bundle-1.0";
        const string LegacyKindV2 = "migr-bundle-2.0";
        const string LegacyKindV21 = "migr-bundle-2.1";
        const string LegacyKindV22 = "migr-bundle-2.2";
        const string PackageVersion = "1.0.1";
        const string ExpectedProfileHash = "fe56024961668585c82c96a48a45ea5fe87b8598a6d04b55eb8e608003186eee";
        const string Package101PreviousProfileHash = "a9bd14623ee3dd1247fa1f4915c8f176dbfd2f2034160ae54dcaa09b816c7d1b";
        const string Miku100ProfileHash = "2a31076c3312ebf53c2a801b17b20ea276ebe87880a73fcc1c8125c37f916be6";
        const string Package220ProfileHash = "50bb9fb048707256b3882a757253a3fc685e791395b5bc9872fb7daf98129848";
        const string Package210ProfileHash = "e847380c02ecf8e16e4496a0709b7ccf8946f71b4cc16622f901bcc41768f331";
        const string Package203ProfileHash = "4970ecd6266173f8c60341e10fd26eafe1cbd6d918428aacea5b3e40eef46ff6";
        const string Package202ProfileHash = "4e30b6e4da6d9d1c7a3e2805355ac5354fa751b14e2458c162099cbc2d10b397";
        const string Package200And201ProfileHash = "549551f13909f1c56da9effb58a635eb3e813e9be4c17325211c53abc1ea997c";
        const string Package121ProfileHash = "b08ac3e4506bf127709cef9b42679dbca836615e62eaf2df9b4ca79ff6393f16";
        const string Package120ProfileHash = "72d2487e908af41734e6c6212232f5080b47cab7e09af536c552160b71de628d";
        const string Package111ProfileHash = "2bfabadccf3741871c3ad5db93b8cdf4eece9805944494c5bad95e87a706f67f";
        const string PreviousProfileHash = "b5198d826633a92f5c712cd7337d7f722edd238d3ae1ab42778dd6b780e491b3";
        const string Package110ProfileHash = "e5af9bcb4e02c54e556d8aed0653182d767b841cf3705b46be653dbf8c914b4a";
        const string Exporter110ProfileHash = "a42e43993e27ec18f409b1d574ab2ecc088c93de0a03b2c0ca66f3fbd25b1890";
        const string Version100ProfileHash = "a251a0e02eee217296349135b27974060d4f040cda1c1419423ec410ec844e89";
        const string LegacyPresentationProfileHash = "7793b8dfcd7c766360ca686a48bfd2309731179e87bb5330b5600fbfd893197a";
        const string TemplateSubGraph = "Packages/com.miku.shaderconverter/Templates/MikuStandardTemplate.generated.shadersubgraph";
        const string SharedSubGraphGuid = "4c8adeb0338dff2498fc4a5852e3d131";
        const long MaxArtifactBytes = 256L * 1024L * 1024L;
        const long MaxBundleBytes = 2L * 1024L * 1024L * 1024L;
        const int MaxJsonDepth = 128;
        const long MaxIdentityDocumentBytes = 16L * 1024L * 1024L;
        const int MaxResources = 64;
        const int MaxTextureDimension = 4096;
        const int MaxIdentityDirectories = 10000;
        const int MaxIdentityDocuments = 10000;

        static readonly Regex Sha256Pattern = new Regex("^[0-9a-f]{64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly Regex DrivePattern = new Regex("^[A-Za-z]:", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        static readonly HashSet<string> SourceMeshPbrTextureSemantics =
            new HashSet<string>(
                new[]
                {
                    "BaseColor",
                    "Metalness",
                    "Roughness",
                    "Normal",
                    "Height",
                    "Emission",
                    "EmissionMask",
                    "Alpha",
                    "AmbientOcclusion",
                },
                StringComparer.Ordinal);
        static readonly HashSet<string> SupportedProfileHashes = new HashSet<string>(
            new[]
            {
                ExpectedProfileHash,
                Package101PreviousProfileHash,
                Miku100ProfileHash,
                Package220ProfileHash,
                Package210ProfileHash,
                Package203ProfileHash,
                Package202ProfileHash,
                Package200And201ProfileHash,
                Package121ProfileHash,
                Package120ProfileHash,
                Package111ProfileHash,
                PreviousProfileHash,
                Package110ProfileHash,
                Exporter110ProfileHash,
                Version100ProfileHash,
                LegacyPresentationProfileHash,
            },
            StringComparer.Ordinal);
        static readonly HashSet<string> ReservedNames = new HashSet<string>(
            new[] { "CON", "PRN", "AUX", "NUL" }
                .Concat(Enumerable.Range(1, 9).Select(index => "COM" + index))
                .Concat(Enumerable.Range(1, 9).Select(index => "LPT" + index)),
            StringComparer.OrdinalIgnoreCase);

        sealed class MaterialIdentityLocation
        {
            public string materialRoot = "";
            public string identityPath = "";
            public string fileStem = "";
            public JObject document;
            public bool reusedOutsideOutputRoot;
        }

        [InitializeOnLoadMethod]
        static void ScheduleIncompleteTransactionRecovery()
        {
            EditorApplication.delayCall += RecoverIncompleteTransactions;
        }

        public static MikuImportResult Import(MikuImportRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.bundlePath))
                throw new ArgumentException("bundlePath is required", nameof(request));

            var result = new MikuImportResult();
            string transactionRoot = null;
            string materialRoot = null;
            string backupRoot = null;
            string journalPath = null;
            try
            {
                ValidateRenderPipeline();
                var bundlePath = Path.GetFullPath(request.bundlePath);
                if (!File.Exists(bundlePath))
                    return Fail(result, "MIKU_BUNDLE_MISSING");
                var bundle = ParseJson(bundlePath, "MIKU_BUNDLE_JSON_INVALID");
                ValidateBundleHeader(bundle);
                var bundleProfileHash = RequireSha256(bundle, "targetProfileHash");
                if (string.Equals(
                        bundleProfileHash,
                        LegacyPresentationProfileHash,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        bundleProfileHash,
                        Package111ProfileHash,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        bundleProfileHash,
                        PreviousProfileHash,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        bundleProfileHash,
                        Package110ProfileHash,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        bundleProfileHash,
                        Exporter110ProfileHash,
                        StringComparison.Ordinal) ||
                    string.Equals(
                        bundleProfileHash,
                        Version100ProfileHash,
                        StringComparison.Ordinal))
                {
                    result.diagnostics.Add(
                        "MIKU_TARGET_PROFILE_LEGACY_PRESENTATION_COMPATIBILITY");
                }
                var bundleHash = RequireSha256(bundle, "canonicalHash");
                result.transactionId = Sha256(
                    bundleHash + "\0" + (request.outputRoot ?? "") + "\0" +
                    request.fullRegeneration + "\0" + request.createMaterialVariant).Substring(0, 32);
                transactionRoot = StageVerifiedBundle(bundlePath, bundle, result.transactionId);

                var persistentSourceId = RequireString(bundle, "persistentSourceId");
                var persistentMaterialId = RequireString(bundle, "persistentMaterialId");
                var sourceName = RequireString(bundle, "sourceName");
                var safeName = SanitizeName(sourceName);
                var outputRoot = NormalizeAssetPath(request.outputRoot);
                if (outputRoot == null)
                    return Fail(result, "MIKU_OUTPUT_ROOT_INVALID");
                var location = ResolveMaterialIdentityLocation(
                    outputRoot,
                    safeName,
                    persistentSourceId,
                    persistentMaterialId);
                materialRoot = location.materialRoot;
                var absoluteMaterialRoot = ToAbsoluteProjectPath(materialRoot);
                var recoveryRoot = Path.Combine(ProjectRoot, "Library", "Miku", "Transactions", result.transactionId);
                backupRoot = Path.Combine(recoveryRoot, "backup");
                journalPath = Path.Combine(recoveryRoot, "transaction.json");
                if (location.reusedOutsideOutputRoot)
                {
                    AddDiagnosticOnce(
                        result.diagnostics,
                        "MIKU_OUTPUT_IDENTITY_REUSED_OUTSIDE_OUTPUT_ROOT:" +
                        materialRoot);
                }

                var ir = LoadStagedDocument(
                    transactionRoot,
                    bundle["ir"] as JObject,
                    "miku-material-ir-1.0");
                var hasSurfaceModelPlan =
                    MikuSurfaceModelBackends.IsMaterialIr2(ir);
                if (hasSurfaceModelPlan &&
                    (
                        string.Equals(
                            bundleProfileHash,
                            Package203ProfileHash,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            bundleProfileHash,
                            Package202ProfileHash,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            bundleProfileHash,
                            Package200And201ProfileHash,
                            StringComparison.Ordinal)
                    ))
                {
                    if (string.Equals(
                            bundleProfileHash,
                            Package200And201ProfileHash,
                            StringComparison.Ordinal) &&
                        MikuSurfaceModelBackends.RequiresClearCoat(ir))
                        throw new InvalidDataException(
                            "MIKU_COAT_PROFILE_REEXPORT_REQUIRED_2_0_2");
                    AddDiagnosticOnce(
                        result.diagnostics,
                        "MIKU_TARGET_PROFILE_2_0_X_COMPATIBILITY");
                }
                if (!hasSurfaceModelPlan &&
                    ir["surfaceContract"] != null &&
                    !string.Equals(
                        bundleProfileHash,
                        Package121ProfileHash,
                        StringComparison.Ordinal))
                {
                    if (string.Equals(
                            bundleProfileHash,
                            Package120ProfileHash,
                            StringComparison.Ordinal))
                    {
                        var surfaceModel =
                            ir["surfaceContract"]?["model"]?.Value<string>() ??
                            "";
                        if (string.Equals(
                                surfaceModel,
                                "DielectricScreenRefraction",
                                StringComparison.Ordinal))
                            throw new InvalidDataException(
                                "MIKU_DIELECTRIC_REEXPORT_REQUIRED_1_2_1");
                        AddDiagnosticOnce(
                            result.diagnostics,
                            "MIKU_TARGET_PROFILE_1_2_0_SURFACE_COMPATIBILITY");
                    }
                    else
                    {
                        throw new InvalidDataException(
                            "MIKU_SURFACE_CONTRACT_PROFILE_UNSUPPORTED:" +
                            bundleProfileHash);
                    }
                }
                var manifest = LoadStagedDocument(
                    transactionRoot,
                    bundle["manifest"] as JObject,
                    "miku-conversion-manifest-1.0");
                ValidateManifest(manifest, bundle);
                if (HasLegacyZeroNormalChannel(ir))
                {
                    AddDiagnosticOnce(
                        result.diagnostics,
                        "MIKU_LEGACY_ZERO_NORMAL_NORMALIZED");
                }
                var workflowBackend = MikuWorkflowBackends.Resolve(ir);
                var createUserMaterialVariant =
                    request.createMaterialVariant ||
                    string.Equals(
                        workflowBackend.Kind,
                        "generic_toon",
                        StringComparison.Ordinal);
                var surfaceGenerator =
                    hasSurfaceModelPlan &&
                    string.Equals(
                        workflowBackend.Kind,
                        "standard_pbr",
                        StringComparison.Ordinal)
                        ? MikuSurfaceModelBackends.Resolve(ir)
                        : null;
                var wrapperContract = surfaceGenerator != null
                    ? surfaceGenerator.WrapperContract(ir)
                    : ir["surfaceContract"] as JObject;
                AddSurfaceProjectSetupDiagnostics(ir, result.diagnostics);

                var graphGuid = StableAssetGuid(persistentSourceId, persistentMaterialId, "WrapperGraph");
                var subGraphGuid = StableAssetGuid(persistentSourceId, persistentMaterialId, "GeneratedSubGraph");
                var baseMaterialGuid = StableAssetGuid(persistentSourceId, persistentMaterialId, "GeneratedBaseMaterial");
                var materialVariantGuid = StableAssetGuid(persistentSourceId, persistentMaterialId, "UserMaterialVariant");
                var toonRecipeGuid = StableAssetGuid(
                    persistentSourceId,
                    persistentMaterialId,
                    "ToonMaterialRecipe");
                var outputStem = string.IsNullOrEmpty(location.fileStem)
                    ? safeName
                    : location.fileStem;
                var graphPath = RecordedAssetPath(
                    location,
                    "WrapperGraph",
                    materialRoot + "/" + outputStem + ".shadergraph",
                    graphGuid,
                    ".shadergraph");
                var baseMaterialPath = RecordedAssetPath(
                    location,
                    "GeneratedBaseMaterial",
                    materialRoot + "/" + outputStem + ".generated.mat",
                    baseMaterialGuid,
                    ".mat");
                var materialVariantPath = RecordedAssetPath(
                    location,
                    "UserMaterialVariant",
                    materialRoot + "/" + outputStem + ".mat",
                    materialVariantGuid,
                    ".mat");
                var toonRecipePath = RecordedAssetPath(
                    location,
                    "ToonMaterialRecipe",
                    materialRoot + "/" + outputStem + ".toon-recipe.asset",
                    toonRecipeGuid,
                    ".asset");
                var manifestPath = materialRoot + "/" + outputStem + ".miku-manifest.json";
                var identityPath = string.IsNullOrEmpty(location.identityPath)
                    ? materialRoot + "/" + outputStem + ".miku-assets.json"
                    : location.identityPath;
                var receiptPath = materialRoot + "/" + outputStem + ".miku-unity-receipt.json";
                // Shader Graph derives HLSL function identifiers from the Sub Graph
                // file name. A Unicode material name (for example, 金10) therefore
                // produces invalid HLSL even though the asset itself imports. Keep
                // the user-facing wrapper/material names, but give the generated
                // Sub Graph a deterministic ASCII-only file name.
                var defaultSubGraphPath = materialRoot + "/miku_" +
                                          subGraphGuid.Substring(0, 20) +
                                          ".generated.shadersubgraph";
                var recordedSubGraphPath = RecordedAssetPath(
                    location,
                    "GeneratedSubGraph",
                    defaultSubGraphPath,
                    subGraphGuid,
                    ".shadersubgraph");
                var subGraphPath = Path.GetFileName(recordedSubGraphPath)
                    .StartsWith("miku_", StringComparison.Ordinal)
                    ? recordedSubGraphPath
                    : defaultSubGraphPath;
                var legacyNamedSubGraphPath =
                    materialRoot + "/" + outputStem + ".generated.shadersubgraph";
                if (workflowBackend.UsesEditableGraph)
                {
                    ValidateStableGuidOwnership(
                        "WrapperGraph",
                        graphPath,
                        graphGuid);
                    ValidateStableGuidOwnership(
                        "GeneratedSubGraph",
                        subGraphPath,
                        subGraphGuid,
                        request.fullRegeneration
                            ? recordedSubGraphPath
                            : null);
                }
                if (createUserMaterialVariant)
                {
                    ValidateStableGuidOwnership(
                        "GeneratedBaseMaterial",
                        baseMaterialPath,
                        baseMaterialGuid);
                    ValidateStableGuidOwnership(
                        "UserMaterialVariant",
                        materialVariantPath,
                        materialVariantGuid);
                    if (string.Equals(
                            workflowBackend.Kind,
                            "generic_toon",
                            StringComparison.Ordinal))
                        ValidateStableGuidOwnership(
                            "ToonMaterialRecipe",
                            toonRecipePath,
                            toonRecipeGuid);
                }
                PreflightResourceGuidOwnership(
                    bundle["resources"] as JArray,
                    location,
                    materialRoot,
                    persistentSourceId,
                    persistentMaterialId);
                BeginTransaction(
                    journalPath,
                    materialRoot,
                    absoluteMaterialRoot,
                    backupRoot,
                    bundleHash);
                Directory.CreateDirectory(absoluteMaterialRoot);
                if (workflowBackend.UsesEditableGraph)
                {
                    if (request.fullRegeneration)
                    {
                        if (File.Exists(ToAbsoluteProjectPath(subGraphPath)) &&
                            !string.Equals(
                                AssetDatabase.AssetPathToGUID(subGraphPath),
                                subGraphGuid,
                                StringComparison.Ordinal))
                        {
                            AssetDatabase.DeleteAsset(subGraphPath);
                        }
                        if (!string.Equals(
                                legacyNamedSubGraphPath,
                                subGraphPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(ToAbsoluteProjectPath(legacyNamedSubGraphPath)) &&
                            !File.Exists(ToAbsoluteProjectPath(subGraphPath)))
                        {
                            var moveError = AssetDatabase.MoveAsset(
                                legacyNamedSubGraphPath,
                                subGraphPath);
                            if (!string.IsNullOrEmpty(moveError))
                                throw new InvalidDataException(
                                    "MIKU_SUBGRAPH_ASCII_RENAME_FAILED:" + moveError);
                        }
                    }
                    var hasRuntimeExpressions =
                        MikuShaderGraph17RuntimeBackend.HasRuntimeExpressions(ir);
                    var usesStructuredGraph =
                        surfaceGenerator != null ||
                        hasRuntimeExpressions ||
                        ir["surfaceContract"] != null;
                    string generatedSubGraph = null;
                    if (usesStructuredGraph)
                    {
                        generatedSubGraph =
                            surfaceGenerator != null
                                ? surfaceGenerator.GenerateSubGraph(
                                    ir,
                                    persistentMaterialId)
                                : MikuShaderGraph17RuntimeBackend.Generate(
                                    ir,
                                    persistentMaterialId);
                        MikuAtomicAssetWriter.WriteIfChanged(
                            ToAbsoluteProjectPath(subGraphPath),
                            generatedSubGraph);
                        EnsureMetaGuid(subGraphPath, subGraphGuid, false);
                        result.diagnostics.Add(
                            surfaceGenerator != null
                                ? "MIKU_SURFACE_MODEL_PRESERVED:" +
                                  surfaceGenerator.Kind
                                : hasRuntimeExpressions
                                ? "MIKU_RUNTIME_INPUT_PRESERVED"
                                : "MIKU_SURFACE_CONTRACT_PRESERVED");
                    }
                    else
                    {
                        CopyTemplateAsset(
                            TemplateSubGraph,
                            subGraphPath,
                            subGraphGuid,
                            true);
                    }
                    SynchronizeGeneratedSubGraph(
                        subGraphPath,
                        subGraphGuid);
                    var graphExists = File.Exists(ToAbsoluteProjectPath(graphPath));
                    var writeWrapper = request.fullRegeneration || !graphExists;
                    if (!writeWrapper &&
                    string.Equals(
                        workflowBackend.Kind,
                        "standard_pbr",
                        StringComparison.Ordinal))
                {
                    var surface = wrapperContract;
                    var renderContractMatches = surface != null &&
                        MikuShaderGraph17RuntimeBackend
                            .WrapperRenderContractMatches(
                                File.ReadAllText(
                                    ToAbsoluteProjectPath(graphPath),
                                    Encoding.UTF8),
                                surface);
                    if (renderContractMatches ||
                        surface == null &&
                        WrapperMatchesTemplate(
                            graphPath,
                            workflowBackend.WrapperTemplatePath,
                            subGraphGuid))
                    {
                        // Already current.
                    }
                    else if (wrapperContract != null ||
                             MatchesAnyKnownWrapperTemplate(
                                 graphPath,
                                 subGraphGuid))
                    {
                        result.diagnostics.Add(
                            "MIKU_WRAPPER_RENDER_CONTRACT_MISMATCH:" +
                            "use Full Regeneration after reviewing wrapper edits");
                    }
                    else
                    {
                        result.diagnostics.Add(
                            "MIKU_WRAPPER_PRESENTATION_MIKUATION_REQUIRED");
                    }
                }
                    if (usesStructuredGraph && writeWrapper)
                {
                    var wrapperTemplatePath =
                        ToAbsoluteProjectPath(
                            workflowBackend.WrapperTemplatePath);
                    if (!File.Exists(wrapperTemplatePath))
                        throw new FileNotFoundException(
                            "MIKU_TEMPLATE_MISSING",
                            wrapperTemplatePath);
                    var runtimeWrapper =
                        MikuShaderGraph17RuntimeBackend.GenerateWrapper(
                            File.ReadAllText(
                                wrapperTemplatePath,
                                Encoding.UTF8),
                            generatedSubGraph,
                            persistentMaterialId,
                            subGraphGuid,
                            wrapperContract);
                    runtimeWrapper = ApplyRuntimeWrapperVertexContract(
                        runtimeWrapper,
                        ir);
                    runtimeWrapper =
                        MikuShaderGraph17RuntimeBackend.ApplyWrapperContract(
                            runtimeWrapper,
                            wrapperContract);
                    MikuAtomicAssetWriter.WriteIfChanged(
                        ToAbsoluteProjectPath(graphPath),
                        runtimeWrapper);
                    EnsureMetaGuid(graphPath, graphGuid, false);
                }
                    else
                    {
                    CopyTemplateAsset(
                        workflowBackend.WrapperTemplatePath,
                        graphPath,
                        graphGuid,
                        writeWrapper,
                        subGraphGuid);
                    if (writeWrapper && wrapperContract is JObject surface)
                    {
                        var wrapperAbsolute =
                            ToAbsoluteProjectPath(graphPath);
                        MikuAtomicAssetWriter.WriteIfChanged(
                            wrapperAbsolute,
                            MikuShaderGraph17RuntimeBackend
                                .ApplyWrapperContract(
                                    File.ReadAllText(
                                        wrapperAbsolute,
                                        Encoding.UTF8),
                                    surface));
                    }
                    if (usesStructuredGraph && !writeWrapper)
                    {
                        var wrapperText = File.ReadAllText(
                            ToAbsoluteProjectPath(graphPath),
                            Encoding.UTF8);
                        var missingRuntimeProperties =
                            MikuShaderGraph17RuntimeBackend
                                .RuntimePropertyReferences(
                                    generatedSubGraph)
                                .Where(reference =>
                                    !wrapperText.Contains(
                                        reference,
                                        StringComparison.Ordinal))
                                .ToArray();
                        if (missingRuntimeProperties.Length > 0)
                        {
                            result.diagnostics.Add(
                                "MIKU_RUNTIME_WRAPPER_PROPERTIES_MISSING:" +
                                string.Join(
                                    ",",
                                    missingRuntimeProperties) +
                                ":use Full Regeneration after reviewing " +
                                "wrapper edits");
                        }
                    }
                    }
                }
                else
                {
                    result.diagnostics.Add(
                        "MIKU_STATIC_TOON_BACKEND:GenericOpaque");
                }
                CopyStagedReference(transactionRoot, bundle["manifest"] as JObject, manifestPath);

                var textures = ImportResources(
                    transactionRoot,
                    bundle["resources"] as JArray,
                    location,
                    materialRoot,
                    persistentSourceId,
                    persistentMaterialId,
                    result);
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                Shader editableGraphShader = null;
                if (workflowBackend.UsesEditableGraph)
                {
                    AssetDatabase.ImportAsset(
                        subGraphPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    AssetDatabase.ImportAsset(
                        graphPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    editableGraphShader =
                        AssetDatabase.LoadAssetAtPath<Shader>(graphPath);
                    ValidateShader(editableGraphShader, graphPath);
                }
                var shader = workflowBackend.ResolveShader(ir, editableGraphShader);
                ValidateShader(shader, workflowBackend.UsesEditableGraph ? graphPath : shader.name);

                if (createUserMaterialVariant)
                {
                    var baseMaterial = CreateOrUpdateMaterial(
                        baseMaterialPath,
                        baseMaterialGuid,
                        shader,
                        outputStem + ".generated",
                        wrapperContract);
                    BindMaterial(
                        baseMaterial,
                        ir,
                        textures,
                        workflowBackend.Kind,
                        workflowBackend.UsesEditableGraph,
                        result.diagnostics);
                    MikuManualTextureKeywordUtility.SyncKeywords(baseMaterial);
                    EditorUtility.SetDirty(baseMaterial);
                    AssetDatabase.SaveAssetIfDirty(baseMaterial);
                    AssetDatabase.ImportAsset(
                        baseMaterialPath,
                        ImportAssetOptions.ForceSynchronousImport);
                    ValidateMaterial(
                        baseMaterialPath,
                        shader,
                        textures,
                        workflowBackend.Kind,
                        ir);
                    var userMaterial = GetOrCreateUserMaterialVariant(
                        materialVariantPath,
                        materialVariantGuid,
                        baseMaterial,
                        sourceName);
                    ValidateMaterialVariant(materialVariantPath, baseMaterial);
                    result.assetPaths.Add(baseMaterialPath);
                    result.assetPaths.Add(materialVariantPath);
                    if (string.Equals(
                            workflowBackend.Kind,
                            "generic_toon",
                            StringComparison.Ordinal))
                    {
                        var recipeExisted =
                            File.Exists(
                                ToAbsoluteProjectPath(toonRecipePath));
                        MikuToonRecipeUtility.CreateOrUpdateImported(
                            toonRecipePath,
                            baseMaterial,
                            userMaterial);
                        EnsureMetaGuid(
                            toonRecipePath,
                            toonRecipeGuid,
                            !recipeExisted);
                        AssetDatabase.ImportAsset(
                            toonRecipePath,
                            ImportAssetOptions.ForceSynchronousImport);
                        result.assetPaths.Add(toonRecipePath);
                    }
                }
                var sourceMeshResource = (
                    bundle["resources"] as JArray ?? new JArray())
                    .OfType<JObject>()
                    .SingleOrDefault(item => string.Equals(
                        item["semantic"]?.Value<string>(),
                        "SourceMesh",
                        StringComparison.Ordinal));
                if (sourceMeshResource != null)
                    AddDiagnosticOnce(
                        result.diagnostics,
                        "MIKU_SOURCE_MESH_IGNORED_EXPLICIT_TOOL_REQUIRED");

                var identity = BuildIdentityDocument(
                    persistentSourceId,
                    persistentMaterialId,
                    workflowBackend.UsesEditableGraph ? graphPath : "",
                    graphGuid,
                    workflowBackend.UsesEditableGraph ? subGraphPath : "",
                    subGraphGuid,
                    createUserMaterialVariant ? baseMaterialPath : "",
                    createUserMaterialVariant ? AssetDatabase.AssetPathToGUID(baseMaterialPath) : "",
                    createUserMaterialVariant ? materialVariantPath : "",
                    createUserMaterialVariant ? AssetDatabase.AssetPathToGUID(materialVariantPath) : "",
                    string.Equals(
                        workflowBackend.Kind,
                        "generic_toon",
                        StringComparison.Ordinal)
                        ? toonRecipePath
                        : "",
                    string.Equals(
                        workflowBackend.Kind,
                        "generic_toon",
                        StringComparison.Ordinal)
                        ? AssetDatabase.AssetPathToGUID(toonRecipePath)
                        : "",
                    textures);
                MikuAtomicAssetWriter.WriteIfChanged(ToAbsoluteProjectPath(identityPath), identity.ToString(Formatting.Indented) + "\n");
                if (workflowBackend.UsesEditableGraph)
                    result.assetPaths.AddRange(new[] { graphPath, subGraphPath });
                result.assetPaths.AddRange(new[] { manifestPath, identityPath });

                var receipt = BuildReceipt(
                    bundle,
                    result,
                    sourceName,
                    baseMaterialPath,
                    materialVariantPath,
                    workflowBackend.UsesEditableGraph ? graphPath : "",
                    workflowBackend.UsesEditableGraph ? subGraphPath : "",
                    shader,
                    textures,
                    createUserMaterialVariant,
                    string.Equals(
                        workflowBackend.Kind,
                        "generic_toon",
                        StringComparison.Ordinal)
                        ? toonRecipePath
                        : "",
                    workflowBackend.Kind);
                MikuAtomicAssetWriter.WriteIfChanged(ToAbsoluteProjectPath(receiptPath), receipt.ToString(Formatting.Indented) + "\n");
                result.receiptPath = receiptPath;
                result.assetPaths.Add(receiptPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                CompleteTransaction(journalPath, receipt["canonicalHash"]?.Value<string>() ?? "");
                result.success = true;
                return result;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(journalPath))
                {
                    try
                    {
                        RollBackTransaction(journalPath, materialRoot, backupRoot);
                    }
                    catch (Exception rollbackError)
                    {
                        result.diagnostics.Add("MIKU_ROLLBACK_FAILED:" + rollbackError.Message);
                    }
                }
                var root = RootCause(ex);
                return Fail(result, "MIKU_IMPORT_FAILED:" + root.Message);
            }
        }

        static void ValidateBundleHeader(JObject bundle)
        {
            var kind = bundle["documentKind"]?.Value<string>() ?? "";
            var version = bundle["schemaVersion"]?.Value<string>() ?? "";
            var supported =
                string.Equals(
                    kind,
                    ExpectedKindV1,
                    StringComparison.Ordinal) &&
                string.Equals(version, "1.0", StringComparison.Ordinal) ||
                string.Equals(
                    kind,
                    LegacyKindV1,
                    StringComparison.Ordinal) &&
                string.Equals(version, "1.0", StringComparison.Ordinal) ||
                string.Equals(
                    kind,
                    LegacyKindV2,
                    StringComparison.Ordinal) &&
                string.Equals(version, "2.0", StringComparison.Ordinal) ||
                string.Equals(
                    kind,
                    LegacyKindV21,
                    StringComparison.Ordinal) &&
                string.Equals(version, "2.1", StringComparison.Ordinal) ||
                string.Equals(
                    kind,
                    LegacyKindV22,
                    StringComparison.Ordinal) &&
                string.Equals(version, "2.2", StringComparison.Ordinal);
            if (!supported)
                throw new InvalidDataException("MIKU_UNKNOWN_SCHEMA");
            ValidateCanonicalHash(bundle);
            var profile = RequireSha256(bundle, "targetProfileHash");
            if (!SupportedProfileHashes.Contains(profile))
                throw new InvalidDataException(
                    "MIKU_TARGET_PROFILE_MISMATCH:" +
                    "bundle=" + profile +
                    ";package=" + PackageVersion +
                    ";action=re-export with Miku " + PackageVersion +
                    " or install a package that explicitly supports this hash");
            RequireString(bundle, "persistentSourceId");
            RequireString(bundle, "persistentMaterialId");
            var resources = bundle["resources"] as JArray ?? throw new InvalidDataException("MIKU_RESOURCE_LIST_INVALID");
            if (resources.Count > MaxResources)
                throw new InvalidDataException("MIKU_RESOURCE_LIMIT");
            if (!string.Equals(kind, ExpectedKindV1, StringComparison.Ordinal) &&
                !string.Equals(kind, LegacyKindV22, StringComparison.Ordinal) &&
                resources.OfType<JObject>().Any(resource =>
                    string.Equals(
                        resource["mediaType"]?.Value<string>(),
                        "image/jpeg",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        resource["semantic"]?.Value<string>(),
                        "Height",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        resource["semantic"]?.Value<string>(),
                        "EmissionMask",
                        StringComparison.Ordinal) ||
                    resource["channelBindings"] != null ||
                    string.Equals(
                        resource["normalConvention"]?.Value<string>(),
                        "TangentDirectXNegativeY",
                        StringComparison.Ordinal)))
                throw new InvalidDataException(
                    "MIKU_RESOURCE_REQUIRES_BUNDLE_2_2");
            foreach (var resource in resources.OfType<JObject>())
            {
                ValidateResourceChannelBindings(resource);
                var normalUsage =
                    string.Equals(
                        resource["semantic"]?.Value<string>(),
                        "Normal",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        resource["usage"]?.Value<string>(),
                        "Normal",
                        StringComparison.Ordinal);
                if (!normalUsage)
                    continue;
                var convention =
                    resource["normalConvention"]?.Value<string>() ?? "";
                if (!string.Equals(
                        convention,
                        "TangentOpenGLPositiveY",
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        convention,
                        "TangentDirectXNegativeY",
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "MIKU_NORMAL_CONVENTION_INVALID");
            }
            ValidateMeshBoundResources(kind, resources);
            var references = new List<JObject>();
            foreach (var key in new[] { "ir", "plan", "manifest", "sourceMap" })
                references.Add(RequireReference(bundle[key] as JObject, "application/json"));
            references.AddRange(resources.Select(item => RequireReference(item as JObject, null)));
            var normalized = references.Select(item => NormalizeRelativePath(RequireString(item, "relativePath"))).ToArray();
            if (normalized.Select(item => item.Normalize(NormalizationForm.FormC).ToUpperInvariant()).Distinct(StringComparer.Ordinal).Count() != normalized.Length)
                throw new InvalidDataException("MIKU_ARTIFACT_PATH_DUPLICATE");
            long total = 0;
            foreach (var reference in references)
            {
                checked { total += reference["byteLength"]?.Value<long>() ?? -1; }
                if (total > MaxBundleBytes)
                    throw new InvalidDataException("MIKU_BUNDLE_SIZE_LIMIT");
            }
            var expectedSeal = ComputeSealedDigest(bundle);
            if (!string.Equals(RequireSha256(bundle, "sealedDigest"), expectedSeal, StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_BUNDLE_SEAL_MISMATCH");
        }

        static void ValidateMeshBoundResources(
            string bundleKind,
            JArray resources)
        {
            var sourceMeshes = resources
                .OfType<JObject>()
                .Where(item => string.Equals(
                    item["semantic"]?.Value<string>(),
                    "SourceMesh",
                    StringComparison.Ordinal))
                .ToArray();
            var meshBoundTextures = resources
                .OfType<JObject>()
                .Where(item =>
                    !string.Equals(
                        item["semantic"]?.Value<string>(),
                        "SourceMesh",
                        StringComparison.Ordinal) &&
                    item["meshBinding"] is JObject)
                .ToArray();
            if (string.Equals(
                    bundleKind,
                    LegacyKindV2,
                    StringComparison.Ordinal) &&
                meshBoundTextures.Length > 0)
                throw new InvalidDataException(
                    "MIKU_LEGACY_MESH_BOUND_BUNDLE_UNSAFE");
            var isV21 = string.Equals(
                bundleKind,
                LegacyKindV21,
                StringComparison.Ordinal);
            var isLatest =
                string.Equals(
                    bundleKind,
                    ExpectedKindV1,
                    StringComparison.Ordinal) ||
                string.Equals(
                    bundleKind,
                    LegacyKindV22,
                    StringComparison.Ordinal);
            if (!isV21 && !isLatest)
            {
                if (sourceMeshes.Length > 0)
                    throw new InvalidDataException(
                        "MIKU_SOURCE_MESH_RESOURCE_INVALID");
                return;
            }
            if (isV21 && sourceMeshes.Length != 1)
                throw new InvalidDataException(
                    "MIKU_SOURCE_MESH_RESOURCE_INVALID");
            if (isLatest &&
                (sourceMeshes.Length > 1 ||
                 meshBoundTextures.Length > 0 && sourceMeshes.Length != 1))
                throw new InvalidDataException(
                    "MIKU_SOURCE_MESH_RESOURCE_INVALID");
            if (sourceMeshes.Length == 0)
                return;
            var sourceMesh = sourceMeshes[0];
            if (!string.Equals(
                    RequireString(sourceMesh, "mediaType"),
                    "model/gltf-binary",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    RequireString(sourceMesh, "kind"),
                    "SourceMesh",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_SOURCE_MESH_RESOURCE_INVALID");
            if ((sourceMesh["meshCount"]?.Value<int>() ?? 0) <= 0 ||
                (sourceMesh["vertexCount"]?.Value<int>() ?? 0) <= 0 ||
                (sourceMesh["indexCount"]?.Value<int>() ?? 0) <= 0 ||
                sourceMesh["hasUv0"]?.Value<bool>() != true)
                throw new InvalidDataException(
                    "MIKU_MESH_BINDING_MISMATCH");
            var sourceBinding = sourceMesh["meshBinding"] as JObject
                ?? throw new InvalidDataException(
                    "MIKU_MESH_BINDING_MISMATCH");
            var bindingHash = RequireSha256(sourceBinding, "sha256");
            var rendererBindings = sourceMesh["rendererBindings"] as JArray;
            if (rendererBindings == null || rendererBindings.Count == 0)
                throw new InvalidDataException(
                    "MIKU_MESH_BINDING_MISMATCH");
            foreach (var binding in rendererBindings.OfType<JObject>())
            {
                RequireString(binding, "rendererPath");
                RequireString(binding, "sourceObject");
                RequireSha256(binding, "meshFingerprint");
                if ((binding["exportedVertices"]?.Value<int>() ?? 0) <= 0 ||
                    (binding["exportedIndices"]?.Value<int>() ?? 0) <= 0 ||
                    binding["hasUv0"]?.Value<bool>() != true ||
                    !(binding["materialSlots"] is JArray))
                    throw new InvalidDataException(
                        "MIKU_MESH_BINDING_MISMATCH");
            }
            foreach (var texture in meshBoundTextures)
            {
                var textureBinding = texture["meshBinding"] as JObject;
                if (textureBinding == null ||
                    !string.Equals(
                        RequireSha256(textureBinding, "sha256"),
                        bindingHash,
                        StringComparison.Ordinal))
                    throw new InvalidDataException(
                        "MIKU_MESH_BINDING_MISMATCH");
            }
        }

        static string StageVerifiedBundle(string bundlePath, JObject bundle, string transactionId)
        {
            var sourceRoot = Path.GetDirectoryName(bundlePath) ?? throw new InvalidDataException("MIKU_BUNDLE_ROOT_INVALID");
            var root = Path.Combine(ProjectRoot, "Library", "Miku", "Staging");
            Directory.CreateDirectory(root);
            var transactionRoot = Path.GetFullPath(Path.Combine(root, transactionId));
            RequireInsideRoot(root, transactionRoot);
            if (Directory.Exists(transactionRoot))
                Directory.Delete(transactionRoot, true);
            Directory.CreateDirectory(transactionRoot);
            var references = new List<JObject>();
            foreach (var key in new[] { "ir", "plan", "manifest", "sourceMap" })
                references.Add(bundle[key] as JObject);
            references.AddRange((bundle["resources"] as JArray ?? new JArray()).OfType<JObject>());
            foreach (var reference in references)
            {
                var relative = NormalizeRelativePath(RequireString(reference, "relativePath"));
                var source = RequireSecureFile(sourceRoot, relative);
                var destination = Path.GetFullPath(Path.Combine(transactionRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
                RequireInsideRoot(transactionRoot, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                CopyAndVerify(source, destination, RequireSha256(reference, "sha256"), reference["byteLength"]?.Value<long>() ?? -1);
            }
            return transactionRoot;
        }

        static JObject LoadStagedDocument(string transactionRoot, JObject reference, string expectedKind)
        {
            RequireReference(reference, "application/json");
            var path = Path.Combine(transactionRoot, NormalizeRelativePath(RequireString(reference, "relativePath")).Replace('/', Path.DirectorySeparatorChar));
            var document = ParseJson(path, "MIKU_DOCUMENT_JSON_INVALID");
            var actualKind =
                document["documentKind"]?.Value<string>() ?? "";
            var legacyPrefix = expectedKind
                .Substring(0, expectedKind.LastIndexOf('-', expectedKind.Length - 2))
                .Replace("miku-", "migr-") + "-";
            var legacyVersion = actualKind.StartsWith(
                legacyPrefix,
                StringComparison.Ordinal)
                ? actualKind.Substring(legacyPrefix.Length)
                : "";
            var isLegacy =
                legacyVersion == "1.0" ||
                legacyVersion == "2.0";
            if (!string.Equals(
                    actualKind,
                    expectedKind,
                    StringComparison.Ordinal) &&
                !isLegacy)
                throw new InvalidDataException("MIKU_DOCUMENT_KIND_MISMATCH:" + expectedKind);
            var expectedVersion = isLegacy ? legacyVersion : "1.0";
            if (!string.Equals(
                    document["schemaVersion"]?.Value<string>(),
                    expectedVersion,
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_DOCUMENT_SCHEMA_VERSION_MISMATCH:" +
                    expectedVersion);
            if (document["version"] != null)
                throw new InvalidDataException("MIKU_LEGACY_VERSION_FIELD");
            ValidateCanonicalHash(document);
            if (!string.Equals(document["canonicalHash"]?.Value<string>(), reference["sha256"]?.Value<string>(), StringComparison.Ordinal))
            {
                // Reference hashes cover bytes; document canonical hashes cover
                // semantic content and are intentionally separate.
                var byteHash = Sha256File(path);
                if (!string.Equals(byteHash, reference["sha256"]?.Value<string>(), StringComparison.Ordinal))
                    throw new InvalidDataException("MIKU_DOCUMENT_REFERENCE_HASH_MISMATCH");
            }
            if (isLegacy)
            {
                document["documentKind"] = expectedKind;
                document["schemaVersion"] = "1.0";
            }
            return document;
        }

        static void ValidateManifest(JObject manifest, JObject bundle)
        {
            var manifestProfile = RequireSha256(manifest, "targetProfileHash");
            var bundleProfile = RequireSha256(bundle, "targetProfileHash");
            if (!SupportedProfileHashes.Contains(manifestProfile) ||
                !string.Equals(
                    manifestProfile,
                    bundleProfile,
                    StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_MANIFEST_PROFILE_MISMATCH");
            var completion = manifest["completion"] as JObject ?? throw new InvalidDataException("MIKU_MANIFEST_COMPLETION_MISSING");
            if (!string.Equals(completion["status"]?.Value<string>(), "completed", StringComparison.Ordinal) ||
                completion["exitCode"]?.Value<int>() != 0 ||
                !string.Equals(completion["marker"]?.Value<string>(), "MIKU_CONVERSION_COMPLETE", StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_MANIFEST_NOT_COMPLETED");
            var jobs = manifest["bakeJobs"] as JArray ?? new JArray();
            var artifacts = completion["artifacts"] as JArray ?? new JArray();
            if (jobs.Count > 0 && artifacts.Count == 0)
                throw new InvalidDataException("MIKU_BAKE_ARTIFACTS_MISSING");
            if (!string.Equals(manifest["persistentSourceId"]?.Value<string>(), bundle["persistentSourceId"]?.Value<string>(), StringComparison.Ordinal) ||
                !string.Equals(manifest["persistentMaterialId"]?.Value<string>(), bundle["persistentMaterialId"]?.Value<string>(), StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_MANIFEST_IDENTITY_MISMATCH");
        }

        static void PreflightResourceGuidOwnership(
            JArray resources,
            MaterialIdentityLocation location,
            string materialRoot,
            string sourceId,
            string materialId)
        {
            foreach (var resource in (resources ?? new JArray())
                         .OfType<JObject>()
                         .OrderBy(
                             item => item["id"]?.Value<string>(),
                             StringComparer.Ordinal))
            {
                var semantic = RequireString(resource, "semantic");
                if (string.Equals(
                        semantic,
                        "SourceMesh",
                        StringComparison.Ordinal))
                    continue;
                var bindingKey =
                    resource["bindingKey"]?.Value<string>() ?? semantic;
                var relative = NormalizeRelativePath(
                    RequireString(resource, "relativePath"));
                var extension = Path.GetExtension(relative).ToLowerInvariant();
                if (extension != ".png" &&
                    extension != ".jpg" &&
                    extension != ".jpeg" &&
                    extension != ".exr")
                    throw new InvalidDataException(
                        "MIKU_RESOURCE_EXTENSION_INVALID:" + extension);
                var role = "Texture:" + bindingKey;
                var assetGuid = StableAssetGuid(
                    sourceId,
                    materialId,
                    role);
                var assetPath = RecordedAssetPath(
                    location,
                    role,
                    materialRoot + "/Textures/" +
                    SanitizeName(bindingKey) + extension,
                    assetGuid,
                    extension);
                ValidateStableGuidOwnership(
                    role,
                    assetPath,
                    assetGuid);
            }
        }

        static Dictionary<string, Texture2D> ImportResources(
            string transactionRoot,
            JArray resources,
            MaterialIdentityLocation location,
            string materialRoot,
            string sourceId,
            string materialId,
            MikuImportResult result)
        {
            var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (var resource in (resources ?? new JArray()).OfType<JObject>().OrderBy(item => item["id"]?.Value<string>(), StringComparer.Ordinal))
            {
                var semantic = RequireString(resource, "semantic");
                if (string.Equals(
                        semantic,
                        "SourceMesh",
                        StringComparison.Ordinal))
                    continue;
                var bindingKey =
                    resource["bindingKey"]?.Value<string>() ?? semantic;
                ValidateResourceChannelBindings(resource);
                var width = resource["width"]?.Value<int>() ?? 0;
                var height = resource["height"]?.Value<int>() ?? 0;
                if (width <= 0 || height <= 0 || width > MaxTextureDimension || height > MaxTextureDimension)
                    throw new InvalidDataException("MIKU_RESOURCE_DIMENSION_LIMIT:" + semantic);
                var normalUsage =
                    semantic == "Normal" ||
                    string.Equals(
                        resource["usage"]?.Value<string>(),
                        "Normal",
                        StringComparison.Ordinal);
                if (normalUsage)
                {
                    var convention =
                        resource["normalConvention"]?.Value<string>() ?? "";
                    if (!string.Equals(
                            convention,
                            "TangentOpenGLPositiveY",
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            convention,
                            "TangentDirectXNegativeY",
                            StringComparison.Ordinal))
                        throw new InvalidDataException(
                            "MIKU_NORMAL_CONVENTION_INVALID");
                }
                var relative = NormalizeRelativePath(RequireString(resource, "relativePath"));
                var extension = Path.GetExtension(relative).ToLowerInvariant();
                if (extension != ".png" &&
                    extension != ".jpg" &&
                    extension != ".jpeg" &&
                    extension != ".exr")
                    throw new InvalidDataException("MIKU_RESOURCE_EXTENSION_INVALID:" + extension);
                var assetGuid = StableAssetGuid(
                    sourceId,
                    materialId,
                    "Texture:" + bindingKey);
                var assetPath = RecordedAssetPath(
                    location,
                    "Texture:" + bindingKey,
                    materialRoot + "/Textures/" +
                    SanitizeName(bindingKey) + extension,
                    assetGuid,
                    extension);
                var source = Path.Combine(transactionRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                var absoluteAssetPath = ToAbsoluteProjectPath(assetPath);
                var existedBeforeTransaction = File.Exists(absoluteAssetPath);
                WriteBytesIfChanged(absoluteAssetPath, File.ReadAllBytes(source));
                EnsureMetaGuid(
                    assetPath,
                    assetGuid,
                    !existedBeforeTransaction);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTexture(assetPath, resource);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture == null || texture.width != width || texture.height != height)
                    throw new InvalidDataException("MIKU_TEXTURE_IMPORT_FAILED:" + semantic);
                textures[bindingKey] = texture;
                result.assetPaths.Add(assetPath);
            }
            return textures;
        }

        static void ValidateResourceChannelBindings(JObject resource)
        {
            if (!(resource["channelBindings"] is JArray bindings))
                return;
            if (bindings.Count < 2 || bindings.Count > 24)
                throw new InvalidDataException(
                    "MIKU_CHANNEL_BINDINGS_INVALID");
            if (!string.Equals(
                    resource["usage"]?.Value<string>(),
                    "Scalar",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    resource["colorSpace"]?.Value<string>(),
                    "Linear",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_PACKED_RESOURCE_COLOR_SPACE_CONFLICT");
            var allowed = new HashSet<string>(
                new[]
                {
                    "Metalness",
                    "Roughness",
                    "AmbientOcclusion",
                    "Height",
                    "Alpha",
                    "EmissionMask",
                },
                StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings.OfType<JObject>())
            {
                var semantic =
                    binding["semantic"]?.Value<string>() ?? "";
                var channel =
                    binding["channel"]?.Value<string>() ?? "";
                if (!allowed.Contains(semantic))
                    throw new InvalidDataException(
                        "MIKU_CHANNEL_BINDING_SEMANTIC_INVALID:" +
                        semantic);
                if (channel != "R" &&
                    channel != "G" &&
                    channel != "B" &&
                    channel != "A")
                    throw new InvalidDataException(
                        "MIKU_CHANNEL_BINDING_CHANNEL_INVALID:" +
                        channel);
                if (!seen.Add(semantic + ":" + channel))
                    throw new InvalidDataException(
                        "MIKU_CHANNEL_BINDING_DUPLICATE:" +
                        semantic + ":" + channel);
            }
            if (seen.Count != bindings.Count)
                throw new InvalidDataException(
                    "MIKU_CHANNEL_BINDING_INVALID");
        }

        internal static string ComputeUnityMeshFingerprint(Mesh mesh)
        {
            if (mesh == null)
                return "";
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(
                stream,
                Encoding.UTF8,
                true))
            {
                writer.Write(mesh.vertexCount);
                writer.Write(mesh.subMeshCount);
                foreach (var value in mesh.vertices)
                {
                    writer.Write(value.x);
                    writer.Write(value.y);
                    writer.Write(value.z);
                }
                foreach (var value in mesh.normals)
                {
                    writer.Write(value.x);
                    writer.Write(value.y);
                    writer.Write(value.z);
                }
                foreach (var value in mesh.uv)
                {
                    writer.Write(value.x);
                    writer.Write(value.y);
                }
                for (var subMesh = 0;
                     subMesh < mesh.subMeshCount;
                     subMesh++)
                {
                    var indices = mesh.GetIndices(subMesh);
                    writer.Write(indices.Length);
                    foreach (var index in indices)
                        writer.Write(index);
                }
                writer.Flush();
                return Sha256Bytes(stream.ToArray());
            }
        }

        static int MeshIndexCount(Mesh mesh)
        {
            var total = 0L;
            for (var index = 0; index < mesh.subMeshCount; index++)
                checked
                {
                    total += (long)mesh.GetIndexCount(index);
                }
            if (total <= 0 || total > int.MaxValue)
                throw new InvalidDataException(
                    "MIKU_MESH_BINDING_MISMATCH");
            return (int)total;
        }

        static void ConfigureTexture(string assetPath, JObject resource)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
                ?? throw new InvalidDataException("MIKU_TEXTURE_IMPORTER_MISSING:" + assetPath);
            var semantic = RequireString(resource, "semantic");
            var linear = string.Equals(resource["colorSpace"]?.Value<string>(), "Linear", StringComparison.Ordinal);
            var normalUsage =
                semantic == "Normal" ||
                string.Equals(
                    resource["usage"]?.Value<string>(),
                    "Normal",
                    StringComparison.Ordinal);
            importer.textureType = normalUsage
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.convertToNormalmap = false;
            importer.flipGreenChannel =
                normalUsage &&
                string.Equals(
                    resource["normalConvention"]?.Value<string>(),
                    "TangentDirectXNegativeY",
                    StringComparison.Ordinal);
            importer.maxTextureSize = MaxTextureDimension;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true;
            importer.filterMode = string.Equals(
                resource["interpolation"]?.Value<string>(),
                "CLOSEST",
                StringComparison.Ordinal)
                ? FilterMode.Point
                : FilterMode.Bilinear;
            importer.wrapMode = string.Equals(
                resource["extension"]?.Value<string>(),
                "EXTEND",
                StringComparison.Ordinal)
                ? TextureWrapMode.Clamp
                : TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.crunchedCompression = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = semantic == "BaseColor";
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = false;
            importer.SetPlatformTextureSettings(standalone);
            importer.SaveAndReimport();
        }

        static Material CreateOrUpdateMaterial(
            string materialPath,
            string expectedGuid,
            Shader shader,
            string displayName,
            JObject wrapperContract)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = displayName };
                Directory.CreateDirectory(Path.GetDirectoryName(ToAbsoluteProjectPath(materialPath)));
                AssetDatabase.CreateAsset(material, materialPath);
                EnsureMetaGuid(materialPath, expectedGuid, true);
                AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceSynchronousImport);
                material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            }
            if (material == null)
                throw new InvalidDataException("MIKU_MATERIAL_CREATE_FAILED");
            material.shader = shader;
            material.name = displayName;
            material.SetOverrideTag(
                "RenderType",
                MaterialRenderType(wrapperContract));
            if (!string.Equals(AssetDatabase.AssetPathToGUID(materialPath), expectedGuid, StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_MATERIAL_GUID_MISMATCH");
            return material;
        }

        static string MaterialRenderType(JObject wrapperContract)
        {
            if (wrapperContract == null)
                return "Opaque";
            var renderMethod =
                wrapperContract["renderMethod"]?.Value<string>() ?? "Opaque";
            if (string.Equals(
                    renderMethod,
                    "Dithered",
                    StringComparison.Ordinal))
                return "TransparentCutout";
            if (string.Equals(
                    renderMethod,
                    "AlphaBlend",
                    StringComparison.Ordinal) ||
                string.Equals(
                    wrapperContract["model"]?.Value<string>(),
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal))
                return "Transparent";
            if (!string.Equals(
                    renderMethod,
                    "Opaque",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_RENDER_METHOD_UNSUPPORTED:" + renderMethod);
            return "Opaque";
        }

        static Material GetOrCreateUserMaterialVariant(
            string materialPath,
            string expectedGuid,
            Material baseMaterial,
            string displayName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
            {
                if (material.parent != baseMaterial)
                    throw new InvalidDataException(
                        "MIKU_USER_VARIANT_OWNERSHIP_CONFLICT:" + materialPath);
                return material;
            }
            material = new Material(baseMaterial)
            {
                name = displayName,
                parent = baseMaterial,
            };
            Directory.CreateDirectory(
                Path.GetDirectoryName(ToAbsoluteProjectPath(materialPath)));
            AssetDatabase.CreateAsset(material, materialPath);
            EnsureMetaGuid(materialPath, expectedGuid, true);
            AssetDatabase.ImportAsset(
                materialPath,
                ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Material>(materialPath)
                ?? throw new InvalidDataException("MIKU_MATERIAL_VARIANT_CREATE_FAILED");
        }

        static void ValidateMaterialVariant(
            string materialPath,
            Material baseMaterial)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || !material.isVariant || material.parent != baseMaterial)
                throw new InvalidDataException("MIKU_MATERIAL_VARIANT_INVALID");
        }

        static void BindMaterial(
            Material material,
            JObject ir,
            IDictionary<string, Texture2D> textures,
            string workflowKind,
            bool usesEditableGraph,
            IList<string> diagnostics)
        {
            var standardPbr = usesEditableGraph &&
                              string.Equals(
                                  workflowKind,
                                  "standard_pbr",
                                  StringComparison.Ordinal);
            var hasStandardControls = standardPbr &&
                                      HasStandardPbrAuthoringControls(material);
            if (standardPbr && !hasStandardControls)
            {
                AddDiagnosticOnce(
                    diagnostics,
                    "MIKU_STANDARD_PBR_AUTHORING_CONTROLS_UNAVAILABLE");
            }
            if (usesEditableGraph)
                ResetSurfaceAuthoringDefaults(material, ir);
            foreach (var item in textures)
            {
                if (!RequiresMaterialTextureBinding(ir, item.Key))
                {
                    if (IsApproximatedSourceMeshPbrChannel(ir, item.Key))
                    {
                        AddDiagnosticOnce(
                            diagnostics,
                            "MIKU_SOURCE_MESH_PBR_CHANNEL_APPROXIMATED:" +
                            item.Key +
                            ":URP_METALLIC_WORKFLOW_FIXED_F0");
                    }
                    continue;
                }
                if (usesEditableGraph)
                {
                    if ((IsDielectricSurface(ir) ||
                         UsesEvaluatedClosureRadiance(ir)) &&
                        string.Equals(
                            item.Key,
                            "BaseColor",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (item.Key.StartsWith(
                            "_MIKU_Baked_",
                            StringComparison.Ordinal) ||
                        item.Key.StartsWith(
                            "_MIKU_Packed_",
                            StringComparison.Ordinal))
                    {
                        if (!material.HasProperty(item.Key))
                            throw new InvalidDataException(
                                "MIKU_SHADER_PROPERTY_MISSING:" + item.Key);
                        material.SetTexture(item.Key, item.Value);
                        continue;
                    }
                    if (hasStandardControls &&
                        TryBindStandardPbrTexture(
                            material,
                            item.Key,
                            item.Value,
                            diagnostics))
                    {
                        continue;
                    }
                    var property = EditableTextureProperty(item.Key);
                    if (!material.HasProperty(property))
                        throw new InvalidDataException(
                            "MIKU_SHADER_PROPERTY_MISSING:" + property);
                    material.SetTexture(property, item.Value);
                }
                else if (!TryBindStaticTexture(material, item.Key, item.Value))
                {
                    throw new InvalidDataException(
                        "MIKU_WORKFLOW_TEXTURE_PROPERTY_MISSING:" + item.Key);
                }
            }
            const string bakedParity = "_MIKU_UseBakedParity";
            if (material.HasProperty(bakedParity))
                material.SetFloat(bakedParity, 1.0f);
            foreach (var channel in (ir["channels"] as JArray ?? new JArray()).OfType<JObject>())
            {
                var value = channel["value"] as JObject;
                if (value == null || value["kind"]?.Value<string>() != "Constant")
                    continue;
                var semantic = channel["semantic"]?.Value<string>() ?? "";
                var constant = value["value"];
                if (!usesEditableGraph)
                {
                    TryBindStaticConstant(material, semantic, constant);
                    continue;
                }
                if (TryBindSurfaceContractConstant(
                        material,
                        ir,
                        semantic,
                        constant))
                {
                    continue;
                }
                if (hasStandardControls)
                {
                    if (TryBindStandardPbrConstant(
                            material,
                            semantic,
                            constant,
                            diagnostics))
                    {
                        continue;
                    }
                }
                var property = "_MIKU_" + semantic + "Value";
                if (!material.HasProperty(property))
                    continue;
                if (constant is JArray array && array.Count >= 3)
                {
                    material.SetColor(
                        property,
                        new Color(
                            array[0].Value<float>(),
                            array[1].Value<float>(),
                            array[2].Value<float>(),
                            array.Count > 3 ? array[3].Value<float>() : 1.0f));
                }
                else if (constant != null && constant.Type != JTokenType.Null)
                {
                    var scalar = constant.Value<float>();
                    if (semantic == "Roughness")
                        scalar = 1.0f - scalar;
                    material.SetFloat(property, scalar);
                }
            }
        }

        static void ResetSurfaceAuthoringDefaults(
            Material material,
            JObject ir)
        {
            var surface = EffectiveSurfaceContract(ir);
            if (surface == null)
                return;
            if (UsesEvaluatedClosureRadiance(ir))
            {
                if (material.HasProperty("_BaseMap"))
                    material.SetTexture("_BaseMap", null);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", Color.white);
            }
            var renderMethod =
                surface["renderMethod"]?.Value<string>() ?? "Opaque";
            if (!string.Equals(
                    renderMethod,
                    "Opaque",
                    StringComparison.Ordinal) &&
                material.HasProperty("_Opacity"))
            {
                material.SetFloat("_Opacity", 1.0f);
            }
            if (!string.Equals(
                    surface["model"]?.Value<string>(),
                    "DielectricScreenRefraction",
                    StringComparison.Ordinal))
            {
                return;
            }
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            SetFloatIfPresent(material, "_IOR", 1.5f);
            SetFloatIfPresent(material, "_TransmissionWeight", 1.0f);
            SetFloatIfPresent(material, "_Opacity", 1.0f);
            SetFloatIfPresent(material, "_RefractionStrength", 0.05f);
            SetFloatIfPresent(material, "_ReflectionStrength", 1.0f);
            SetFloatIfPresent(material, "_Thickness", 0.1f);
        }

        static bool TryBindSurfaceContractConstant(
            Material material,
            JObject ir,
            string semantic,
            JToken constant)
        {
            var surface = EffectiveSurfaceContract(ir);
            if (surface == null)
                return false;
            switch (semantic)
            {
                case "BaseColor":
                    return IsDielectricSurface(ir) ||
                           UsesEvaluatedClosureRadiance(ir);
                case "Alpha":
                    if (string.Equals(
                            surface["renderMethod"]?.Value<string>(),
                            "Opaque",
                            StringComparison.Ordinal) ||
                        !material.HasProperty("_Opacity"))
                    {
                        return false;
                    }
                    material.SetFloat(
                        "_Opacity",
                        RequireUnitFloat(constant, semantic));
                    return true;
                case "TransmissionWeight":
                    if (!material.HasProperty("_TransmissionWeight"))
                        return false;
                    material.SetFloat(
                        "_TransmissionWeight",
                        RequireUnitFloat(constant, semantic));
                    return true;
                case "IOR":
                    if (!material.HasProperty("_IOR"))
                        return false;
                    var ior = RequireFiniteFloat(constant, semantic);
                    if (ior < 1.0f)
                        throw new InvalidDataException(
                            "MIKU_STANDARD_PBR_SCALAR_CONSTANT_RANGE:" +
                            semantic);
                    material.SetFloat("_IOR", ior);
                    return true;
                case "Thickness":
                    if (!material.HasProperty("_Thickness"))
                        return false;
                    var thickness = RequireFiniteFloat(constant, semantic);
                    if (thickness < 0.0f)
                        throw new InvalidDataException(
                            "MIKU_STANDARD_PBR_SCALAR_CONSTANT_RANGE:" +
                            semantic);
                    material.SetFloat("_Thickness", thickness);
                    return true;
                default:
                    return false;
            }
        }

        static bool IsDielectricSurface(JObject ir)
        {
            return string.Equals(
                EffectiveSurfaceContract(ir)?
                    ["model"]?.Value<string>(),
                "DielectricScreenRefraction",
                StringComparison.Ordinal);
        }

        static bool UsesEvaluatedClosureRadiance(JObject ir)
        {
            var surfaceKind =
                ir?["surfaceModelPlan"]?["kind"]?.Value<string>() ?? "";
            return string.Equals(
                       surfaceKind,
                       "TransparentEmission",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       surfaceKind,
                       "TransparentLit",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       surfaceKind,
                       "CustomMultiLobe",
                       StringComparison.Ordinal);
        }

        static JObject EffectiveSurfaceContract(JObject ir)
        {
            if (MikuSurfaceModelBackends.IsMaterialIr2(ir))
                return MikuSurfaceModelBackends
                    .Resolve(ir)
                    .WrapperContract(ir);
            return ir?["surfaceContract"] as JObject;
        }

        static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        static bool HasStandardPbrAuthoringControls(Material material)
        {
            return new[]
            {
                "_BaseColor",
                "_Metallic",
                "_Roughness",
                "_NormalStrength",
                "_EmissionColor",
                "_EmissionStrength",
            }.All(material.HasProperty);
        }

        static bool TryBindStandardPbrTexture(
            Material material,
            string semantic,
            Texture2D texture,
            IList<string> diagnostics)
        {
            var property = EditableTextureProperty(semantic);
            if (!material.HasProperty(property))
                return false;
            material.SetTexture(property, texture);
            switch (semantic)
            {
                case "BaseColor":
                    material.SetColor("_BaseColor", Color.white);
                    return true;
                case "Metalness":
                    material.SetFloat("_Metallic", 1.0f);
                    return true;
                case "Roughness":
                    material.SetFloat("_Roughness", 1.0f);
                    return true;
                case "Normal":
                    material.SetFloat("_NormalStrength", 1.0f);
                    return true;
                case "Emission":
                    material.SetColor("_EmissionColor", Color.white);
                    material.SetFloat("_EmissionStrength", 1.0f);
                    return true;
                case "EmissionMask":
                    return true;
                case "AmbientOcclusion":
                    SetFloatIfPresent(
                        material,
                        "_OcclusionStrength",
                        1.0f);
                    return true;
                case "Height":
                    SetFloatIfPresent(
                        material,
                        "_MIKU_BumpStrength",
                        1.0f);
                    SetFloatIfPresent(
                        material,
                        "_MIKU_BumpDistance",
                        1.0f);
                    SetFloatIfPresent(
                        material,
                        "_MIKU_HeightMidlevel",
                        0.5f);
                    SetFloatIfPresent(
                        material,
                        "_MIKU_HeightScale",
                        1.0f);
                    return true;
                case "Alpha":
                    SetFloatIfPresent(material, "_Opacity", 1.0f);
                    return true;
                default:
                    return false;
            }
        }

        static bool TryBindStandardPbrConstant(
            Material material,
            string semantic,
            JToken constant,
            IList<string> diagnostics)
        {
            switch (semantic)
            {
                case "BaseColor":
                    ResetTexture(material, "_BaseMap");
                    material.SetColor(
                        "_BaseColor",
                        RequireColor(constant, "BaseColor"));
                    return true;
                case "Metalness":
                    ResetTexture(material, "_MetallicMap");
                    material.SetFloat(
                        "_Metallic",
                        RequireUnitFloat(constant, "Metalness"));
                    return true;
                case "Roughness":
                    ResetTexture(material, "_RoughnessMap");
                    material.SetFloat(
                        "_Roughness",
                        RequireUnitFloat(constant, "Roughness"));
                    return true;
                case "Normal":
                    var legacyZeroNormal = IsLegacyZeroNormal(constant);
                    if (!legacyZeroNormal && !IsFlatNormal(constant))
                    {
                        AddDiagnosticOnce(
                            diagnostics,
                            "MIKU_STANDARD_PBR_NORMAL_CONSTANT_UNSUPPORTED");
                        return true;
                    }
                    if (legacyZeroNormal)
                    {
                        AddDiagnosticOnce(
                            diagnostics,
                            "MIKU_LEGACY_ZERO_NORMAL_NORMALIZED");
                    }
                    ResetTexture(material, "_BumpMap");
                    material.SetFloat("_NormalStrength", 1.0f);
                    return true;
                case "Emission":
                    ResetTexture(material, "_EmissionMap");
                    material.SetColor(
                        "_EmissionColor",
                        RequireColor(constant, "Emission"));
                    material.SetFloat("_EmissionStrength", 1.0f);
                    return true;
                case "AmbientOcclusion":
                    material.SetFloat(
                        "_OcclusionStrength",
                        RequireUnitFloat(constant, "AmbientOcclusion"));
                    return true;
                case "Alpha":
                    AddDiagnosticOnce(
                        diagnostics,
                        "MIKU_STANDARD_PBR_ALPHA_IGNORED_OPAQUE");
                    return true;
                default:
                    return false;
            }
        }

        static void ResetTexture(Material material, string property)
        {
            if (!material.HasProperty(property))
                throw new InvalidDataException(
                    "MIKU_SHADER_PROPERTY_MISSING:" + property);
            material.SetTexture(property, null);
        }

        static Color RequireColor(JToken value, string semantic)
        {
            if (!(value is JArray array) || array.Count < 3)
                throw new InvalidDataException(
                    "MIKU_STANDARD_PBR_COLOR_CONSTANT_INVALID:" + semantic);
            return new Color(
                RequireFiniteFloat(array[0], semantic),
                RequireFiniteFloat(array[1], semantic),
                RequireFiniteFloat(array[2], semantic),
                array.Count > 3
                    ? RequireFiniteFloat(array[3], semantic)
                    : 1.0f);
        }

        static float RequireUnitFloat(JToken value, string semantic)
        {
            var result = RequireFiniteFloat(value, semantic);
            if (result < 0.0f || result > 1.0f)
                throw new InvalidDataException(
                    "MIKU_STANDARD_PBR_SCALAR_CONSTANT_RANGE:" + semantic);
            return result;
        }

        static float RequireFiniteFloat(JToken value, string semantic)
        {
            if (value == null || value.Type == JTokenType.Null)
                throw new InvalidDataException(
                    "MIKU_STANDARD_PBR_CONSTANT_MISSING:" + semantic);
            var result = value.Value<float>();
            if (float.IsNaN(result) || float.IsInfinity(result))
                throw new InvalidDataException(
                    "MIKU_STANDARD_PBR_CONSTANT_NONFINITE:" + semantic);
            return result;
        }

        static bool IsFlatNormal(JToken value)
        {
            if (!(value is JArray array) || array.Count < 3)
                return false;
            var x = RequireFiniteFloat(array[0], "Normal");
            var y = RequireFiniteFloat(array[1], "Normal");
            var z = RequireFiniteFloat(array[2], "Normal");
            const float epsilon = 0.0001f;
            return Math.Abs(x) <= epsilon &&
                   Math.Abs(y) <= epsilon &&
                   Math.Abs(z - 1.0f) <= epsilon;
        }

        static bool IsLegacyZeroNormal(JToken value)
        {
            if (!(value is JArray array) || array.Count < 3)
                return false;
            var x = RequireFiniteFloat(array[0], "Normal");
            var y = RequireFiniteFloat(array[1], "Normal");
            var z = RequireFiniteFloat(array[2], "Normal");
            const float epsilon = 0.0001f;
            return Math.Abs(x) <= epsilon &&
                   Math.Abs(y) <= epsilon &&
                   Math.Abs(z) <= epsilon;
        }

        static bool HasLegacyZeroNormalChannel(JObject ir)
        {
            return (ir?["channels"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Any(channel =>
                    string.Equals(
                        channel["semantic"]?.Value<string>(),
                        "Normal",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        channel["value"]?["kind"]?.Value<string>(),
                        "Constant",
                        StringComparison.Ordinal) &&
                    IsLegacyZeroNormal(channel["value"]?["value"]));
        }

        static void AddDiagnosticOnce(
            IList<string> diagnostics,
            string diagnostic)
        {
            if (!diagnostics.Contains(diagnostic))
                diagnostics.Add(diagnostic);
        }

        static string EditableTextureProperty(string semantic)
        {
            return semantic switch
            {
                "BaseColor" => "_BaseMap",
                "Metalness" => "_MetallicMap",
                "Roughness" => "_RoughnessMap",
                "Normal" => "_BumpMap",
                "Height" => "_MIKU_HeightMap",
                "Emission" => "_EmissionMap",
                "EmissionMask" => "_MIKU_EmissionMask",
                "Alpha" => "_AlphaMap",
                "AmbientOcclusion" => "_OcclusionMap",
                _ => "_MIKU_" + semantic,
            };
        }

        internal static bool RequiresMaterialTextureBinding(
            JObject ir,
            string bindingKey)
        {
            if (!MikuSurfaceModelBackends.RequiresMaterialTextureBinding(
                    ir,
                    bindingKey))
            {
                return false;
            }
            return !MikuSurfaceModelBackends.UsesSourceMeshPbrProjection(ir) ||
                   SourceMeshPbrTextureSemantics.Contains(bindingKey ?? "");
        }

        static bool IsApproximatedSourceMeshPbrChannel(
            JObject ir,
            string bindingKey)
        {
            return MikuSurfaceModelBackends.UsesSourceMeshPbrProjection(ir) &&
                   !string.IsNullOrEmpty(bindingKey) &&
                   !bindingKey.StartsWith(
                       "_MIKU_Baked_",
                       StringComparison.Ordinal) &&
                   !bindingKey.StartsWith(
                       "_MIKU_Packed_",
                       StringComparison.Ordinal) &&
                   !SourceMeshPbrTextureSemantics.Contains(bindingKey);
        }

        static bool TryBindStaticTexture(
            Material material,
            string semantic,
            Texture texture)
        {
            var candidates = semantic switch
            {
                "BaseColor" => new[] { "_BaseMap", "_MainTex", "_DiffuseMap", "_Albedo" },
                "Normal" => new[] { "_BumpMap", "_NormalMap", "_NormalTex" },
                "Emission" => new[] { "_EmissionMap", "_EmissionTex" },
                "Metalness" => new[] { "_MetallicGlossMap", "_MetalMap", "_MetallicMap" },
                "Roughness" => new[] { "_RoughnessMap", "_LightMap", "_MaskMap" },
                "Alpha" => new[] { "_AlphaMap", "_MainTex" },
                "AmbientOcclusion" => new[] { "_OcclusionMap", "_LightMap", "_MaskMap" },
                "Height" => new[] { "_MIKU_HeightMap" },
                _ => Array.Empty<string>(),
            };
            foreach (var property in candidates)
            {
                if (!material.HasProperty(property))
                    continue;
                material.SetTexture(property, texture);
                return true;
            }
            return false;
        }

        static void TryBindStaticConstant(
            Material material,
            string semantic,
            JToken constant)
        {
            if (constant == null || constant.Type == JTokenType.Null)
                return;
            if (constant is JArray color && color.Count >= 3)
            {
                foreach (var property in new[] { "_BaseColor", "_Color" })
                {
                    if (!material.HasProperty(property))
                        continue;
                    material.SetColor(
                        property,
                        new Color(
                            color[0].Value<float>(),
                            color[1].Value<float>(),
                            color[2].Value<float>(),
                            color.Count > 3 ? color[3].Value<float>() : 1.0f));
                    return;
                }
                return;
            }
            var scalar = constant.Value<float>();
            var candidates = semantic switch
            {
                "Metalness" => new[] { "_Metallic", "_Metalness" },
                "Roughness" => new[] { "_Roughness" },
                "Alpha" => new[] { "_Alpha", "_Opacity" },
                "AmbientOcclusion" => new[] { "_OcclusionStrength" },
                _ => Array.Empty<string>(),
            };
            foreach (var property in candidates)
            {
                if (!material.HasProperty(property))
                    continue;
                material.SetFloat(property, scalar);
                return;
            }
            if (semantic == "Roughness" && material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 1.0f - scalar);
        }

        static void ValidateShader(Shader shader, string graphPath)
        {
            if (shader == null)
                throw new InvalidDataException("MIKU_SHADERGRAPH_LOAD_FAILED:" + graphPath);
            if (!shader.isSupported)
                throw new InvalidDataException("MIKU_SHADER_UNSUPPORTED:" + shader.name);
            var errors = ShaderUtil.GetShaderMessages(shader)
                .Where(IsShaderCompileError)
                .ToArray();
            if (ShaderUtil.ShaderHasError(shader) || errors.Length > 0)
                throw new InvalidDataException(
                    "MIKU_SHADER_COMPILE_FAILED:" +
                    string.Join(" | ", errors.Select(message => message.message).Take(5)));
        }

        static bool IsShaderCompileError(ShaderMessage message)
        {
            return string.Equals(
                message.severity.ToString(),
                "Error",
                StringComparison.OrdinalIgnoreCase);
        }

        static void ValidateMaterial(
            string materialPath,
            Shader shader,
            IDictionary<string, Texture2D> textures,
            string workflowKind,
            JObject ir)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || material.shader == null || material.shader != shader)
                throw new InvalidDataException("MIKU_MATERIAL_SHADER_INVALID");
            if (material.shader.name == "Hidden/InternalErrorShader")
                throw new InvalidDataException("MIKU_INTERNAL_ERROR_SHADER");
            foreach (var item in textures)
            {
                if (!RequiresMaterialTextureBinding(ir, item.Key))
                    continue;
                if (!material.GetTexturePropertyNames().Any(
                        name => material.GetTexture(name) == item.Value))
                    throw new InvalidDataException(
                        "MIKU_MATERIAL_TEXTURE_BINDING_MISSING:" +
                        item.Value.name);
            }
        }

        static void CopyTemplateAsset(
            string templatePath,
            string destinationPath,
            string assetGuid,
            bool write,
            string subGraphGuid = "")
        {
            var source = ToAbsoluteProjectPath(templatePath);
            var destination = ToAbsoluteProjectPath(destinationPath);
            if (!File.Exists(source))
                throw new FileNotFoundException("MIKU_TEMPLATE_MISSING", source);
            if (write)
            {
                var template = File.ReadAllText(source, Encoding.UTF8);
                if (templatePath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
                    template = template.Replace(SharedSubGraphGuid, subGraphGuid, StringComparison.Ordinal);
                MikuAtomicAssetWriter.WriteIfChanged(destination, template);
            }
            EnsureMetaGuid(destinationPath, assetGuid, false);
        }

        static bool WrapperMatchesTemplate(
            string wrapperPath,
            string templatePath,
            string subGraphGuid)
        {
            var wrapper = ToAbsoluteProjectPath(wrapperPath);
            var template = ToAbsoluteProjectPath(templatePath);
            if (!File.Exists(wrapper) || !File.Exists(template))
                return false;
            var expected = File.ReadAllText(template, Encoding.UTF8)
                .Replace(
                    SharedSubGraphGuid,
                    subGraphGuid,
                    StringComparison.Ordinal);
            var actual = File.ReadAllText(wrapper, Encoding.UTF8);
            return string.Equals(actual, expected, StringComparison.Ordinal);
        }

        static bool MatchesAnyKnownWrapperTemplate(
            string wrapperPath,
            string subGraphGuid)
        {
            return new[]
                {
                    MikuWorkflowBackends.StandardWrapperTemplate,
                    MikuWorkflowBackends.ClearCoatWrapperTemplate,
                    MikuWorkflowBackends.AlphaBlendWrapperTemplate,
                    MikuWorkflowBackends.DitheredWrapperTemplate,
                    MikuWorkflowBackends.DielectricWrapperTemplate,
                }
                .Any(template =>
                    WrapperMatchesTemplate(
                        wrapperPath,
                        template,
                        subGraphGuid));
        }

        static MaterialIdentityLocation ResolveMaterialIdentityLocation(
            string outputRoot,
            string safeName,
            string sourceId,
            string materialId)
        {
            var matches = FindIdentityMatchesInAssets(
                sourceId,
                materialId);
            if (matches.Count > 1)
            {
                throw new InvalidDataException(
                    "MIKU_OUTPUT_IDENTITY_DUPLICATE:" +
                    "sourceId=" + sourceId +
                    ":materialId=" + materialId +
                    ":directories=" +
                    string.Join("|", matches.Select(item => item.materialRoot)));
            }
            if (matches.Count == 1)
            {
                matches[0].reusedOutsideOutputRoot =
                    !IsAssetPathInside(
                        outputRoot,
                        matches[0].materialRoot);
                return matches[0];
            }

            var candidate = outputRoot.TrimEnd('/') +
                            "/" +
                            safeName +
                            "__" +
                            ShortIdentity(materialId);
            var absoluteCandidate = ToAbsoluteProjectPath(candidate);
            if (!Directory.Exists(absoluteCandidate) && !File.Exists(absoluteCandidate))
            {
                return new MaterialIdentityLocation
                {
                    materialRoot = candidate,
                    fileStem = safeName,
                };
            }

            var existing = Directory.Exists(absoluteCandidate)
                ? ReadIdentityLocations(candidate, absoluteCandidate).FirstOrDefault()
                : null;
            var existingSourceId =
                existing?.document?["persistentSourceId"]?.Value<string>() ?? "<unowned>";
            var existingMaterialId =
                existing?.document?["persistentMaterialId"]?.Value<string>() ?? "<unowned>";
            throw new InvalidDataException(
                "MIKU_OUTPUT_IDENTITY_CONFLICT:" +
                "directory=" + candidate +
                ":requestedSourceId=" + sourceId +
                ":requestedMaterialId=" + materialId +
                ":existingSourceId=" + existingSourceId +
                ":existingMaterialId=" + existingMaterialId);
        }

        static List<MaterialIdentityLocation> FindIdentityMatchesInAssets(
            string sourceId,
            string materialId)
        {
            var result = new List<MaterialIdentityLocation>();
            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(Application.dataPath));
            var directoryCount = 0;
            var identityDocumentCount = 0;
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                var attributes = File.GetAttributes(directory);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                directoryCount++;
                if (directoryCount > MaxIdentityDirectories)
                    throw new InvalidDataException(
                        "MIKU_OUTPUT_DIRECTORY_LIMIT:" +
                        directoryCount +
                        ":max=" +
                        MaxIdentityDirectories);
                var materialRoot = AssetPathForDirectory(directory);
                var identityPaths = Directory.GetFiles(
                    directory,
                    "*.miku-assets.json",
                    SearchOption.TopDirectoryOnly);
                identityDocumentCount += identityPaths.Length;
                if (identityDocumentCount > MaxIdentityDocuments)
                    throw new InvalidDataException(
                        "MIKU_OUTPUT_IDENTITY_DOCUMENT_LIMIT:" +
                        identityDocumentCount +
                        ":max=" +
                        MaxIdentityDocuments);
                result.AddRange(
                    ReadIdentityLocations(materialRoot, directory)
                        .Where(item =>
                            string.Equals(
                                item.document?["persistentSourceId"]?
                                    .Value<string>(),
                                sourceId,
                                StringComparison.Ordinal) &&
                            string.Equals(
                                item.document?["persistentMaterialId"]?
                                    .Value<string>(),
                                materialId,
                                StringComparison.Ordinal)));
                var children = Directory.GetDirectories(
                        directory,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .OrderByDescending(
                        path => path,
                        StringComparer.Ordinal)
                    .ToArray();
                foreach (var child in children)
                {
                    if ((File.GetAttributes(child) &
                         FileAttributes.ReparsePoint) == 0)
                        pending.Push(child);
                }
            }
            return result
                .OrderBy(item => item.materialRoot, StringComparer.Ordinal)
                .ThenBy(item => item.identityPath, StringComparer.Ordinal)
                .ToList();
        }

        static string AssetPathForDirectory(string absoluteDirectory)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var directory = Path.GetFullPath(absoluteDirectory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            if (string.Equals(
                    assetsRoot,
                    directory,
                    StringComparison.OrdinalIgnoreCase))
                return "Assets";
            RequireInsideRoot(assetsRoot, directory);
            return "Assets/" +
                   directory.Substring(assetsRoot.Length)
                       .TrimStart(
                           Path.DirectorySeparatorChar,
                           Path.AltDirectorySeparatorChar)
                       .Replace(Path.DirectorySeparatorChar, '/')
                       .Replace(Path.AltDirectorySeparatorChar, '/');
        }

        static bool IsAssetPathInside(string root, string candidate)
        {
            var normalizedRoot = root.TrimEnd('/');
            return string.Equals(
                       normalizedRoot,
                       candidate,
                       StringComparison.OrdinalIgnoreCase) ||
                   candidate.StartsWith(
                       normalizedRoot + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        static List<MaterialIdentityLocation> ReadIdentityLocations(
            string materialRoot,
            string absoluteMaterialRoot)
        {
            const int maxIdentityFiles = 64;
            var paths = Directory.GetFiles(
                absoluteMaterialRoot,
                "*.miku-assets.json",
                SearchOption.TopDirectoryOnly);
            if (paths.Length > maxIdentityFiles)
                throw new InvalidDataException(
                    "MIKU_OUTPUT_IDENTITY_DOCUMENT_LIMIT:" +
                    materialRoot +
                    ":max=" +
                    maxIdentityFiles);
            var result = new List<MaterialIdentityLocation>();
            foreach (var path in paths.OrderBy(item => item, StringComparer.Ordinal))
            {
                var info = new FileInfo(path);
                if (info.Length > MaxIdentityDocumentBytes)
                    throw new InvalidDataException(
                        "MIKU_OUTPUT_IDENTITY_FILE_SIZE_LIMIT:" +
                        materialRoot +
                        "/" +
                        Path.GetFileName(path));
                JObject document;
                try
                {
                    document = ParseJson(path, "MIKU_GENERATED_IDENTITY_JSON_INVALID");
                }
                catch (InvalidDataException)
                {
                    continue;
                }
                if (!string.Equals(
                        document["schema"]?.Value<string>(),
                        "miku-generated-asset-identity-1.0",
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(document["persistentSourceId"]?.Value<string>()) ||
                    string.IsNullOrWhiteSpace(document["persistentMaterialId"]?.Value<string>()))
                    continue;
                var fileName = Path.GetFileName(path);
                const string suffix = ".miku-assets.json";
                result.Add(new MaterialIdentityLocation
                {
                    materialRoot = materialRoot,
                    identityPath = materialRoot + "/" + fileName,
                    fileStem = fileName.Substring(0, fileName.Length - suffix.Length),
                    document = document,
                });
            }
            return result;
        }

        static string RecordedAssetPath(
            MaterialIdentityLocation location,
            string role,
            string fallback,
            string expectedGuid,
            string expectedSuffix)
        {
            if (location?.document == null)
                return fallback;
            var matches = (location.document["assets"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Where(item => string.Equals(
                    item["role"]?.Value<string>(),
                    role,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
                return fallback;
            if (matches.Length > 1)
                throw new InvalidDataException("MIKU_GENERATED_IDENTITY_ROLE_DUPLICATE:" + role);
            var assetPath = NormalizeAssetPath(matches[0]["assetPath"]?.Value<string>() ?? "");
            if (assetPath == null ||
                !assetPath.StartsWith(
                    location.materialRoot.TrimEnd('/') + "/",
                    StringComparison.OrdinalIgnoreCase) ||
                !assetPath.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "MIKU_GENERATED_IDENTITY_ASSET_PATH_INVALID:" + role);
            var recordedGuid = matches[0]["guid"]?.Value<string>() ?? "";
            if (!string.IsNullOrEmpty(recordedGuid) &&
                !string.Equals(recordedGuid, expectedGuid, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_GENERATED_IDENTITY_GUID_MISMATCH:" +
                    role +
                    ":" +
                    recordedGuid +
                    ":" +
                    expectedGuid);
            return assetPath;
        }

        static string ShortIdentity(string value)
        {
            var compact = new string((value ?? "").Where(char.IsLetterOrDigit).ToArray());
            return compact.Length >= 12
                ? compact.Substring(0, 12).ToLowerInvariant()
                : Sha256(value ?? "").Substring(0, 12);
        }


        static JObject BuildIdentityDocument(
            string sourceId,
            string materialId,
            string graphPath,
            string graphGuid,
            string subGraphPath,
            string subGraphGuid,
            string baseMaterialPath,
            string baseMaterialGuid,
            string materialVariantPath,
            string materialVariantGuid,
            string toonRecipePath,
            string toonRecipeGuid,
            IDictionary<string, Texture2D> textures)
        {
            var assets = new JArray();
            if (!string.IsNullOrEmpty(graphPath))
            {
                assets.Add(new JObject { ["role"] = "WrapperGraph", ["assetPath"] = graphPath, ["guid"] = graphGuid, ["owner"] = "UserAfterCreation" });
                assets.Add(new JObject { ["role"] = "GeneratedSubGraph", ["assetPath"] = subGraphPath, ["guid"] = subGraphGuid, ["owner"] = "Miku" });
            }
            if (!string.IsNullOrEmpty(baseMaterialPath))
            {
                assets.Add(new JObject
                {
                    ["role"] = "GeneratedBaseMaterial",
                    ["assetPath"] = baseMaterialPath,
                    ["guid"] = baseMaterialGuid,
                    ["owner"] = "Miku",
                });
                assets.Add(new JObject
                {
                    ["role"] = "UserMaterialVariant",
                    ["assetPath"] = materialVariantPath,
                    ["guid"] = materialVariantGuid,
                    ["owner"] = "UserAfterCreation",
                });
            }
            if (!string.IsNullOrEmpty(toonRecipePath))
            {
                assets.Add(new JObject
                {
                    ["role"] = "ToonMaterialRecipe",
                    ["assetPath"] = toonRecipePath,
                    ["guid"] = toonRecipeGuid,
                    ["owner"] = "Miku",
                });
            }
            foreach (var item in textures.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GetAssetPath(item.Value);
                assets.Add(new JObject
                {
                    ["role"] = "Texture:" + item.Key,
                    ["assetPath"] = path,
                    ["guid"] = AssetDatabase.AssetPathToGUID(path),
                    ["owner"] = "Miku",
                });
            }
            return new JObject
            {
                ["schema"] = "miku-generated-asset-identity-1.0",
                ["persistentSourceId"] = sourceId,
                ["persistentMaterialId"] = materialId,
                ["assets"] = assets,
            };
        }

        static JObject BuildReceipt(
            JObject bundle,
            MikuImportResult result,
            string sourceName,
            string baseMaterialPath,
            string materialVariantPath,
            string graphPath,
            string subGraphPath,
            Shader shader,
            IDictionary<string, Texture2D> textures,
            bool materialCreated,
            string toonRecipePath,
            string workflow)
        {
            var assets = new JArray();
            if (!string.IsNullOrEmpty(graphPath))
            {
                assets.Add(new JObject { ["assetPath"] = graphPath, ["type"] = "ShaderGraph", ["guid"] = AssetDatabase.AssetPathToGUID(graphPath) });
                assets.Add(new JObject { ["assetPath"] = subGraphPath, ["type"] = "ShaderSubGraph", ["guid"] = AssetDatabase.AssetPathToGUID(subGraphPath) });
            }
            if (materialCreated)
            {
                assets.Add(new JObject
                {
                    ["assetPath"] = baseMaterialPath,
                    ["type"] = "GeneratedBaseMaterial",
                    ["displayName"] = sourceName + " (Miku Generated)",
                    ["guid"] = AssetDatabase.AssetPathToGUID(baseMaterialPath),
                });
                assets.Add(new JObject
                {
                    ["assetPath"] = materialVariantPath,
                    ["type"] = "UserMaterialVariant",
                    ["displayName"] = sourceName,
                    ["guid"] = AssetDatabase.AssetPathToGUID(materialVariantPath),
                });
            }
            if (!string.IsNullOrEmpty(toonRecipePath))
            {
                assets.Add(new JObject
                {
                    ["assetPath"] = toonRecipePath,
                    ["type"] = "ToonMaterialRecipe",
                    ["guid"] = AssetDatabase.AssetPathToGUID(toonRecipePath),
                });
            }
            var textureSettings = new JArray();
            foreach (var item in textures.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var path = AssetDatabase.GetAssetPath(item.Value);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                textureSettings.Add(new JObject
                {
                    ["semantic"] = item.Key,
                    ["assetPath"] = path,
                    ["guid"] = AssetDatabase.AssetPathToGUID(path),
                    ["textureType"] = importer?.textureType.ToString() ?? "",
                    ["sRGB"] = importer?.sRGBTexture ?? false,
                    ["compression"] = importer?.textureCompression.ToString() ?? "",
                    ["maxSize"] = importer?.maxTextureSize ?? 0,
                    ["mipmaps"] = importer?.mipmapEnabled ?? false,
                    ["runtimeFormat"] = item.Value.format.ToString(),
                });
            }
            var receipt = new JObject
            {
                ["documentKind"] = "miku-unity-import-receipt-1.0",
                ["schemaVersion"] = "1.0",
                ["toolVersion"] = PackageVersion,
                ["id"] = result.transactionId,
                ["transactionId"] = result.transactionId,
                ["bundleHash"] = bundle["canonicalHash"]?.Value<string>() ?? "",
                ["targetProfileHash"] = bundle["targetProfileHash"] ?? "",
                ["importerProfileHash"] = ExpectedProfileHash,
                ["status"] = "committed",
                ["workflow"] = workflow,
                ["diagnostics"] = new JArray(
                    result.diagnostics
                        .OrderBy(item => item, StringComparer.Ordinal)),
                ["assets"] = assets,
                ["compile"] = new JObject
                {
                    ["shaderGraphLoaded"] = shader != null,
                    ["shaderCompiled"] = shader != null &&
                                         shader.isSupported &&
                                         !ShaderUtil.ShaderHasError(shader) &&
                                         !ShaderUtil.GetShaderMessages(shader).Any(IsShaderCompileError),
                    ["shaderName"] = shader?.name ?? "",
                    ["messages"] = new JArray(ShaderUtil.GetShaderMessages(shader).Select(message => message.message)),
                },
                ["textureImporterSettings"] = textureSettings,
                ["rollback"] = new JObject { ["status"] = "available-until-commit", ["transactionId"] = result.transactionId },
                ["verification"] = new JObject
                {
                    ["assetReferences"] = true,
                    ["textureBindings"] = textures.Count > 0 || !materialCreated,
                    ["renderPipeline"] = GraphicsSettings.currentRenderPipeline?.GetType().FullName ?? "",
                },
            };
            receipt["canonicalHash"] = CanonicalHash(receipt, "canonicalHash");
            return receipt;
        }

        static void BeginTransaction(
            string journalPath,
            string materialRootAssetPath,
            string materialRoot,
            string backupRoot,
            string bundleHash)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(journalPath));
            if (Directory.Exists(backupRoot))
                Directory.Delete(backupRoot, true);
            if (Directory.Exists(materialRoot))
                CopyDirectory(materialRoot, backupRoot);
            var materialRootMeta = materialRoot + ".meta";
            var backupMeta = backupRoot + ".material-root.meta";
            if (File.Exists(backupMeta))
                File.Delete(backupMeta);
            if (File.Exists(materialRootMeta))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupMeta));
                File.Copy(materialRootMeta, backupMeta, true);
            }
            var journal = new JObject
            {
                ["schema"] = "miku-unity-transaction-1.0",
                ["status"] = "in-progress",
                ["bundleHash"] = bundleHash,
                ["materialRootAssetPath"] = materialRootAssetPath,
                ["materialRoot"] = materialRoot,
                ["backupRoot"] = backupRoot,
                ["hadMaterialRoot"] = Directory.Exists(materialRoot),
                ["backupTreeHash"] = Directory.Exists(backupRoot) ? DirectoryTreeHash(backupRoot) : "",
                ["backupMetaHash"] = File.Exists(backupMeta) ? Sha256File(backupMeta) : "",
            };
            MikuAtomicAssetWriter.WriteIfChanged(journalPath, journal.ToString(Formatting.Indented) + "\n");
        }

        static void CompleteTransaction(string journalPath, string receiptHash)
        {
            var journal = ParseJson(journalPath, "MIKU_TRANSACTION_JOURNAL_INVALID");
            journal["status"] = "committed";
            journal["receiptHash"] = receiptHash;
            MikuAtomicAssetWriter.WriteIfChanged(journalPath, journal.ToString(Formatting.Indented) + "\n");
        }

        static void RollBackTransaction(string journalPath, string materialRootAssetPath, string backupRoot)
        {
            var absoluteMaterialRoot = string.IsNullOrEmpty(materialRootAssetPath) ? "" : ToAbsoluteProjectPath(materialRootAssetPath);
            if (string.IsNullOrEmpty(absoluteMaterialRoot))
                return;
            if (Directory.Exists(absoluteMaterialRoot))
                Directory.Delete(absoluteMaterialRoot, true);
            var absoluteMaterialRootMeta = absoluteMaterialRoot + ".meta";
            if (File.Exists(absoluteMaterialRootMeta))
                File.Delete(absoluteMaterialRootMeta);
            if (!string.IsNullOrEmpty(backupRoot) && Directory.Exists(backupRoot))
                CopyDirectory(backupRoot, absoluteMaterialRoot);
            var backupMeta = string.IsNullOrEmpty(backupRoot) ? "" : backupRoot + ".material-root.meta";
            if (!string.IsNullOrEmpty(backupMeta) && File.Exists(backupMeta))
                File.Copy(backupMeta, absoluteMaterialRootMeta, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var journal = File.Exists(journalPath) ? ParseJson(journalPath, "MIKU_TRANSACTION_JOURNAL_INVALID") : new JObject();
            var hadMaterialRoot = journal["hadMaterialRoot"]?.Value<bool>() ?? false;
            var expectedTreeHash = journal["backupTreeHash"]?.Value<string>() ?? "";
            var expectedMetaHash = journal["backupMetaHash"]?.Value<string>() ?? "";
            if (hadMaterialRoot)
            {
                if (!Directory.Exists(absoluteMaterialRoot) ||
                    !string.Equals(DirectoryTreeHash(absoluteMaterialRoot), expectedTreeHash, StringComparison.Ordinal))
                    throw new InvalidDataException("MIKU_ROLLBACK_TREE_VERIFICATION_FAILED");
            }
            else if (Directory.Exists(absoluteMaterialRoot))
            {
                throw new InvalidDataException("MIKU_ROLLBACK_NEW_TREE_REMAINS");
            }
            if (!string.IsNullOrEmpty(expectedMetaHash) &&
                (!File.Exists(absoluteMaterialRootMeta) ||
                 !string.Equals(Sha256File(absoluteMaterialRootMeta), expectedMetaHash, StringComparison.Ordinal)))
                throw new InvalidDataException("MIKU_ROLLBACK_META_VERIFICATION_FAILED");
            journal["status"] = "rolled-back";
            MikuAtomicAssetWriter.WriteIfChanged(journalPath, journal.ToString(Formatting.Indented) + "\n");
        }

        static void RecoverIncompleteTransactions()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RecoverIncompleteTransactions;
                return;
            }
            RecoverIncompleteTransactionsNow();
        }

        static void RecoverIncompleteTransactionsNow()
        {
            var transactionsRoot = Path.Combine(ProjectRoot, "Library", "Miku", "Transactions");
            if (!Directory.Exists(transactionsRoot))
                return;
            foreach (var journalPath in Directory.GetFiles(
                         transactionsRoot,
                         "transaction.json",
                         SearchOption.AllDirectories))
            {
                try
                {
                    var journal = ParseJson(journalPath, "MIKU_TRANSACTION_JOURNAL_INVALID");
                    if (!string.Equals(journal["status"]?.Value<string>(), "in-progress", StringComparison.Ordinal))
                        continue;
                    var materialRootAssetPath = NormalizeAssetPath(
                        journal["materialRootAssetPath"]?.Value<string>() ?? "");
                    if (materialRootAssetPath == null)
                        throw new InvalidDataException("MIKU_RECOVERY_ASSET_PATH_INVALID");
                    var backupRoot = Path.GetFullPath(journal["backupRoot"]?.Value<string>() ?? "");
                    RequireInsideRoot(Path.GetDirectoryName(journalPath), backupRoot);
                    RollBackTransaction(journalPath, materialRootAssetPath, backupRoot);
                    Debug.Log("MIKU_TRANSACTION_RECOVERED:" + journalPath);
                }
                catch (Exception ex)
                {
                    Debug.LogError(
                        "MIKU_TRANSACTION_RECOVERY_FAILED:" +
                        journalPath + ":" + ex.Message);
                }
            }
        }

        static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(directory.Replace(source, destination));
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = file.Replace(source, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        static void CopyStagedReference(string transactionRoot, JObject reference, string assetPath)
        {
            var relative = NormalizeRelativePath(RequireString(reference, "relativePath"));
            var source = Path.Combine(transactionRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            WriteBytesIfChanged(ToAbsoluteProjectPath(assetPath), File.ReadAllBytes(source));
        }

        static void EnsureMetaGuid(string assetPath, string expectedGuid, bool replaceNewlyCreated)
        {
            if (expectedGuid == null || expectedGuid.Length != 32 || expectedGuid.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException("MIKU_ASSET_GUID_INVALID");
            var absoluteMeta = ToAbsoluteProjectPath(assetPath) + ".meta";
            var generatedMeta = !File.Exists(absoluteMeta);
            if (generatedMeta)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!File.Exists(absoluteMeta))
                    throw new InvalidDataException("MIKU_ASSET_META_GENERATION_FAILED:" + assetPath);
            }
            var meta = File.ReadAllText(absoluteMeta, Encoding.UTF8);
            var match = Regex.Match(meta, "^guid:\\s*([0-9a-f]{32})$", RegexOptions.Multiline);
            if (!match.Success)
                throw new InvalidDataException("MIKU_ASSET_META_GUID_MISSING:" + assetPath);
            var existing = match.Groups[1].Value;
            if (string.Equals(existing, expectedGuid, StringComparison.Ordinal))
                return;
            if (!generatedMeta && !replaceNewlyCreated)
                throw new InvalidDataException("MIKU_ASSET_GUID_DRIFT:" + assetPath + ":" + existing + ":" + expectedGuid);
            var updated = meta.Substring(0, match.Groups[1].Index) +
                expectedGuid +
                meta.Substring(match.Groups[1].Index + match.Groups[1].Length);
            MikuAtomicAssetWriter.WriteIfChanged(absoluteMeta, updated);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void SynchronizeGeneratedSubGraph(
            string assetPath,
            string expectedGuid)
        {
            try
            {
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(
                    assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            catch (Exception error)
            {
                throw new InvalidDataException(
                    "MIKU_SUBGRAPH_IMPORT_FAILED:" + assetPath,
                    error);
            }

            var actualGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.Equals(
                    actualGuid,
                    expectedGuid,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "MIKU_SUBGRAPH_GUID_SYNC_FAILED:" +
                    assetPath +
                    ":actual=" + actualGuid +
                    ":expected=" + expectedGuid);
            }
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            {
                throw new InvalidDataException(
                    "MIKU_SUBGRAPH_IMPORT_FAILED:" + assetPath);
            }
        }

        internal static string ApplyRuntimeWrapperVertexContract(
            string wrapperText,
            JObject ir)
        {
            if (HasRuntimeVertexDisplacement(ir))
                return wrapperText;

            var objects = ParseShaderGraphMultiJson(wrapperText);
            var graph = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith("GraphData", StringComparison.Ordinal));
            var byId = objects
                .Where(item => !string.IsNullOrEmpty(
                    item["m_ObjectId"]?.Value<string>()))
                .ToDictionary(
                    item => item["m_ObjectId"].Value<string>(),
                    item => item,
                    StringComparer.Ordinal);
            var subGraphNode = objects.Single(item =>
                (item["m_Type"]?.Value<string>() ?? "")
                .EndsWith("SubGraphNode", StringComparison.Ordinal));
            var vertexPositionSlot =
                (subGraphNode["m_Slots"] as JArray ?? new JArray())
                .OfType<JObject>()
                .Select(reference =>
                {
                    byId.TryGetValue(
                        reference["m_Id"]?.Value<string>() ?? "",
                        out var slot);
                    return slot;
                })
                .Single(slot =>
                    slot != null &&
                    slot["m_SlotType"]?.Value<int>() == 1 &&
                    string.Equals(
                        slot["m_DisplayName"]?.Value<string>(),
                        "Vertex Position",
                        StringComparison.Ordinal));
            var vertexPositionBlock = objects.Single(item =>
                string.Equals(
                    item["m_Name"]?.Value<string>(),
                    "VertexDescription.Position",
                    StringComparison.Ordinal));
            var subGraphNodeId =
                subGraphNode["m_ObjectId"]?.Value<string>() ?? "";
            var vertexPositionSlotId =
                vertexPositionSlot["m_Id"]?.Value<int>() ?? 0;
            var vertexPositionBlockId =
                vertexPositionBlock["m_ObjectId"]?.Value<string>() ?? "";
            var edges = graph["m_Edges"] as JArray ?? new JArray();
            var retained = edges
                .OfType<JObject>()
                .Where(edge =>
                    !string.Equals(
                        edge["m_OutputSlot"]?["m_Node"]?["m_Id"]
                            ?.Value<string>(),
                        subGraphNodeId,
                        StringComparison.Ordinal) ||
                    edge["m_OutputSlot"]?["m_SlotId"]?.Value<int>() !=
                        vertexPositionSlotId ||
                    !string.Equals(
                        edge["m_InputSlot"]?["m_Node"]?["m_Id"]
                            ?.Value<string>(),
                        vertexPositionBlockId,
                        StringComparison.Ordinal) ||
                    edge["m_InputSlot"]?["m_SlotId"]?.Value<int>() != 0)
                .ToArray();
            if (retained.Length != edges.Count - 1)
            {
                throw new InvalidDataException(
                    "MIKU_WRAPPER_VERTEX_POSITION_EDGE_MISSING");
            }
            edges.Clear();
            foreach (var edge in retained)
                edges.Add(edge);
            return string.Join(
                       "\n\n",
                       objects.Select(item =>
                           item.ToString(Formatting.Indented))) +
                   "\n";
        }

        internal static bool HasRuntimeVertexDisplacement(JObject ir)
        {
            var displacement = (ir?["channels"] as JArray ?? new JArray())
                .OfType<JObject>()
                .FirstOrDefault(channel => string.Equals(
                    channel["semantic"]?.Value<string>(),
                    "Displacement",
                    StringComparison.Ordinal));
            return displacement?["value"] is JObject value &&
                   string.Equals(
                       value["kind"]?.Value<string>(),
                       "Expression",
                       StringComparison.Ordinal);
        }

        static List<JObject> ParseShaderGraphMultiJson(string value)
        {
            var objects = new List<JObject>();
            using (var textReader = new StringReader(value))
            using (var jsonReader = new JsonTextReader(textReader)
                   {
                       SupportMultipleContent = true,
                   })
            {
                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.StartObject)
                        objects.Add(JObject.Load(jsonReader));
                }
            }
            return objects;
        }

        static void ValidateStableGuidOwnership(
            string role,
            string requestedPath,
            string expectedGuid,
            string allowedExistingPath = null)
        {
            if (expectedGuid == null ||
                expectedGuid.Length != 32 ||
                expectedGuid.Any(character => !Uri.IsHexDigit(character)))
                throw new InvalidDataException("MIKU_ASSET_GUID_INVALID");
            var existingPath = AssetDatabase.GUIDToAssetPath(expectedGuid);
            if (string.IsNullOrEmpty(existingPath) ||
                !AssetPathExists(existingPath) ||
                string.Equals(
                    existingPath,
                    requestedPath,
                    StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(allowedExistingPath) &&
                string.Equals(
                    existingPath,
                    allowedExistingPath,
                    StringComparison.OrdinalIgnoreCase))
                return;
            throw new InvalidDataException(
                "MIKU_ASSET_GUID_COLLISION:" +
                "role=" + role +
                ":existingPath=" + existingPath +
                ":requestedPath=" + requestedPath);
        }

        static bool AssetPathExists(string assetPath)
        {
            var absolutePath = Path.Combine(
                ProjectRoot,
                assetPath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            return File.Exists(absolutePath) ||
                   Directory.Exists(absolutePath) ||
                   File.Exists(absolutePath + ".meta");
        }

        static string StableAssetGuid(string sourceId, string materialId, string role)
        {
            return Sha256(sourceId + "\0" + materialId + "\0" + role).Substring(0, 32);
        }

        static JObject RequireReference(JObject reference, string expectedMediaType)
        {
            if (reference == null)
                throw new InvalidDataException("MIKU_ARTIFACT_REFERENCE_INVALID");
            NormalizeRelativePath(RequireString(reference, "relativePath"));
            RequireSha256(reference, "sha256");
            var length = reference["byteLength"]?.Value<long>() ?? -1;
            if (length <= 0 || length > MaxArtifactBytes)
                throw new InvalidDataException("MIKU_ARTIFACT_SIZE_INVALID");
            var mediaType = RequireString(reference, "mediaType");
            if (expectedMediaType != null && !string.Equals(mediaType, expectedMediaType, StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_ARTIFACT_MEDIA_TYPE_INVALID");
            if (expectedMediaType == null &&
                mediaType != "image/png" &&
                mediaType != "image/jpeg" &&
                mediaType != "image/x-exr" &&
                mediaType != "model/gltf-binary")
                throw new InvalidDataException("MIKU_RESOURCE_MEDIA_TYPE_INVALID");
            return reference;
        }

        static string NormalizeRelativePath(string value)
        {
            var normalized = (value ?? "").Normalize(NormalizationForm.FormC).Replace('\\', '/');
            if (string.IsNullOrEmpty(normalized) || normalized.StartsWith("/", StringComparison.Ordinal) || DrivePattern.IsMatch(normalized))
                throw new InvalidDataException("MIKU_ARTIFACT_PATH_INVALID");
            var parts = normalized.Split('/');
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part) || part == "." || part == ".." || part != part.TrimEnd(' ', '.'))
                    throw new InvalidDataException("MIKU_ARTIFACT_PATH_INVALID");
                var stem = part.Split('.')[0];
                if (ReservedNames.Contains(stem))
                    throw new InvalidDataException("MIKU_ARTIFACT_PATH_RESERVED");
            }
            return string.Join("/", parts);
        }

        static string RequireSecureFile(string root, string relative)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', Path.DirectorySeparatorChar)));
            RequireInsideRoot(rootFull, candidate);
            var current = candidate;
            while (!string.IsNullOrEmpty(current))
            {
                if ((File.Exists(current) || Directory.Exists(current)) &&
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("MIKU_ARTIFACT_REPARSE_POINT");
                if (string.Equals(
                    Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    rootFull,
                    StringComparison.OrdinalIgnoreCase))
                    break;
                current = Path.GetDirectoryName(current);
            }
            if (!File.Exists(candidate))
                throw new FileNotFoundException("MIKU_ARTIFACT_MISSING", candidate);
            return candidate;
        }

        static void CopyAndVerify(string source, string destination, string expectedHash, long expectedLength)
        {
            if (expectedLength <= 0 || expectedLength > MaxArtifactBytes)
                throw new InvalidDataException("MIKU_ARTIFACT_SIZE_INVALID");
            using var sha = SHA256.Create();
            long length = 0;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[1024 * 1024];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    length += read;
                    if (length > expectedLength || length > MaxArtifactBytes)
                        throw new InvalidDataException("MIKU_ARTIFACT_SIZE_MISMATCH");
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    output.Write(buffer, 0, read);
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                output.Flush(true);
            }
            var actualHash = string.Concat(sha.Hash.Select(item => item.ToString("x2")));
            if (length != expectedLength || !string.Equals(actualHash, expectedHash, StringComparison.Ordinal) ||
                new FileInfo(destination).Length != expectedLength || !string.Equals(Sha256File(destination), expectedHash, StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_ARTIFACT_HASH_MISMATCH");
        }

        static string ComputeSealedDigest(JObject bundle)
        {
            var artifacts = new JArray();
            foreach (var role in new[] { "ir", "plan", "manifest", "sourceMap" })
            {
                var reference = bundle[role] as JObject;
                artifacts.Add(new JObject
                {
                    ["role"] = role,
                    ["relativePath"] = reference?["relativePath"] ?? "",
                    ["sha256"] = reference?["sha256"] ?? "",
                    ["byteLength"] = reference?["byteLength"] ?? -1,
                });
            }
            foreach (var reference in (bundle["resources"] as JArray ?? new JArray()).OfType<JObject>())
            {
                artifacts.Add(new JObject
                {
                    ["role"] = "resource:" + (reference["id"]?.Value<string>() ?? ""),
                    ["relativePath"] = reference["relativePath"] ?? "",
                    ["sha256"] = reference["sha256"] ?? "",
                    ["byteLength"] = reference["byteLength"] ?? -1,
                });
            }
            var sorted = new JArray(artifacts.OfType<JObject>().OrderBy(
                item => (item["role"]?.Value<string>() ?? "") + "\0" + (item["relativePath"]?.Value<string>() ?? ""),
                StringComparer.Ordinal));
            var payload = new JObject
            {
                ["materialKey"] = bundle["materialKey"] ?? "",
                ["persistentSourceId"] = bundle["persistentSourceId"] ?? "",
                ["persistentMaterialId"] = bundle["persistentMaterialId"] ?? "",
                ["targetProfileHash"] = bundle["targetProfileHash"] ?? "",
                ["artifacts"] = sorted,
            };
            return Sha256(CanonicalJson(payload));
        }

        static void ValidateCanonicalHash(JObject document)
        {
            var expected = RequireSha256(document, "canonicalHash");
            var actual = CanonicalHash(document, "canonicalHash");
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_CANONICAL_HASH_MISMATCH");
        }

        internal static string CanonicalHash(
            JObject value,
            string excludedProperty)
        {
            var clone = (JObject)value.DeepClone();
            clone.Remove(excludedProperty);
            return Sha256(CanonicalJson(clone));
        }

        static string CanonicalJson(JToken token)
        {
            if (token is JObject obj)
            {
                return "{" + string.Join(",", obj.Properties()
                    .OrderBy(property => property.Name.Normalize(NormalizationForm.FormC), StringComparer.Ordinal)
                    .Select(property => JsonConvert.ToString(property.Name.Normalize(NormalizationForm.FormC)) + ":" + CanonicalJson(property.Value))) + "}";
            }
            if (token is JArray array)
                return "[" + string.Join(",", array.Select(CanonicalJson)) + "]";
            if (token.Type == JTokenType.String)
                return JsonConvert.ToString(token.Value<string>()?.Normalize(NormalizationForm.FormC) ?? "");
            if (token.Type == JTokenType.Float)
            {
                var value = token.Value<double>();
                if (double.IsNaN(value) || double.IsInfinity(value))
                    throw new InvalidDataException("MIKU_INVALID_NUMBER");
                return PythonCompatibleFloat(value);
            }
            return token.ToString(Formatting.None);
        }

        static string PythonCompatibleFloat(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidDataException("MIKU_INVALID_NUMBER");
            if (value == 0.0)
                return "0.0";

            var negative = value < 0.0;
            var magnitude = Math.Abs(value);
            var selected = BigInteger.Zero;
            var decimalPower = 0;
            var found = false;
            for (var precision = 1; precision <= 17; precision++)
            {
                var candidate = magnitude.ToString(
                    "G" + precision,
                    CultureInfo.InvariantCulture);
                ParseDecimalCandidate(
                    candidate,
                    out var candidateSignificand,
                    out var candidatePower);
                if (TrySelectClosestRoundTrip(
                        magnitude,
                        candidateSignificand,
                        candidatePower,
                        out selected))
                {
                    decimalPower = candidatePower;
                    found = true;
                    break;
                }
            }
            if (!found)
                throw new InvalidDataException("MIKU_FLOAT_FORMAT_FAILED");
            while (selected > 0 && selected % 10 == 0)
            {
                selected /= 10;
                decimalPower++;
            }
            var digits = selected.ToString(CultureInfo.InvariantCulture);
            var decimalExponent = decimalPower + digits.Length - 1;
            string formatted;
            if (decimalExponent < -4 || decimalExponent >= 16)
            {
                var coefficient = digits.Length == 1
                    ? digits
                    : digits.Substring(0, 1) + "." + digits.Substring(1);
                formatted = coefficient + "e" + decimalExponent.ToString(
                    "+00;-00",
                    CultureInfo.InvariantCulture);
            }
            else
            {
                var outputPosition = decimalExponent + 1;
                if (outputPosition <= 0)
                {
                    formatted =
                        "0." + new string('0', -outputPosition) + digits;
                }
                else if (outputPosition >= digits.Length)
                {
                    formatted =
                        digits +
                        new string('0', outputPosition - digits.Length) +
                        ".0";
                }
                else
                {
                    formatted =
                        digits.Substring(0, outputPosition) + "." +
                        digits.Substring(outputPosition);
                }
            }
            return negative ? "-" + formatted : formatted;
        }

        static void ParseDecimalCandidate(
            string value,
            out BigInteger significand,
            out int decimalPower)
        {
            var exponentIndex = value.IndexOfAny(new[] { 'e', 'E' });
            var mantissa = exponentIndex >= 0
                ? value.Substring(0, exponentIndex)
                : value;
            var explicitExponent = exponentIndex >= 0
                ? int.Parse(
                    value.Substring(exponentIndex + 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture)
                : 0;
            var pointIndex = mantissa.IndexOf('.');
            var decimalPosition = pointIndex >= 0
                ? pointIndex
                : mantissa.Length;
            var rawDigits = mantissa.Replace(".", "");
            var firstSignificant = 0;
            while (firstSignificant < rawDigits.Length &&
                   rawDigits[firstSignificant] == '0')
                firstSignificant++;
            var digits = rawDigits.Substring(firstSignificant).TrimEnd('0');
            if (digits.Length == 0)
                digits = "0";
            var decimalExponent =
                explicitExponent + decimalPosition - firstSignificant - 1;
            significand = BigInteger.Parse(
                digits,
                CultureInfo.InvariantCulture);
            decimalPower = decimalExponent - digits.Length + 1;
        }

        static bool TrySelectClosestRoundTrip(
            double magnitude,
            BigInteger significand,
            int decimalPower,
            out BigInteger selected)
        {
            var targetBits = BitConverter.DoubleToInt64Bits(magnitude);
            selected = BigInteger.Zero;
            var selectedDistance = BigInteger.Zero;
            var found = false;
            for (var delta = -2; delta <= 2; delta++)
            {
                var candidate = significand + delta;
                if (candidate <= 0)
                    continue;
                var candidateText =
                    candidate.ToString(CultureInfo.InvariantCulture) +
                    "e" +
                    decimalPower.ToString(
                        "+0;-0;0",
                        CultureInfo.InvariantCulture);
                if (!double.TryParse(
                        candidateText,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var parsed) ||
                    BitConverter.DoubleToInt64Bits(parsed) != targetBits)
                    continue;
                var distance = DecimalDistanceNumerator(
                    magnitude,
                    candidate,
                    decimalPower);
                if (!found ||
                    distance < selectedDistance ||
                    (distance == selectedDistance &&
                     candidate.IsEven &&
                     !selected.IsEven))
                {
                    selected = candidate;
                    selectedDistance = distance;
                    found = true;
                }
            }
            return found;
        }

        static BigInteger DecimalDistanceNumerator(
            double magnitude,
            BigInteger decimalSignificand,
            int decimalPower)
        {
            var bits = (ulong)BitConverter.DoubleToInt64Bits(magnitude);
            var exponentBits = (int)((bits >> 52) & 0x7ff);
            var fraction = bits & ((1UL << 52) - 1);
            var binarySignificand = exponentBits == 0
                ? new BigInteger(fraction)
                : new BigInteger(fraction | (1UL << 52));
            var binaryPower = exponentBits == 0
                ? -1074
                : exponentBits - 1023 - 52;
            var binaryNumerator = binarySignificand;
            var binaryDenominator = BigInteger.One;
            if (binaryPower >= 0)
                binaryNumerator <<= binaryPower;
            else
                binaryDenominator <<= -binaryPower;

            var decimalNumerator = decimalSignificand;
            var decimalDenominator = BigInteger.One;
            if (decimalPower >= 0)
                decimalNumerator *= BigInteger.Pow(10, decimalPower);
            else
                decimalDenominator = BigInteger.Pow(10, -decimalPower);
            return BigInteger.Abs(
                binaryNumerator * decimalDenominator -
                decimalNumerator * binaryDenominator);
        }

        static void ValidateRenderPipeline()
        {
            if (!string.Equals(
                    Application.unityVersion,
                    "6000.4.5f1",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_UNITY_VERSION_UNSUPPORTED:" +
                    Application.unityVersion +
                    ":expected=6000.4.5f1");
            RequirePackageVersion(
                "Packages/com.unity.render-pipelines.universal",
                "17.4.0",
                "MIKU_URP_VERSION_UNSUPPORTED");
            RequirePackageVersion(
                "Packages/com.unity.shadergraph",
                "17.4.0",
                "MIKU_SHADERGRAPH_VERSION_UNSUPPORTED");
            var pipeline = GraphicsSettings.currentRenderPipeline ??
                           QualitySettings.renderPipeline ??
                           GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null || !(pipeline.GetType().FullName ?? "").Contains("UniversalRenderPipelineAsset"))
                throw new InvalidDataException("MIKU_URP_PIPELINE_REQUIRED");
        }

        static void AddSurfaceProjectSetupDiagnostics(
            JObject materialIr,
            IList<string> diagnostics)
        {
            var contract = materialIr["surfaceContract"] as JObject;
            var requirements = materialIr["surfaceModelPlan"]?
                ["shaderRequirements"] as JObject;
            var dielectric = string.Equals(
                contract?["model"]?.Value<string>(),
                "DielectricScreenRefraction",
                StringComparison.Ordinal) ||
                string.Equals(
                    materialIr["surfaceModelPlan"]?
                        ["kind"]?.Value<string>(),
                    "RefractiveGlass",
                    StringComparison.Ordinal);
            var requiresOpaqueTexture =
                requirements?["requiresOpaqueTexture"]?.Value<bool>() ??
                dielectric;
            if (!dielectric && !requiresOpaqueTexture)
                return;
            if (dielectric &&
                PlayerSettings.colorSpace != ColorSpace.Linear)
                AddDiagnosticOnce(
                    diagnostics,
                    "MIKU_LINEAR_COLOR_SPACE_RECOMMENDED:" +
                    "RequiresProjectSetup");
            var pipeline = GraphicsSettings.currentRenderPipeline ??
                           QualitySettings.renderPipeline ??
                           GraphicsSettings.defaultRenderPipeline;
            var opaqueTextureProperty = pipeline?.GetType().GetProperty(
                "supportsCameraOpaqueTexture",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic);
            var supportsOpaqueTexture =
                opaqueTextureProperty?.GetValue(pipeline) is bool enabled &&
                enabled;
            if (requiresOpaqueTexture && !supportsOpaqueTexture)
                AddDiagnosticOnce(
                    diagnostics,
                    "MIKU_URP_OPAQUE_TEXTURE_REQUIRED:" +
                    "RequiresProjectSetup");
        }

        static void RequirePackageVersion(
            string assetPath,
            string expected,
            string code)
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                assetPath);
            var actual = package?.version ?? "missing";
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException(
                    code + ":" + actual + ":expected=" + expected);
        }

        static void WriteBytesIfChanged(string path, byte[] bytes)
        {
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes))
                return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }

        static JObject ParseJson(string path, string code)
        {
            try
            {
                using var reader = File.OpenText(path);
                using var json = new JsonTextReader(reader)
                {
                    FloatParseHandling = FloatParseHandling.Double,
                    MaxDepth = MaxJsonDepth,
                };
                return JObject.Load(json);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(code + ":" + ex.Message, ex);
            }
        }

        static string RequireString(JObject obj, string property)
        {
            var value = obj?[property]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException("MIKU_REQUIRED_FIELD_MISSING:" + property);
            return value;
        }

        static string RequireSha256(JObject obj, string property)
        {
            var value = RequireString(obj, property);
            if (!Sha256Pattern.IsMatch(value))
                throw new InvalidDataException("MIKU_SHA256_INVALID:" + property);
            return value;
        }

        static MikuImportResult Fail(MikuImportResult result, string message)
        {
            result.success = false;
            result.diagnostics.Add(message);
            return result;
        }

        static Exception RootCause(Exception error)
        {
            var current = error;
            while (current is TargetInvocationException invocation &&
                   invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        static string NormalizeAssetPath(string value)
        {
            var path = (value ?? "").Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path.Split('/').Any(segment => segment == ".." || segment.Length == 0))
                return null;
            var absolute = ToAbsoluteProjectPath(path);
            try
            {
                RequireInsideRoot(Application.dataPath, absolute);
                return path;
            }
            catch
            {
                return null;
            }
        }

        static void RequireInsideRoot(string root, string path)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var pathFull = Path.GetFullPath(path);
            if (!pathFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("MIKU_PATH_ESCAPE");
        }

        static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        static string SanitizeName(string value)
        {
            var chars = (value ?? "Material").Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch > 127 ? ch : '_').ToArray();
            var result = new string(chars).Trim('_', '-');
            if (string.IsNullOrEmpty(result)) result = "Material";
            return ReservedNames.Contains(result) ? "_" + result : result;
        }

        static string Sha256(string text)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(text)).Select(item => item.ToString("x2")));
        }

        static string Sha256Bytes(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(
                sha.ComputeHash(bytes)
                    .Select(item => item.ToString("x2")));
        }

        static string Sha256File(string path)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return string.Concat(sha.ComputeHash(stream).Select(item => item.ToString("x2")));
        }

        static string DirectoryTreeHash(string root)
        {
            if (!Directory.Exists(root))
                return "";
            using var hash = SHA256.Create();
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                         .OrderBy(
                             path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                             StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                var relativeBytes = Encoding.UTF8.GetBytes(relative);
                hash.TransformBlock(
                    relativeBytes,
                    0,
                    relativeBytes.Length,
                    null,
                    0);
                var bytes = File.ReadAllBytes(file);
                hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return string.Concat(hash.Hash.Select(item => item.ToString("x2")));
        }
    }
}
