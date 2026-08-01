// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Miku.ShaderConverter.Editor
{
    [ScriptedImporter(1, "mikubundle")]
    public class MikuBundleScriptedImporter : ScriptedImporter
    {
        const long MaxManifestBytes = 4L * 1024L * 1024L;
        static readonly Regex Sha256 = new Regex(
            "^[0-9a-f]{64}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public override void OnImportAsset(AssetImportContext context)
        {
            var asset = ScriptableObject.CreateInstance<MikuBundleAsset>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            asset.outputRoot = OutputRootFor(context.assetPath);
            try
            {
                var bundle = ParseBundle(context.assetPath);
                asset.documentKind = RequireOneOf(
                    bundle,
                    "documentKind",
                    "miku-bundle-1.0",
                    "migr-bundle-1.0",
                    "migr-bundle-2.0",
                    "migr-bundle-2.1",
                    "migr-bundle-2.2");
                var legacyBundleV2 = asset.documentKind.StartsWith(
                    "migr-bundle-2.",
                    StringComparison.Ordinal);
                asset.schemaVersion = RequireHeader(
                    bundle,
                    "schemaVersion",
                    string.Equals(
                        asset.documentKind,
                        "migr-bundle-2.2",
                        StringComparison.Ordinal)
                        ? "2.2"
                        : string.Equals(
                            asset.documentKind,
                            "migr-bundle-2.1",
                            StringComparison.Ordinal)
                            ? "2.1"
                            : legacyBundleV2 ? "2.0" : "1.0");
                asset.bundleHash = RequireSha256(bundle, "canonicalHash");
                asset.materialName = RequireString(bundle, "sourceName");
                RegisterDependencies(context, bundle, asset);
                var ir = ReadSiblingDocument(
                    context.assetPath,
                    bundle["ir"] as JObject,
                    "miku-material-ir-1.0");
                asset.workflow = MikuWorkflowBackends.Resolve(ir).Kind;
                if (MikuImportScheduler.TryGetCommitted(
                        context.assetPath,
                        asset.bundleHash,
                        out var receiptPath))
                {
                    asset.status = "committed";
                    asset.receiptPath = receiptPath;
                }
                else
                {
                    asset.status = "queued";
                    MikuImportScheduler.Enqueue(
                        context.assetPath,
                        asset.bundleHash,
                        asset.outputRoot);
                }
            }
            catch (Exception ex)
            {
                asset.status = "invalid";
                asset.diagnostics.Add(ex.Message);
                context.LogImportError(ex.Message, asset);
                Debug.LogError(ex.Message, asset);
            }

            context.AddObjectToAsset("bundle", asset);
            context.SetMainObject(asset);
        }

        static JObject ParseBundle(string assetPath)
        {
            var absolute = ToAbsoluteProjectPath(assetPath);
            var info = new FileInfo(absolute);
            if (!info.Exists)
                throw new FileNotFoundException("MIKU_BUNDLE_MISSING", absolute);
            if (info.Length <= 0 || info.Length > MaxManifestBytes)
                throw new InvalidDataException("MIKU_BUNDLE_MANIFEST_SIZE_INVALID");
            using var reader = File.OpenText(absolute);
            using var json = new JsonTextReader(reader)
            {
                FloatParseHandling = FloatParseHandling.Double,
                MaxDepth = 128,
            };
            return JObject.Load(json);
        }

        static void RegisterDependencies(
            AssetImportContext context,
            JObject bundle,
            MikuBundleAsset asset)
        {
            var references = new List<JObject>();
            foreach (var role in new[] { "ir", "plan", "manifest", "sourceMap" })
                references.Add(bundle[role] as JObject
                    ?? throw new InvalidDataException(
                        "MIKU_ARTIFACT_REFERENCE_INVALID:" + role));
            references.AddRange(
                (bundle["resources"] as JArray ?? new JArray()).OfType<JObject>());
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in references)
            {
                var dependency = ResolveSiblingAssetPath(
                    context.assetPath,
                    RequireString(reference, "relativePath"));
                if (!seen.Add(dependency))
                    throw new InvalidDataException(
                        "MIKU_ARTIFACT_PATH_DUPLICATE:" + dependency);
                if (!File.Exists(ToAbsoluteProjectPath(dependency)))
                    throw new FileNotFoundException(
                        "MIKU_ARTIFACT_MISSING",
                        dependency);
                context.DependsOnSourceAsset(dependency);
                asset.dependencies.Add(dependency);
            }
        }

        static JObject ReadSiblingDocument(
            string bundlePath,
            JObject reference,
            string expectedKind)
        {
            if (reference == null)
                throw new InvalidDataException("MIKU_ARTIFACT_REFERENCE_INVALID");
            var assetPath = ResolveSiblingAssetPath(
                bundlePath,
                RequireString(reference, "relativePath"));
            using var reader = File.OpenText(ToAbsoluteProjectPath(assetPath));
            using var json = new JsonTextReader(reader)
            {
                FloatParseHandling = FloatParseHandling.Double,
                MaxDepth = 128,
            };
            var document = JObject.Load(json);
            var actualKind = RequireString(document, "documentKind");
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
                throw new InvalidDataException(
                    "MIKU_DOCUMENT_KIND_MISMATCH:" + expectedKind);
            RequireHeader(
                document,
                "schemaVersion",
                isLegacy ? legacyVersion : "1.0");
            if (document["version"] != null)
                throw new InvalidDataException("MIKU_LEGACY_VERSION_FIELD");
            if (isLegacy)
            {
                document["documentKind"] = expectedKind;
                document["schemaVersion"] = "1.0";
            }
            return document;
        }

        static string ResolveSiblingAssetPath(string bundlePath, string relative)
        {
            var normalized = (relative ?? "").Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized) ||
                normalized.StartsWith("/", StringComparison.Ordinal) ||
                Regex.IsMatch(normalized, "^[A-Za-z]:") ||
                normalized.Split('/').Any(
                    part => part.Length == 0 || part == "." || part == ".."))
                throw new InvalidDataException("MIKU_ARTIFACT_PATH_INVALID");
            var bundleRoot = Path.GetDirectoryName(bundlePath)?.Replace('\\', '/')
                ?? throw new InvalidDataException("MIKU_BUNDLE_ROOT_INVALID");
            var candidate = Path.GetFullPath(Path.Combine(
                ProjectRoot,
                bundleRoot.Replace('/', Path.DirectorySeparatorChar),
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            var root = Path.GetFullPath(Path.Combine(
                ProjectRoot,
                bundleRoot.Replace('/', Path.DirectorySeparatorChar)));
            RequireInside(root, candidate);
            var relativeToProject = Path.GetRelativePath(ProjectRoot, candidate)
                .Replace('\\', '/');
            if (!relativeToProject.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_ARTIFACT_OUTSIDE_ASSETS");
            return relativeToProject;
        }

        static string OutputRootFor(string bundleAssetPath)
        {
            var directory = Path.GetDirectoryName(bundleAssetPath)
                ?.Replace('\\', '/')
                ?.TrimEnd('/');
            if (string.IsNullOrEmpty(directory) ||
                !directory.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException("MIKU_OUTPUT_ROOT_INVALID");
            return directory + "/Generated";
        }

        static string RequireHeader(
            JObject document,
            string property,
            string expected)
        {
            var value = RequireString(document, property);
            if (!string.Equals(value, expected, StringComparison.Ordinal))
                throw new InvalidDataException(
                    "MIKU_UNKNOWN_SCHEMA:" + property + ":" + value);
            return value;
        }

        static string RequireOneOf(
            JObject document,
            string property,
            params string[] expected)
        {
            var value = RequireString(document, property);
            if (!expected.Contains(value, StringComparer.Ordinal))
                throw new InvalidDataException(
                    "MIKU_UNKNOWN_SCHEMA:" + property + ":" + value);
            return value;
        }

        static string RequireString(JObject document, string property)
        {
            var value = document?[property]?.Value<string>() ?? "";
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidDataException(
                    "MIKU_REQUIRED_FIELD_MISSING:" + property);
            return value;
        }

        static string RequireSha256(JObject document, string property)
        {
            var value = RequireString(document, property);
            if (!Sha256.IsMatch(value))
                throw new InvalidDataException("MIKU_SHA256_INVALID:" + property);
            return value;
        }

        static void RequireInside(string root, string path)
        {
            var prefix = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(path);
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("MIKU_PATH_ESCAPE");
        }

        static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.Combine(
                ProjectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("MIKU_PROJECT_ROOT_INVALID");
    }

    /// <summary>
    /// Read-only compatibility importer for sealed MiGR bundle assets.
    /// New exports and writes always use .mikubundle.
    /// </summary>
    [ScriptedImporter(1, "migrbundle")]
    public sealed class MikuLegacyBundleScriptedImporter :
        MikuBundleScriptedImporter
    {
    }

    [Serializable]
    internal sealed class MikuImportQueueDocument
    {
        public string schema = "miku-import-queue-1.0";
        public List<MikuImportQueueJob> jobs = new List<MikuImportQueueJob>();
    }

    [Serializable]
    internal sealed class MikuImportQueueJob
    {
        public string assetPath = "";
        public string bundleHash = "";
        public string outputRoot = "";
        public string status = "pending";
        public string receiptPath = "";
        public List<string> diagnostics = new List<string>();
    }

    [InitializeOnLoad]
    internal static class MikuImportScheduler
    {
        const int MaxQueueEntries = 1000;
        static readonly string JournalPath = Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? ".",
            "Library",
            "Miku",
            "ImportQueue.json");
        static MikuImportQueueDocument journal;
        static bool scheduled;
        static bool running;

        static MikuImportScheduler()
        {
            journal = Load();
            foreach (var job in journal.jobs.Where(
                         item => item.status == "processing"))
                job.status = "pending";
            Save();
            Schedule();
        }

        public static void Enqueue(
            string assetPath,
            string bundleHash,
            string outputRoot)
        {
            journal.jobs.RemoveAll(
                item => string.Equals(
                    item.assetPath,
                    assetPath,
                    StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        item.bundleHash,
                        bundleHash,
                        StringComparison.Ordinal));
            var job = journal.jobs.FirstOrDefault(
                item => string.Equals(
                    item.assetPath,
                    assetPath,
                    StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        item.bundleHash,
                        bundleHash,
                        StringComparison.Ordinal));
            if (job != null && job.status == "committed")
                return;
            if (job == null)
            {
                if (journal.jobs.Count >= MaxQueueEntries)
                    journal.jobs.RemoveAll(item => item.status == "committed");
                if (journal.jobs.Count >= MaxQueueEntries)
                    throw new InvalidDataException("MIKU_IMPORT_QUEUE_LIMIT");
                job = new MikuImportQueueJob
                {
                    assetPath = assetPath,
                    bundleHash = bundleHash,
                    outputRoot = outputRoot,
                };
                journal.jobs.Add(job);
            }
            job.outputRoot = outputRoot;
            job.status = "pending";
            job.receiptPath = "";
            job.diagnostics.Clear();
            Save();
            Schedule();
        }

        public static bool TryGetCommitted(
            string assetPath,
            string bundleHash,
            out string receiptPath)
        {
            var job = journal.jobs.FirstOrDefault(
                item => string.Equals(
                    item.assetPath,
                    assetPath,
                    StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        item.bundleHash,
                        bundleHash,
                        StringComparison.Ordinal) &&
                    item.status == "committed");
            receiptPath = job?.receiptPath ?? "";
            return job != null &&
                !string.IsNullOrEmpty(receiptPath) &&
                File.Exists(ToAbsoluteProjectPath(receiptPath));
        }

        static void Schedule()
        {
            if (scheduled)
                return;
            scheduled = true;
            EditorApplication.delayCall += Pump;
        }

        static void Pump()
        {
            scheduled = false;
            if (running)
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                Schedule();
                return;
            }
            var job = journal.jobs.FirstOrDefault(item => item.status == "pending");
            if (job == null)
                return;
            running = true;
            job.status = "processing";
            Save();
            try
            {
                var result = MikuBundleImporter.Import(new MikuImportRequest
                {
                    bundlePath = ToAbsoluteProjectPath(job.assetPath),
                    outputRoot = job.outputRoot,
                    fullRegeneration = false,
                    createMaterialVariant = true,
                });
                job.diagnostics = result.diagnostics.ToList();
                if (!result.success)
                    throw new InvalidDataException(
                        string.Join(" | ", result.diagnostics));
                job.status = "committed";
                job.receiptPath = result.receiptPath;
                Save();
                Debug.Log(
                    "MIKU_AUTO_IMPORT_COMMITTED:" + job.assetPath + ":" +
                    result.receiptPath);
                AssetDatabase.ImportAsset(
                    job.assetPath,
                    ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                job.status = "failed";
                if (!job.diagnostics.Contains(ex.Message))
                    job.diagnostics.Add(ex.Message);
                Save();
                Debug.LogError(
                    "MIKU_AUTO_IMPORT_FAILED:" + job.assetPath + ":" + ex.Message);
            }
            finally
            {
                running = false;
                if (journal.jobs.Any(item => item.status == "pending"))
                    Schedule();
            }
        }

        static MikuImportQueueDocument Load()
        {
            try
            {
                if (!File.Exists(JournalPath))
                    return new MikuImportQueueDocument();
                var value = JsonUtility.FromJson<MikuImportQueueDocument>(
                    File.ReadAllText(JournalPath));
                if (value == null ||
                    !string.Equals(
                        value.schema,
                        "miku-import-queue-1.0",
                        StringComparison.Ordinal))
                    throw new InvalidDataException("MIKU_IMPORT_QUEUE_SCHEMA_INVALID");
                value.jobs ??= new List<MikuImportQueueJob>();
                return value;
            }
            catch (Exception ex)
            {
                Debug.LogError("MIKU_IMPORT_QUEUE_LOAD_FAILED:" + ex.Message);
                return new MikuImportQueueDocument();
            }
        }

        static void Save()
        {
            journal.jobs = journal.jobs
                .OrderBy(item => item.assetPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.bundleHash, StringComparer.Ordinal)
                .ToList();
            MikuAtomicAssetWriter.WriteIfChanged(
                JournalPath,
                JsonUtility.ToJson(journal, true) + "\n");
        }

        static string ToAbsoluteProjectPath(string assetPath)
        {
            var root = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("MIKU_PROJECT_ROOT_INVALID");
            var absolute = Path.GetFullPath(Path.Combine(
                root,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("MIKU_PATH_ESCAPE");
            return absolute;
        }
    }
}
