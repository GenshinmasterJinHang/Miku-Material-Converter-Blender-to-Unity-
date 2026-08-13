// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Runtime.Wuwa
{
    public sealed class MikuWuwaHairShadowRendererFeature : ScriptableRendererFeature
    {
        public const string HairShadowTextureName =
            "_WuwaHairShadowTexture";
        static readonly int HairShadowAvailableId = Shader.PropertyToID("_WuwaHairShadowAvailable");

        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingOpaques;
            public LayerMask layerMask = -1;
            [HideInInspector]
            public string textureName = HairShadowTextureName;
            public FilterMode filterMode = FilterMode.Bilinear;
            public TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        }

        public Settings settings = new Settings();
        WuwaHairShadowPass pass;

        public override void Create()
        {
            Shader.SetGlobalFloat(HairShadowAvailableId, 0f);
            settings.textureName = HairShadowTextureName;
            pass = new WuwaHairShadowPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Shader.SetGlobalFloat(HairShadowAvailableId, 0f);
            if (renderingData.cameraData.cameraType != CameraType.Game && renderingData.cameraData.cameraType != CameraType.SceneView)
                return;

            pass.Setup(renderingData.cameraData.cameraTargetDescriptor);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            Shader.SetGlobalFloat(HairShadowAvailableId, 0f);
            pass?.Dispose();
            pass = null;
        }

        sealed class WuwaHairShadowPass : ScriptableRenderPass
        {
            const string PassName = "Wuwa Hair Shadow";
            static readonly ShaderTagId HairShadowShaderTag = new ShaderTagId("WuwaHairShadow");
            readonly ProfilingSampler mikuProfilingSampler =
                new ProfilingSampler(PassName);
            readonly Settings settings;
            readonly FilteringSettings filteringSettings;
            readonly int textureId;
            RenderTextureDescriptor descriptor;
            RenderTextureDescriptor depthDescriptor;
            RTHandle shadowTexture;
            RTHandle shadowDepthTexture;

            sealed class PassData
            {
                public RendererListHandle rendererList;
                public int availabilityId;
            }

            public WuwaHairShadowPass(Settings settings)
            {
                this.settings = settings;
                renderPassEvent = settings.passEvent;
                filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.layerMask);
                textureId = Shader.PropertyToID(HairShadowTextureName);
            }

            public void Setup(RenderTextureDescriptor cameraDescriptor)
            {
                descriptor = cameraDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.graphicsFormat = SystemInfo.IsFormatSupported(
                    GraphicsFormat.R16_SFloat,
                    GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16_SFloat
                    : GraphicsFormat.R32_SFloat;

                depthDescriptor = cameraDescriptor;
                depthDescriptor.graphicsFormat = GraphicsFormat.None;
                depthDescriptor.depthStencilFormat = SystemInfo.IsFormatSupported(
                    GraphicsFormat.D32_SFloat,
                    GraphicsFormatUsage.Render)
                    ? GraphicsFormat.D32_SFloat
                    : GraphicsFormat.D24_UNorm_S8_UInt;
                depthDescriptor.depthBufferBits = 32;
                depthDescriptor.msaaSamples = 1;
                depthDescriptor.bindMS = false;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();
                RenderingUtils.ReAllocateHandleIfNeeded(ref shadowTexture, descriptor, settings.filterMode, settings.wrapMode, name: HairShadowTextureName);
                RenderingUtils.ReAllocateHandleIfNeeded(ref shadowDepthTexture, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: HairShadowTextureName + "Depth");
                TextureHandle shadowTextureHandle = renderGraph.ImportTexture(shadowTexture);
                TextureHandle shadowDepthTextureHandle = renderGraph.ImportTexture(shadowDepthTexture);
                if (!shadowTextureHandle.IsValid() || !shadowDepthTextureHandle.IsValid())
                    return;

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                           PassName,
                           out var passData,
                           mikuProfilingSampler))
                {
                    var drawSettings = RenderingUtils.CreateDrawingSettings(HairShadowShaderTag, renderingData, cameraData, lightData, cameraData.defaultOpaqueSortFlags);
                    drawSettings.perObjectData = PerObjectData.None;
                    var rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                    rendererListParams.filteringSettings.batchLayerMask = uint.MaxValue;
                    passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
                    passData.availabilityId = HairShadowAvailableId;

                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(shadowTextureHandle, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(shadowDepthTextureHandle, AccessFlags.Write);
                    builder.SetGlobalTextureAfterPass(shadowTextureHandle, textureId);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(true, true, Color.white);
                        context.cmd.DrawRendererList(data.rendererList);
                        context.cmd.SetGlobalFloat(data.availabilityId, 1f);
                    });
                }
            }

            public void Dispose()
            {
                Shader.SetGlobalFloat(HairShadowAvailableId, 0f);
                shadowTexture?.Release();
                shadowTexture = null;
                shadowDepthTexture?.Release();
                shadowDepthTexture = null;
            }
        }
    }
}
