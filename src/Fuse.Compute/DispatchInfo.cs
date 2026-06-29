using System;
using System.Collections.Generic;
using System.Reflection;
using Stride.Core.Mathematics;
using Stride.Rendering;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using VL.Stride.Rendering.ComputeEffect;

namespace Fuse.compute;

public interface IDispatchInfo
{
    ComputeDispatchRequest Request { get; }

    ComputeDispatchSize ThreadGroupSize { get; }

    ComputeDispatchSize DispatchGroups { get; }

    ComputeDispatchValidationResult Validation { get; }

    IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics { get; }

    bool IsValid { get; }

    ComputeDispatchSize GetCount();

    ComputeDispatchInfoSplit Split();
}

public interface IDispatcher : IIndexProvider
{
    IDispatchInfo GetDispatchInfo();
}

public interface IDispatcherProvider
{
    IDispatcher GetDispatcher();

    int GetChangedTick();
}

public sealed class DirectDispatcher : IDispatcher, IDispatcherProvider
{
    private IDispatchInfo _dispatchInfo;
    private IIndexProvider _indexProvider;

    public DirectDispatcher(
        IDispatchInfo dispatchInfo,
        IIndexProvider indexProvider = null)
    {
        _dispatchInfo = dispatchInfo;
        _indexProvider = indexProvider ?? new DispatchIdIndexProvider();
    }

    public int ChangedTick { get; private set; }

    public IDispatcher GetDispatcher()
    {
        return this;
    }

    public int GetChangedTick()
    {
        return ChangedTick;
    }

    public IDispatchInfo GetDispatchInfo()
    {
        return _dispatchInfo;
    }

    public void SetDispatchInfo(IDispatchInfo dispatchInfo)
    {
        if (ReferenceEquals(_dispatchInfo, dispatchInfo))
            return;

        _dispatchInfo = dispatchInfo;
        ChangedTick++;
    }

    public void SetIndexProvider(IIndexProvider indexProvider)
    {
        if (ReferenceEquals(_indexProvider, indexProvider))
            return;

        _indexProvider = indexProvider ?? new DispatchIdIndexProvider();
        ChangedTick++;
    }

    public void Index(NodeContext nodeContext, out ShaderNode<Int3> theReadIndex, out ShaderNode<Int3> theWriteIndex)
    {
        (_indexProvider ?? new DispatchIdIndexProvider()).Index(nodeContext, out theReadIndex, out theWriteIndex);
    }
}

public readonly record struct ComputeDispatchInfoSplit(
    ComputeDispatchSize DispatchGroups,
    ComputeDispatchSize ThreadGroupSize,
    IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics,
    bool IsValid,
    IGraphicsRendererBase PreRenderCommand = null,
    IComputeEffectDispatcher Dispatcher = null,
    bool SkipOutsideRange = false);

internal static class ComputeEffectDispatcherFactory
{
    private const string DirectDispatcherTypeName =
        "VL.Stride.Rendering.ComputeEffect.DirectComputeEffectDispatcher";

    private static readonly Lazy<Type> DirectDispatcherType = new(() =>
        typeof(IComputeEffectDispatcher).Assembly.GetType(
            DirectDispatcherTypeName,
            throwOnError: true));

    public static IComputeEffectDispatcher CreateDirect(ComputeDispatchSize dispatchGroups)
    {
        var dispatcher = Activator.CreateInstance(DirectDispatcherType.Value, nonPublic: true);
        DirectDispatcherType.Value
            .GetProperty(
                "ThreadGroupCount",
                BindingFlags.Public | BindingFlags.Instance)
            ?.SetValue(dispatcher, dispatchGroups.ToInt3());

        return (IComputeEffectDispatcher)dispatcher;
    }
}

[ProcessNode(Name = "Buffer1DDispatchInfo", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class Buffer1DDispatchInfo : IDispatchInfo
{
    public const long DefaultElementCount = 100_000;
    public const long DefaultThreadGroupSize = 256;

    private readonly ComputeDispatchLimits _limits;

    [Fragment(Order = 0)]
    public Buffer1DDispatchInfo()
        : this(DefaultElementCount, DefaultThreadGroupSize)
    {
    }

    public Buffer1DDispatchInfo(
        long elementCount,
        long threadGroupSize = DefaultThreadGroupSize,
        ComputeDispatchLimits? limits = null)
    {
        _limits = limits ?? ComputeDispatchLimits.D3D11;
        ElementCount = elementCount;
        ThreadGroupSize1D = threadGroupSize;
        Validation = ValidateCurrent();
    }

    public long ElementCount { get; private set; }

    public long ThreadGroupSize1D { get; private set; }

    public ComputeDispatchRequest Request => ComputeDispatchRequest.For1D(ElementCount, ThreadGroupSize1D);

    public ComputeDispatchSize ThreadGroupSize => Validation.Request.ThreadGroupSize;

    public ComputeDispatchSize DispatchGroups => Validation.DispatchGroups;

    public ComputeDispatchValidationResult Validation { get; private set; }

    public IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics => Validation.Diagnostics;

    public bool IsValid => Validation.IsValid;

    [Fragment(Order = 100)]
    public Buffer1DDispatchInfo Output => this;

    public static Buffer1DDispatchInfo Create(
        long elementCount = DefaultElementCount,
        long threadGroupSize = DefaultThreadGroupSize,
        ComputeDispatchLimits? limits = null)
    {
        return new Buffer1DDispatchInfo(elementCount, threadGroupSize, limits);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public Buffer1DDispatchInfo Update()
    {
        Validation = ValidateCurrent();
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public Buffer1DDispatchInfo SetElementCount(long elementCount)
    {
        ElementCount = elementCount;
        return Update();
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public Buffer1DDispatchInfo SetThreadGroupSize(long threadGroupSize)
    {
        ThreadGroupSize1D = threadGroupSize;
        return Update();
    }

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
            Dispatcher: IsValid ? ComputeEffectDispatcherFactory.CreateDirect(DispatchGroups) : null);
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public Buffer1DDispatchInfo Split(
        out IGraphicsRendererBase preRenderCommand,
        out IComputeEffectDispatcher dispatcher,
        out ComputeDispatchSize threadGroupSize,
        out bool skipOutsideRange)
    {
        var split = Split();
        preRenderCommand = split.PreRenderCommand;
        dispatcher = split.Dispatcher;
        threadGroupSize = split.ThreadGroupSize;
        skipOutsideRange = split.SkipOutsideRange;
        return this;
    }

    private ComputeDispatchValidationResult ValidateCurrent()
    {
        return ComputeDispatchValidator.Validate(Request, _limits);
    }
}

public interface IStructuredBufferResourceInfo
{
    long ElementCount { get; }
}

[ProcessNode(Name = "StructuredBufferResourceDispatchInfo", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class StructuredBufferResourceDispatchInfo : IDispatchInfo
{
    public const long DefaultElementCount = 100_000;
    public const long DefaultThreadGroupSize = 64;

    private readonly ComputeDispatchLimits _limits;

    [Fragment(Order = 0)]
    public StructuredBufferResourceDispatchInfo()
        : this(null, DefaultThreadGroupSize)
    {
    }

    public StructuredBufferResourceDispatchInfo(
        IStructuredBufferResourceInfo resource,
        long threadGroupSize = DefaultThreadGroupSize,
        ComputeDispatchLimits? limits = null)
    {
        _limits = limits ?? ComputeDispatchLimits.D3D11;
        Resource = resource;
        ThreadGroupSize1D = threadGroupSize;
        ElementCount = GetElementCount(resource);
        Validation = ValidateCurrent();
    }

    public long ElementCount { get; private set; }

    public long ThreadGroupSize1D { get; private set; }

    public IStructuredBufferResourceInfo Resource { get; private set; }

    public ComputeDispatchRequest Request => ComputeDispatchRequest.For1D(ElementCount, ThreadGroupSize1D);

    public ComputeDispatchSize ThreadGroupSize => Validation.Request.ThreadGroupSize;

    public ComputeDispatchSize DispatchGroups => Validation.DispatchGroups;

    public ComputeDispatchValidationResult Validation { get; private set; }

    public IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics => Validation.Diagnostics;

    public bool IsValid => Validation.IsValid;

    [Fragment(Order = 100)]
    public StructuredBufferResourceDispatchInfo Output => this;

    public static StructuredBufferResourceDispatchInfo Create(
        IStructuredBufferResourceInfo resource = null,
        long threadGroupSize = DefaultThreadGroupSize,
        ComputeDispatchLimits? limits = null)
    {
        return new StructuredBufferResourceDispatchInfo(resource, threadGroupSize, limits);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResourceDispatchInfo Update()
    {
        ElementCount = GetElementCount(Resource);
        Validation = ValidateCurrent();
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResourceDispatchInfo SetResource(IStructuredBufferResourceInfo resource)
    {
        Resource = resource;
        return Update();
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResourceDispatchInfo SetThreadGroupSize(long threadGroupSize)
    {
        ThreadGroupSize1D = threadGroupSize;
        return Update();
    }

    public long GetElementCount()
    {
        return ElementCount;
    }

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
            Dispatcher: IsValid ? ComputeEffectDispatcherFactory.CreateDirect(DispatchGroups) : null);
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResourceDispatchInfo Split(
        out IGraphicsRendererBase preRenderCommand,
        out IComputeEffectDispatcher dispatcher,
        out ComputeDispatchSize threadGroupSize,
        out bool skipOutsideRange)
    {
        var split = Split();
        preRenderCommand = split.PreRenderCommand;
        dispatcher = split.Dispatcher;
        threadGroupSize = split.ThreadGroupSize;
        skipOutsideRange = split.SkipOutsideRange;
        return this;
    }

    private ComputeDispatchValidationResult ValidateCurrent()
    {
        return ComputeDispatchValidator.Validate(Request, _limits);
    }

    private static long GetElementCount(IStructuredBufferResourceInfo resource)
    {
        return resource?.ElementCount ?? DefaultElementCount;
    }
}

[ProcessNode(Name = "TextureDispatchInfo", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed class TextureDispatchInfo : IDispatchInfo
{
    public static readonly ComputeDispatchSize DefaultDimension = ComputeDispatchSize.One;
    public static readonly ComputeDispatchSize DefaultThreadGroupSize = new(64, 1, 1);

    private readonly ComputeDispatchLimits _limits;

    [Fragment(Order = 0)]
    public TextureDispatchInfo()
        : this(DefaultDimension, DefaultThreadGroupSize)
    {
    }

    public TextureDispatchInfo(
        ComputeDispatchSize dimension,
        ComputeDispatchSize threadGroupSize,
        ComputeDispatchLimits? limits = null)
    {
        _limits = limits ?? ComputeDispatchLimits.D3D11;
        Dimension = EnsureOne(dimension);
        ThreadGroupSizeValue = EnsureOne(threadGroupSize);
        Validation = ValidateCurrent();
    }

    public ComputeDispatchSize Dimension { get; private set; }

    public ComputeDispatchSize ThreadGroupSizeValue { get; private set; }

    public ComputeDispatchRequest Request => new(Dimension, ThreadGroupSizeValue);

    public ComputeDispatchSize ThreadGroupSize => Validation.Request.ThreadGroupSize;

    public ComputeDispatchSize DispatchGroups => Validation.DispatchGroups;

    public ComputeDispatchValidationResult Validation { get; private set; }

    public IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics => Validation.Diagnostics;

    public bool IsValid => Validation.IsValid;

    [Fragment(Order = 100)]
    public TextureDispatchInfo Output => this;

    public static TextureDispatchInfo Create(
        Int3? dimension = null,
        ComputeDispatchSize? threadGroupSize = null,
        ComputeDispatchLimits? limits = null)
    {
        return new TextureDispatchInfo(
            dimension.HasValue ? ComputeDispatchSize.FromInt3(dimension.Value) : DefaultDimension,
            threadGroupSize ?? DefaultThreadGroupSize,
            limits);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureDispatchInfo Update(
        Int3? dimension = null,
        ComputeDispatchSize? threadGroupSize = null)
    {
        if (dimension.HasValue)
            Dimension = EnsureOne(ComputeDispatchSize.FromInt3(dimension.Value));
        if (threadGroupSize.HasValue)
            ThreadGroupSizeValue = EnsureOne(threadGroupSize.Value);

        Validation = ValidateCurrent();
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureDispatchInfo SetDimension(Int3 dimension)
    {
        return Update(dimension);
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureDispatchInfo SetThreadGroupSize(ComputeDispatchSize threadGroupSize)
    {
        return Update(threadGroupSize: threadGroupSize);
    }

    public ComputeDispatchInfoSplit Split()
    {
        return new ComputeDispatchInfoSplit(
            DispatchGroups,
            ThreadGroupSize,
            Diagnostics,
            IsValid,
            Dispatcher: IsValid ? ComputeEffectDispatcherFactory.CreateDirect(DispatchGroups) : null,
            SkipOutsideRange: true);
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureDispatchInfo Split(
        out IGraphicsRendererBase preRenderCommand,
        out IComputeEffectDispatcher dispatcher,
        out ComputeDispatchSize threadGroupSize,
        out bool skipOutsideRange)
    {
        var split = Split();
        preRenderCommand = split.PreRenderCommand;
        dispatcher = split.Dispatcher;
        threadGroupSize = split.ThreadGroupSize;
        skipOutsideRange = split.SkipOutsideRange;
        return this;
    }

    public (ComputeDispatchSize ThreadGroupSize, bool SkipOutsideRange) GetThreadGroupInfo()
    {
        return (ThreadGroupSize, true);
    }

    public ComputeDispatchSize GetCount()
    {
        return DispatchGroups;
    }

    public ComputeDispatchSize GetCountGpu()
    {
        return DispatchGroups;
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
