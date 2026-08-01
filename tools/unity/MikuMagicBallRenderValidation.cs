using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Miku.ShaderConverter.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Imports and renders the real Miku 2.0.3 Magic Ball corpus in a deterministic
/// Unity 6000.4/URP 17.4 editor-time validation scene.
/// </summary>
public static class MikuMagicBallRenderValidation
{
    const int RenderSize = 512;
    const string GeneratedRoot = "Assets/M203";
    const string PipelineAssetPath = "Assets/Settings/PC_RPAsset.asset";
    static RenderPipelineAsset previousDefaultPipeline;
    static RenderPipelineAsset previousQualityPipeline;

    sealed class RenderCase
    {
        public string name;
        public string bundlePath;
        public string blenderImage;
    }

    static readonly List<Object> TransientObjects = new List<Object>();
    static List<RenderCase> activeCases;
    static Dictionary<string, Material> activeMaterials;
    static SceneState activeScene;
    static JArray importResults;
    static JArray renderResults;
    static string activeImageRoot;
    static string activeResultPath;
    static int currentCaseIndex;
    static double nextRenderTime;
    static double renderStartTime;
    static bool completed;

    public static void Run()
    {
        string resultPath = null;
        try
        {
            var arguments = Environment.GetCommandLineArgs();
            var manifestPath = Argument(arguments, "--miku-manifest");
            var imageRoot = Argument(arguments, "--miku-image-root");
            resultPath = Argument(arguments, "--miku-result");
            var modelAssetPath = Argument(arguments, "--miku-model-asset");
            Directory.CreateDirectory(imageRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));

            var manifest = JObject.Parse(
                File.ReadAllText(manifestPath));
            var cases = manifest["cases"]
                .Children<JObject>()
                .Select(item => new RenderCase
                {
                    name = item.Value<string>("name"),
                    bundlePath = item.Value<string>("bundlePath"),
                    blenderImage = item.Value<string>("blenderImage"),
                })
                .ToList();
            if (cases.Count != 11)
                throw new InvalidDataException(
                    "MIKU_MAGIC_BALL_RENDER_CASE_COUNT:" + cases.Count);

            ConfigurePipeline();
            EnsureFolder(GeneratedRoot);
            var materials = new Dictionary<string, Material>(
                StringComparer.Ordinal);
            var imports = new JArray();
            var importIndex = 0;
            foreach (var item in cases.Concat(
                         manifest["importOnly"]
                             .Children<JObject>()
                             .Select(value => new RenderCase
                             {
                                 name = value.Value<string>("name"),
                                 bundlePath =
                                     value.Value<string>("bundlePath"),
                             })))
            {
                var imported = MikuBundleImporter.Import(
                    new MikuImportRequest
                    {
                        bundlePath = item.bundlePath,
                        outputRoot = GeneratedRoot + "/c" +
                            importIndex.ToString(
                                "00",
                                CultureInfo.InvariantCulture),
                        fullRegeneration = true,
                        createMaterialVariant = true,
                    });
                imports.Add(JObject.FromObject(
                    new
                    {
                        item.name,
                        imported.success,
                        imported.diagnostics,
                        imported.assetPaths,
                    }));
                if (!imported.success)
                    throw new InvalidDataException(
                        "MIKU_MAGIC_BALL_IMPORT_FAILED:" +
                        item.name + ":" +
                        string.Join("|", imported.diagnostics));
                if (item.blenderImage == null)
                {
                    importIndex++;
                    continue;
                }
                var materialPath = imported.assetPaths.Single(path =>
                    path.EndsWith(
                        ".mat",
                        StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(
                        ".generated.mat",
                        StringComparison.OrdinalIgnoreCase));
                var material =
                    AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader == null ||
                    !material.shader.isSupported)
                {
                    throw new InvalidDataException(
                        "MIKU_MAGIC_BALL_SHADER_UNAVAILABLE:" +
                        item.name);
                }
                materials.Add(item.name, material);
                importIndex++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var model =
                AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            if (model == null)
                throw new InvalidDataException(
                    "MIKU_MAGIC_BALL_MODEL_MISSING:" + modelAssetPath);

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            foreach (var item in cases)
            {
                var material = materials[item.name];
                var compilerErrors = ShaderUtil
                    .GetShaderMessages(material.shader)
                    .Where(message =>
                        message.severity ==
                        ShaderCompilerMessageSeverity.Error)
                    .Select(message => message.message)
                    .ToArray();
                if (compilerErrors.Length > 0)
                {
                    throw new InvalidDataException(
                        "MIKU_MAGIC_BALL_SHADER_COMPILE_FAILED:" +
                        item.name + ":" +
                        string.Join("|", compilerErrors));
                }
            }
            activeCases = cases;
            activeMaterials = materials;
            activeScene = BuildScene(model);
            importResults = imports;
            renderResults = new JArray();
            activeImageRoot = imageRoot;
            activeResultPath = resultPath;
            currentCaseIndex = 0;
            nextRenderTime = 0.0;
            renderStartTime = EditorApplication.timeSinceStartup;
            completed = false;
            activeScene.renderer.sharedMaterial =
                activeMaterials[activeCases[0].name];
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions =
                EnterPlayModeOptions.DisableDomainReload |
                EnterPlayModeOptions.DisableSceneReload;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.isPlaying = true;
        }
        catch (Exception error)
        {
            activeResultPath = resultPath;
            Fail(error);
        }
    }

    static void CaptureCurrentCase()
    {
        if (completed || activeScene == null)
            return;
        try
        {
            var item = activeCases[currentCaseIndex];
            var material = activeMaterials[item.name];
            var imagePath = Path.Combine(
                activeImageRoot,
                item.name + ".png");
            var statistics = Capture(
                activeScene.renderTexture,
                imagePath);
            var caseResult = new JObject
            {
                ["name"] = item.name,
                ["blenderImage"] = item.blenderImage,
                ["unityImage"] = Path.GetFullPath(imagePath),
                ["shader"] = material.shader.name,
                ["shaderCompilerErrors"] = new JArray(),
                ["materialDiagnostics"] =
                    CaptureMaterialDiagnostics(material),
                ["statistics"] = statistics,
            };
            if (currentCaseIndex == 9)
            {
                caseResult["wireframeDiagnostic"] =
                    CaptureWhiteBakedTextureDiagnostic(material);
                caseResult["generatedShader"] =
                    WriteGeneratedShaderDiagnostic(material);
            }
            renderResults.Add(caseResult);
            currentCaseIndex++;
            if (currentCaseIndex >= activeCases.Count)
            {
                completed = true;
                EditorApplication.delayCall += FinishSuccess;
                return;
            }
            activeScene.renderer.sharedMaterial =
                activeMaterials[activeCases[currentCaseIndex].name];
            nextRenderTime = EditorApplication.timeSinceStartup + 0.1;
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    static JObject CaptureMaterialDiagnostics(Material material)
    {
        var bakedTextures = new JArray();
        for (var index = 0; index < material.shader.GetPropertyCount(); index++)
        {
            if (material.shader.GetPropertyType(index) !=
                    ShaderPropertyType.Texture)
                continue;
            var referenceName = material.shader.GetPropertyName(index);
            if (!referenceName.StartsWith(
                    "_MIKU_Baked_",
                    StringComparison.Ordinal))
                continue;
            var texture = material.GetTexture(referenceName);
            bakedTextures.Add(
                new JObject
                {
                    ["referenceName"] = referenceName,
                    ["texture"] = texture == null ? null : texture.name,
                    ["width"] = texture == null ? 0 : texture.width,
                    ["height"] = texture == null ? 0 : texture.height,
                    ["gpuStatistics"] =
                        texture == null
                            ? null
                            : CaptureTextureStatistics(texture),
                });
        }
        return new JObject
        {
            ["renderQueue"] = material.renderQueue,
            ["passCount"] = material.passCount,
            ["baseColor"] =
                material.HasProperty("_BaseColor")
                    ? ColorArray(material.GetColor("_BaseColor"))
                    : null,
            ["emissionColor"] =
                material.HasProperty("_EmissionColor")
                    ? ColorArray(material.GetColor("_EmissionColor"))
                    : null,
            ["emissionStrength"] =
                material.HasProperty("_EmissionStrength")
                    ? material.GetFloat("_EmissionStrength")
                    : 0f,
            ["bakedTextures"] = bakedTextures,
        };
    }

    static JArray ColorArray(Color color)
    {
        return new JArray(color.r, color.g, color.b, color.a);
    }

    static JObject CaptureWhiteBakedTextureDiagnostic(Material material)
    {
        var references = new List<string>();
        var originals = new List<Texture>();
        for (var index = 0; index < material.shader.GetPropertyCount(); index++)
        {
            if (material.shader.GetPropertyType(index) !=
                    ShaderPropertyType.Texture)
                continue;
            var referenceName = material.shader.GetPropertyName(index);
            if (!referenceName.StartsWith(
                    "_MIKU_Baked_",
                    StringComparison.Ordinal))
                continue;
            references.Add(referenceName);
            originals.Add(material.GetTexture(referenceName));
            material.SetTexture(referenceName, Texture2D.whiteTexture);
        }
        try
        {
            activeScene.camera.Render();
            var path = Path.Combine(
                activeImageRoot,
                "magic-ball-10-white-baked-diagnostic.png");
            return new JObject
            {
                ["image"] = Path.GetFullPath(path),
                ["statistics"] = Capture(activeScene.renderTexture, path),
            };
        }
        finally
        {
            for (var index = 0; index < references.Count; index++)
                material.SetTexture(references[index], originals[index]);
            activeScene.camera.Render();
        }
    }

    static string WriteGeneratedShaderDiagnostic(Material material)
    {
        var graphPath = AssetDatabase.GetAssetPath(material.shader);
        var importer = AssetImporter.GetAtPath(graphPath);
        var getShaderText = importer
            .GetType()
            .GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic)
            .Single(candidate =>
            {
                if (!string.Equals(
                        candidate.Name,
                        "GetShaderText",
                        StringComparison.Ordinal))
                    return false;
                var parameters = candidate.GetParameters();
                return parameters.Length == 4 &&
                       parameters[1].IsOut &&
                       parameters[3].IsOut;
            });
        var arguments = new object[]
        {
            graphPath,
            null,
            null,
            null,
        };
        var generatedShader =
            (string)getShaderText.Invoke(null, arguments);
        var outputPath = Path.Combine(
            activeImageRoot,
            "magic-ball-10.generated.shader");
        File.WriteAllText(outputPath, generatedShader);
        return Path.GetFullPath(outputPath);
    }

    static JObject CaptureTextureStatistics(Texture texture)
    {
        const int sampleSize = 128;
        var renderTexture = RenderTexture.GetTemporary(
            sampleSize,
            sampleSize,
            0,
            RenderTextureFormat.ARGBFloat,
            RenderTextureReadWrite.Linear);
        var readable = new Texture2D(
            sampleSize,
            sampleSize,
            TextureFormat.RGBAFloat,
            false,
            true);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, renderTexture);
            RenderTexture.active = renderTexture;
            readable.ReadPixels(
                new Rect(0, 0, sampleSize, sampleSize),
                0,
                0,
                false);
            readable.Apply(false, false);
            var red = readable.GetPixels().Select(color => color.r).ToArray();
            return new JObject
            {
                ["redMinimum"] = red.Min(),
                ["redMaximum"] = red.Max(),
                ["redMean"] = red.Average(),
                ["redAboveOnePercent"] =
                    red.Count(value => value > 0.01f) / (double)red.Length,
            };
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(readable);
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    static void OnEditorUpdate()
    {
        if (!completed &&
            EditorApplication.isPlaying &&
            activeScene != null &&
            EditorApplication.timeSinceStartup >= nextRenderTime)
        {
            nextRenderTime = EditorApplication.timeSinceStartup + 0.25;
            activeScene.camera.Render();
            CaptureCurrentCase();
        }
        if (!completed &&
            EditorApplication.timeSinceStartup - renderStartTime > 120.0)
        {
            Fail(new TimeoutException(
                "MIKU_MAGIC_BALL_RENDER_TIMEOUT"));
        }
    }

    static void FinishSuccess()
    {
        try
        {
            var one = renderResults
                .Children<JObject>()
                .Single(item =>
                    item.Value<string>("name") == "魔法球1");
            var five = renderResults
                .Children<JObject>()
                .Single(item =>
                    item.Value<string>("name") == "魔法球5");
            var oneStats = (JObject)one["statistics"];
            var fiveStats = (JObject)five["statistics"];
            var nine = (JObject)renderResults[8];
            var ten = (JObject)renderResults[9];
            var tenPointOne = (JObject)renderResults[10];
            var nineStats = (JObject)nine["statistics"];
            var tenStats = (JObject)ten["statistics"];
            var tenPointOneStats = (JObject)tenPointOne["statistics"];
            var tenBakedTextures =
                (JArray)ten["materialDiagnostics"]["bakedTextures"];
            var rgbDistance = RgbDistance(oneStats, fiveStats);
            var acceptance = new JObject
            {
                ["magicBall1NotBlack"] =
                    oneStats.Value<double>("centerMeanLuminance") > 0.02,
                ["magicBall5NotBlack"] =
                    fiveStats.Value<double>("centerMeanLuminance") > 0.02,
                ["magicBall1HasVariation"] =
                    oneStats.Value<double>("centerLuminanceVariance") >
                    0.0001,
                ["magicBall5HasVariation"] =
                    fiveStats.Value<double>("centerLuminanceVariance") >
                    0.0001,
                ["magicBall1And5MeanRgbDistance"] = rgbDistance,
                ["magicBall1And5Distinct"] = rgbDistance > 0.02,
                ["magicBall9NotBlack"] =
                    nineStats.Value<double>("centerMeanLuminance") > 0.01,
                ["magicBall9HasNormalVariation"] =
                    nineStats.Value<double>("centerLuminanceVariance") >
                    0.0001,
                ["magicBall10NotBlack"] =
                    tenStats.Value<double>("centerMeanLuminance") > 0.005,
                ["magicBall10HasWireframeVariation"] =
                    tenStats.Value<double>("centerLuminanceVariance") >
                    0.00001,
                ["magicBall10BakedTextureBound"] =
                    tenBakedTextures.Count > 0 &&
                    tenBakedTextures
                        .Children<JObject>()
                        .All(texture =>
                            !string.IsNullOrEmpty(
                                texture.Value<string>("texture")) &&
                            texture["gpuStatistics"]
                                .Value<double>("redMaximum") > 0.01),
                ["magicBall10PointOneNotBlack"] =
                    tenPointOneStats
                        .Value<double>("centerMeanLuminance") > 0.01,
            };
            if (acceptance.Properties().Any(property =>
                    property.Value.Type == JTokenType.Boolean &&
                    !property.Value.Value<bool>()))
            {
                throw new InvalidDataException(
                    "MIKU_MAGIC_BALL_UNITY_VISUAL_ACCEPTANCE_FAILED:" +
                    acceptance.ToString(Formatting.None));
            }
            var result = new JObject
            {
                ["schema"] =
                    "miku-magic-ball-unity-validation-1.0",
                ["success"] = true,
                ["unityVersion"] = Application.unityVersion,
                ["renderPipeline"] =
                    GraphicsSettings.currentRenderPipeline == null
                        ? "BuiltIn"
                        : GraphicsSettings.currentRenderPipeline
                            .GetType().FullName,
                ["colorSpace"] =
                    QualitySettings.activeColorSpace.ToString(),
                ["graphicsDeviceType"] =
                    SystemInfo.graphicsDeviceType.ToString(),
                ["resolution"] = new JArray(RenderSize, RenderSize),
                ["imports"] = importResults,
                ["renders"] = renderResults,
                ["acceptance"] = acceptance,
            };
            File.WriteAllText(
                activeResultPath,
                result.ToString(Formatting.Indented) + "\n");
            Finish(0);
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    static void Fail(Exception error)
    {
        completed = true;
        if (!string.IsNullOrEmpty(activeResultPath))
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(activeResultPath));
            File.WriteAllText(
                activeResultPath,
                JsonConvert.SerializeObject(
                    new
                    {
                        schema =
                            "miku-magic-ball-unity-validation-1.0",
                        success = false,
                        error = error.GetBaseException().Message,
                        imports = importResults,
                        renders = renderResults,
                    },
                    Formatting.Indented) + "\n");
        }
        Debug.LogException(error);
        EditorApplication.delayCall += () => Finish(2);
    }

    static void Finish(int exitCode)
    {
        EditorApplication.update -= OnEditorUpdate;
        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
        Cleanup(activeScene);
        EditorApplication.Exit(exitCode);
    }

    sealed class SceneState
    {
        public Camera camera;
        public Renderer renderer;
        public RenderTexture renderTexture;
    }

    static SceneState BuildScene(GameObject model)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(
            0.055f,
            0.055f,
            0.055f,
            1f);

        var sphere = Track(
            PrefabUtility.InstantiatePrefab(model) as GameObject);
        if (sphere == null)
            throw new InvalidDataException(
                "MIKU_MAGIC_BALL_MODEL_INSTANTIATION_FAILED");
        sphere.name = "Miku Magic Ball Validation Sphere";
        sphere.transform.position = Vector3.zero;
        sphere.transform.rotation = Quaternion.identity;
        var renderer = sphere.GetComponentInChildren<Renderer>();
        if (renderer == null)
            throw new InvalidDataException(
                "MIKU_MAGIC_BALL_MODEL_RENDERER_MISSING");

        var cameraObject =
            Track(new GameObject("Miku Validation Camera"));
        var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0.25f, -4.8f);
        camera.transform.rotation = Quaternion.LookRotation(
            Vector3.zero - camera.transform.position,
            Vector3.up);
        camera.fieldOfView = 36.87f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 30f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor =
            new Color(0.055f, 0.055f, 0.055f, 1f);
        var cameraData =
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.requiresColorTexture = true;
        cameraData.requiresDepthTexture = true;
        var renderTexture = Track(
            new RenderTexture(
                RenderSize,
                RenderSize,
                24,
                RenderTextureFormat.ARGB32));
        renderTexture.Create();
        camera.targetTexture = renderTexture;

        var ground = Track(
            GameObject.CreatePrimitive(PrimitiveType.Plane));
        ground.name = "Miku Validation Ground";
        ground.transform.position = new Vector3(0f, -1.3f, 0f);
        ground.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
        var litShader =
            Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
            throw new InvalidDataException(
                "MIKU_MAGIC_BALL_URP_LIT_MISSING");
        var groundMaterial = Track(new Material(litShader));
        if (groundMaterial.HasProperty("_BaseColor"))
        {
            groundMaterial.SetColor(
                "_BaseColor",
                new Color(0.18f, 0.18f, 0.18f, 1f));
        }
        if (groundMaterial.HasProperty("_Smoothness"))
            groundMaterial.SetFloat("_Smoothness", 0.45f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        AddDirectional(
            "Miku Key",
            new Vector3(35f, 0f, -30f),
            2.6f,
            new Color(1f, 0.92f, 0.82f, 1f));
        AddDirectional(
            "Miku Fill",
            new Vector3(55f, 0f, 145f),
            0.8f,
            new Color(0.62f, 0.76f, 1f, 1f));

        return new SceneState
        {
            camera = camera,
            renderer = renderer,
            renderTexture = renderTexture,
        };
    }

    static void ConfigurePipeline()
    {
        previousDefaultPipeline =
            GraphicsSettings.defaultRenderPipeline;
        previousQualityPipeline = QualitySettings.renderPipeline;
        var pipeline =
            AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(
                PipelineAssetPath);
        if (pipeline == null)
            throw new InvalidDataException(
                "MIKU_MAGIC_BALL_PIPELINE_MISSING:" +
                PipelineAssetPath);
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }

    static void AddDirectional(
        string name,
        Vector3 rotation,
        float intensity,
        Color color)
    {
        var lightObject = Track(new GameObject(name));
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        lightObject.transform.rotation = Quaternion.Euler(rotation);
    }

    static JObject Capture(
        RenderTexture renderTexture,
        string imagePath)
    {
        var prior = RenderTexture.active;
        Texture2D image = null;
        try
        {
            RenderTexture.active = renderTexture;
            image = new Texture2D(
                RenderSize,
                RenderSize,
                TextureFormat.RGBA32,
                false,
                false);
            image.ReadPixels(
                new Rect(0, 0, RenderSize, RenderSize),
                0,
                0);
            image.Apply();
            var pixels = image.GetPixels32();
            var displayPixels = pixels.ToArray();
            for (var index = 0; index < displayPixels.Length; index++)
            {
                var pixel = displayPixels[index];
                displayPixels[index] = new Color32(
                    (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(
                            Mathf.LinearToGammaSpace(pixel.r / 255f)) *
                        255f),
                    (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(
                            Mathf.LinearToGammaSpace(pixel.g / 255f)) *
                        255f),
                    (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(
                            Mathf.LinearToGammaSpace(pixel.b / 255f)) *
                        255f),
                    pixel.a);
            }
            image.SetPixels32(displayPixels);
            image.Apply();
            File.WriteAllBytes(imagePath, image.EncodeToPNG());
            var radius = RenderSize * 0.205;
            var radiusSquared = radius * radius;
            var rgbSum = new double[3];
            double luminanceSum = 0;
            double luminanceSquaredSum = 0;
            var count = 0;
            for (var y = 0; y < RenderSize; y++)
            {
                var dy = y + 0.5 - RenderSize * 0.5;
                for (var x = 0; x < RenderSize; x++)
                {
                    var dx = x + 0.5 - RenderSize * 0.5;
                    if (dx * dx + dy * dy > radiusSquared)
                        continue;
                    var pixel = pixels[y * RenderSize + x];
                    var red = pixel.r / 255.0;
                    var green = pixel.g / 255.0;
                    var blue = pixel.b / 255.0;
                    var luminance =
                        0.2126 * red +
                        0.7152 * green +
                        0.0722 * blue;
                    rgbSum[0] += red;
                    rgbSum[1] += green;
                    rgbSum[2] += blue;
                    luminanceSum += luminance;
                    luminanceSquaredSum += luminance * luminance;
                    count++;
                }
            }
            var mean = luminanceSum / Math.Max(count, 1);
            var variance =
                luminanceSquaredSum / Math.Max(count, 1) -
                mean * mean;
            return new JObject
            {
                ["centerMeanRgb"] = new JArray(
                    rgbSum.Select(value =>
                        Math.Round(
                            value / Math.Max(count, 1),
                            8))),
                ["centerMeanLuminance"] = Math.Round(mean, 8),
                ["centerLuminanceVariance"] =
                    Math.Round(Math.Max(variance, 0), 8),
            };
        }
        finally
        {
            RenderTexture.active = prior;
            if (image != null)
                Object.DestroyImmediate(image);
        }
    }

    static double RgbDistance(JObject left, JObject right)
    {
        var leftRgb = left["centerMeanRgb"].Values<double>().ToArray();
        var rightRgb = right["centerMeanRgb"].Values<double>().ToArray();
        return Math.Round(
            Math.Sqrt(
                leftRgb.Zip(
                        rightRgb,
                        (a, b) => (a - b) * (a - b))
                    .Sum()),
            8);
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var index = 1; index < parts.Length; index++)
        {
            var next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }

    static string Argument(string[] arguments, string name)
    {
        var normalized = name.TrimStart('-');
        var index = Array.FindIndex(
            arguments,
            value => string.Equals(
                value.TrimStart('-'),
                normalized,
                StringComparison.Ordinal));
        if (index < 0 || index + 1 >= arguments.Length)
            throw new ArgumentException(
                "MIKU_MAGIC_BALL_ARGUMENT_MISSING:" + name);
        return arguments[index + 1];
    }

    static T Track<T>(T item) where T : Object
    {
        if (item != null)
            TransientObjects.Add(item);
        return item;
    }

    static void Cleanup(SceneState scene)
    {
        QualitySettings.renderPipeline = previousQualityPipeline;
        GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
        if (scene != null && scene.camera != null)
            scene.camera.targetTexture = null;
        foreach (var item in TransientObjects.AsEnumerable().Reverse())
        {
            if (item != null)
                Object.DestroyImmediate(item);
        }
        TransientObjects.Clear();
    }
}
