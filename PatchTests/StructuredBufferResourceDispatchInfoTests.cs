using System.Linq;
using Fuse.compute;
using NUnit.Framework;
using Stride.Core.Mathematics;
using VL.Stride.Rendering.ComputeEffect;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class StructuredBufferResourceDispatchInfoTests
{
    [Test]
    public void Create_WithoutResource_UsesVlDefaults()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create();

        Assert.That(dispatchInfo.Resource, Is.Null);
        Assert.That(dispatchInfo.ElementCount, Is.EqualTo(100_000));
        Assert.That(dispatchInfo.ThreadGroupSize1D, Is.EqualTo(64));
        Assert.That(dispatchInfo.IsValid, Is.True);
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(1_563, 1, 1)));
    }

    [Test]
    public void Create_WithResource_UsesResourceElementCount()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(1_000));

        Assert.That(dispatchInfo.GetElementCount(), Is.EqualTo(1_000));
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(16, 1, 1)));
    }

    [Test]
    public void SetResource_RevalidatesCount()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(1_000));

        dispatchInfo.SetResource(new TestStructuredBufferResource(128));

        Assert.That(dispatchInfo.GetElementCount(), Is.EqualTo(128));
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(2, 1, 1)));
        Assert.That(dispatchInfo.IsValid, Is.True);
    }

    [Test]
    public void SetResource_NullFallsBackToDefaultElementCount()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(128));

        dispatchInfo.SetResource(null);

        Assert.That(dispatchInfo.Resource, Is.Null);
        Assert.That(dispatchInfo.GetElementCount(), Is.EqualTo(100_000));
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(1_563, 1, 1)));
    }

    [Test]
    public void SetThreadGroupSize_RevalidatesCount()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(1_000));

        dispatchInfo.SetThreadGroupSize(128);

        Assert.That(dispatchInfo.ThreadGroupSize1D, Is.EqualTo(128));
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(8, 1, 1)));
        Assert.That(dispatchInfo.IsValid, Is.True);
    }

    [Test]
    public void Split_ReturnsCurrentState()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(1_000));

        var split = dispatchInfo.Split();

        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(16, 1, 1)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(64, 1, 1)));
        Assert.That(split.IsValid, Is.True);
        Assert.That(split.Diagnostics, Is.Empty);
        Assert.That(split.Dispatcher, Is.AssignableTo<IComputeEffectDispatcher>());
        Assert.That(GetThreadGroupCount(split.Dispatcher), Is.EqualTo(new Int3(16, 1, 1)));
        Assert.That(split.PreRenderCommand, Is.Null);
        Assert.That(split.SkipOutsideRange, Is.False);
    }

    [Test]
    public void Split_FragmentOutputsMatchCurrentState()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(1_000));

        var result = dispatchInfo.Split(
            out var preRenderCommand,
            out var dispatcher,
            out var threadGroupSize,
            out var skipOutsideRange);

        Assert.That(result, Is.SameAs(dispatchInfo));
        Assert.That(preRenderCommand, Is.Null);
        Assert.That(dispatcher, Is.AssignableTo<IComputeEffectDispatcher>());
        Assert.That(GetThreadGroupCount(dispatcher), Is.EqualTo(new Int3(16, 1, 1)));
        Assert.That(threadGroupSize, Is.EqualTo(new ComputeDispatchSize(64, 1, 1)));
        Assert.That(skipOutsideRange, Is.False);
    }

    [Test]
    public void InvalidResourceElementCount_IsReportedOnDispatchInfo()
    {
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(0));

        Assert.That(dispatchInfo.IsValid, Is.False);
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(0, 1, 1)));
        Assert.That(
            dispatchInfo.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ElementCountNonPositive));
    }

    [Test]
    public void InvalidDispatchCount_IsReportedOnDispatchInfo()
    {
        var elementCount = (ComputeDispatchLimits.D3D11.MaxDispatchGroups.X + 1) * 64;
        var dispatchInfo = StructuredBufferResourceDispatchInfo.Create(new TestStructuredBufferResource(elementCount));

        Assert.That(dispatchInfo.IsValid, Is.False);
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(65_536, 1, 1)));
        Assert.That(
            dispatchInfo.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
    }

    private sealed class TestStructuredBufferResource : IStructuredBufferResourceInfo
    {
        public TestStructuredBufferResource(long elementCount)
        {
            ElementCount = elementCount;
        }

        public long ElementCount { get; }
    }

    private static Int3 GetThreadGroupCount(IComputeEffectDispatcher dispatcher)
    {
        return (Int3)dispatcher
            .GetType()
            .GetProperty("ThreadGroupCount")
            .GetValue(dispatcher);
    }
}
