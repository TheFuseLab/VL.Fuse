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