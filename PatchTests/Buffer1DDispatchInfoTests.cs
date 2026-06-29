using System.Linq;
using Fuse.compute;
using NUnit.Framework;
using Stride.Core.Mathematics;
using VL.Stride.Rendering.ComputeEffect;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class Buffer1DDispatchInfoTests
{
    [Test]
    public void Create_UsesVlDefaults()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create();

        Assert.That(dispatchInfo.ElementCount, Is.EqualTo(100_000));
        Assert.That(dispatchInfo.ThreadGroupSize1D, Is.EqualTo(256));
        Assert.That(dispatchInfo.IsValid, Is.True);
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(391, 1, 1)));
    }

    [Test]
    public void GetCount_ReturnsCeilDispatchGroupCount()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_000, 256);

        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(4, 1, 1)));
    }

    [Test]
    public void SetElementCount_RevalidatesCount()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_000, 256);

        dispatchInfo.SetElementCount(512);

        Assert.That(dispatchInfo.ElementCount, Is.EqualTo(512));
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(2, 1, 1)));
        Assert.That(dispatchInfo.IsValid, Is.True);
    }

    [Test]
    public void SetThreadGroupSize_RevalidatesCount()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_000, 256);

        dispatchInfo.SetThreadGroupSize(128);

        Assert.That(dispatchInfo.ThreadGroupSize1D, Is.EqualTo(128));
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(8, 1, 1)));
        Assert.That(dispatchInfo.IsValid, Is.True);
    }

    [Test]
    public void Split_ReturnsCurrentState()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_000, 256);

        var split = dispatchInfo.Split();

        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(4, 1, 1)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(256, 1, 1)));
        Assert.That(split.IsValid, Is.True);
        Assert.That(split.Diagnostics, Is.Empty);
        Assert.That(split.Dispatcher, Is.AssignableTo<IComputeEffectDispatcher>());
        Assert.That(GetThreadGroupCount(split.Dispatcher), Is.EqualTo(new Int3(4, 1, 1)));
        Assert.That(split.PreRenderCommand, Is.Null);
        Assert.That(split.SkipOutsideRange, Is.False);
    }

    [Test]
    public void Split_FragmentOutputsMatchCurrentState()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_000, 256);

        var result = dispatchInfo.Split(
            out var preRenderCommand,
            out var dispatcher,
            out var threadGroupSize,
            out var skipOutsideRange);

        Assert.That(result, Is.SameAs(dispatchInfo));
        Assert.That(preRenderCommand, Is.Null);
        Assert.That(dispatcher, Is.AssignableTo<IComputeEffectDispatcher>());
        Assert.That(GetThreadGroupCount(dispatcher), Is.EqualTo(new Int3(4, 1, 1)));
        Assert.That(threadGroupSize, Is.EqualTo(new ComputeDispatchSize(256, 1, 1)));
        Assert.That(skipOutsideRange, Is.False);
    }

    [Test]
    public void InvalidDispatchCount_IsReportedOnDispatchInfo()
    {
        var elementCount = (ComputeDispatchLimits.D3D11.MaxDispatchGroups.X + 1) * 256;

        var dispatchInfo = Buffer1DDispatchInfo.Create(elementCount, 256);

        Assert.That(dispatchInfo.IsValid, Is.False);
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(65_536, 1, 1)));
        Assert.That(
            dispatchInfo.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
    }

    [Test]
    public void InvalidThreadGroupSize_IsReportedOnDispatchInfo()
    {
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_000, 0);

        Assert.That(dispatchInfo.IsValid, Is.False);
        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(0, 1, 1)));
        Assert.That(
            dispatchInfo.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ThreadGroupSizeNonPositive));
    }

    private static Int3 GetThreadGroupCount(IComputeEffectDispatcher dispatcher)
    {
        return (Int3)dispatcher
            .GetType()
            .GetProperty("ThreadGroupCount")
            .GetValue(dispatcher);
    }
}
