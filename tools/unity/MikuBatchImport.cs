using System;
using System.Collections.Generic;
using System.IO;
using Miku.ShaderConverter.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MikuBatchImport
{
    public static void Run()
    {
        var arguments = Environment.GetCommandLineArgs();
        var resultPath = Argument(arguments, "--miku-result");
        var results = new List<MikuImportResult>();
        try
        {
            if (Array.IndexOf(
                    arguments,
                    "--miku-create-test-pipeline") >= 0)
                EnsureTestPipeline();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (!string.Equals(
                        arguments[index],
                        "--miku-bundle",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (index + 3 >= arguments.Length ||
                    !string.Equals(
                        arguments[index + 2],
                        "--miku-output",
                        StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "MIKU_BATCH_IMPORT_PAIR_INVALID");
                }
                var imported = MikuBundleImporter.Import(
                    new MikuImportRequest
                    {
                        bundlePath = arguments[index + 1],
                        outputRoot = arguments[index + 3],
                        fullRegeneration = true,
                        createMaterialVariant = true,
                    });
                results.Add(imported);
                if (!imported.success)
                    throw new InvalidDataException(
                        "MIKU_BATCH_IMPORT_FAILED:" +
                        string.Join("|", imported.diagnostics));
                index += 3;
            }
            if (results.Count == 0)
                throw new ArgumentException(
                    "MIKU_BATCH_IMPORT_EMPTY");
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(
                resultPath,
                JsonConvert.SerializeObject(
                    new
                    {
                        schema = "miku-batch-import-result-1.0",
                        success = true,
                        imports = results,
                    },
                    Formatting.Indented) + "\n");
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            File.WriteAllText(
                resultPath,
                JsonConvert.SerializeObject(
                    new
                    {
                        schema = "miku-batch-import-result-1.0",
                        success = false,
                        error = error.GetBaseException().Message,
                        imports = results,
                    },
                    Formatting.Indented) + "\n");
            UnityEngine.Debug.LogException(error);
            EditorApplication.Exit(2);
        }
    }

    static void EnsureTestPipeline()
    {
        if (GraphicsSettings.defaultRenderPipeline is
            UniversalRenderPipelineAsset)
            return;
        const string path = "Assets/MikuE2E/ValidationPipeline.asset";
        if (!AssetDatabase.IsValidFolder("Assets/MikuE2E"))
            AssetDatabase.CreateFolder("Assets", "MikuE2E");
        var pipeline = AssetDatabase.LoadAssetAtPath<
            UniversalRenderPipelineAsset>(path);
        if (pipeline == null)
        {
            pipeline = UniversalRenderPipelineAsset.Create();
            AssetDatabase.CreateAsset(pipeline, path);
            var serialized = new SerializedObject(pipeline);
            var rendererDataList =
                serialized.FindProperty("m_RendererDataList");
            for (var index = 0;
                 rendererDataList != null &&
                 index < rendererDataList.arraySize;
                 index++)
            {
                var rendererData = rendererDataList
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue;
                if (rendererData != null &&
                    !AssetDatabase.Contains(rendererData))
                    AssetDatabase.AddObjectToAsset(
                        rendererData,
                        pipeline);
            }
            AssetDatabase.SaveAssets();
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }

    static string Argument(string[] arguments, string name)
    {
        var index = Array.IndexOf(arguments, name);
        if (index < 0 || index + 1 >= arguments.Length)
            throw new ArgumentException(
                "MIKU_BATCH_IMPORT_ARGUMENT_MISSING:" + name);
        return arguments[index + 1];
    }
}
