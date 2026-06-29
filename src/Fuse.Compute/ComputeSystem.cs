using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.ComputeSystem;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core.Import;
using VL.Model;

namespace Fuse.compute;

public sealed record ComputeStageRegistration(string NodePath, IComputeStageProvider StageProvider, IComputeStage Stage);

public enum ComputeSystemLifecycleStep
{
    PrepareResources,
    HandleAttributes,
    WriteAttributesComputeStage,
    AddDispatcherProvider,
    SetPreGraphRenderer,
    ProcessMainResource,
    GetResources,
    FinishResources
}

[ProcessNode(Name = "ComputeSystem (Spectral Advanced)", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public class ComputeSystemSpectral : IGraphicsRendererBase, IComputeChangeGraph, IDisposable
{
    private readonly List<IComputeChangeGraph> _changeGraphListeners = new();
    private readonly Dictionary<string, IComputeStageProvider> _appendedStages = new(StringComparer.Ordinal);
    private readonly List<ComputeResource> _explicitResources = new();
    private readonly List<ComputeSystemLifecycleStep> _lastLifecycleSteps = new();
    private readonly HashSet<StructuredBufferResource> _preparedStructuredResources = new();
    private readonly HashSet<TextureResource> _preparedTextureResources = new();
    private readonly List<ComputeResource> _resources = new();
    private int _autoStagePathIndex;

    [Fragment(Order = 0)]
    public ComputeSystemSpectral(object attributeValues = null)
    {
        AttributeValues = attributeValues;
        AttributeHandler = GlobalAttributeHandler.Create(attributeValues);
        Enabled = true;
    }

    [Fragment(Order = 1000)]
    public ComputeSystemSpectral Output => this;

    public object AttributeValues { get; private set; }

    public GlobalAttributeHandler AttributeHandler { get; }

    public IReadOnlyDictionary<string, AbstractShaderNode> GlobalAttributes => AttributeHandler.Values;

    public bool Enabled { get; private set; }

    public bool HasChanged { get; private set; }

    public object ExternalScheduler { get; private set; }

    public object InternalScheduler { get; private set; }

    public bool IsScheduled { get; private set; }

    public bool IsExternallyScheduled => ExternalScheduler != null;

    public bool WriteAttributes { get; private set; } = true;

    public AbstractShaderNode PreGraphRenderer { get; private set; }

    public IComputeResourceProvider MainResourceProvider { get; private set; }

    public IReadOnlyList<ComputeSystemLifecycleStep> LastLifecycleSteps => _lastLifecycleSteps;

    public IReadOnlyDictionary<string, IComputeStageProvider> AppendedStages => _appendedStages;

    public IReadOnlyList<ComputeStageRegistration> StageRegistrations => _appendedStages
        .Select(registration => new ComputeStageRegistration(
            registration.Key,
            registration.Value,
            registration.Value?.GetComputeStage()))
        .Where(registration => registration.Stage != null)
        .ToArray();

    public IReadOnlyList<IComputeStage> Stages => StageRegistrations
        .Select(registration => registration.Stage)
        .ToArray();

    public IReadOnlyList<IComputeStage> EnabledStages => Stages
        .Where(stage => Enabled && stage.Enabled)
        .ToArray();

    public IReadOnlyList<ComputeResource> Resources => _resources;

    public IReadOnlyList<ComputeDispatchDiagnostic> DispatchDiagnostics => EnabledStages
        .SelectMany(stage => (stage.GetDispatcher()?.GetDispatchInfo() ?? stage.DispatchInfo)?.Diagnostics ?? [])
        .ToArray();

    public bool IsValid => DispatchDiagnostics.Count == 0;

    public static ComputeSystemSpectral Create(object attributeValues = null)
    {
        return new ComputeSystemSpectral(attributeValues);
    }

    public virtual ComputeSystemSpectral Clear()
    {
        ClearStages();
        _explicitResources.Clear();
        _resources.Clear();
        HasChanged = true;
        return this;
    }

    public virtual void Dispose()
    {
        UnscheduleInternal();
        foreach (var stage in Stages)
        {
            stage.RemoveChangeGraph(this);
            stage.Dispose();
        }

        _changeGraphListeners.Clear();
        _appendedStages.Clear();
        _explicitResources.Clear();
        _lastLifecycleSteps.Clear();
        _preparedStructuredResources.Clear();
        _preparedTextureResources.Clear();
        _resources.Clear();
        _autoStagePathIndex = 0;
        AttributeValues = null;
        ExternalScheduler = null;
        InternalScheduler = null;
        IsScheduled = false;
        MainResourceProvider = null;
        PreGraphRenderer = null;
        HasChanged = true;
    }

    public virtual ComputeSystemSpectral Update(
        object externalScheduler = null,
        IEnumerable<IComputeStageProvider> computeStages = null,
        bool enabled = true)
    {
        AttributeValues = AttributeValues ?? AttributeHandler.AttributeValues;
        AttributeHandler.Update(AttributeValues);
        SetExternalScheduler(externalScheduler);
        SetEnabled(enabled);
        if (computeStages != null)
        {
            ClearStages();
            foreach (var stageProvider in computeStages)
                AppendComputeStage(null, stageProvider);
        }

        BuildComputeGraph();
        UpdateScheduler();
        return this;
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral Update(
        object externalScheduler,
        IEnumerable<IComputeStageProvider> computeStages,
        bool enabled,
        out IReadOnlyDictionary<string, AbstractShaderNode> globalAttributes,
        out bool hasChanged)
    {
        Update(externalScheduler, computeStages, enabled);
        globalAttributes = GlobalAttributes;
        hasChanged = HasChanged;
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral SetInput(
        IComputeResourceProvider resourceProvider,
        IEnumerable<IComputeStageProvider> stages)
    {
        Clear();

        if (resourceProvider != null)
            MergeExplicitResources([resourceProvider.GetComputeResource()]);

        if (stages != null)
        {
            foreach (var stageProvider in stages)
                AppendComputeStage(null, stageProvider);
        }

        RebuildResources();
        return this;
    }

    public virtual ComputeSystemSpectral ChangeGraph(AbstractShaderNode node)
    {
        HasChanged = true;
        foreach (var listener in _changeGraphListeners)
            listener.ChangeGraph(node);

        return this;
    }

    void IComputeChangeGraph.ChangeGraph(AbstractShaderNode node)
    {
        ChangeGraph(node);
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral PrepareResources()
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.PrepareResources);
        _preparedStructuredResources.Clear();
        _preparedTextureResources.Clear();
        AttributeHandler.Prepare();
        return this;
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral HandleAttributes()
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.HandleAttributes);
        foreach (var stage in Stages)
            stage.HandleAttributes();

        var executionStages = GetLifecycleStages().ToArray();
        AttributeHandler.BindAttributes(executionStages, AttributeValues);
        foreach (var stage in executionStages)
        {
            HandleStructuredBufferAttributes(stage);
            HandleTextureAttributes(stage);
        }

        return this;
    }

    [Fragment(Order = 50)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral FinishResources()
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.FinishResources);
        HasChanged = AttributeHandler.Finish() || HasChanged;
        foreach (var resource in _preparedStructuredResources)
        {
            resource.FinishAttributeMap();
            if (resource.ChangedAttributes)
            {
                resource.UpdateStruct();
                HasChanged = true;
            }
        }

        _preparedStructuredResources.Clear();
        foreach (var resource in _preparedTextureResources)
        {
            resource.FinishAttributeMap();
            if (resource.ChangedAttributes)
                HasChanged = true;
        }

        _preparedTextureResources.Clear();
        return this;
    }

    public virtual ComputeSystemSpectral RemoveEventHook(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null)
            _changeGraphListeners.Remove(changeGraph);

        return this;
    }

    public virtual ComputeSystemSpectral AddEventHook(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null && !_changeGraphListeners.Contains(changeGraph))
            _changeGraphListeners.Add(changeGraph);

        return this;
    }

    public virtual IReadOnlyList<ShaderSource> Draw(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        return DrawResult(context, baseKeys).ShaderSources;
    }

    public virtual IReadOnlyList<ComputeDispatchExecutionResult> Draw(
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

    void IGraphicsRendererBase.Draw(RenderDrawContext context)
    {
        Draw(context);
    }

    public virtual ComputeDrawResult DrawResult(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        if (!Enabled)
            return ComputeDrawResult.Disabled();

        return BuildExecutionPlan(context, baseKeys).ToDrawResult();
    }

    public virtual IReadOnlyList<ComputeDispatchExecutionResult> Execute(
        RenderDrawContext renderDrawContext,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null,
        TextureResourceFailureDispatchPolicy textureResourceFailurePolicy =
            TextureResourceFailureDispatchPolicy.BlockAllFailures)
    {
        return Draw(renderDrawContext, context, baseKeys, textureResourceFailurePolicy);
    }

    public virtual ComputeDispatchRenderer ToRenderer(
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null)
    {
        return DrawResult(context ?? new ShaderGeneratorContext(), baseKeys)
            .ToRenderer();
    }

    public virtual bool TryCreateRenderer(
        out ComputeDispatchRenderer renderer,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null)
    {
        return DrawResult(context ?? new ShaderGeneratorContext(), baseKeys)
            .TryCreateRenderer(out renderer);
    }

    [Fragment(Order = 60)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral SetEnabled(bool enabled)
    {
        var wasEnabled = Enabled;
        Enabled = enabled;
        if (wasEnabled && !enabled)
            UnscheduleInternal();

        return this;
    }

    public virtual ComputeSystemSpectral SetExternalScheduler(object externalScheduler)
    {
        if (ReferenceEquals(ExternalScheduler, externalScheduler))
            return this;

        UnscheduleInternal();
        ExternalScheduler = externalScheduler;
        return this;
    }

    public virtual ComputeSystemSpectral SetInternalScheduler(object internalScheduler)
    {
        if (ReferenceEquals(InternalScheduler, internalScheduler))
            return this;

        UnscheduleInternal();
        InternalScheduler = internalScheduler;
        return this;
    }

    [Fragment(Order = 70)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral WriteAttributesComputeStage()
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.WriteAttributesComputeStage);
        foreach (var stage in Stages)
            stage.SetWriteAttributes(WriteAttributes);

        return this;
    }

    [Fragment(Order = 80)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral AddDispatcherProvider()
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.AddDispatcherProvider);
        foreach (var stage in Stages)
            stage.AddDispatchProvider();

        return this;
    }

    [Fragment(Order = 90)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral SetPreGraphRenderer(AbstractShaderNode preGraphRenderer)
    {
        PreGraphRenderer = preGraphRenderer;
        RecordLifecycleStep(ComputeSystemLifecycleStep.SetPreGraphRenderer);
        foreach (var stage in Stages)
            stage.SetPreGraphRenderer(preGraphRenderer);

        return this;
    }

    [Fragment(Order = 100)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral BuildComputeGraph()
    {
        BeginLifecycle();
        PrepareResources();
        HandleAttributes();
        WriteAttributesComputeStage();
        AddDispatcherProvider();
        ApplyPreGraphRenderer();
        if (MainResourceProvider != null)
            ProcessMainResource(MainResourceProvider);
        else
            GetResources();
        foreach (var stage in Stages)
            stage.BuildComputeGraph();
        FinishResources();

        return this;
    }

    public virtual int GetTicket()
    {
        return Stages
            .Select(stage => stage.GetTicket())
            .DefaultIfEmpty(0)
            .Max();
    }

    [Fragment(Order = 110)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        if (resourceProvider == null)
            return this;

        MainResourceProvider = resourceProvider;
        RecordLifecycleStep(ComputeSystemLifecycleStep.ProcessMainResource);
        MergeExplicitResources([resourceProvider.GetComputeResource()]);
        foreach (var stage in Stages)
            stage.ProcessMainResource(resourceProvider);

        RebuildResources();
        return this;
    }

    [Fragment(Order = 120)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeSystemSpectral AppendComputeStage(IComputeStageProvider stageProvider)
    {
        return AppendComputeStage(null, stageProvider);
    }

    public virtual ComputeSystemSpectral AppendComputeStage(string nodePath, IComputeStageProvider stageProvider)
    {
        if (stageProvider == null)
            return this;

        var normalizedNodePath = NormalizeNodePath(nodePath);
        if (_appendedStages.TryGetValue(normalizedNodePath, out var previousProvider))
            previousProvider?.GetComputeStage()?.RemoveChangeGraph(this);

        _appendedStages[normalizedNodePath] = stageProvider;
        stageProvider.GetComputeStage()?.AddChangeGraph(this);
        RebuildResources();
        HasChanged = true;
        return this;
    }

    [Fragment(Order = 130)]
    public virtual IReadOnlyList<ComputeResource> GetResources(IEnumerable<ComputeResource> resources = null)
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.GetResources);
        MergeExplicitResources(resources ?? Array.Empty<ComputeResource>());
        RebuildResources();

        return Resources;
    }

    public IReadOnlyList<ShaderSource> GenerateShaderSources(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys,
        bool includeDisabledStages = false)
    {
        if (!Enabled && !includeDisabledStages)
            return Array.Empty<ShaderSource>();

        return BuildExecutionPlan(
                context,
                baseKeys,
                generateShaderSources: true,
                includeDisabledStages: includeDisabledStages)
            .ToDrawResult(Enabled || includeDisabledStages)
            .ShaderSources;
    }

    public ComputeExecutionPlan BuildExecutionPlan(
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null,
        bool generateShaderSources = true,
        bool includeDisabledStages = false)
    {
        return new ComputeExecutionPlanBuilder
        {
            GenerateShaderSources = generateShaderSources,
            IncludeDisabledStages = includeDisabledStages
        }.Build(this, context, baseKeys);
    }

    public IComputeStage FindStage(string name)
    {
        return Stages.FirstOrDefault(stage => stage.Name == name);
    }

    protected void ClearStages()
    {
        foreach (var stage in Stages)
            stage.RemoveChangeGraph(this);

        _appendedStages.Clear();
        _resources.Clear();
        HasChanged = true;
    }

    protected void MergeExplicitResources(IEnumerable<ComputeResource> resources)
    {
        var merged = ComputeResource.MergeResources(_explicitResources, resources).ToArray();
        _explicitResources.Clear();
        _explicitResources.AddRange(merged);
    }

    protected void MergeResources(IEnumerable<ComputeResource> resources)
    {
        var merged = ComputeResource.MergeResources(_resources, resources).ToArray();
        _resources.Clear();
        _resources.AddRange(merged);
    }

    private string NormalizeNodePath(string nodePath)
    {
        return string.IsNullOrWhiteSpace(nodePath)
            ? $"__auto/{_autoStagePathIndex++}"
            : nodePath;
    }

    private void RebuildResources()
    {
        _resources.Clear();
        MergeResources(_explicitResources);
        foreach (var stage in Stages)
            MergeResources(stage.GetResources());
    }

    private void BeginLifecycle()
    {
        _lastLifecycleSteps.Clear();
    }

    private void RecordLifecycleStep(ComputeSystemLifecycleStep step)
    {
        _lastLifecycleSteps.Add(step);
    }

    private void ApplyPreGraphRenderer()
    {
        RecordLifecycleStep(ComputeSystemLifecycleStep.SetPreGraphRenderer);
        if (PreGraphRenderer == null)
            return;

        foreach (var stage in Stages)
            stage.SetPreGraphRenderer(PreGraphRenderer);
    }

    private IEnumerable<IComputeStage> GetLifecycleStages()
    {
        return Stages.SelectMany(stage => stage.GetExecutionStages(includeDisabledStages: true));
    }

    private void HandleStructuredBufferAttributes(IComputeStage stage)
    {
        var resource = stage.ResourceProvider as StructuredBufferResource
                       ?? MainResourceProvider as StructuredBufferResource;
        if (resource == null)
            return;

        var attributes = GlobalAttributeHandler
            .CollectAttributes(stage.ComputeGraph)
            .Where(attribute => attribute.AttributeType == AttributeType.StructuredBuffer)
            .ToArray();
        if (attributes.Length == 0)
            return;

        if (_preparedStructuredResources.Add(resource))
            resource.Prepare();

        foreach (var attribute in attributes)
            resource.HandleAttribute(attribute);
    }

    private void HandleTextureAttributes(IComputeStage stage)
    {
        var resource = stage.ResourceProvider as TextureResource
                       ?? MainResourceProvider as TextureResource;
        if (resource == null)
            return;

        var attributes = GlobalAttributeHandler
            .CollectAttributes(stage.ComputeGraph)
            .Where(attribute => attribute.AttributeType == AttributeType.Texture)
            .ToArray();
        if (attributes.Length == 0)
            return;

        if (_preparedTextureResources.Add(resource))
            resource.Prepare();

        foreach (var attribute in attributes)
            resource.HandleAttribute(attribute);
    }

    private void UpdateScheduler()
    {
        if (!Enabled || ExternalScheduler != null)
        {
            UnscheduleInternal();
            return;
        }

        if (!IsScheduled)
            IsScheduled = ComputeRendererScheduler.Schedule(InternalScheduler, this);
    }

    private void UnscheduleInternal()
    {
        if (!IsScheduled)
            return;

        if (ComputeRendererScheduler.Remove(InternalScheduler, this))
            IsScheduled = false;
    }
}

[ProcessNode(Name = "ComputeSystem", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public class ComputeSystem : ComputeSystemSpectral
{
    [Fragment(Order = 0)]
    public ComputeSystem(object attributeValues = null)
        : base(attributeValues)
    {
    }

    [Fragment(Order = 100)]
    public new ComputeSystem Output => this;

    public new static ComputeSystem Create(object attributeValues = null)
    {
        return new ComputeSystem(attributeValues);
    }

    public ComputeSystem Update(
        IComputeStage computeStage,
        object externalScheduler = null,
        bool enabled = true)
    {
        base.Update(externalScheduler, computeStage == null ? [] : [computeStage], enabled);
        return this;
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public ComputeSystem Update(
        IComputeStage computeStage,
        object externalScheduler,
        bool enabled,
        out bool hasChanged)
    {
        Update(computeStage, externalScheduler, enabled);
        hasChanged = HasChanged;
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeSystem SetEnabled(bool enabled)
    {
        base.SetEnabled(enabled);
        return this;
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeSystem SetInput(
        IComputeResourceProvider resourceProvider,
        IEnumerable<IComputeStageProvider> stages)
    {
        base.SetInput(resourceProvider, stages);
        return this;
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeSystem ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        base.ProcessMainResource(resourceProvider);
        return this;
    }

    [Fragment(Order = 50)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeSystem AppendComputeStage(IComputeStageProvider stageProvider)
    {
        base.AppendComputeStage(stageProvider);
        return this;
    }

    [Fragment(Order = 60)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeSystem BuildComputeGraph()
    {
        base.BuildComputeGraph();
        return this;
    }

    [Fragment(Order = 70)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public override ComputeSystem Clear()
    {
        base.Clear();
        return this;
    }
}
