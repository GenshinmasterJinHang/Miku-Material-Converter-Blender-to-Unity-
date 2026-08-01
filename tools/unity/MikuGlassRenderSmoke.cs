using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Miku.ShaderConverter.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class MikuGlassRenderSmoke
{
    static readonly List<UnityEngine.Object> TransientObjects =
        new List<UnityEngine.Object>();

    static Camera validationCamera;
    static RenderTexture renderTexture;
    static string imagePath;
    static string resultPath;
    static int updateCount;
    static bool completed;

    public static void Run()
    {
        try
        {
            var arguments = Environment.GetCommandLineArgs();
            var bundlePath = Argument(arguments, "--miku-bundle");
            imagePath = Argument(arguments, "--miku-image");
            resultPath = Argument(arguments, "--miku-result");
            var outputRoot = Argument(arguments, "--miku-output");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));

            var imported = MikuBundleImporter.Import(
                new MikuImportRequest
                {
                    bundlePath = bundlePath,
                    outputRoot = outputRoot,
                    fullRegeneration = true,
                    createMaterialVariant = true,
                });
            if (!imported.success)
                throw new InvalidDataException(
                    "MIKU_GLASS_SMOKE_IMPORT_FAILED:" +
                    string.Join("|", imported.diagnostics));
            var materialPath = imported.assetPaths.Single(path =>
                path.EndsWith(".mat", StringComparison.Ordinal) &&
                !path.EndsWith(".generated.mat", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                materialPath);
            if (material == null || material.shader == null ||
                !material.shader.isSupported)
            {
                throw new InvalidDataException(
                    "MIKU_GLASS_SMOKE_SHADER_UNAVAILABLE");
            }
            foreach (var property in new[]
                     {
                         "_IOR",
                         "_TransmissionWeight",
                         "_Opacity",
                         "_RefractionStrength",
                         "_ReflectionStrength",
                         "_Thickness",
                     })
            {
                if (!material.HasProperty(property))
                    throw new InvalidDataException(
                        "MIKU_GLASS_SMOKE_PROPERTY_MISSING:" + property);
            }
            if (Math.Abs(
                    material.GetFloat("_TransmissionWeight") - 1.0f) >
                0.0001f)
            {
                throw new InvalidDataException(
                    "MIKU_GLASS_SMOKE_TRANSMISSION_WEIGHT_INVALID");
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            BuildScene(material);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload |
                EnterPlayModeOptions.DisableSceneReload;
            RenderPipelineManager.endCameraRendering += OnCameraRendered;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    static void BuildScene(Material material)
    {
        var cameraObject = Track(
            new GameObject("Miku 1.2.1 Glass Camera"));
        validationCamera = cameraObject.AddComponent<Camera>();
        validationCamera.transform.position = new Vector3(0f, 0f, -5f);
        validationCamera.transform.rotation = Quaternion.identity;
        validationCamera.fieldOfView = 42f;
        validationCamera.nearClipPlane = 0.1f;
        validationCamera.farClipPlane = 30f;
        validationCamera.clearFlags = CameraClearFlags.SolidColor;
        validationCamera.backgroundColor =
            new Color(0.03f, 0.08f, 0.18f, 1f);
        var cameraData =
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.requiresColorTexture = true;
        renderTexture = Track(
            new RenderTexture(
                512,
                512,
                24,
                RenderTextureFormat.ARGB32));
        renderTexture.Create();
        validationCamera.targetTexture = renderTexture;

        var lightObject = Track(
            new GameObject("Miku 1.2.1 Main Directional Light"));
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.color = new Color(1f, 0.95f, 0.86f, 1f);
        lightObject.transform.rotation = Quaternion.Euler(35f, -25f, 0f);

        var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null)
            throw new InvalidDataException(
                "MIKU_GLASS_SMOKE_URP_UNLIT_MISSING");
        var colors = new[]
        {
            new Color(0.95f, 0.16f, 0.05f, 1f),
            new Color(0.08f, 0.78f, 0.22f, 1f),
            new Color(0.05f, 0.32f, 1.0f, 1f),
        };
        for (var index = 0; index < colors.Length; index++)
        {
            var backdrop = Track(
                GameObject.CreatePrimitive(PrimitiveType.Quad));
            backdrop.name = "Miku Backdrop " + index;
            backdrop.transform.position =
                new Vector3((index - 1) * 1.8f, 0f, 1.4f);
            backdrop.transform.localScale =
                new Vector3(2.0f, 5.0f, 1f);
            var backdropMaterial = Track(new Material(unlitShader));
            if (backdropMaterial.HasProperty("_BaseColor"))
                backdropMaterial.SetColor("_BaseColor", colors[index]);
            backdrop.GetComponent<Renderer>().sharedMaterial =
                backdropMaterial;
        }

        var sphere = Track(
            GameObject.CreatePrimitive(PrimitiveType.Sphere));
        sphere.name = "Miku Glass Sphere";
        sphere.transform.localScale = Vector3.one * 2.2f;
        var glass = Track(new Material(material));
        glass.SetFloat("_TransmissionWeight", 1.0f);
        glass.SetFloat("_Opacity", 1.0f);
        glass.SetFloat("_RefractionStrength", 0.05f);
        glass.SetFloat("_ReflectionStrength", 0.35f);
        sphere.GetComponent<Renderer>().sharedMaterial = glass;

        var probeObject = Track(
            new GameObject("Miku Reflection Probe"));
        var probe = probeObject.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
        probe.size = new Vector3(10f, 10f, 10f);
        probe.RenderProbe();
    }

    static void OnCameraRendered(
        ScriptableRenderContext context,
        Camera camera)
    {
        if (completed || camera != validationCamera || Time.frameCount < 3)
            return;
        completed = true;
        var prior = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = renderTexture;
            image = new Texture2D(
                512,
                512,
                TextureFormat.RGBA32,
                false,
                false);
            image.ReadPixels(new Rect(0f, 0f, 512f, 512f), 0, 0);
            image.Apply();
            File.WriteAllBytes(imagePath, image.EncodeToPNG());
            var pixels = image.GetPixels32();
            double sum = 0.0;
            var visible = 0;
            var count = 0;
            for (var y = 156; y < 356; y++)
            {
                for (var x = 156; x < 356; x++)
                {
                    var dx = x - 256;
                    var dy = y - 256;
                    if (dx * dx + dy * dy > 95 * 95)
                        continue;
                    var pixel = pixels[y * 512 + x];
                    var luminance =
                        (0.2126 * pixel.r +
                         0.7152 * pixel.g +
                         0.0722 * pixel.b) / 255.0;
                    sum += luminance;
                    if (luminance > 0.05)
                        visible++;
                    count++;
                }
            }
            var mean = sum / Math.Max(count, 1);
            var ratio = (double)visible / Math.Max(count, 1);
            var passed = mean > 0.05 && ratio > 0.5;
            File.WriteAllText(
                resultPath,
                "{\n" +
                "  \"schema\": \"miku-glass-render-smoke-1.0\",\n" +
                "  \"passed\": " +
                passed.ToString().ToLowerInvariant() + ",\n" +
                "  \"centerMean\": " +
                mean.ToString("0.000000", CultureInfo.InvariantCulture) +
                ",\n" +
                "  \"centerVisibleRatio\": " +
                ratio.ToString("0.000000", CultureInfo.InvariantCulture) +
                ",\n" +
                "  \"colorSpace\": \"" +
                QualitySettings.activeColorSpace + "\"\n" +
                "}\n");
            EditorApplication.delayCall += () => Finish(passed ? 0 : 3);
        }
        catch (Exception error)
        {
            Fail(error);
        }
        finally
        {
            RenderTexture.active = prior;
            if (image != null)
                UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void OnEditorUpdate()
    {
        updateCount++;
        if (!completed && updateCount > 1200)
            Fail(new TimeoutException("MIKU_GLASS_SMOKE_TIMEOUT"));
    }

    static void Finish(int exitCode)
    {
        RenderPipelineManager.endCameraRendering -= OnCameraRendered;
        EditorApplication.update -= OnEditorUpdate;
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
        if (validationCamera != null)
            validationCamera.targetTexture = null;
        foreach (var item in TransientObjects.AsEnumerable().Reverse())
        {
            if (item != null)
                UnityEngine.Object.DestroyImmediate(item);
        }
        TransientObjects.Clear();
        EditorApplication.Exit(exitCode);
    }

    static void Fail(Exception error)
    {
        try
        {
            if (!string.IsNullOrEmpty(resultPath))
            {
                Directory.CreateDirectory(
                    Path.GetDirectoryName(resultPath));
                File.WriteAllText(
                    resultPath,
                    "{\n" +
                    "  \"schema\": \"miku-glass-render-smoke-1.0\",\n" +
                    "  \"passed\": false,\n" +
                    "  \"error\": " +
                    Newtonsoft.Json.JsonConvert.SerializeObject(
                        error.GetBaseException().Message) +
                    "\n}\n");
            }
        }
        finally
        {
            Debug.LogException(error);
            EditorApplication.delayCall += () => Finish(2);
        }
    }

    static string Argument(string[] arguments, string name)
    {
        var index = Array.IndexOf(arguments, name);
        if (index < 0 || index + 1 >= arguments.Length)
            throw new ArgumentException(
                "MIKU_GLASS_SMOKE_ARGUMENT_MISSING:" + name);
        return arguments[index + 1];
    }

    static T Track<T>(T item) where T : UnityEngine.Object
    {
        TransientObjects.Add(item);
        return item;
    }
}
