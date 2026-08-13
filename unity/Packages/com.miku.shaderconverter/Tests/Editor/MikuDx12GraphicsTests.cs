// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Editor.Tests
{
    [Category("MikuGpuAcceptance")]
    public sealed class MikuDx12GraphicsTests
    {
        const string Direct3D12Required = "MIKU_D3D12_REQUIRED";

        [Test]
        public void GraphicsAcceptanceRunsOnDirect3D12()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                Assert.Ignore(Direct3D12Required);
            Assert.That(
                SystemInfo.graphicsDeviceType,
                Is.EqualTo(GraphicsDeviceType.Direct3D12),
                Direct3D12Required);
        }

        [Test]
        public void ZeroWidthVertexMaskAndNonFiniteCoverageProduceNoOutlinePixels()
        {
            RequireDirect3D12();
            var fullMaskMesh = CreateBackShellMesh(1f);
            var zeroMaskMesh = CreateBackShellMesh(0f);
            var material = CreateGenshinFaceMaterial();
            try
            {
                material.SetFloat("_OutlineWidth", 0.05f);
                material.SetFloat("_OutlineReferenceDistance", 5f);
                material.SetFloat("_OutlineDistanceScale", 1f);
                Assert.That(
                    RenderOutlinePixels(
                        material,
                        fullMaskMesh,
                        "MikuToonOutline"),
                    Is.GreaterThan(0),
                    "The positive control must render the inverted shell.");

                material.SetFloat("_OutlineWidth", 0f);
                Assert.That(
                    RenderOutlinePixels(
                        material,
                        fullMaskMesh,
                        "MikuToonOutline"),
                    Is.Zero);

                material.SetFloat("_OutlineWidth", 0.05f);
                Assert.That(
                    RenderOutlinePixels(
                        material,
                        zeroMaskMesh,
                        "MikuToonOutline"),
                    Is.Zero);

                foreach (var propertyName in new[]
                {
                    "_OutlineWidth",
                    "_OutlineReferenceDistance",
                    "_OutlineDistanceScale",
                })
                {
                    foreach (var nonFinite in new[]
                    {
                        float.NaN,
                        float.PositiveInfinity,
                        float.NegativeInfinity,
                    })
                    {
                        material.SetFloat("_OutlineWidth", 0.05f);
                        material.SetFloat("_OutlineReferenceDistance", 5f);
                        material.SetFloat("_OutlineDistanceScale", 1f);
                        material.SetFloat(propertyName, nonFinite);
                        Assert.That(
                            RenderOutlinePixels(
                                material,
                                fullMaskMesh,
                                "MikuToonOutline"),
                            Is.Zero,
                            propertyName + "=" + nonFinite);
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(fullMaskMesh);
                UnityEngine.Object.DestroyImmediate(zeroMaskMesh);
            }
        }

        [Test]
        public void ZeroEndfieldTextureMaskAndDisabledStateProduceNoOutlinePixels()
        {
            RequireDirect3D12();
            var mesh = CreateBackShellMesh(1f);
            var shader = Shader.Find("MIKU/Endfield/Face");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            try
            {
                material.SetTexture("_BaseMap", Texture2D.whiteTexture);
                material.SetColor("_OutlineColorTint", Color.white);
                material.SetFloat("_OutlineGamma", 1f);
                material.SetFloat("_OutlineWidth", 0.05f);
                material.SetFloat("_UseOutline", 1f);
                material.SetFloat("_UseOutlineMask", 1f);
                material.SetTexture("_OutlineMask", Texture2D.whiteTexture);
                Assert.That(
                    RenderOutlinePixels(material, mesh, "Outline"),
                    Is.GreaterThan(0),
                    "The Endfield positive control must render the shell.");

                material.SetTexture("_OutlineMask", Texture2D.blackTexture);
                Assert.That(
                    RenderOutlinePixels(material, mesh, "Outline"),
                    Is.Zero);

                material.SetTexture("_OutlineMask", Texture2D.whiteTexture);
                material.SetFloat("_UseOutline", 0f);
                Assert.That(
                    RenderOutlinePixels(material, mesh, "Outline"),
                    Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WuwaBodyForwardPlusUsesDirectionalMainLight()
        {
            RequireDirect3D12();
            var previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQualityPipeline = QualitySettings.renderPipeline;
            var previousSun = RenderSettings.sun;
            var rendererData = ScriptableObject.CreateInstance<
                UniversalRendererData>();
            rendererData.renderingMode = RenderingMode.ForwardPlus;
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            var mesh = CreateForwardLitQuad();
            var material = CreateWuwaBodyMainLightProbeMaterial();
            var cameraObject = new GameObject("Miku WuWa Forward+ Camera");
            var meshObject = new GameObject("Miku WuWa Forward+ Mesh");
            var lightObject = new GameObject("Miku WuWa Forward+ Main Light");
            var target = new RenderTexture(
                64,
                64,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(
                64,
                64,
                TextureFormat.RGBA32,
                false,
                true);
            var previousActive = RenderTexture.active;
            try
            {
                Assert.That(
                    rendererData.renderingMode,
                    Is.EqualTo(RenderingMode.ForwardPlus));
                pipelineAsset.supportsHDR = false;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;

                var camera = cameraObject.AddComponent<Camera>();
                const int probeLayer = 31;
                cameraObject.layer = probeLayer;
                meshObject.layer = probeLayer;
                lightObject.layer = probeLayer;
                camera.cullingMask = 1 << probeLayer;
                camera.transform.position = new Vector3(0f, 0f, -3f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.allowHDR = false;
                camera.targetTexture = target;

                meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = meshObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << probeLayer;
                light.color = new Color(1f, 0.02f, 0.01f, 1f);
                light.intensity = 1f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.identity;
                RenderSettings.sun = light;

                target.Create();
                camera.Render();
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, 64f, 64f), 0, 0);
                readback.Apply(false, false);
                var center = readback.GetPixel(32, 32);
                Assert.That(center.r, Is.GreaterThan(0.1f), center.ToString());
                Assert.That(center.r, Is.GreaterThan(center.g * 2f),
                    center.ToString());
                Assert.That(center.r, Is.GreaterThan(center.b * 2f),
                    center.ToString());
            }
            finally
            {
                var cleanupCamera = cameraObject.GetComponent<Camera>();
                if (cleanupCamera != null)
                    cleanupCamera.targetTexture = null;
                RenderTexture.active = previousActive;
                target.Release();
                GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
                QualitySettings.renderPipeline = previousQualityPipeline;
                RenderSettings.sun = previousSun;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(meshObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(pipelineAsset);
                Object.DestroyImmediate(rendererData);
            }
        }

        [Test]
        public void GenshinBodyAndHairForwardPlusRespondToLightYaw()
        {
            RequireDirect3D12();
            var mesh = CreateForwardLitQuad();
            var lightMap = CreateSolidTexture(
                new Color(0f, 1f, 0f, 0f),
                "Miku Genshin light-map probe");
            var ramp = CreateHorizontalRampTexture(
                Color.black,
                Color.white,
                "Miku Genshin ramp probe");
            try
            {
                foreach (var part in new[] { "Body", "Hair" })
                {
                    var material = CreateGenshinMainLightProbeMaterial(
                        part,
                        lightMap,
                        ramp);
                    try
                    {
                        AssertLightYawChangesFinalColor(
                            material,
                            mesh,
                            0.25f,
                            "Genshin " + part);
                    }
                    finally
                    {
                        Object.DestroyImmediate(material);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(ramp);
                Object.DestroyImmediate(lightMap);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void GenshinMetalUsesViewNormalAndIgnoresMainLightYaw()
        {
            RequireDirect3D12();
            var negativeNormalMesh = CreateForwardLitQuad(Vector3.left);
            var positiveNormalMesh = CreateForwardLitQuad(Vector3.right);
            var lightMap = CreateSolidTexture(
                new Color(1f, 1f, 0f, 0f),
                "Miku Genshin metal-mask probe");
            var metalMap = CreateHorizontalRampTexture(
                Color.black,
                Color.white,
                "Miku Genshin view-normal metal probe");
            var ramp = CreateHorizontalRampTexture(
                Color.black,
                Color.white,
                "Miku Genshin unused metal ramp probe");
            var material = CreateGenshinMainLightProbeMaterial(
                "Body",
                lightMap,
                ramp);
            try
            {
                material.SetTexture("_MetalMap", metalMap);
                material.SetColor("_BaseColorTint", Color.red);
                material.SetColor("_MetalMapColor", Color.blue);
                material.SetFloat("_MetalIntensity", 1f);
                var negative = RenderOpposedDirectionalLights(
                    material,
                    negativeNormalMesh);
                var positive = RenderOpposedDirectionalLights(
                    material,
                    positiveNormalMesh);
                AssertColorNear(negative[0], negative[1], 2f / 255f,
                    "Metal must not change when only the Main Light yaw changes.");
                AssertColorNear(positive[0], positive[1], 2f / 255f,
                    "Metal must not change when only the Main Light yaw changes.");
                Assert.That(
                    Mathf.Abs(negative[0].r - positive[0].r) +
                    Mathf.Abs(negative[0].b - positive[0].b),
                    Is.GreaterThan(0.25f),
                    "View-space normal RG must select a different metal-map value.");
                Assert.That(negative[0].b, Is.GreaterThan(negative[0].r));
                Assert.That(positive[0].r, Is.GreaterThan(positive[0].b));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(ramp);
                Object.DestroyImmediate(metalMap);
                Object.DestroyImmediate(lightMap);
                Object.DestroyImmediate(positiveNormalMesh);
                Object.DestroyImmediate(negativeNormalMesh);
            }
        }

        [Test]
        public void GenshinFaceSdfMaskAndFinalColorRespondToLightYaw()
        {
            RequireDirect3D12();
            var mesh = CreateForwardLitQuad();
            var sdf = CreateSolidTexture(
                new Color(0.5f, 0.5f, 0.5f, 0.5f),
                "Miku Genshin face-SDF probe");
            var ramp = CreateHorizontalRampTexture(
                Color.black,
                Color.white,
                "Miku Genshin face ramp probe");
            var material = CreateGenshinFaceSdfProbeMaterial(sdf, ramp);
            try
            {
                material.SetFloat("_FaceSdfDebugMode", 5f);
                AssertLightYawChangesFinalColor(
                    material,
                    mesh,
                    0.75f,
                    "Genshin Face SDF debug mask");
                material.SetFloat("_FaceSdfDebugMode", 0f);
                AssertLightYawChangesFinalColor(
                    material,
                    mesh,
                    0.25f,
                    "Genshin Face final color");
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(ramp);
                Object.DestroyImmediate(sdf);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WuwaFaceSdfMaskAndFinalColorRespondToLightYaw()
        {
            RequireDirect3D12();
            var mesh = CreateForwardLitQuad();
            var sdf = CreateSolidTexture(
                new Color(0.5f, 0.5f, 0.5f, 0.5f),
                "Miku WuWa face-SDF probe");
            var material = CreateWuwaFaceSdfProbeMaterial(sdf);
            try
            {
                material.SetFloat("_FaceSdfDebugMode", 5f);
                AssertLightYawChangesFinalColor(
                    material,
                    mesh,
                    0.75f,
                    "WuWa Face SDF debug mask");
                material.SetFloat("_FaceSdfDebugMode", 0f);
                AssertLightYawChangesFinalColor(
                    material,
                    mesh,
                    0.25f,
                    "WuWa Face final color");
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sdf);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WuwaFaceSdfMirrorTransitionIsContinuousInDebugAndFinal()
        {
            RequireDirect3D12();
            var mesh = CreateForwardLitQuad();
            var sdf = CreateAsymmetricWuwaFaceSdfTexture();
            var material = CreateWuwaFaceSdfProbeMaterial(sdf);
            try
            {
                // Quaternion.identity makes the directional light point along
                // the configured face-forward axis. A small yaw therefore
                // crosses sideDot == 0 while keeping the SDF threshold near
                // 0.5, where the two deliberately asymmetric samples differ.
                material.SetFloat("_FaceThresholdBias", 0.5f);
                material.SetFloat("_FaceShadowSoftness", 0.02f);
                material.SetFloat("_FaceSdfMirrorBlendWidth", 0.1f);

                material.SetFloat("_FaceSdfDebugMode", 5f);
                AssertContinuousWuwaFaceMirrorSweep(
                    material,
                    mesh,
                    "WuWa Face SDF debug mask");
                AssertWuwaFaceWideYawResponse(material, mesh);

                material.SetFloat("_FaceSdfDebugMode", 0f);
                AssertContinuousWuwaFaceMirrorSweep(
                    material,
                    mesh,
                    "WuWa Face final color");
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(sdf);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void WuwaEyeParallaxMovesIrisLayersButKeepsSurfaceHighlightFixed()
        {
            RequireDirect3D12();
            var mesh = CreateForwardLitQuad();
            var black = CreateSolidTexture(
                new Color(0f, 0f, 0f, 1f),
                "Miku WuWa eye black base probe");
            var spot = CreateCenteredSpotTexture(
                "Miku WuWa eye parallax spot probe");
            var material = CreateWuwaEyeParallaxProbeMaterial(black, spot);
            var cameraYaws = new[] { -45f, 0f, 45f };
            try
            {
                MikuManualTextureKeywordUtility.SyncKeywords(material);
                Assert.That(
                    material.IsKeywordEnabled(
                        "_WUWA_EYE_UPPER_HIGHLIGHT_ON"),
                    Is.True);
                foreach (var debugView in new[] { 2f, 3f })
                {
                    material.SetFloat("_EyeDebugView", debugView);
                    var centroids = RenderCameraYawCentroids(
                        material,
                        mesh,
                        cameraYaws,
                        "WuWa Eye debug " + debugView);
                    Assert.That(
                        centroids[0].x,
                        Is.LessThan(centroids[1].x - 0.75f),
                        "A negative view yaw must move the iris-layer spot left.");
                    Assert.That(
                        centroids[2].x,
                        Is.GreaterThan(centroids[1].x + 0.75f),
                        "A positive view yaw must move the iris-layer spot right.");
                }

                // Keep the production HET+HDMF+Upper variant selected, but
                // eliminate iris-layer contributions so the final-color
                // centroid measures only the surface-UV upper highlight.
                material.SetFloat("_EyeHETScleraStrength", 0f);
                material.SetFloat("_EyeHETPupilStrength", 0f);
                material.SetFloat("_EyeHDMFHighlightStrength", 0f);
                material.SetFloat("_EyeDebugView", 0f);
                var highlightCentroids = RenderCameraYawCentroids(
                    material,
                    mesh,
                    cameraYaws,
                    "WuWa Eye upper surface highlight");
                for (var index = 1; index < highlightCentroids.Length; index++)
                {
                    Assert.That(
                        Vector2.Distance(
                            highlightCentroids[0],
                            highlightCentroids[index]),
                        Is.LessThanOrEqualTo(1f),
                        "The authored upper highlight must remain attached to " +
                        "surface UV while camera yaw changes.");
                }
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(spot);
                Object.DestroyImmediate(black);
                Object.DestroyImmediate(mesh);
            }
        }

        static void AssertContinuousWuwaFaceMirrorSweep(
            Material material,
            Mesh mesh,
            string label)
        {
            var yaws = new float[15];
            for (var index = 0; index < yaws.Length; index++)
                yaws[index] = index - 7f;
            var colors = RenderDirectionalLightYaws(material, mesh, yaws);
            var values = new float[colors.Length];
            for (var index = 0; index < colors.Length; index++)
                values[index] = Mathf.Max(colors[index].r, colors[index].g, colors[index].b);

            var endpointDifference = Mathf.Abs(values[values.Length - 1] - values[0]);
            Assert.That(
                endpointDifference,
                Is.GreaterThan(0.4f),
                label + " positive control must select visibly different SDF sides.");

            var lowerEndpoint = Mathf.Min(values[0], values[values.Length - 1]);
            var upperEndpoint = Mathf.Max(values[0], values[values.Length - 1]);
            var intermediateCount = 0;
            var maximumAdjacentDifference = 0f;
            var direction = Mathf.Sign(values[values.Length - 1] - values[0]);
            for (var index = 0; index < values.Length; index++)
            {
                if (values[index] > lowerEndpoint + 0.05f &&
                    values[index] < upperEndpoint - 0.05f)
                    intermediateCount++;
                if (index == 0)
                    continue;
                var delta = values[index] - values[index - 1];
                maximumAdjacentDifference = Mathf.Max(
                    maximumAdjacentDifference,
                    Mathf.Abs(delta));
                Assert.That(
                    direction * delta,
                    Is.GreaterThanOrEqualTo(-2f / 255f),
                    label + " must not reverse during the mirror transition.");
            }

            Assert.That(
                intermediateCount,
                Is.GreaterThanOrEqualTo(5),
                label + " must contain multiple rendered intermediate states.");
            Assert.That(
                maximumAdjacentDifference,
                Is.LessThanOrEqualTo(0.25f),
                label + " must not pop between adjacent one-degree samples.");
        }

        static void AssertWuwaFaceWideYawResponse(
            Material material,
            Mesh mesh)
        {
            var colors = RenderDirectionalLightYaws(
                material,
                mesh,
                new[] { -90f, -45f, 0f, 45f, 90f });
            var maximumResponse = 0f;
            foreach (var color in colors)
            {
                foreach (var channel in new[]
                {
                    color.r,
                    color.g,
                    color.b,
                    color.a,
                })
                {
                    Assert.That(
                        float.IsNaN(channel) || float.IsInfinity(channel),
                        Is.False,
                        "WuWa Face wide-yaw output must remain finite.");
                    Assert.That(
                        channel,
                        Is.InRange(0f, 1f),
                        "WuWa Face wide-yaw output must remain normalized.");
                }
            }
            for (var index = 1; index < colors.Length; index++)
            {
                maximumResponse = Mathf.Max(
                    maximumResponse,
                    Mathf.Abs(colors[index].r - colors[index - 1].r));
            }
            Assert.That(
                maximumResponse,
                Is.GreaterThan(0.25f),
                "WuWa Face must respond across the broad light-yaw sweep.");
        }

        static void AssertLightYawChangesFinalColor(
            Material material,
            Mesh mesh,
            float minimumDifference,
            string label)
        {
            var colors = RenderOpposedDirectionalLights(material, mesh);
            var difference = Mathf.Max(
                Mathf.Abs(colors[0].r - colors[1].r),
                Mathf.Abs(colors[0].g - colors[1].g),
                Mathf.Abs(colors[0].b - colors[1].b));
            Assert.That(
                difference,
                Is.GreaterThan(minimumDifference),
                label + " must change when the Main Light yaw reverses. " +
                "yaw0=" + colors[0] + ", yaw180=" + colors[1]);
        }

        static void AssertColorNear(
            Color actual,
            Color expected,
            float tolerance,
            string message)
        {
            Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThanOrEqualTo(tolerance), message);
            Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThanOrEqualTo(tolerance), message);
            Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThanOrEqualTo(tolerance), message);
        }

        static Color[] RenderOpposedDirectionalLights(
            Material material,
            Mesh mesh)
        {
            return RenderDirectionalLightYaws(
                material,
                mesh,
                new[] { 0f, 180f });
        }

        static Color[] RenderDirectionalLightYaws(
            Material material,
            Mesh mesh,
            float[] yaws)
        {
            var previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQualityPipeline = QualitySettings.renderPipeline;
            var previousSun = RenderSettings.sun;
            var rendererData = ScriptableObject.CreateInstance<
                UniversalRendererData>();
            rendererData.renderingMode = RenderingMode.ForwardPlus;
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            var cameraObject = new GameObject("Miku light-yaw GPU camera");
            var meshObject = new GameObject("Miku light-yaw GPU mesh");
            var lightObject = new GameObject("Miku light-yaw GPU main light");
            var target = new RenderTexture(
                64,
                64,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(
                64,
                64,
                TextureFormat.RGBA32,
                false,
                true);
            var previousActive = RenderTexture.active;
            try
            {
                Assert.That(
                    rendererData.renderingMode,
                    Is.EqualTo(RenderingMode.ForwardPlus));
                pipelineAsset.supportsHDR = false;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;

                var camera = cameraObject.AddComponent<Camera>();
                const int probeLayer = 31;
                cameraObject.layer = probeLayer;
                meshObject.layer = probeLayer;
                lightObject.layer = probeLayer;
                camera.cullingMask = 1 << probeLayer;
                camera.transform.position = new Vector3(0f, 0f, -3f);
                camera.transform.rotation = Quaternion.identity;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.allowHDR = false;
                camera.targetTexture = target;

                meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = meshObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << probeLayer;
                light.color = Color.white;
                light.intensity = 1f;
                light.shadows = LightShadows.None;
                RenderSettings.sun = light;
                target.Create();

                var colors = new Color[yaws.Length];
                for (var index = 0; index < yaws.Length; index++)
                {
                    colors[index] = RenderCenter(
                        camera,
                        light,
                        Quaternion.Euler(0f, yaws[index], 0f),
                        target,
                        readback);
                }
                return colors;
            }
            finally
            {
                var cleanupCamera = cameraObject.GetComponent<Camera>();
                if (cleanupCamera != null)
                    cleanupCamera.targetTexture = null;
                RenderTexture.active = previousActive;
                target.Release();
                GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
                QualitySettings.renderPipeline = previousQualityPipeline;
                RenderSettings.sun = previousSun;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(meshObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(pipelineAsset);
                Object.DestroyImmediate(rendererData);
            }
        }

        static Vector2[] RenderCameraYawCentroids(
            Material material,
            Mesh mesh,
            float[] yaws,
            string label)
        {
            const int size = 192;
            var previousDefaultPipeline = GraphicsSettings.defaultRenderPipeline;
            var previousQualityPipeline = QualitySettings.renderPipeline;
            var previousSun = RenderSettings.sun;
            var rendererData = ScriptableObject.CreateInstance<
                UniversalRendererData>();
            rendererData.renderingMode = RenderingMode.ForwardPlus;
            var pipelineAsset = UniversalRenderPipelineAsset.Create(rendererData);
            var cameraObject = new GameObject("Miku WuWa eye-view GPU camera");
            var meshObject = new GameObject("Miku WuWa eye-view GPU mesh");
            var lightObject = new GameObject("Miku WuWa eye-view GPU main light");
            var target = new RenderTexture(
                size,
                size,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true);
            var previousActive = RenderTexture.active;
            try
            {
                Assert.That(
                    rendererData.renderingMode,
                    Is.EqualTo(RenderingMode.ForwardPlus));
                pipelineAsset.supportsHDR = false;
                GraphicsSettings.defaultRenderPipeline = pipelineAsset;
                QualitySettings.renderPipeline = pipelineAsset;

                var camera = cameraObject.AddComponent<Camera>();
                const int probeLayer = 31;
                cameraObject.layer = probeLayer;
                meshObject.layer = probeLayer;
                lightObject.layer = probeLayer;
                camera.cullingMask = 1 << probeLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 1f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.allowHDR = false;
                camera.targetTexture = target;

                meshObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var meshRenderer = meshObject.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;

                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << probeLayer;
                light.color = Color.white;
                light.intensity = 1f;
                light.shadows = LightShadows.None;
                light.transform.rotation = Quaternion.identity;
                RenderSettings.sun = light;
                target.Create();

                var centroids = new Vector2[yaws.Length];
                for (var index = 0; index < yaws.Length; index++)
                {
                    var radians = yaws[index] * Mathf.Deg2Rad;
                    camera.transform.position = new Vector3(
                        Mathf.Sin(radians) * 3f,
                        0f,
                        -Mathf.Cos(radians) * 3f);
                    camera.transform.rotation = Quaternion.LookRotation(
                        -camera.transform.position,
                        Vector3.up);
                    // Local shader-feature changes may compile asynchronously
                    // on their first draw in a fresh isolated project.
                    // Discard that frame so the assertion measures the shader,
                    // not variant warm-up latency.
                    target.DiscardContents();
                    camera.Render();
                    target.DiscardContents();
                    camera.Render();
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                    readback.Apply(false, false);
                    centroids[index] = WeightedCentroid(
                        readback,
                        label + " yaw=" + yaws[index]);
                }
                return centroids;
            }
            finally
            {
                var cleanupCamera = cameraObject.GetComponent<Camera>();
                if (cleanupCamera != null)
                    cleanupCamera.targetTexture = null;
                RenderTexture.active = previousActive;
                target.Release();
                GraphicsSettings.defaultRenderPipeline = previousDefaultPipeline;
                QualitySettings.renderPipeline = previousQualityPipeline;
                RenderSettings.sun = previousSun;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(meshObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(pipelineAsset);
                Object.DestroyImmediate(rendererData);
            }
        }

        static Vector2 WeightedCentroid(Texture2D texture, string label)
        {
            var weightedX = 0f;
            var weightedY = 0f;
            var total = 0f;
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    var color = texture.GetPixel(x, y);
                    var weight = Mathf.Max(color.r, color.g, color.b);
                    weightedX += x * weight;
                    weightedY += y * weight;
                    total += weight;
                }
            }
            Assert.That(total, Is.GreaterThan(1f), label);
            return new Vector2(weightedX / total, weightedY / total);
        }

        static Color RenderCenter(
            Camera camera,
            Light light,
            Quaternion rotation,
            RenderTexture target,
            Texture2D readback)
        {
            light.transform.rotation = rotation;
            target.DiscardContents();
            camera.Render();
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0f, 0f, 64f, 64f), 0, 0);
            readback.Apply(false, false);
            return readback.GetPixel(32, 32);
        }

        static void RequireDirect3D12()
        {
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                Assert.Ignore(Direct3D12Required);
        }

        static Material CreateGenshinFaceMaterial()
        {
            var shader = Shader.Find("MIKU/Genshin/Face");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColorTint", Color.white);
            material.SetColor("_OutlineColor0", Color.white);
            material.SetColor("_OutlineColorTint", Color.white);
            material.SetFloat("_DiffuseA", 0f);
            return material;
        }

        static Material CreateWuwaBodyMainLightProbeMaterial()
        {
            var shader = Shader.Find("MIKU/Wuwa/Body");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetColor("_BaseColorTint", Color.white);
            material.SetColor("_LitTint", Color.white);
            material.SetColor("_ShadowTint", Color.white);
            material.SetFloat("_MainLightColorUsage", 1f);
            material.SetFloat("_IndirectLightUsage", 0f);
            material.SetFloat("_ReflectionStrength", 0f);
            material.SetFloat("_SpecularStrength", 0f);
            material.SetFloat("_SkinSSSIntensity", 0f);
            material.SetFloat("_RimLightBrightness", 0f);
            material.SetFloat("_VerticalGradientStrength", 0f);
            material.SetFloat("_BodyEmissionStrength", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Roughness", 0.6f);
            return material;
        }

        static Material CreateGenshinMainLightProbeMaterial(
            string part,
            Texture2D lightMap,
            Texture2D ramp)
        {
            var shader = Shader.Find("MIKU/Genshin/" + part);
            Assert.That(shader, Is.Not.Null, part);
            var material = new Material(shader);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetTexture("_LightMap", lightMap);
            material.SetTexture(
                part == "Body" ? "_ShadowRampMap" : "_HairRampMap",
                ramp);
            material.SetColor("_BaseColorTint", Color.white);
            material.SetFloat("_DiffuseA", 0f);
            material.SetFloat("_MainLightColorUsage", 0f);
            material.SetFloat("_IndirectLightUsage", 0f);
            material.SetFloat("_GlossStrength", 0f);
            material.SetFloat("_FresnelStrength", 0f);
            material.SetFloat("_MetalIntensity", 0f);
            material.SetFloat("_SkinSSSIntensity", 0f);
            material.SetFloat("_EmissionIntensity", 0f);
            if (material.HasProperty("_HairSpecIntensity"))
                material.SetFloat("_HairSpecIntensity", 0f);
            return material;
        }

        static Material CreateGenshinFaceSdfProbeMaterial(
            Texture2D sdf,
            Texture2D ramp)
        {
            var shader = Shader.Find("MIKU/Genshin/Face");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetTexture("_FaceSDFMap", sdf);
            material.SetTexture("_ShadowRampMap", ramp);
            material.SetColor("_BaseColorTint", Color.white);
            material.SetFloat("_DiffuseA", 0f);
            material.SetFloat("_FaceShadowOffset", 0f);
            material.SetFloat("_FaceShadowSoftness", 0.01f);
            material.SetFloat("_FaceSdfFlipY", 0f);
            material.SetFloat("_MainLightColorUsage", 0f);
            material.SetFloat("_IndirectLightUsage", 0f);
            material.SetFloat("_FresnelStrength", 0f);
            material.SetFloat("_SkinSSSIntensity", 0f);
            material.SetFloat("_EmissionIntensity", 0f);
            return material;
        }

        static Material CreateWuwaFaceSdfProbeMaterial(Texture2D sdf)
        {
            var shader = Shader.Find("MIKU/Wuwa/Face");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            material.SetTexture("_FaceSDF", sdf);
            material.SetColor("_BaseColorTint", Color.white);
            material.SetColor("_LitTint", Color.white);
            material.SetColor("_ShadowTint", Color.black);
            material.SetFloat("_UseFaceBasis", 1f);
            material.SetVector(
                "_FaceRight",
                new Vector4(1f, 0f, 0f, 0f));
            material.SetVector(
                "_FaceUp",
                new Vector4(0f, 1f, 0f, 0f));
            material.SetVector(
                "_FaceForward",
                new Vector4(0f, 0f, -1f, 0f));
            material.SetFloat("_FaceSdfMainChannel", 3f);
            material.SetFloat("_FaceSdfSoftChannel", 2f);
            material.SetFloat("_FaceShadowOffset", 0f);
            material.SetFloat("_FaceShadowSoftness", 0.01f);
            material.SetFloat("_FaceThresholdBias", 0f);
            material.SetFloat("_FaceSoftChannelStrength", 1f);
            material.SetFloat("_FaceShadowStrength", 1f);
            material.SetFloat("_MainLightColorUsage", 0f);
            material.SetFloat("_IndirectLightUsage", 0f);
            material.SetFloat("_ReflectionStrength", 0f);
            material.SetFloat("_SpecularStrength", 0f);
            material.SetFloat("_SkinSSSIntensity", 0f);
            material.SetFloat("_RimLightBrightness", 0f);
            material.SetFloat("_VerticalGradientStrength", 0f);
            material.SetFloat("_FaceBlushStrength", 0f);
            material.SetFloat("_FaceExtraLightStrength", 0f);
            material.SetFloat("_UseHairShadow", 0f);
            material.DisableKeyword("_WUWA_HAIR_SHADOW_ON");
            return material;
        }

        static Material CreateWuwaEyeParallaxProbeMaterial(
            Texture2D baseMap,
            Texture2D spot)
        {
            var shader = Shader.Find("MIKU/Wuwa/Eye");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_EyeHET", spot);
            material.SetTexture("_EyeHDMF", spot);
            material.SetTexture("_EyeUpperHighlight", spot);
            material.SetColor("_BaseColorTint", Color.white);
            material.SetColor("_EyeHighlightColor", Color.white);
            material.SetFloat("_EyeHighlightStrength", 1f);
            material.SetFloat("_EyeParallaxStrength", 0.02f);
            material.SetFloat("_EyeTopShadowStrength", 0f);
            material.SetFloat("_MainLightColorUsage", 0f);
            material.SetFloat("_IndirectLightUsage", 0f);
            material.SetFloat("_ReflectionStrength", 0f);
            material.SetFloat("_SpecularStrength", 0f);
            material.SetFloat("_EyeBaseEmissionStrength", 0f);
            material.SetFloat("_EmissionStrength", 1f);
            material.SetVector(
                "_EyeUpperHighlightUVRow0",
                new Vector4(1f, 0f, 0f, 0f));
            material.SetVector(
                "_EyeUpperHighlightUVRow1",
                new Vector4(0f, 1f, 0f, 0f));
            material.SetVector(
                "_EyeUpperHighlightScale",
                new Vector4(1f, 1f, 0f, 0f));
            material.SetVector("_EyeUpperHighlightOffset", Vector4.zero);
            return material;
        }

        static Texture2D CreateSolidTexture(Color color, string name)
        {
            var texture = new Texture2D(
                1,
                1,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply(false, false);
            return texture;
        }

        static Texture2D CreateHorizontalRampTexture(
            Color shadow,
            Color lit,
            string name)
        {
            var texture = new Texture2D(
                2,
                1,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixels(new[] { shadow, lit });
            texture.Apply(false, false);
            return texture;
        }

        static Texture2D CreateAsymmetricWuwaFaceSdfTexture()
        {
            const int width = 64;
            var texture = new Texture2D(
                width,
                1,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Miku WuWa asymmetric A-B face-SDF probe",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width];
            for (var x = 0; x < width; x++)
            {
                pixels[x] = x < width / 2
                    ? new Color(0f, 0f, 0.12f, 0.18f)
                    : new Color(0f, 0f, 0.82f, 0.88f);
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static Texture2D CreateCenteredSpotTexture(string name)
        {
            const int size = 64;
            const int radius = 4;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(
                        new Vector2(x, y),
                        new Vector2(center, center));
                    var value = Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(value, value, value, value);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static Mesh CreateForwardLitQuad()
        {
            return CreateForwardLitQuad(Vector3.back);
        }

        static Mesh CreateForwardLitQuad(Vector3 normal)
        {
            var mesh = new Mesh { name = "Miku WuWa Forward+ GPU Probe" };
            mesh.vertices = new[]
            {
                new Vector3(-0.75f, -0.75f, 0f),
                new Vector3(0.75f, -0.75f, 0f),
                new Vector3(0.75f, 0.75f, 0f),
                new Vector3(-0.75f, 0.75f, 0f),
            };
            mesh.normals = new[]
            {
                normal,
                normal,
                normal,
                normal,
            };
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
            };
            mesh.uv = new[]
            {
                Vector2.zero,
                Vector2.right,
                Vector2.one,
                Vector2.up,
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh CreateBackShellMesh(float outlineMask)
        {
            var mesh = new Mesh { name = "Miku Outline GPU Probe" };
            mesh.vertices = new[]
            {
                new Vector3(-0.4f, -0.4f, -1f),
                new Vector3(0.4f, -0.4f, -1f),
                new Vector3(0f, 0.4f, -1f),
            };
            mesh.normals = new[]
            {
                Vector3.right,
                Vector3.right,
                Vector3.right,
            };
            mesh.tangents = new[]
            {
                new Vector4(0f, 1f, 0f, 1f),
                new Vector4(0f, 1f, 0f, 1f),
                new Vector4(0f, 1f, 0f, 1f),
            };
            mesh.uv = new[]
            {
                Vector2.zero,
                Vector2.right,
                Vector2.up,
            };
            mesh.colors = new[]
            {
                new Color(1f, outlineMask, 1f, 1f),
                new Color(1f, outlineMask, 1f, 1f),
                new Color(1f, outlineMask, 1f, 1f),
            };
            mesh.SetUVs(7, new List<Vector4>
            {
                new Vector4(0f, 0f, 1f, 2f),
                new Vector4(0f, 0f, 1f, 2f),
                new Vector4(0f, 0f, 1f, 2f),
            });
            // Both windings make the probe independent of graphics-API front
            // face conventions while Cull Front still leaves one back shell.
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        static int RenderOutlinePixels(
            Material material,
            Mesh mesh,
            string passName)
        {
            const int size = 64;
            var pass = material.FindPass(passName);
            Assert.That(pass, Is.GreaterThanOrEqualTo(0), passName);
            var target = RenderTexture.GetTemporary(
                size,
                size,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var readback = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true);
            var command = new CommandBuffer
            {
                name = "Miku outline hard-disable GPU probe",
            };
            try
            {
                command.SetRenderTarget(target);
                command.ClearRenderTarget(true, true, Color.clear);
                command.SetViewProjectionMatrices(
                    Matrix4x4.identity,
                    GL.GetGPUProjectionMatrix(
                        Matrix4x4.Ortho(-1f, 1f, -1f, 1f, 0.1f, 10f),
                        true));
                command.SetGlobalVector(
                    "_ScreenParams",
                    new Vector4(size, size, 1f + 1f / size, 1f + 1f / size));
                command.SetGlobalVector("_WorldSpaceCameraPos", Vector4.zero);
                command.DrawMesh(mesh, Matrix4x4.identity, material, 0, pass);
                Graphics.ExecuteCommandBuffer(command);

                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                readback.Apply(false, false);
                var pixels = 0;
                foreach (var color in readback.GetPixels32())
                {
                    if (color.a > 0)
                        pixels++;
                }
                return pixels;
            }
            finally
            {
                command.Release();
                UnityEngine.Object.DestroyImmediate(readback);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
