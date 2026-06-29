using System;
using System.Collections.Generic;
using System.Linq;
using Fuse;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Rendering.ComputeEffect;

namespace Fuse.compute;

public enum ComputeStageExecutionStatus
{
    Ready,
    Disabled,
    InvalidDispatch,
    MissingGraph,
    ShaderGenerationFailed
}

public sealed class ComputeExecutionPlan
{
    public ComputeExecutionPlan(
        IReadOnlyList<ComputeStageExecutionPlan> stages,
        IReadOnlyList<ComputeResource> resources)
    {
        Stages = stages ?? [];
        Resources = resources ?? [];
    }

    public IReadOnlyList<ComputeStageExecutionPlan> Stages { get; }

    public IReadOnlyList<ComputeResource> Resources { get; }

    public IReadOnlyList<ComputeStageExecutionPlan> ReadyStages => Stages
        .Where(stage => stage.Status == ComputeStageExecutionStatus.Ready)
        .ToArray();

    public IReadOnlyList<ComputeStageExecutionPlan> SkippedStages => Stages
        .Where(stage => stage.Status != ComputeStageExecutionStatus.Ready)
        .ToArray();

    public IReadOnlyList<ComputeDispatchDiagnostic> DispatchDiagnostics => Stages
        .SelectMany(stage => stage.Dispatch.Diagnostics ?? [])
        .ToArray();

    public bool CanExecute => Stages.Count > 0 && SkippedStages.Count == 0;

    public ComputeDrawResult ToDrawResult(bool enabled = true)
    {
        return new ComputeDrawResult(this, enabled);
    }
}

public sealed class ComputeDrawResult
{
    public ComputeDrawResult(
        ComputeExecutionPlan plan,
        bool enabled = true)
    {
        Plan = plan ?? new ComputeExecutionPlan([], []);
        Enabled = enabled;
        Stages = Plan.Stages
            .Select(stage => new ComputeStageDrawResult(stage))
            .ToArray();
    }

    public bool Enabled { get; }

    public ComputeExecutionPlan Plan { get; }

    public IReadOnlyList<ComputeStageDrawResult> Stages { get; }

    public IReadOnlyList<ComputeStageDrawResult> ReadyStages => Stages
        .Where(stage => stage.Status == ComputeStageExecutionStatus.Ready)
        .ToArray();

    public IReadOnlyList<ComputeStageDrawResult> SkippedStages => Stages
        .Where(stage => stage.Status != ComputeStageExecutionStatus.Ready)
        .ToArray();

    public IReadOnlyList<ShaderSource> ShaderSources => ReadyStages
        .Select(stage => stage.ShaderSource)
        .Where(shaderSource => shaderSource != null)
        .ToArray();

    public IReadOnlyList<ComputeDispatchCommand> DispatchCommands => ReadyStages
        .SelectMany(stage => stage.DispatchCommands)
        .ToArray();

    public IReadOnlyList<ComputeResource> Resources => Plan.Resources;

    public IReadOnlyList<ComputeDispatchDiagnostic> DispatchDiagnostics => Plan.DispatchDiagnostics;

    public bool CanDispatch => Enabled && Plan.CanExecute;

    public static ComputeDrawResult Disabled()
    {
        return new ComputeDrawResult(new ComputeExecutionPlan([], []), enabled: false);
    }

    public IReadOnlyList<ComputeDispatchExecutionResult> Execute(
        RenderDrawContext renderDrawContext = null,
        TextureResourceFailureDispatchPolicy textureResourceFailurePolicy =
            TextureResourceFailureDispatchPolicy.BlockAllFailures)
    {
        return new ComputeDispatchExecutor
        {
            RenderDrawContext = renderDrawContext,
            TextureResourceFailurePolicy = textureResourceFailurePolicy
        }.Execute(this);
    }

    public ComputeDispatchRenderer ToRenderer()
    {
        return new ComputeDispatchRenderer
        {
            DrawResult = this
        };
    }

    public bool TryCreateRenderer(out ComputeDispatchRenderer renderer)
    {
        try
        {
            renderer = ToRenderer();
            return true;
        }
        catch (InvalidOperationException exception) when (ComputeDispatchRenderer.IsMissingAppHost(exception))
        {
            renderer = null;
            return false;
        }
    }
}

public sealed class ComputeStageDrawResult
{
    public ComputeStageDrawResult(ComputeStageExecutionPlan stagePlan)
    {
        StagePlan = stagePlan ?? throw new ArgumentNullException(nameof(stagePlan));
    }

    public ComputeStageExecutionPlan StagePlan { get; }

    public IComputeStage Stage => StagePlan.Stage;

    public string StageName => Stage?.Name;

    public ComputeStageExecutionStatus Status => StagePlan.Status;

    public ComputeDispatchSize DispatchGroups => StagePlan.DispatchGroups;

    public ComputeDispatchSize ThreadGroupSize => StagePlan.ThreadGroupSize;

    public int IterationCount => StagePlan.IterationCount;

    public ShaderSource ShaderSource => StagePlan.ShaderSource;

    public IGraphicsRendererBase PreRenderCommand => StagePlan.Dispatch.PreRenderCommand;

    public IComputeEffectDispatcher Dispatcher => StagePlan.Dispatch.Dispatcher;

    public IDispatcher StageDispatcher => StagePlan.StageDispatcher;

    public IDispatchInfo DispatchInfo => StagePlan.DispatchInfo;

    public bool SkipOutsideRange => StagePlan.Dispatch.SkipOutsideRange;

    public IReadOnlyList<ComputeDispatchCommand> DispatchCommands => CanDispatch
        ? Enumerable
            .Range(0, global::System.Math.Max(1, IterationCount))
            .Select(iterationIndex => new ComputeDispatchCommand(this, iterationIndex))
            .ToArray()
        : [];

    public string Reason => StagePlan.Reason;

    public Exception Exception => StagePlan.Exception;

    public IReadOnlyList<ComputeDispatchDiagnostic> DispatchDiagnostics =>
        StagePlan.Dispatch.Diagnostics ?? [];

    public bool CanDispatch => Status == ComputeStageExecutionStatus.Ready;
}

public sealed class ComputeDispatchCommand
{
    public ComputeDispatchCommand(
        ComputeStageDrawResult stageResult,
        int iterationIndex)
    {
        StageResult = stageResult ?? throw new ArgumentNullException(nameof(stageResult));
        IterationIndex = iterationIndex;
        IterationIndexGlobal = Fuse.ComputeSystem.IterationIndexGlobal.Create(
            stageResult.Stage?.NodeContext,
            iterationIndex);
        IterationIndexSet = new Fuse.ComputeSystem.GlobalAttributeSet<int>(
            IterationIndexGlobal,
            IterationIndexValue);
    }

    public ComputeStageDrawResult StageResult { get; }

    public IComputeStage Stage => StageResult.Stage;

    public string StageName => StageResult.StageName;

    public int IterationIndex { get; }

    public Fuse.ComputeSystem.IterationIndexGlobal IterationIndexGlobal { get; }

    public Fuse.ComputeSystem.GlobalAttributeSet<int> IterationIndexSet { get; }

    public ShaderNode<int> IterationIndexGraph => IterationIndexGlobal.GetGraph();

    public ShaderNode<int> IterationIndexValue => IterationIndexGlobal.GetValue();

    public int IterationCount => StageResult.IterationCount;

    public ComputeDispatchSize DispatchGroups => StageResult.DispatchGroups;

    public ComputeDispatchSize ThreadGroupSize => StageResult.ThreadGroupSize;

    public IGraphicsRendererBase PreRenderCommand => StageResult.PreRenderCommand;

    public IComputeEffectDispatcher Dispatcher => StageResult.Dispatcher;

    public IDispatcher StageDispatcher => StageResult.StageDispatcher;

    public IDispatchInfo DispatchInfo => StageResult.DispatchInfo;

    public bool SkipOutsideRange => StageResult.SkipOutsideRange;

    public ShaderSource ShaderSource => StageResult.ShaderSource;
}

public sealed class ComputeStageExecutionPlan
{
    public ComputeStageExecutionPlan(
        IComputeStage stage,
        ComputeStageExecutionStatus status,
        ComputeDispatchInfoSplit dispatch,
        IDispatcherProvider dispatcherProvider = null,
        IDispatcher stageDispatcher = null,
        IDispatchInfo dispatchInfo = null,
        ShaderSource shaderSource = null,
        string reason = null,
        Exception exception = null)
    {
        Stage = stage;
        Status = status;
        Dispatch = dispatch;
        DispatcherProvider = dispatcherProvider;
        StageDispatcher = stageDispatcher;
        DispatchInfo = dispatchInfo;
        ShaderSource = shaderSource;
        Reason = reason;
        Exception = exception;
    }

    public IComputeStage Stage { get; }

    public ComputeStageExecutionStatus Status { get; }

    public ComputeDispatchInfoSplit Dispatch { get; }

    public IDispatcherProvider DispatcherProvider { get; }

    public IDispatcher StageDispatcher { get; }

    public IDispatchInfo DispatchInfo { get; }

    public ShaderSource ShaderSource { get; }

    public string Reason { get; }

    public Exception Exception { get; }

    public ComputeDispatchSize DispatchGroups => Dispatch.DispatchGroups;

    public ComputeDispatchSize ThreadGroupSize => Dispatch.ThreadGroupSize;

    public int IterationCount => Stage?.IterationCount ?? 0;
}

public sealed class ComputeExecutionPlanBuilder
{
    public bool GenerateShaderSources { get; set; } = true;

    public bool IncludeDisabledStages { get; set; }

    public ComputeExecutionPlan Build(
        ComputeSystemSpectral system,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null)
    {
        if (system == null)
            throw new ArgumentNullException(nameof(system));

        return Build(system.Stages, system.GetResources(), context, baseKeys);
    }

    public ComputeExecutionPlan Build(
        IEnumerable<IComputeStage> stages,
        IEnumerable<ComputeResource> resources = null,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null)
    {
        var executionStages = (stages ?? Array.Empty<IComputeStage>())
            .Where(stage => stage != null)
            .SelectMany(stage => stage.GetExecutionStages(IncludeDisabledStages))
            .ToArray();

        var drawStagePipeline = new ComputeDrawStagePipeline
        {
            GenerateShaderSource = GenerateShaderSources
        };
        var shaderContext = context ?? new ShaderGeneratorContext();
        var stagePlans = new List<ComputeStageExecutionPlan>();
        foreach (var stage in executionStages)
            stagePlans.Add(drawStagePipeline.Build(stage, shaderContext, baseKeys));

        var mergedResources = ComputeResource
            .MergeResources(
                resources ?? Array.Empty<ComputeResource>(),
                executionStages.SelectMany(stage => stage.GetResources()))
            .ToArray();

        return new ComputeExecutionPlan(stagePlans, mergedResources);
    }

    private static ComputeDispatchInfoSplit EmptyDispatch => new(
        ComputeDispatchSize.Zero,
        ComputeDispatchSize.Zero,
        Array.Empty<ComputeDispatchDiagnostic>(),
        false);
}

internal sealed class ComputeDrawStagePipeline
{
    public bool GenerateShaderSource { get; set; } = true;

    public ComputeStageExecutionPlan Build(
        IComputeStage stage,
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys)
    {
        if (stage == null)
            return new ComputeStageExecutionPlan(
                null,
                ComputeStageExecutionStatus.MissingGraph,
                EmptyDispatch,
                reason: "Stage is null.");

        var dispatcherProvider = stage.GetDispatcherProvider();
        var stageDispatcher = dispatcherProvider?.GetDispatcher();
        var dispatchInfo = stageDispatcher?.GetDispatchInfo() ?? stage.DispatchInfo;
        var dispatch = dispatchInfo?.Split()
            ?? stage.SplitDispatchInfo();

        if (!stage.Enabled)
            return new ComputeStageExecutionPlan(
                stage,
                ComputeStageExecutionStatus.Disabled,
                dispatch,
                dispatcherProvider: dispatcherProvider,
                stageDispatcher: stageDispatcher,
                dispatchInfo: dispatchInfo,
                reason: "Stage is disabled.");

        if (stage.ComputeGraph == null)
            return new ComputeStageExecutionPlan(
                stage,
                ComputeStageExecutionStatus.MissingGraph,
                dispatch,
                dispatcherProvider: dispatcherProvider,
                stageDispatcher: stageDispatcher,
                dispatchInfo: dispatchInfo,
                reason: "Stage has no compute graph.");

        if (!dispatch.IsValid)
            return new ComputeStageExecutionPlan(
                stage,
                ComputeStageExecutionStatus.InvalidDispatch,
                dispatch,
                dispatcherProvider: dispatcherProvider,
                stageDispatcher: stageDispatcher,
                dispatchInfo: dispatchInfo,
                reason: "Stage dispatch info is invalid.");

        if (!GenerateShaderSource)
            return new ComputeStageExecutionPlan(
                stage,
                ComputeStageExecutionStatus.Ready,
                dispatch,
                dispatcherProvider: dispatcherProvider,
                stageDispatcher: stageDispatcher,
                dispatchInfo: dispatchInfo);

        try
        {
            var shaderSource = stage.GenerateShaderSource(context, baseKeys);
            return new ComputeStageExecutionPlan(
                stage,
                ComputeStageExecutionStatus.Ready,
                dispatch,
                dispatcherProvider: dispatcherProvider,
                stageDispatcher: stageDispatcher,
                dispatchInfo: dispatchInfo,
                shaderSource: shaderSource);
        }
        catch (Exception exception)
        {
            return new ComputeStageExecutionPlan(
                stage,
                ComputeStageExecutionStatus.ShaderGenerationFailed,
                dispatch,
                dispatcherProvider: dispatcherProvider,
                stageDispatcher: stageDispatcher,
                dispatchInfo: dispatchInfo,
                reason: exception.Message,
                exception: exception);
        }
    }

    private static ComputeDispatchInfoSplit EmptyDispatch => new(
        ComputeDispatchSize.Zero,
        ComputeDispatchSize.Zero,
        Array.Empty<ComputeDispatchDiagnostic>(),
        false);
}
