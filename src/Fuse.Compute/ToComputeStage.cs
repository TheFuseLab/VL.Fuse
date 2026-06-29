using System;
using System.Collections.Generic;
using Fuse.ShaderFX;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Core.Import;
using VL.Model;

namespace Fuse.compute;

[ProcessNode(Name = "ToComputeStage", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class ToComputeStage : IComputeStage, IComputeStageProvider
{
    private readonly List<IComputeChangeGraph> _changeGraphListeners = new();

    [Fragment(Order = 0)]
    public ToComputeStage(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext = null,
        IGraphicsRendererBase input = null,
        bool enabled = true)
    {
        NodeContext = nodeContext;
        Input = input;
        Enabled = enabled;
    }

    public NodeContext NodeContext { get; }

    public string Name => "ToComputeStage (IRenderer)";

    public bool Enabled { get; private set; }

    public int IterationCount => 1;

    public bool WriteAttributes => false;

    public IIndexProvider IndexProvider => null;

    public IDispatchInfo DispatchInfo => null;

    public IDispatcherProvider DispatcherProvider => null;

    public ComputeResource Resource => null;

    public IComputeResourceProvider ResourceProvider => null;

    public ShaderNode<GpuVoid> ComputeGraph => null;

    public ShaderDiagnosticContext LastDiagnosticContext => null;

    public string ShaderCode => null;

    public IGraphicsRendererBase Input { get; private set; }

    public int Ticket { get; private set; }

    [Fragment(Order = 100)]
    public ToComputeStage Output => this;

    public static ToComputeStage Create(
        NodeContext nodeContext = null,
        IGraphicsRendererBase input = null,
        bool enabled = true)
    {
        return new ToComputeStage(nodeContext, input, enabled);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public ToComputeStage Update(
        IGraphicsRendererBase input,
        bool enabled = true)
    {
        Input = input;
        Enabled = enabled;
        CallChangeGraph(null);
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public ToComputeStage SetEnabled(bool enabled)
    {
        Enabled = enabled;
        return this;
    }

    public bool GetEnabled()
    {
        return Enabled;
    }

    public IComputeStage GetComputeStage()
    {
        return this;
    }

    public IComputeStage SetWriteAttributes(bool writeAttributes)
    {
        return this;
    }

    public IComputeStage AddChangeGraph(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null && !_changeGraphListeners.Contains(changeGraph))
            _changeGraphListeners.Add(changeGraph);

        return this;
    }

    public IComputeStage RemoveChangeGraph(IComputeChangeGraph changeGraph)
    {
        if (changeGraph != null)
            _changeGraphListeners.Remove(changeGraph);

        return this;
    }

    public IComputeStage HandleAttributes()
    {
        return this;
    }

    public IComputeStage AddDispatchProvider()
    {
        return this;
    }

    public IDispatcherProvider GetDispatcherProvider()
    {
        return null;
    }

    public IDispatcher GetDispatcher()
    {
        return null;
    }

    public IEnumerable<IComputeNode> GetChildren(object context = null)
    {
        return Array.Empty<IComputeNode>();
    }

    public string GetName()
    {
        return Name;
    }

    public ComputeResource GetResource()
    {
        return Resource;
    }

    public int GetTicket()
    {
        return Ticket;
    }

    public void Dispose()
    {
        _changeGraphListeners.Clear();
        Input = null;
        Ticket++;
    }

    public IComputeStage BuildComputeGraph()
    {
        return this;
    }

    public IComputeStage ProcessMainResource(IComputeResourceProvider resourceProvider)
    {
        return this;
    }

    public IComputeStage SetPreGraphRenderer(AbstractShaderNode preGraphRenderer)
    {
        return this;
    }

    public IEnumerable<ComputeResource> GetResources(IEnumerable<ComputeResource> resources = null)
    {
        return resources ?? Array.Empty<ComputeResource>();
    }

    public IEnumerable<IComputeStage> GetExecutionStages(bool includeDisabledStages = false)
    {
        if (includeDisabledStages || Enabled)
            yield return this;
    }

    public ComputeDispatchInfoSplit SplitDispatchInfo()
    {
        return new ComputeDispatchInfoSplit(
            ComputeDispatchSize.Zero,
            ComputeDispatchSize.Zero,
            Array.Empty<ComputeDispatchDiagnostic>(),
            false);
    }

    public ComputeDrawResult DrawResult(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        return ComputeDrawResult.Disabled();
    }

    public IReadOnlyList<ShaderSource> Draw(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        return [];
    }

    public ShaderSource DrawStage(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        return null;
    }

    public IReadOnlyList<ComputeDispatchExecutionResult> DrawStage(
        RenderDrawContext renderDrawContext,
        ShaderGeneratorContext context = null,
        MaterialComputeColorKeys baseKeys = null,
        TextureResourceFailureDispatchPolicy textureResourceFailurePolicy =
            TextureResourceFailureDispatchPolicy.BlockAllFailures)
    {
        if (Enabled && Input != null && renderDrawContext != null)
            Input.Draw(renderDrawContext);

        return [];
    }

    public ShaderSource GenerateShaderSource(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys)
    {
        return null;
    }

    private void CallChangeGraph(AbstractShaderNode node)
    {
        Ticket++;
        foreach (var changeGraph in _changeGraphListeners)
            changeGraph.ChangeGraph(node);
    }
}
