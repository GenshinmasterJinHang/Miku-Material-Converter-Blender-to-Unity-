// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Runtime.GenericToon
{
    /// <summary>
    /// Draws MikuToonCharacterMask with each renderer's original material, then
    /// composites a depth-safe inner rim before transparent rendering.
    /// </summary>
    public sealed class MikuToonScreenRimRendererFeature :
        ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public RenderPassEvent passEvent =
                RenderPassEvent.BeforeRenderingTransparents;
            public LayerMask layerMask = -1;
        }

        public Settings settings = new Settings();
        MikuToonScreenRimPass pass;
        Material compositeMaterial;

        public override void Create()
        {
            var shader = Shader.Find(
                "Hidden/Miku/GenericToon/ScreenRimComposite");
            compositeMaterial = shader != null
                ? CoreUtils.CreateEngineMaterial(shader)
                : null;
            pass = new MikuToonScreenRimPass(settings);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (compositeMaterial == null ||
                renderingData.cameraData.cameraType != CameraType.Game &&
                renderingData.cameraData.cameraType != CameraType.SceneView)
                return;
            pass.Setup(compositeMaterial);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(compositeMaterial);
            compositeMaterial = null;
            pass = null;
        }

        sealed class MikuToonScreenRimPass : ScriptableRenderPass
        {
            const string MaskPassName = "Miku Toon Character Mask";
            const string CompositePassName = "Miku Toon Screen Rim";
            static readonly ShaderTagId MaskTag =
                new ShaderTagId("MikuToonCharacterMask");
            static readonly int MaskTextureId =
                Shader.PropertyToID("_MIKU_ToonCharacterMaskTexture");
            readonly Settings settings;
            readonly FilteringSettings filtering;
            readonly ProfilingSampler maskSampler =
                new ProfilingSampler(MaskPassName);
            readonly ProfilingSampler compositeSampler =
                new ProfilingSampler(CompositePassName);
            Material material;

            sealed class MaskPassData
            {
                public RendererListHandle rendererList;
            }

            sealed class CompositePassData
            {
                public TextureHandle source;
                public TextureHandle mask;
                public Material material;
            }

            public MikuToonScreenRimPass(Settings settings)
            {
                this.settings = settings;
                filtering = new FilteringSettings(
                    RenderQueueRange.opaque,
                    settings.layerMask);
                renderPassEvent = settings.passEvent;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(Material value)
            {
                material = value;
                renderPassEvent = settings.passEvent;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer)
                    return;
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();
                var source = resources.activeColorTexture;

                var maskDescriptor = renderGraph.GetTextureDesc(source);
                maskDescriptor.name = "_MIKU_ToonCharacterMaskTexture";
                maskDescriptor.clearBuffer = true;
                maskDescriptor.clearColor = Color.clear;
                maskDescriptor.depthBufferBits = DepthBits.None;
                maskDescriptor.msaaSamples = MSAASamples.None;
                maskDescriptor.format = SystemInfo.IsFormatSupported(
                    GraphicsFormat.R16G16B16A16_SFloat,
                    GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.R8G8B8A8_UNorm;
                var mask = renderGraph.CreateTexture(maskDescriptor);

                using (var builder =
                       renderGraph.AddRasterRenderPass<MaskPassData>(
                           MaskPassName,
                           out var passData,
                           maskSampler))
                {
                    var drawing = RenderingUtils.CreateDrawingSettings(
                        MaskTag,
                        renderingData,
                        cameraData,
                        lightData,
                        cameraData.defaultOpaqueSortFlags);
                    drawing.perObjectData = PerObjectData.None;
                    var parameters = new RendererListParams(
                        renderingData.cullResults,
                        drawing,
                        filtering);
                    parameters.filteringSettings.batchLayerMask =
                        uint.MaxValue;
                    passData.rendererList =
                        renderGraph.CreateRendererList(parameters);
                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(
                        mask,
                        0,
                        AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(
                        resources.activeDepthTexture,
                        AccessFlags.Read);
                    builder.SetGlobalTextureAfterPass(mask, MaskTextureId);
                    builder.SetRenderFunc(static (
                        MaskPassData data,
                        RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }

                var destinationDescriptor =
                    renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "MikuToonScreenRimColor";
                destinationDescriptor.clearBuffer = false;
                var destination =
                    renderGraph.CreateTexture(destinationDescriptor);
                using (var builder =
                       renderGraph.AddRasterRenderPass<CompositePassData>(
                           CompositePassName,
                           out var passData,
                           compositeSampler))
                {
                    passData.source = source;
                    passData.mask = mask;
                    passData.material = material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.SetRenderAttachment(
                        destination,
                        0,
                        AccessFlags.Write);
                    builder.SetRenderFunc(static (
                        CompositePassData data,
                        RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(
                            MaskTextureId,
                            data.mask);
                        Blitter.BlitTexture(
                            context.cmd,
                            data.source,
                            Vector2.one,
                            data.material,
                            0);
                    });
                }
                resources.cameraColor = destination;
            }
        }
    }
}
