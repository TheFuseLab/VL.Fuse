using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.ComputeSystem;
using Fuse.ShaderFX;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace Fuse.compute;

public interface IComputeResourceProvider
{
    ComputeResource GetComputeResource();
}

public interface IComputeStageProvider
{
    IComputeStage GetComputeStage();
}

public interface IComputeChangeGraph
{
    void ChangeGraph(AbstractShaderNode node);
}

public interface IComputeStage : IComputeStageProvider, IDisposable
{
    NodeContext NodeContext { get; }

    string Name { get; }

    bool Enabled { get; }

    int IterationCount { get; }

    bool WriteAttributes { get; }

    IIndexProvider IndexProvider { get; }

    IDispatchInfo DispatchInfo { get; }

    IDispatcherProvider DispatcherProvider { get; }

    ComputeResource Resource { get; }

    IComputeResourceProvider ResourceProvider { get; }

    ShaderNode<GpuVoid> ComputeGraph { get; }

    ShaderDiagnosticContext LastDiagnosticContext { get; }

    string ShaderCode { get; }

    IComputeStage SetWriteAttributes(bool writeAttributes);

    IComputeStage AddChangeGraph(IComputeChangeGraph changeGraph);

    IComputeStage RemoveChangeGraph(IComputeChangeGraph changeGraph);

    IComputeStage HandleAttributes();

    IComputeStage AddDispatchProvider();

    IDispatcherProvider GetDispatcherProvider();

    IDispatcher GetDispatcher();

    IEnumerable<IComputeNode> GetChildren(object context = null);

    string GetName();

    ComputeResource GetResource();

    int GetTicket();

    IComputeStage BuildComputeGraph();

    IComputeStage ProcessMainResource(IComputeResourceProvider resourceProvider);

    IComputeStage SetPreGraphRenderer(AbstractShaderNode preGraphRenderer);

    IEnumerable<ComputeResource> GetResources(IEnumerable<ComputeResource> resources = null);

    IEnumerable<IComputeStage> GetExecutionStages(bool includeDisabledStages = false);

    ComputeDispatchInfoSplit SplitDispatchInfo();

    ComputeDrawResult DrawResult(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys = null);

    IReadOnlyList<ShaderSource> Draw(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys = null);

    ShaderSource DrawStage(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys = null);

    IReadOnlyList<ComputeDispatchExecutionResult> DrawStage(
        RenderDrawContext renderDrawContext,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null,
        TextureResourceFailureDispatchPolicy textureResourceFailurePolicy =
            TextureResourceFailureDispatchPolicy.BlockAllFailures);

    ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys);
}

[ProcessNode(Name = "ComputeStage", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public class ComputeStage : IComputeStage, IComputeStageProvider
{
    private readonly List<IComputeChangeGraph> _changeGraphListeners = new();

    [Fragment(Order = 0)]
    public ComputeStage(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext,
        ShaderNode<GpuVoid> computeGraph = null,
        IDispatchInfo dispatchInfo = null,
        string name = null)
    {
        NodeContext = nodeContext;
        ComputeGraph = computeGraph;
        ShaderNode = computeGraph;
        DispatchInfo = dispatchInfo;
        Name = string.IsNullOrWhiteSpace(name) ? "ComputeStage" : name;
        Enabled = true;
        IterationCount = 1;
        WriteAttributes = true;
        IndexProvider = new DispatchIdIndexProvider();
    }

    [Fragment(Order = 100)]
    public ComputeStage Output => this;

    public NodeContext NodeContext { get; }

    public string Name { get; private set; }

    public bool Enabled { get; private set; }

    public int IterationCount { get; private set; }

    public bool WriteAttributes { get; private set; }

    public bool RegisterGeneratedShaderSource { get; set; } = true;

    public IIndexProvider IndexProvider { get; private set; }

    public IDispatchInfo DispatchInfo { get; private set; }

    public IDispatcherProvider DispatcherProvider { get; private set; }

    public ComputeResource Resource { get; private set; }

    public IComputeResourceProvider ResourceProvider { get; private set; }

    public StructuredBufferResourceBindings StructuredBufferBindings { get; private set; }

    public ComputeGraph ComputeGraphNode { get; private set; }

    public ShaderNode<GpuVoid> ComputeGraph { get; private set; }

    public AbstractShaderNode ShaderNode { get; private set; }

    public AbstractShaderNode PreGraphRenderer { get; private set; }

    public ShaderDiagnosticContext LastDiagnosticContext { get; private set; }

    public string ShaderCode { get; private set; }

    public string LastError { get; private set; }

    public int Ticket { get; private set; }

    public ToComputeFx<GpuVoid> LastShaderGenerator { get; private set; }

    public static ComputeStage Create(
        NodeContext nodeContext,
        IIndexProvider indexProvider = null,
        IComputeResourceProvider resourceProvider = null)
    {
        var stage = new ComputeStage(nodeContext)
            .SetIndexProvider(indexProvider)
            .SetResourceProvider(resourceProvider);

        if (resourceProvider is StructuredBufferResource structuredBufferResource)
            stage.BindStructuredBufferResource(structuredBufferResource);

        return stage;
    }

    public static ComputeStage FromStructuredBufferResource(
        StructuredBufferResource resource,
        NodeContext nodeContext = null,
        IIndexProvider indexProvider = null,
        AttributeMap attributeMap = null,
        string name = null)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        return new ComputeStage(nodeContext, name: name)
            .SetIndexProvider(indexProvider ?? new DispatchIdIndexProvider())
            .BindStructuredBufferResource(resource, attributeMap);
    }

    public IComputeStage GetComputeStage()
    {
        return this;
    }

    public virtual ComputeStage SetName(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "ComputeStage" : name;
        return this;
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetEnabled(bool enabled)
    {
        Enabled = enabled;
        return this;
    }

    [Fragment(Order = 11)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetIterationCount(int iterationCount)
    {
        IterationCount = global::System.Math.Max(1, iterationCount);
        return this;
    }

    [Fragment(Order = 12)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetWriteAttributes(bool writeAttributes)
    {
        WriteAttributes = writeAttributes;
        return this;
    }

    IComputeStage IComputeStage.SetWriteAttributes(bool writeAttributes)
    {
        return SetWriteAttributes(writeAttributes);
    }

    [Fragment(Order = 13)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetIndexProvider(IIndexProvider indexProvider)
    {
        IndexProvider = indexProvider ?? new DispatchIdIndexProvider();
        if (DispatcherProvider is DirectDispatcher directDispatcher)
            directDispatcher.SetIndexProvider(IndexProvider);
        return this;
    }

    public virtual ComputeStage SetDispatcherProvider(IDispatchInfo dispatchInfo)
    {
        DispatchInfo = dispatchInfo;
        DispatcherProvider = dispatchInfo == null
            ? null
            : new DirectDispatcher(dispatchInfo, IndexProvider);
        return this;
    }

    public virtual ComputeStage SetDispatcherProvider(IDispatcherProvider dispatcherProvider)
    {
        DispatcherProvider = dispatcherProvider;
        DispatchInfo = dispatcherProvider?.GetDispatcher()?.GetDispatchInfo() ?? DispatchInfo;
        return this;
    }

    public IDispatcherProvider GetDispatcherProvider()
    {
        return DispatcherProvider;
    }

    public IDispatcher GetDispatcher()
    {
        return DispatcherProvider?.GetDispatcher();
    }

    [Fragment(Order = 14)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetDispatchInfo(IDispatchInfo dispatchInfo)
    {
        DispatchInfo = dispatchInfo;
        if (DispatcherProvider is DirectDispatcher directDispatcher)
            directDispatcher.SetDispatchInfo(dispatchInfo);
        else if (DispatcherProvider == null && dispatchInfo != null)
            DispatcherProvider = new DirectDispatcher(dispatchInfo, IndexProvider);
        return this;
    }

    public virtual ComputeStage SetResource(ComputeResource resource)
    {
        Resource = resource;
        return this;
    }

    [Fragment(Order = 15)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetResourceProvider(IComputeResourceProvider resourceProvider)
    {
        ResourceProvider = resourceProvider;
        Resource = resourceProvider?.GetComputeResource();
        if (resourceProvider is TextureResource textureResource)
            SetDispatchInfo(textureResource.GetDispatchInfo());
        return this;
    }

    [Fragment(Order = 16)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetComputeGraph(ShaderNode<GpuVoid> computeGraph)
    {
        ComputeGraphNode = null;
        ComputeGraph = computeGraph;
        ShaderNode = computeGraph;
        CallChangeGraph(computeGraph);
        return this;
    }

    [Fragment(Order = 17)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage SetPreGraphRenderer(AbstractShaderNode preGraphRenderer)
    {
        PreGraphRenderer = preGraphRenderer;
        return this;
    }

    IComputeStage IComputeStage.SetPreGraphRenderer(AbstractShaderNode preGraphRenderer)
    {
        return SetPreGraphRenderer(preGraphRenderer);
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeStage Update(
        ShaderNode<GpuVoid> computeGraph = null,
        AbstractShaderNode shaderNode = null,
        bool forceRecompile = false,
        string name = null,
        string profilingName = null,
        bool profilingAppend = false)
    {
        if (name != null)
            SetName(name);
        if (computeGraph != null)
            SetComputeGraph(computeGraph);
        if (shaderNode != null)
            ShaderNode = shaderNode;
        if (forceRecompile)
            CallChangeGraph(shaderNode ?? computeGraph);

        return this;
    }

    public virtual ComputeStage UpdateIndexProvider(DynamicIndex node)
    {
        if (node != null)
            node.IndexProvider = IndexProvider;

        return this;
    }

    public virtual ShaderSource DrawStage(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys = null)
    {
        return DrawResult(context ?? new ShaderGeneratorContext(), baseKeys)
            .ReadyStages
            .FirstOrDefault()
            ?.ShaderSource;
    }

    public virtual IReadOnlyList<ComputeDispatchExecutionResult> DrawStage(
        RenderDrawContext renderDrawContext,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null,
        TextureResourceFailureDispatchPolicy textureResourceFailurePolicy =
            TextureResourceFailureDispatchPolicy.BlockAllFailures)
    {
        return DrawResult(context ?? new ShaderGeneratorContext(), baseKeys)
            .Execute(renderDrawContext, textureResourceFailurePolicy);
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
        return new ComputeExecutionPlanBuilder()
            .Build([this], GetResources(), context, baseKeys)
            .ToDrawResult();
    }

    public AbstractShaderNode GetShaderNode()
    {
        return ShaderNode;
    }

    public string GetShaderCode()
    {
        return ShaderCode;
    }

    public string GetLastError()
    {
        return LastError;
    }

    public bool GetEnabled()
    {
        return Enabled;
    }

    public virtual ComputeStage SetDefaults()
    {
        Enabled = true;
        IterationCount = 1;
        WriteAttributes = true;
        LastError = null;
        return this;
    }

    public virtual void Dispose()
    {
        _changeGraphListeners.Clear();
        StructuredBufferBindings = null;
        ComputeGraphNode = null;
        ComputeGraph = null;
        ShaderNode = null;
        PreGraphRenderer = null;
        LastDiagnosticContext = null;
        ShaderCode = null;
        LastError = null;
        LastShaderGenerator = null;
        DispatcherProvider = null;
        DispatchInfo = null;
        ResourceProvider = null;
        Resource = null;
        Ticket++;
    }

    public virtual ComputeStage AddChangeGraph(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null && !_changeGraphListeners.Contains(changeGraph))
            _changeGraphListeners.Add(changeGraph);

        return this;
    }

    IComputeStage IComputeStage.AddChangeGraph(IComputeChangeGraph changeGraph)
    {
        return AddChangeGraph(changeGraph);
    }

    public virtual ComputeStage RemoveChangeGraph(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null)
            _changeGraphListeners.Remove(changeGraph);

        return this;
    }

    IComputeStage IComputeStage.RemoveChangeGraph(IComputeChangeGraph changeGraph)
    {
        return RemoveChangeGraph(changeGraph);
    }

    public virtual ComputeStage HandleAttributes()
    {
        StructuredBufferBindings?.BufferInput?.CallPrepareGraph();
        return this;
    }

    IComputeStage IComputeStage.HandleAttributes()
    {
        return HandleAttributes();
    }

    public virtual ComputeStage AddDispatchProvider()
    {
        if (DispatcherProvider == null && DispatchInfo != null)
            DispatcherProvider = new DirectDispatcher(DispatchInfo, IndexProvider);

        return this;
    }

    IComputeStage IComputeStage.AddDispatchProvider()
    {
        return AddDispatchProvider();
    }

    public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
    {
        return ComputeGraph?.GetChildren(context) ?? Array.Empty<IComputeNode>();
    }

    public int GetTicket()
    {
        return Ticket;
    }

    public virtual ComputeStage ReadsAndWrites()
    {
        return this;
    }

    public virtual ComputeStage BuildComputeGraph()
    {
        if (ResourceProvider is StructuredBufferResource structuredBufferResource)
        {
            var index = CreateIndexX(NodeContext, IndexProvider);
            StructuredBufferBindings = structuredBufferResource.BindAttributes(
                NodeContext,
                index,
                null,
                PreGraphRenderer,
                WriteAttributes);
            SetComputeGraphNode(CreateComputeGraph1D(StructuredBufferBindings.WriteGroup, structuredBufferResource.GetDispatchInfo()));
        }
        else if (ResourceProvider is TextureResource textureResource)
        {
            var graphNode = CreateTextureComputeGraph(textureResource, null);
            var readIndex = graphNode.Index;
            var writeIndex = graphNode.Index;
            var attributeMap = CreateStageAttributeMap(AttributeType.Texture);
            textureResource.BindComputeStage(
                NodeContext,
                readIndex,
                writeIndex,
                attributeMap,
                PreGraphRenderer,
                WriteAttributes);
            graphNode.Update(textureResource.WriteGroup);
            SetComputeGraphNode(graphNode);
        }
        else if (StructuredBufferBindings != null)
        {
            StructuredBufferBindings.SetGroups(NodeContext, PreGraphRenderer);
            SetComputeGraphNode(CreateComputeGraph1D(StructuredBufferBindings.WriteGroup, DispatchInfo));
        }

        CallChangeGraph(ShaderNode);
        return this;
    }

    IComputeStage IComputeStage.BuildComputeGraph()
    {
        return BuildComputeGraph();
    }

    public virtual ShaderNode<GpuVoid> GetComputeGraph(
        ShaderNode<GpuVoid> computeGraph = null,
        AbstractShaderNode stageNode = null)
    {
        if (computeGraph != null)
            SetComputeGraph(computeGraph);
        if (stageNode != null)
            ShaderNode = stageNode;

        return ComputeGraph;
    }

    public virtual ComputeStage ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        if (ResourceProvider != null || Resource != null || resourceProvider == null)
            return this;

        SetResourceProvider(resourceProvider);
        if (ResourceProvider is StructuredBufferResource structuredBufferResource)
            BindStructuredBufferResource(structuredBufferResource);

        return this;
    }

    IComputeStage IComputeStage.ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        return ProcessMainResource(resourceProvider);
    }

    public virtual ComputeStage BindAttributes(AttributeMap attributeMap = null)
    {
        if (ResourceProvider is StructuredBufferResource structuredBufferResource)
            BindStructuredBufferResource(structuredBufferResource, attributeMap);
        if (ResourceProvider is TextureResource textureResource)
        {
            var graphNode = CreateTextureComputeGraph(textureResource, null);
            var readIndex = graphNode.Index;
            var writeIndex = graphNode.Index;
            textureResource.BindComputeStage(
                NodeContext,
                readIndex,
                writeIndex,
                attributeMap,
                PreGraphRenderer,
                WriteAttributes);
            graphNode.Update(textureResource.WriteGroup);
            SetComputeGraphNode(graphNode);
        }

        return this;
    }

    public virtual StructuredBufferResourceBindings CreateWrite(AttributeMap attributeMap = null)
    {
        BindAttributes(attributeMap);
        return StructuredBufferBindings;
    }

    public ComputeResource GetResource()
    {
        return Resource;
    }

    public virtual IEnumerable<ComputeResource> GetResources(IEnumerable<ComputeResource> resources = null)
    {
        return ComputeResource.MergeResources(
            resources ?? Array.Empty<ComputeResource>(),
            Resource == null ? Array.Empty<ComputeResource>() : [Resource]);
    }

    public virtual IEnumerable<IComputeStage> GetExecutionStages(bool includeDisabledStages = false)
    {
        if (includeDisabledStages || Enabled)
            yield return this;
    }

    public string GetName()
    {
        return Name;
    }

    public virtual ComputeStage BindStructuredBufferResource(
        StructuredBufferResource resource,
        AttributeMap attributeMap = null)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        SetResourceProvider(resource);
        SetDispatchInfo(resource.GetDispatchInfo());

        var index = CreateIndexX(NodeContext, IndexProvider);
        StructuredBufferBindings = resource.BindComputeStage(NodeContext, index, attributeMap, PreGraphRenderer, WriteAttributes);
        SetComputeGraphNode(CreateComputeGraph1D(StructuredBufferBindings.WriteGroup, resource.GetDispatchInfo()));
        return this;
    }

    private ComputeStage SetComputeGraphNode(ComputeGraph computeGraphNode)
    {
        ComputeGraphNode = computeGraphNode;
        if (ComputeGraphNode != null)
        {
            ComputeGraphNode.Dispatcher.SetIndexProvider(IndexProvider);
            DispatcherProvider ??= ComputeGraphNode;
            ComputeGraph = ComputeGraphNode.GetComputeGraph();
        }
        else
        {
            ComputeGraph = null;
        }

        ShaderNode = ComputeGraph;
        return this;
    }

    private ComputeGraph1D CreateComputeGraph1D(
        ShaderNode<GpuVoid> shaderNode,
        IDispatchInfo dispatchInfo)
    {
        var request = dispatchInfo?.Request
                      ?? new ComputeDispatchRequest(ComputeDispatchSize.One, ComputeDispatchSize.One);
        return ComputeGraph1D.Create(
            shaderNode,
            NodeContext,
            global::System.Math.Max(1, request.ElementCount.X),
            global::System.Math.Max(1, request.ThreadGroupSize.X),
            skipOutsideRange: true,
            Name);
    }

    private ComputeGraph CreateTextureComputeGraph(
        TextureResource textureResource,
        ShaderNode<GpuVoid> shaderNode)
    {
        var size = textureResource?.GetSize() ?? new Int3(1);
        var dispatchInfo = textureResource?.GetDispatchInfo();
        var threadGroupSize = dispatchInfo?.Request.ThreadGroupSize ?? ComputeDispatchSize.One;

        return TypeHelpers.GetDimensionFromInt3(size) switch
        {
            1 => ComputeGraph1D.Create(
                shaderNode,
                NodeContext,
                size.X,
                threadGroupSize.X,
                skipOutsideRange: true,
                Name),
            2 => ComputeGraph2D.Create(
                shaderNode,
                NodeContext,
                new Int2(size.X, size.Y),
                new Int2(
                    checked((int)global::System.Math.Max(1, threadGroupSize.X)),
                    checked((int)global::System.Math.Max(1, threadGroupSize.Y))),
                skipOutsideRange: true,
                Name),
            _ => ComputeGraph3D.Create(
                shaderNode,
                NodeContext,
                size,
                new Int3(
                    checked((int)global::System.Math.Max(1, threadGroupSize.X)),
                    checked((int)global::System.Math.Max(1, threadGroupSize.Y)),
                    checked((int)global::System.Math.Max(1, threadGroupSize.Z))),
                skipOutsideRange: true,
                Name)
        };
    }

    private AttributeMap CreateStageAttributeMap(AttributeType attributeType)
    {
        if (ComputeGraph == null)
            return null;

        var attributes = GlobalAttributeHandler
            .CollectAttributes(ComputeGraph)
            .Where(attribute => attribute.AttributeType == attributeType)
            .ToArray();
        if (attributes.Length == 0)
            return null;

        var attributeMap = new AttributeMap(attributeType);
        attributeMap.Prepare();
        foreach (var attribute in attributes)
            attributeMap.HandleAttribute(attribute);

        attributeMap.Finish(syncAttributes: true);
        return attributeMap;
    }

    public ComputeDispatchInfoSplit SplitDispatchInfo()
    {
        return (GetDispatcher()?.GetDispatchInfo() ?? DispatchInfo)?.Split()
            ?? new ComputeDispatchInfoSplit(
                ComputeDispatchSize.Zero,
                ComputeDispatchSize.Zero,
                Array.Empty<ComputeDispatchDiagnostic>(),
                false);
    }

    public ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
    {
        if (!Enabled)
            return null;
        if (ComputeGraph == null)
            throw new InvalidOperationException("Cannot generate a compute stage without a compute graph.");

        try
        {
            LastShaderGenerator = new ToComputeFx<GpuVoid>(ComputeGraph)
            {
                RegisterGeneratedShaderSource = RegisterGeneratedShaderSource
            };

            var shaderSource = LastShaderGenerator.GenerateShaderSource(context, baseKeys);
            ShaderCode = LastShaderGenerator.ShaderCode;
            LastDiagnosticContext = LastShaderGenerator.LastDiagnosticContext;
            LastError = null;
            return shaderSource;
        }
        catch (Exception exception)
        {
            LastError = exception.Message;
            throw;
        }
    }

    protected void CallChangeGraph(AbstractShaderNode node)
    {
        Ticket++;
        foreach (var changeGraph in _changeGraphListeners)
            changeGraph.ChangeGraph(node);
    }

    private static ShaderNode<int> CreateIndexX(NodeContext nodeContext, IIndexProvider indexProvider)
    {
        if (indexProvider is DispatchIdIndexProvider)
            return new DispatchThreadIdX(nodeContext);

        (indexProvider ?? new DispatchIdIndexProvider()).Index(nodeContext, out _, out var writeIndex);
        return new GetMember<Int3, int>(nodeContext, writeIndex, "x");
    }

    private static void CreateTextureIndexes(
        NodeContext nodeContext,
        IIndexProvider indexProvider,
        TextureResource textureResource,
        out AbstractShaderNode readIndex,
        out AbstractShaderNode writeIndex)
    {
        (indexProvider ?? new DispatchIdIndexProvider()).Index(nodeContext, out var sourceReadIndex, out var sourceWriteIndex);
        readIndex = CreateTextureIndex(nodeContext, sourceReadIndex, textureResource?.GetSize() ?? new Int3(1));
        writeIndex = CreateTextureIndex(nodeContext, sourceWriteIndex, textureResource?.GetSize() ?? new Int3(1));
    }

    private static AbstractShaderNode CreateTextureIndex(
        NodeContext nodeContext,
        ShaderNode<Int3> index,
        Int3 size)
    {
        return TypeHelpers.GetDimensionFromInt3(size) switch
        {
            1 => index is DispatchThreadId ? new DispatchThreadIdX(nodeContext) : new GetMember<Int3, int>(nodeContext, index, "x"),
            2 => new GetMember<Int3, Int2>(nodeContext, index, "xy"),
            _ => index
        };
    }
}
