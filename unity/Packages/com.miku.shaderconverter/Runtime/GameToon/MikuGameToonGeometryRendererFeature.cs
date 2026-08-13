// SPDX-FileCopyrightText: 2026 Miku Project Authors
// SPDX-License-Identifier: MIT

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Miku.ShaderConverter.Runtime.GameToon
{
    /// <summary>
    /// Draws the explicit Genshin UV1 backface pass followed by the shared
    /// UV7 toon outline pass after ordinary opaque rendering.
    /// </summary>
    public sealed class MikuGameToonGeometryRendererFeature :
        ScriptableRendererFeature
    {
        [System.Serializable]
        public sealed class Settings
        {
            public LayerMask layerMask = -1;
        }

        public Settings settings = new Settings();
        GeometryPass pass;

        public override void Create()
        {
            pass = new GeometryPass(settings);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType != CameraType.Game &&
                cameraType != CameraType.SceneView)
                return;
            renderer.EnqueuePass(pass);
        }

        sealed class GeometryPass : ScriptableRenderPass
        {
            const string BackfaceName = "MikuGenshinBackface";
            const string OutlineName = "MikuToonOutline";
            static readonly ShaderTagId BackfaceTag =
                new ShaderTagId(BackfaceName);
            static readonly ShaderTagId OutlineTag =
                new ShaderTagId(OutlineName);
            readonly FilteringSettings filtering;
            readonly ProfilingSampler backfaceSampler =
                new ProfilingSampler(BackfaceName);
            readonly ProfilingSampler outlineSampler =
                new ProfilingSampler(OutlineName);

            sealed class PassData
            {
                public RendererListHandle rendererList;
            }

            public GeometryPass(Settings settings)
            {
                filtering = new FilteringSettings(
                    RenderQueueRange.opaque,
                    settings.layerMask);
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var lightData = frameData.Get<UniversalLightData>();

                using (var builder =
                       renderGraph.AddRasterRenderPass<PassData>(
                           BackfaceName,
                           out var passData,
                           backfaceSampler))
                {
                    var drawing = RenderingUtils.CreateDrawingSettings(
                        BackfaceTag,
                        renderingData,
                        cameraData,
                        lightData,
                        cameraData.defaultOpaqueSortFlags);
                    var parameters = new RendererListParams(
                        renderingData.cullResults,
                        drawing,
                        filtering);
                    passData.rendererList =
                        renderGraph.CreateRendererList(parameters);
                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(
                        resources.activeColorTexture,
                        0,
                        AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(
                        resources.activeDepthTexture,
                        AccessFlags.ReadWrite);
                    if (resources.mainShadowsTexture.IsValid())
                        builder.UseTexture(
                            resources.mainShadowsTexture,
                            AccessFlags.Read);
                    builder.SetRenderFunc(static (
                        PassData data,
                        RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }

                using (var builder =
                       renderGraph.AddRasterRenderPass<PassData>(
                           OutlineName,
                           out var passData,
                           outlineSampler))
                {
                    var drawing = RenderingUtils.CreateDrawingSettings(
                        OutlineTag,
                        renderingData,
                        cameraData,
                        lightData,
                        cameraData.defaultOpaqueSortFlags);
                    var parameters = new RendererListParams(
                        renderingData.cullResults,
                        drawing,
                        filtering);
                    passData.rendererList =
                        renderGraph.CreateRendererList(parameters);
                    builder.UseRendererList(passData.rendererList);
                    builder.SetRenderAttachment(
                        resources.activeColorTexture,
                        0,
                        AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(
                        resources.activeDepthTexture,
                        AccessFlags.Read);
                    builder.SetRenderFunc(static (
                        PassData data,
                        RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(data.rendererList);
                    });
                }
            }
        }
    }
}
