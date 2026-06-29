using System;
using System.Reflection;
using Stride.Rendering;

namespace Fuse.compute;

public interface IComputeRendererScheduler
{
    void Schedule(IGraphicsRendererBase renderer);

    void Remove(IGraphicsRendererBase renderer);
}

internal static class ComputeRendererScheduler
{
    public static bool Schedule(object scheduler, IGraphicsRendererBase renderer)
    {
        return Invoke(scheduler, renderer, nameof(IComputeRendererScheduler.Schedule));
    }

    public static bool Remove(object scheduler, IGraphicsRendererBase renderer)
    {
        return Invoke(scheduler, renderer, nameof(IComputeRendererScheduler.Remove));
    }

    private static bool Invoke(
        object scheduler,
        IGraphicsRendererBase renderer,
        string methodName)
    {
        if (scheduler == null || renderer == null)
            return false;

        if (scheduler is IComputeRendererScheduler computeScheduler)
        {
            if (methodName == nameof(IComputeRendererScheduler.Schedule))
                computeScheduler.Schedule(renderer);
            else
                computeScheduler.Remove(renderer);

            return true;
        }

        var method = scheduler
            .GetType()
            .GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: [typeof(IGraphicsRendererBase)],
                modifiers: null);
        if (method == null)
            return false;

        method.Invoke(scheduler, [renderer]);
        return true;
    }
}
