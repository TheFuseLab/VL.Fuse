using System;
using System.Collections.Generic;
using System.Linq;
using Fuse;
using Stride.Rendering;

namespace Fuse.compute;

public enum ComputeDispatchExecutionStepKind
{
    IterationIndexSet,
    PreRenderCommand,
    TextureResourceUpdate,
    Dispatch,
    PostDispatchGraph
}

public enum TextureResourceFailureDispatchPolicy
{
    BlockAllFailures,
    AllowMissingGraphicsDevice
}

public sealed class ComputeDispatchExecutionStep
{
    public ComputeDispatchExecutionStep(
        ComputeDispatchExecutionStepKind kind,
        ComputeDispatchCommand command,
        bool executed,
        string reason = null,
        object payload = null)
    {
        Kind = kind;
        Command = command;
        Executed = executed;
        Reason = reason;
        Payload = payload;
    }

    public ComputeDispatchExecutionStepKind Kind { get; }

    public ComputeDispatchCommand Command { get; }

    public bool Executed { get; }

    public string Reason { get; }

    public object Payload { get; }
}

public sealed class TextureResourceUpdateResult
{
    public TextureResourceUpdateResult(TextureResource resource)
    {
        Resource = resource;
        Statuses = resource?.TextureStatuses?.ToDictionary(pair => pair.Key, pair => pair.Value)
                   ?? new Dictionary<string, string>();
        LastStatus = resource?.LastTextureStatus;
    }

    public TextureResource Resource { get; }

    public IReadOnlyDictionary<string, string> Statuses { get; }

    public string LastStatus { get; }

    public bool HasFailures => Statuses.Values.Any(status =>
        status?.StartsWith("Failed:", StringComparison.Ordinal) == true
        || status?.StartsWith("Exception:", StringComparison.Ordinal) == true);
}

public sealed class ComputeDispatchExecutionResult
{
    public ComputeDispatchExecutionResult(
        ComputeDispatchCommand command,
        IReadOnlyList<ComputeDispatchExecutionStep> steps)
    {
        Command = command;
        Steps = steps ?? [];
    }

    public ComputeDispatchCommand Command { get; }

    public IReadOnlyList<ComputeDispatchExecutionStep> Steps { get; }

    public bool IterationIndexSetApplied => HasExecuted(ComputeDispatchExecutionStepKind.IterationIndexSet);

    public bool PreRenderCommandExecuted => HasExecuted(ComputeDispatchExecutionStepKind.PreRenderCommand);

    public bool TextureResourceUpdated => HasExecuted(ComputeDispatchExecutionStepKind.TextureResourceUpdate);

    public bool Dispatched => HasExecuted(ComputeDispatchExecutionStepKind.Dispatch);

    public bool PostDispatchGraphExecuted => HasExecuted(ComputeDispatchExecutionStepKind.PostDispatchGraph);

    public IReadOnlyDictionary<string, string> TextureResourceStatuses =>
        Steps
            .Where(step => step.Kind == ComputeDispatchExecutionStepKind.TextureResourceUpdate)
            .Select(step => step.Payload)
            .OfType<TextureResourceUpdateResult>()
            .SelectMany(result => result.Statuses)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

    public bool HasTextureResourceFailures =>
        TextureResourceStatuses.Values.Any(status =>
            status?.StartsWith("Failed:", StringComparison.Ordinal) == true
            || status?.StartsWith("Exception:", StringComparison.Ordinal) == true);

    private bool HasExecuted(ComputeDispatchExecutionStepKind kind)
    {
        for (var i = 0; i < Steps.Count; i++)
        {
            if (Steps[i].Kind == kind && Steps[i].Executed)
                return true;
        }

        return false;
    }
}

public sealed class ComputeDispatchExecutor
{
    public RenderDrawContext RenderDrawContext { get; set; }

    public bool DispatchWhenRenderContextAvailable { get; set; } = true;

    public TextureResourceFailureDispatchPolicy TextureResourceFailurePolicy { get; set; } =
        TextureResourceFailureDispatchPolicy.BlockAllFailures;

    public IReadOnlyList<ComputeDispatchExecutionResult> Execute(ComputeDrawResult drawResult)
    {
        if (drawResult == null || !drawResult.CanDispatch)
            return [];

        return Execute(drawResult.DispatchCommands);
    }

    public IReadOnlyList<ComputeDispatchExecutionResult> Execute(IEnumerable<ComputeDispatchCommand> commands)
    {
        var results = new List<ComputeDispatchExecutionResult>();
        if (commands == null)
            return results;

        foreach (var command in commands)
            results.Add(Execute(command));

        return results;
    }

    public ComputeDispatchExecutionResult Execute(ComputeDispatchCommand command)
    {
        if (command == null)
            throw new ArgumentNullException(nameof(command));

        var steps = new List<ComputeDispatchExecutionStep>
        {
            ApplyIterationIndexSet(command),
            ExecutePreRenderCommand(command),
            ExecuteTextureResourceUpdate(command)
        };
        var dispatchStep = ExecuteDispatch(command, steps[^1]);
        steps.Add(dispatchStep);
        steps.Add(ExecutePostDispatchGraph(command, dispatchStep.Executed));

        return new ComputeDispatchExecutionResult(command, steps);
    }

    private static ComputeDispatchExecutionStep ApplyIterationIndexSet(ComputeDispatchCommand command)
    {
        var graph = command.IterationIndexSet?.Apply();
        return new ComputeDispatchExecutionStep(
            ComputeDispatchExecutionStepKind.IterationIndexSet,
            command,
            graph != null,
            graph == null ? "Command has no iteration index set step." : null,
            graph);
    }

    private ComputeDispatchExecutionStep ExecutePreRenderCommand(ComputeDispatchCommand command)
    {
        if (command.PreRenderCommand == null)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.PreRenderCommand,
                command,
                false,
                "Command has no pre-render command.");

        if (RenderDrawContext == null)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.PreRenderCommand,
                command,
                false,
                "RenderDrawContext is not assigned.",
                command.PreRenderCommand);

        command.PreRenderCommand.Draw(RenderDrawContext);
        return new ComputeDispatchExecutionStep(
            ComputeDispatchExecutionStepKind.PreRenderCommand,
            command,
            true,
            payload: command.PreRenderCommand);
    }

    private ComputeDispatchExecutionStep ExecuteTextureResourceUpdate(ComputeDispatchCommand command)
    {
        if (command.Stage?.ResourceProvider is not TextureResource textureResource)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.TextureResourceUpdate,
                command,
                false,
                "Stage has no texture resource.");

        if (RenderDrawContext == null)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.TextureResourceUpdate,
                command,
                false,
                "RenderDrawContext is not assigned.",
                textureResource);

        textureResource.UpdateTextures(RenderDrawContext.GraphicsDevice, nodeContext: command.Stage.NodeContext);
        return new ComputeDispatchExecutionStep(
            ComputeDispatchExecutionStepKind.TextureResourceUpdate,
            command,
            true,
            payload: new TextureResourceUpdateResult(textureResource));
    }

    private ComputeDispatchExecutionStep ExecuteDispatch(
        ComputeDispatchCommand command,
        ComputeDispatchExecutionStep textureResourceUpdateStep)
    {
        if (textureResourceUpdateStep?.Payload is TextureResourceUpdateResult textureUpdateResult)
        {
            var failures = GetBlockingTextureResourceFailures(textureUpdateResult).ToArray();
            if (failures.Length > 0)
            {
                return new ComputeDispatchExecutionStep(
                    ComputeDispatchExecutionStepKind.Dispatch,
                    command,
                    false,
                    $"Texture resource update failed: {FormatTextureResourceFailures(failures)}",
                    textureUpdateResult);
            }
        }

        if (command.Dispatcher == null)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.Dispatch,
                command,
                false,
                "Command has no compute effect dispatcher.");

        if (!DispatchWhenRenderContextAvailable)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.Dispatch,
                command,
                false,
                "Dispatch execution is disabled.",
                command.Dispatcher);

        if (RenderDrawContext == null)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.Dispatch,
                command,
                false,
                "RenderDrawContext is not assigned.",
                command.Dispatcher);

        command.Dispatcher.Dispatch(RenderDrawContext);
        return new ComputeDispatchExecutionStep(
            ComputeDispatchExecutionStepKind.Dispatch,
            command,
            true,
            payload: command.Dispatcher);
    }

    private IEnumerable<KeyValuePair<string, string>> GetBlockingTextureResourceFailures(
        TextureResourceUpdateResult result)
    {
        foreach (var pair in result.Statuses.OrderBy(pair => pair.Key))
        {
            if (!IsFailureStatus(pair.Value))
                continue;
            if (TextureResourceFailurePolicy == TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice
                && IsMissingGraphicsDeviceFailure(pair.Value))
                continue;

            yield return pair;
        }
    }

    private static bool IsFailureStatus(string status)
    {
        return status?.StartsWith("Failed:", StringComparison.Ordinal) == true
               || status?.StartsWith("Exception:", StringComparison.Ordinal) == true;
    }

    private static bool IsMissingGraphicsDeviceFailure(string status)
    {
        return status?.StartsWith("Failed:GraphicsDevice=null", StringComparison.Ordinal) == true;
    }

    private static string FormatTextureResourceFailures(IEnumerable<KeyValuePair<string, string>> failures)
    {
        return string.Join(
            ";",
            failures.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static ComputeDispatchExecutionStep ExecutePostDispatchGraph(
        ComputeDispatchCommand command,
        bool dispatchExecuted)
    {
        if (!dispatchExecuted)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.PostDispatchGraph,
                command,
                false,
                "Dispatch was not executed.");

        if (command.Stage?.ComputeGraph == null)
            return new ComputeDispatchExecutionStep(
                ComputeDispatchExecutionStepKind.PostDispatchGraph,
                command,
                false,
                "Stage has no compute graph.");

        var textureSwaps = EnumerateTextureSwaps(command.Stage.ComputeGraph).ToArray();
        foreach (var textureSwap in textureSwaps)
            textureSwap.Execute();

        return new ComputeDispatchExecutionStep(
            ComputeDispatchExecutionStepKind.PostDispatchGraph,
            command,
            textureSwaps.Length > 0,
            textureSwaps.Length == 0 ? "Compute graph has no post-dispatch texture swaps." : null,
            textureSwaps);
    }

    private static IEnumerable<TextureSwap> EnumerateTextureSwaps(AbstractShaderNode node)
    {
        if (node == null)
            yield break;

        var visited = new HashSet<AbstractShaderNode>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<AbstractShaderNode>();
        stack.Push(node);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == null || !visited.Add(current))
                continue;

            if (current is TextureSwap textureSwap)
                yield return textureSwap;

            for (var i = current.Ins.Count - 1; i >= 0; i--)
                stack.Push(current.Ins[i]);
        }
    }
}
