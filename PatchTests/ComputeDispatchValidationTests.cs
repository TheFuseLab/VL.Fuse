using System.Linq;
using Fuse.compute;
using NUnit.Framework;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeDispatchValidationTests
{
    [Test]
    public void Validate1D_CalculatesCeilDispatchGroups()
    {
        var result = ComputeDispatchValidator.Validate1D(1_000, 256);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DispatchGroups.X, Is.EqualTo(4));
        Assert.That(result.DispatchGroups.Y, Is.EqualTo(1));
        Assert.That(result.DispatchGroups.Z, Is.EqualTo(1));
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void Validate1D_KeepsExactDispatchGroupCount()
    {
        var result = ComputeDispatchValidator.Validate1D(1_024, 256);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.DispatchGroups.X, Is.EqualTo(4));
    }

    [Test]
    public void Validate1D_DetectsDispatchGroupCountAboveD3D11Limit()
    {
        var elementCount = (ComputeDispatchLimits.D3D11.MaxDispatchGroups.X + 1) * 256;

        var result = ComputeDispatchValidator.Validate1D(elementCount, 256);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.DispatchGroups.X, Is.EqualTo(65_536));
        Assert.That(
            result.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
        Assert.That(result.Diagnostics.Single().Dimension, Is.EqualTo("X"));
        Assert.That(result.Diagnostics.Single().Limit, Is.EqualTo(65_535));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validate1D_DetectsNonPositiveElementCount(long elementCount)
    {
        var result = ComputeDispatchValidator.Validate1D(elementCount, 256);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.DispatchGroups.X, Is.EqualTo(0));
        Assert.That(
            result.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ElementCountNonPositive));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Validate1D_DetectsNonPositiveThreadGroupSize(long threadGroupSize)
    {
        var result = ComputeDispatchValidator.Validate1D(1_000, threadGroupSize);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.DispatchGroups.X, Is.EqualTo(0));
        Assert.That(
            result.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ThreadGroupSizeNonPositive));
    }

    [Test]
    public void Validate_DetectsThreadGroupDimensionAboveD3D11Limit()
    {
        var request = new ComputeDispatchRequest(
            new ComputeDispatchSize(1_000, 1, 1),
            new ComputeDispatchSize(1_025, 1, 1));

        var result = ComputeDispatchValidator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ThreadGroupDimensionExceeded));
        Assert.That(result.Diagnostics.Single(d =>
            d.Code == ComputeDispatchDiagnosticCode.ThreadGroupDimensionExceeded).Dimension, Is.EqualTo("X"));
    }

    [Test]
    public void Validate_DetectsTotalThreadsPerGroupAboveD3D11Limit()
    {
        var request = new ComputeDispatchRequest(
            new ComputeDispatchSize(1_000, 1_000, 1),
            new ComputeDispatchSize(32, 33, 1));

        var result = ComputeDispatchValidator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ThreadGroupThreadCountExceeded));
        Assert.That(result.Diagnostics.Single().Value, Is.EqualTo(1_056));
    }

    [Test]
    public void Validate_UsesCustomLimits()
    {
        var limits = new ComputeDispatchLimits(
            new ComputeDispatchSize(2, 2, 2),
            new ComputeDispatchSize(16, 16, 16),
            256);

        var result = ComputeDispatchValidator.Validate1D(49, 16, limits);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.DispatchGroups.X, Is.EqualTo(4));
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
        Assert.That(result.Diagnostics.Single().Limit, Is.EqualTo(2));
    }
}
