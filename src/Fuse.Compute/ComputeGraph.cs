using System;
using System.Collections.Generic;
using Fuse.ShaderFX;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

[ProcessNode(Name = "ComputeGraph", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public class ComputeGraph : IDispatcherProvider
{
    [Fragment(Order = 0)]
    public ComputeGraph(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext,
        ShaderNode<GpuVoid> shaderNode = null,
        ComputeDispatchSize? count = null,
        ComputeDispatchSize? threadGroupSize = null,
        bool skipOutsideRange = false,
        string name = null)
    {
        NodeContext = nodeContext;
        Name = string.IsNullOrWhiteSpace(name) ? GetType().Name : name;
        Count = EnsureOne(count ?? ComputeDispatchSize.One);
        ThreadGroupSize = EnsureOne(threadGroupSize ?? ComputeDispatchSize.One);
        SkipOutsideRange = skipOutsideRange;
        ShaderNode = shaderNode;
        UpdateDispatch();
        BuildComputeGraph();
    }

    [Fragment(Order = 100)]
    public ComputeGraph Output => this;

    public NodeContext NodeContext { get; }

    public string Name { get; protected set; }

    public virtual int Dimension => 3;

    public bool Enabled { get; protected set; } = true;

    public bool SkipOutsideRange { get; protected set; }

    public ComputeDispatchSize Count { get; protected set; }

    public ComputeDispatchSize ThreadGroupSize { get; protected set; }

    public IDispatchInfo DispatchInfo { get; protected set; }

    public DirectDispatcher Dispatcher { get; protected set; }

    public ShaderNode<GpuVoid> ShaderNode { get; protected set; }

    public ShaderNode<GpuVoid> Graph { get; protected set; }

    public AbstractShaderNode Index { get; protected set; }

    public string ShaderCode { get; protected set; }

    public string LastError { get; protected set; }

    public ShaderDiagnosticContext LastDiagnosticContext { get; protected set; }

    public ToComputeFx<GpuVoid> LastShaderGenerator { get; protected set; }

    public bool RegisterGeneratedShaderSource { get; set; } = true;

    public bool HasChanged { get; protected set; }

    public int ChangedTick { get; protected set; }

    public static ComputeGraph Create(
        ShaderNode<GpuVoid> shaderNode = null,
        NodeContext nodeContext = null,
        ComputeDispatchSize? count = null,
        ComputeDispatchSize? threadGroupSize = null,
        bool skipOutsideRange = false,
        string name = null)
    {
        return new ComputeGraph(
            nodeContext,
            shaderNode,
            count,
            threadGroupSize,
            skipOutsideRange,
            name);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public virtual ComputeGraph Update(
        ShaderNode<GpuVoid> shaderNode = null,
        ComputeDispatchSize? count = null,
        ComputeDispatchSize? threadGroupSize = null,
        bool? skipOutsideRange = null,
        bool? enabled = null)
    {
        if (shaderNode != null)
            ShaderNode = shaderNode;
        if (count.HasValue)
            Count = NormalizeCount(count.Value);
        if (threadGroupSize.HasValue)
            ThreadGroupSize = NormalizeThreadGroupSize(threadGroupSize.Value);
        if (skipOutsideRange.HasValue)
            SkipOutsideRange = skipOutsideRange.Value;
        if (enabled.HasValue)
            Enabled = enabled.Value;

        UpdateDispatch();
        BuildComputeGraph();
        return this;
    }

    public ShaderNode<GpuVoid> GetComputeGraph()
    {
        return Graph;
    }

    public IDispatcher GetDispatcher()
    {
        return Dispatcher;
    }

    public int GetChangedTick()
    {
        return ChangedTick;
    }

    public virtual ComputeDispatchInfoSplit SplitDispatchInfo()
    {
        return DispatchInfo?.Split()
               ?? new ComputeDispatchInfoSplit(
                   ComputeDispatchSize.Zero,
                   ComputeDispatchSize.Zero,
                   Array.Empty<ComputeDispatchDiagnostic>(),
                   false);
    }

    public ShaderSource GenerateShaderSource(
        ShaderGeneratorContext context,
        MaterialComputeColorKeys baseKeys = null)
    {
        if (!Enabled)
            return null;
        if (Graph == null)
            throw new InvalidOperationException("Cannot generate a compute graph without a graph node.");

        try
        {
            LastShaderGenerator = new ToComputeFx<GpuVoid>(Graph)
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

    protected virtual AbstractShaderNode CreateIndex()
    {
        return new DispatchThreadId(NodeContext);
    }

    protected virtual ComputeDispatchSize NormalizeCount(ComputeDispatchSize count)
    {
        return EnsureOne(count);
    }

    protected virtual ComputeDispatchSize NormalizeThreadGroupSize(ComputeDispatchSize threadGroupSize)
    {
        return EnsureOne(threadGroupSize);
    }

    protected virtual void BuildComputeGraph()
    {
        Index = CreateIndex();
        if (!Enabled)
        {
            Graph = new EmptyVoid(NodeContext);
        }
        else
        {
            var inputs = new List<AbstractShaderNode>();
            if (SkipOutsideRange)
                inputs.Add(CreateSkipOutsideRangeGuard());
            if (ShaderNode != null)
                inputs.Add(ShaderNode);

            Graph = new Group(NodeContext, inputs, Name);
        }

        HasChanged = true;
        ChangedTick++;
    }

    protected virtual AbstractShaderNode CreateSkipOutsideRangeGuard()
    {
        return new SkipOutsideRange3D(NodeContext, Count);
    }

    protected virtual void UpdateDispatch()
    {
        DispatchInfo = new ComputeGraphDispatchInfo(
            NormalizeCount(Count),
            NormalizeThreadGroupSize(ThreadGroupSize),
            SkipOutsideRange);
        Dispatcher ??= new DirectDispatcher(DispatchInfo);
        Dispatcher.SetDispatchInfo(DispatchInfo);
    }

    internal static ComputeDispatchSize EnsureOne(ComputeDispatchSize size)
    {
        return new ComputeDispatchSize(
            global::System.Math.Max(1, size.X),
            global::System.Math.Max(1, size.Y),
            global::System.Math.Max(1, size.Z));
    }
}

[ProcessNode(Name = "ComputeGraph1D", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class ComputeGraph1D : ComputeGraph
{
    [Fragment(Order = 0)]
    public ComputeGraph1D(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext,
        ShaderNode<GpuVoid> shaderNode = null,
        long count = 1,
        long threadGroupSize = 256,
        bool skipOutsideRange = true,
        string name = null)
        : base(
            nodeContext,
            shaderNode,
            new ComputeDispatchSize(count, 1, 1),
            new ComputeDispatchSize(threadGroupSize, 1, 1),
            skipOutsideRange,
            name)
    {
    }

    public override int Dimension => 1;

    [Fragment(Order = 100)]
    public new ComputeGraph1D Output => this;

    public static ComputeGraph1D Create(
        ShaderNode<GpuVoid> shaderNode = null,
        NodeContext nodeContext = null,
        long count = 1,
        long threadGroupSize = 256,
        bool skipOutsideRange = true,
        string name = null)
    {
        return new ComputeGraph1D(nodeContext, shaderNode, count, threadGroupSize, skipOutsideRange, name);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public ComputeGraph1D Update(
        ShaderNode<GpuVoid> shaderNode = null,
        long? count = null,
        long? threadGroupSize = null,
        bool? skipOutsideRange = null,
        bool? enabled = null)
    {
        base.Update(
            shaderNode,
            count.HasValue ? new ComputeDispatchSize(count.Value, 1, 1) : null,
            threadGroupSize.HasValue ? new ComputeDispatchSize(threadGroupSize.Value, 1, 1) : null,
            skipOutsideRange,
            enabled);
        return this;
    }

    protected override AbstractShaderNode CreateIndex()
    {
        return new DispatchThreadIdX(NodeContext);
    }

    protected override AbstractShaderNode CreateSkipOutsideRangeGuard()
    {
        return new SkipOutsideRange1D(NodeContext, Count.X);
    }

    protected override ComputeDispatchSize NormalizeCount(ComputeDispatchSize count)
    {
        return new ComputeDispatchSize(global::System.Math.Max(1, count.X), 1, 1);
    }

    protected override ComputeDispatchSize NormalizeThreadGroupSize(ComputeDispatchSize threadGroupSize)
    {
        return new ComputeDispatchSize(global::System.Math.Max(1, threadGroupSize.X), 1, 1);
    }
}

[ProcessNode(Name = "ComputeGraph2D", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class ComputeGraph2D : ComputeGraph
{
    [Fragment(Order = 0)]
    public ComputeGraph2D(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext,
        ShaderNode<GpuVoid> shaderNode = null,
        Int2? count = null,
        Int2? threadGroupSize = null,
        bool skipOutsideRange = true,
        string name = null)
        : base(
            nodeContext,
            shaderNode,
            ToDispatchSize(count ?? new Int2(1, 1)),
            ToDispatchSize(threadGroupSize ?? new Int2(8, 8)),
            skipOutsideRange,
            name)
    {
    }

    public override int Dimension => 2;

    [Fragment(Order = 100)]
    public new ComputeGraph2D Output => this;

    public static ComputeGraph2D Create(
        ShaderNode<GpuVoid> shaderNode = null,
        NodeContext nodeContext = null,
        Int2? count = null,
        Int2? threadGroupSize = null,
        bool skipOutsideRange = true,
        string name = null)
    {
        return new ComputeGraph2D(nodeContext, shaderNode, count, threadGroupSize, skipOutsideRange, name);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public ComputeGraph2D Update(
        ShaderNode<GpuVoid> shaderNode = null,
        Int2? count = null,
        Int2? threadGroupSize = null,
        bool? skipOutsideRange = null,
        bool? enabled = null)
    {
        base.Update(
            shaderNode,
            count.HasValue ? ToDispatchSize(count.Value) : null,
            threadGroupSize.HasValue ? ToDispatchSize(threadGroupSize.Value) : null,
            skipOutsideRange,
            enabled);
        return this;
    }

    protected override AbstractShaderNode CreateIndex()
    {
        return new GetMember<Int3, Int2>(NodeContext, new DispatchThreadId(NodeContext), "xy");
    }

    protected override AbstractShaderNode CreateSkipOutsideRangeGuard()
    {
        return new SkipOutsideRange2D(
            NodeContext,
            new ComputeDispatchSize(Count.X, Count.Y, 1));
    }

    protected override ComputeDispatchSize NormalizeCount(ComputeDispatchSize count)
    {
        return new ComputeDispatchSize(
            global::System.Math.Max(1, count.X),
            global::System.Math.Max(1, count.Y),
            1);
    }

    protected override ComputeDispatchSize NormalizeThreadGroupSize(ComputeDispatchSize threadGroupSize)
    {
        return new ComputeDispatchSize(
            global::System.Math.Max(1, threadGroupSize.X),
            global::System.Math.Max(1, threadGroupSize.Y),
            1);
    }

    private static ComputeDispatchSize ToDispatchSize(Int2 value)
    {
        return new ComputeDispatchSize(value.X, value.Y, 1);
    }
}

[ProcessNode(Name = "ComputeGraph3D", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class ComputeGraph3D : ComputeGraph
{
    [Fragment(Order = 0)]
    public ComputeGraph3D(
        [Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext,
        ShaderNode<GpuVoid> shaderNode = null,
        Int3? count = null,
        Int3? threadGroupSize = null,
        bool skipOutsideRange = true,
        string name = null)
        : base(
            nodeContext,
            shaderNode,
            ComputeDispatchSize.FromInt3(count ?? new Int3(1, 1, 1)),
            ComputeDispatchSize.FromInt3(threadGroupSize ?? new Int3(8, 8, 8)),
            skipOutsideRange,
            name)
    {
    }

    public override int Dimension => 3;

    [Fragment(Order = 100)]
    public new ComputeGraph3D Output => this;

    public static ComputeGraph3D Create(
        ShaderNode<GpuVoid> shaderNode = null,
        NodeContext nodeContext = null,
        Int3? count = null,
        Int3? threadGroupSize = null,
        bool skipOutsideRange = true,
        string name = null)
    {
        return new ComputeGraph3D(nodeContext, shaderNode, count, threadGroupSize, skipOutsideRange, name);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public ComputeGraph3D Update(
        ShaderNode<GpuVoid> shaderNode = null,
        Int3? count = null,
        Int3? threadGroupSize = null,
        bool? skipOutsideRange = null,
        bool? enabled = null)
    {
        base.Update(
            shaderNode,
            count.HasValue ? ComputeDispatchSize.FromInt3(count.Value) : null,
            threadGroupSize.HasValue ? ComputeDispatchSize.FromInt3(threadGroupSize.Value) : null,
            skipOutsideRange,
            enabled);
        return this;
    }

    protected override AbstractShaderNode CreateIndex()
    {
        return new DispatchThreadId(NodeContext);
    }

    protected override AbstractShaderNode CreateSkipOutsideRangeGuard()
    {
        return new SkipOutsideRange3D(NodeContext, Count);
    }
}

public sealed class ComputeGraphDispatchInfo : IDispatchInfo
{
    private readonly ComputeDispatchLimits _limits;

    public ComputeGraphDispatchInfo(
        ComputeDispatchSize count,
        ComputeDispatchSize threadGroupSize,
        bool skipOutsideRange = true,
        ComputeDispatchLimits? limits = null)
    {
        _limits = limits ?? ComputeDispatchLimits.D3D11;
        Count = EnsureOne(count);
        ThreadGroupSizeValue = EnsureOne(threadGroupSize);
        SkipOutsideRange = skipOutsideRange;
        Validation = ValidateCurrent();
    }

    public ComputeDispatchSize Count { get; }

    public ComputeDispatchSize ThreadGroupSizeValue { get; }

    public bool SkipOutsideRange { get; }

    public ComputeDispatchRequest Request => new(Count, ThreadGroupSizeValue);

    public ComputeDispatchSize ThreadGroupSize => Validation.Request.ThreadGroupSize;

    public ComputeDispatchSize DispatchGroups => Validation.DispatchGroups;

    public ComputeDispatchValidationResult Validation { get; }

    public IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics => Validation.Diagnostics;

    public bool IsValid => Validation.IsValid;

    public ComputeDispatchSize GetCount()
    {
        return DispatchGroups;
    }

    public ComputeDispatchInfoSplit Split()
    {
        return new ComputeDispatchInfoSplit(
            DispatchGroups,
            ThreadGroupSize,
            Diagnostics,
            IsValid,
            Dispatcher: IsValid ? ComputeEffectDispatcherFactory.CreateDirect(DispatchGroups) : null,
            SkipOutsideRange: SkipOutsideRange);
    }

    private ComputeDispatchValidationResult ValidateCurrent()
    {
        return ComputeDispatchValidator.Validate(Request, _limits);
    }

    private static ComputeDispatchSize EnsureOne(ComputeDispatchSize size)
    {
        return new ComputeDispatchSize(
            global::System.Math.Max(1, size.X),
            global::System.Math.Max(1, size.Y),
            global::System.Math.Max(1, size.Z));
    }
}

public abstract class SkipOutsideRangeGuard : ShaderNode<GpuVoid>, IComputeVoid
{
    protected SkipOutsideRangeGuard(NodeContext nodeContext)
        : base(nodeContext, "SkipOutsideRange", theCreateDefault: false)
    {
    }

    protected override Dictionary<string, string> CreateTemplateMap()
    {
        return new Dictionary<string, string>();
    }

    protected override string GenerateDefaultSource()
    {
        return SourceTemplate();
    }
}

public sealed class SkipOutsideRange1D : SkipOutsideRangeGuard
{
    private readonly long _count;

    public SkipOutsideRange1D(NodeContext nodeContext, long count)
        : base(nodeContext)
    {
        _count = global::System.Math.Max(1, count);
    }

    protected override string SourceTemplate()
    {
        return $"if(streams.DispatchThreadId.x >= {_count}) return;";
    }
}

public sealed class SkipOutsideRange2D : SkipOutsideRangeGuard
{
    private readonly ComputeDispatchSize _count;

    public SkipOutsideRange2D(NodeContext nodeContext, ComputeDispatchSize count)
        : base(nodeContext)
    {
        _count = ComputeGraph.EnsureOne(count);
    }

    protected override string SourceTemplate()
    {
        return $"if(any(streams.DispatchThreadId.xy >= uint2({_count.X}, {_count.Y}))) return;";
    }
}

public sealed class SkipOutsideRange3D : SkipOutsideRangeGuard
{
    private readonly ComputeDispatchSize _count;

    public SkipOutsideRange3D(NodeContext nodeContext, ComputeDispatchSize count)
        : base(nodeContext)
    {
        _count = ComputeGraph.EnsureOne(count);
    }

    protected override string SourceTemplate()
    {
        return $"if(any(streams.DispatchThreadId >= uint3({_count.X}, {_count.Y}, {_count.Z}))) return;";
    }
}
