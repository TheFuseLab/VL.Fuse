using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;
using RendererBase = VL.Stride.Rendering.RendererBase;

namespace Fuse.Rendering;

public class WithRenderTargetAndViewPort : RendererBase
    {
        ViewportState viewportState = new ViewportState();

        public Texture RenderTarget { get; set; }

        public Texture DepthBuffer { get; set; }

        protected override void DrawInternal(RenderDrawContext context)
        {
            var renderTarget = RenderTarget;
            var depthBuffer = DepthBuffer;
            var setRenderTarget = renderTarget != null;
            var setDepthBuffer = depthBuffer != null;

            if (setRenderTarget || setDepthBuffer)
            {
                var renderContext = context.RenderContext;

                using (renderContext.SaveRenderOutputAndRestore())
                using (renderContext.SaveViewportAndRestore())
                using (context.PushRenderTargetsAndRestore())
                {
                    if (setRenderTarget)
                    {
                        renderContext.RenderOutput.RenderTargetFormat0 = renderTarget.ViewFormat;
                        renderContext.RenderOutput.RenderTargetCount = 1;

                        renderContext.ViewportState = viewportState;
                        renderContext.ViewportState.Viewport0 = new Viewport(0, 0, renderTarget.ViewWidth, renderTarget.ViewHeight);
                    }
                    //context.CommandList.SetViewports();
                    //context.CommandList.SetRenderTargets();
                    context.CommandList.SetRenderTargetAndViewport(depthBuffer, renderTarget);
                    
                    DrawInput(context);
                    
                }  
            }
            else
            {
                DrawInput(context);
            }
        }
        
        
    }

public class WithRenderTargetsAndViewPort : RendererBase
{
    ViewportState viewportState = new();

    public List<Texture> RenderTargets { get; set; }

    public Texture DepthBuffer { get; set; }

    protected override void DrawInternal(RenderDrawContext context)
    {
        var depthBuffer = DepthBuffer;
        var setDepthBuffer = depthBuffer != null;
        
        var renderTargets = new Texture[RenderTargets.Count];
        var setRenderTarget = renderTargets.Length > 0;

        if (setRenderTarget || setDepthBuffer)
        {
            var renderContext = context.RenderContext;

            using (renderContext.SaveRenderOutputAndRestore())
            using (renderContext.SaveViewportAndRestore())
            using (context.PushRenderTargetsAndRestore())
            {
                if (setRenderTarget)
                {
                    for (var i = 0; i < RenderTargets.Count; i++)
                    {
                        renderTargets[i] = RenderTargets[i];
                        switch (i)
                        {
                            case 0:
                                renderContext.RenderOutput.RenderTargetFormat0 = renderTargets[i].ViewFormat;
                                break;
                            case 1:
                                renderContext.RenderOutput.RenderTargetFormat1 = renderTargets[i].ViewFormat;
                                break;
                            case 2:
                                renderContext.RenderOutput.RenderTargetFormat2 = renderTargets[i].ViewFormat;
                                break;
                            case 3:
                                renderContext.RenderOutput.RenderTargetFormat3 = renderTargets[i].ViewFormat;
                                break;
                            case 4:
                                renderContext.RenderOutput.RenderTargetFormat4 = renderTargets[i].ViewFormat;
                                break;
                            case 5:
                                renderContext.RenderOutput.RenderTargetFormat5 = renderTargets[i].ViewFormat;
                                break;
                            case 6:
                                renderContext.RenderOutput.RenderTargetFormat6 = renderTargets[i].ViewFormat;
                                break;
                            case 7:
                                renderContext.RenderOutput.RenderTargetFormat7 = renderTargets[i].ViewFormat;
                                break;
                        }
                    }
                    renderContext.RenderOutput.RenderTargetCount = RenderTargets.Count;

                    renderContext.ViewportState = viewportState;
                    renderContext.ViewportState.Viewport0 =
                        new Viewport(0, 0, renderTargets[0].ViewWidth, renderTargets[0].ViewHeight);
                }

                context.CommandList.SetRenderTargetsAndViewport(depthBuffer, renderTargets);

                DrawInput(context);

            }
        }
        else
        {
            DrawInput(context);
        }
    }
}