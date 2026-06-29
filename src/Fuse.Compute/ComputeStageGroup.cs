using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.ShaderFX;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace Fuse.compute;

[ProcessNode(Name = "Group (ComputeStage,Spectral)", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public class ComputeStageGroupSpectral : IComputeStage, IComputeStageProvider
{
    private readonly List<IComputeChangeGraph> _changeGraphListeners = new();
    private readonly List<IComputeStageProvider> _stageProviders = new();
    private readonly List<IComputeStage> _stages = new();
    private readonly List<ComputeResource> _resources = new();

    [Fragment(Order = 0)]
    public ComputeStageGroupSpectral(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext = null,
        IComputeResourceProvider resourceProvider = null,
        IEnumerable<IComputeStageProvider> computeStageProviders = null)
    {
        NodeContext = nodeContext;
        Name = "Group (ComputeStage)";
        Enabled = true;
        IterationCount = 1;
        WriteAttributes = true;
        SetResourceProvider(resourceProvider);
        Update(computeStageProviders);
    }

    [Fragment(Order = 100)]
    public ComputeStageGroupSpectral Output => this;

    public NodeContext NodeContext { get; }

    public string Name { get; private set; }

    public bool Enabled { get; private set; }

    public int IterationCount { get; private set; }

    public bool WriteAttributes { get; private set; }

    public IIndexProvider IndexProvider => null;

    public IDispatchInfo DispatchInfo => null;

    public IDispatcherProvider DispatcherProvider => null;

    public ComputeResource Resource { get; private set; }

    public IComputeResourceProvider ResourceProvider { get; private set; }

    public ShaderNode<GpuVoid> ComputeGraph => null;

    public ShaderDiagnosticContext LastDiagnosticContext => _stages
        .Select(stage => stage.LastDiagnosticContext)
        .FirstOrDefault(context => context != null);

    public string ShaderCode => string.Join(
        Environment.NewLine,
        _stages
            .Select(stage => stage.ShaderCode)
            .Where(code => !string.IsNullOrEmpty(code)));

    public IReadOnlyList<IComputeStageProvider> StageProviders => _stageProviders;

    public IReadOnlyList<IComputeStage> Stages => _stages;

    public IReadOnlyList<IComputeStage> EnabledStages => _stages
        .Where(stage => Enabled && stage.Enabled)
        .ToArray();

    public static ComputeStageGroupSpectral Create(
        NodeContext nodeContext = null,
        IComputeResourceProvider resourceProvider = null,
        IEnumerable<IComputeStageProvider> computeStageProviders = null)
    {
        return new ComputeStageGroupSpectral(nodeContext, resourceProvider, computeStageProviders);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral Update(
        IEnumerable<IComputeStageProvider> computeStageProviders = null,
        bool? enabled = null)
    {
        if (enabled.HasValue)
            SetEnabled(enabled.Value);

        if (computeStageProviders == null)
            return this;

        _stageProviders.Clear();
        _stageProviders.AddRange(computeStageProviders.Where(provider => provider != null));
        RefreshStages();
        return this;
    }

    public IComputeStage GetComputeStage()
    {
        return this;
    }

    public virtual ComputeStageGroupSpectral SetName(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "Group (ComputeStage)" : name;
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral SetEnabled(bool enabled)
    {
        Enabled = enabled;
        return this;
    }

    public bool GetEnabled()
    {
        return Enabled;
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral SetIterationCount(int iterationCount)
    {
        IterationCount = global::System.Math.Max(1, iterationCount);
        return this;
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral SetWriteAttributes(bool writeAttributes)
    {
        WriteAttributes = writeAttributes;
        foreach (var stage in _stages)
            stage.SetWriteAttributes(writeAttributes);

        return this;
    }

    IComputeStage IComputeStage.SetWriteAttributes(bool writeAttributes)
    {
        return SetWriteAttributes(writeAttributes);
    }

    [Fragment(Order = 50)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral SetResourceProvider(IComputeResourceProvider resourceProvider)
    {
        ResourceProvider = resourceProvider;
        Resource = resourceProvider?.GetComputeResource();
        MergeResources([Resource]);
        foreach (var stage in _stages)
            stage.ProcessMainResource(ResourceProvider);

        return this;
    }

    public virtual ComputeStageGroupSpectral AddChangeGraph(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null && !_changeGraphListeners.Contains(changeGraph))
            _changeGraphListeners.Add(changeGraph);

        foreach (var stage in _stages)
            stage.AddChangeGraph(changeGraph);

        return this;
    }

    IComputeStage IComputeStage.AddChangeGraph(IComputeChangeGraph changeGraph)
    {
        return AddChangeGraph(changeGraph);
    }

    public virtual ComputeStageGroupSpectral RemoveChangeGraph(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null)
            _changeGraphListeners.Remove(changeGraph);

        foreach (var stage in _stages)
            stage.RemoveChangeGraph(changeGraph);

        return this;
    }

    IComputeStage IComputeStage.RemoveChangeGraph(IComputeChangeGraph changeGraph)
    {
        return RemoveChangeGraph(changeGraph);
    }

    [Fragment(Order = 60)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral HandleAttributes()
    {
        foreach (var stage in _stages)
            stage.HandleAttributes();

        return this;
    }

    IComputeStage IComputeStage.HandleAttributes()
    {
        return HandleAttributes();
    }

    [Fragment(Order = 70)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral AddDispatchProvider()
    {
        foreach (var stage in _stages)
            stage.AddDispatchProvider();

        return this;
    }

    IComputeStage IComputeStage.AddDispatchProvider()
    {
        return AddDispatchProvider();
    }

    public IDispatcherProvider GetDispatcherProvider()
    {
        return DispatcherProvider;
    }

    public IDispatcher GetDispatcher()
    {
        return DispatcherProvider?.GetDispatcher();
    }

    public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
    {
        return _stages.SelectMany(stage => stage.GetChildren(context));
    }

    public string GetName()
    {
        return Name;
    }

    public ComputeResource GetResource()
    {
        return Resource;
    }

    public virtual int GetTicket()
    {
        return _stages
            .Select(stage => stage.GetTicket())
            .DefaultIfEmpty(0)
            .Max();
    }

    public virtual void Dispose()
    {
        foreach (var stage in _stages)
            stage.Dispose();

        _changeGraphListeners.Clear();
        _stageProviders.Clear();
        _stages.Clear();
        _resources.Clear();
        ResourceProvider = null;
        Resource = null;
    }

    [Fragment(Order = 80)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral BuildComputeGraph()
    {
        foreach (var stage in _stages)
            stage.BuildComputeGraph();

        CallChangeGraph(null);
        return this;
    }

    IComputeStage IComputeStage.BuildComputeGraph()
    {
        return BuildComputeGraph();
    }

    [Fragment(Order = 90)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        if (ResourceProvider == null && Resource == null && resourceProvider != null)
            SetResourceProvider(resourceProvider);

        var effectiveResourceProvider = ResourceProvider ?? resourceProvider;
        foreach (var stage in _stages)
            stage.ProcessMainResource(effectiveResourceProvider);

        return this;
    }

    IComputeStage IComputeStage.ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        return ProcessMainResource(resourceProvider);
    }

    [Fragment(Order = 95)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStageGroupSpectral SetPreGraphRenderer(AbstractShaderNode preGraphRenderer)
    {
        foreach (var stage in _stages)
            stage.SetPreGraphRenderer(preGraphRenderer);

        return this;
    }

    IComputeStage IComputeStage.SetPreGraphRenderer(AbstractShaderNode preGraphRenderer)
    {
        return SetPreGraphRenderer(preGraphRenderer);
    }

    public virtual IEnumerable<ComputeResource> GetResources(IEnumerable<ComputeResource> resources = null)
    {
        MergeResources(resources ?? Array.Empty<ComputeResource>());
        foreach (var stage in _stages)
            MergeResources(stage.GetResources());

        return _resources;
    }

    public virtual IEnumerable<IComputeStage> GetExecutionStages(bool includeDisabledStages = false)
    {
        if (!includeDisabledStages && !Enabled)
            return Array.Empty<IComputeStage>();

        return _stages.SelectMany(stage => stage.GetExecutionStages(includeDisabledStages));
    }

    public virtual IReadOnlyList<ShaderSource> Draw(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        return DrawResult(context, baseKeys).ShaderSources;
    }

    public virtual ComputeDrawResult DrawResult(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        if (!Enabled)
            return ComputeDrawResult.Disabled();

        return new ComputeExecutionPlanBuilder()
            .Build(_stages, GetResources(), context, baseKeys)
            .ToDrawResult();
    }

    public virtual ShaderSource DrawStage(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        return Draw(context, baseKeys).FirstOrDefault();
    }

    public virtual IReadOnlyList<ComputeDispatchExecutionResult> DrawStage(
        RenderDrawContext renderDrawContext,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null,
        TextureResourceFailureDispatchPolicy textureResourceFailurePolicy =
            TextureResourceFailureDispatchPolicy.BlockAllFailures)
    {
        if (!Enabled)
            return [];

        var shaderContext = context ?? new ShaderGeneratorContext();
        return EnabledStages
            .SelectMany(stage => stage.DrawStage(
                renderDrawContext,
                shaderContext,
                baseKeys,
                textureResourceFailurePolicy))
            .ToArray();
    }

    public ComputeDispatchInfoSplit SplitDispatchInfo()
    {
        return new ComputeDispatchInfoSplit(
            ComputeDispatchSize.Zero,
            ComputeDispatchSize.Zero,
            Array.Empty<ComputeDispatchDiagnostic>(),
            false);
    }

    public ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
    {
        return DrawStage(context, baseKeys);
    }

    protected void CallChangeGraph(AbstractShaderNode node)
    {
        foreach (var listener in _changeGraphListeners)
            listener.ChangeGraph(node);
    }

    private void RefreshStages()
    {
        _stages.Clear();
        foreach (var stage in _stageProviders.Select(provider => provider.GetComputeStage()).Where(stage => stage != null))
        {
            _stages.Add(stage);
            stage.ProcessMainResource(ResourceProvider);
        }

        MergeResources(_stages.Select(stage => stage.Resource));
        CallChangeGraph(null);
    }

    private void MergeResources(IEnumerable<ComputeResource> resources)
    {
        var merged = ComputeResource.MergeResources(_resources, resources).ToArray();
        _resources.Clear();
        _resources.AddRange(merged);
    }
}

[ProcessNode(Name = "Group (ComputeStage)", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public class ComputeStageGroup : ComputeStageGroupSpectral
{
    [Fragment(Order = 0)]
    public ComputeStageGroup(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext = null,
        IComputeResourceProvider resourceProvider = null,
        IEnumerable<IComputeStageProvider> computeStageProviders = null)
        : base(nodeContext, resourceProvider, computeStageProviders)
    {
    }

    [Fragment(Order = 100)]
    public new ComputeStageGroup Output => this;

    public new static ComputeStageGroup Create(
        NodeContext nodeContext = null,
        IComputeResourceProvider resourceProvider = null,
        IEnumerable<IComputeStageProvider> computeStageProviders = null)
    {
        return new ComputeStageGroup(nodeContext, resourceProvider, computeStageProviders);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeStageGroup Update(
        IEnumerable<IComputeStageProvider> computeStageProviders = null,
        bool? enabled = null)
    {
        base.Update(computeStageProviders, enabled);
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeStageGroup SetEnabled(bool enabled)
    {
        base.SetEnabled(enabled);
        return this;
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeStageGroup SetIterationCount(int iterationCount)
    {
        base.SetIterationCount(iterationCount);
        return this;
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeStageGroup SetWriteAttributes(bool writeAttributes)
    {
        base.SetWriteAttributes(writeAttributes);
        return this;
    }

    [Fragment(Order = 50)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeStageGroup SetResourceProvider(IComputeResourceProvider resourceProvider)
    {
        base.SetResourceProvider(resourceProvider);
        return this;
    }
}
