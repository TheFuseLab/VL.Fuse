using System;
using System.Collections.Generic;
using Stride.Rendering;
using RendererBase = VL.Stride.Rendering.RendererBase;

namespace Fuse.compute;

public class ComputeDispatchRenderer : RendererBase
{
    public ComputeDrawResult DrawResult { get; set; }

    public bool DrawInputAfterDispatch { get; set; } = true;

    public TextureResourceFailureDispatchPolicy TextureResourceFailurePolicy { get; set; } =
        TextureResourceFailureDispatchPolicy.BlockAllFailures;

    public IReadOnlyList<ComputeDispatchExecutionResult> LastExecutionResults { get; private set; } = [];

    public IReadOnlyList<ComputeDispatchExecutionResult> Execute(RenderDrawContext context)
    {
        LastExecutionResults = DrawResult?.Execute(context, TextureResourceFailurePolicy) ?? [];
        return LastExecutionResults;
    }

    internal static bool IsMissingAppHost(InvalidOperationException exception)
    {
        return exception?.Message?.IndexOf("No app host", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    protected override void DrawInternal(RenderDrawContext context)
    {
        Execute(context);

        if (DrawInputAfterDispatch)
            DrawInput(context);
    }
}
