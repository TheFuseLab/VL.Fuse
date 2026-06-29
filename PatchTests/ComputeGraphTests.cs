using System;
using System.Linq;
using Fuse;
using Fuse.compute;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using Stride.Shaders;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeGraphTests
{
    [Test]
    public void ComputeGraph1D_BuildsDispatchIndexAndSkipGuard()
    {
        var shaderNode = new EmptyVoid(null);
        var graph = ComputeGraph1D.Create(
            shaderNode,
            count: 1_000,
            threadGroupSize: 128);

        var split = graph.SplitDispatchInfo();
        var source = graph.GetComputeGraph().BuildSourceCode();

        Assert.That(graph.Dimension, Is.EqualTo(1));
        Assert.That(graph.Count, Is.EqualTo(new ComputeDispatchSize(1_000, 1, 1)));
        Assert.That(graph.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(128, 1, 1)));
        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(8, 1, 1)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(128, 1, 1)));
        Assert.That(split.SkipOutsideRange, Is.True);
        Assert.That(graph.Index, Is.TypeOf<DispatchThreadIdX>());
        Assert.That(graph.GetDispatcher().GetDispatchInfo(), Is.SameAs(graph.DispatchInfo));
        Assert.That(graph.GetComputeGraph().Ins, Does.Contain(shaderNode));
        Assert.That(graph.GetComputeGraph().Ins.OfType<SkipOutsideRange1D>(), Has.Exactly(1).Items);
        Assert.That(source, Does.Contain("streams.DispatchThreadId.x >= 1000"));
        Assert.That(source, Does.Contain("return;"));
    }

    [Test]
    public void ComputeGraph2D_BuildsDispatchIndexAndSkipGuard()
    {
        var shaderNode = new EmptyVoid(null);
        var graph = ComputeGraph2D.Create(
            shaderNode,
            count: new Int2(16, 8),
            threadGroupSize: new Int2(8, 4));

        var split = graph.SplitDispatchInfo();
        var source = graph.GetComputeGraph().BuildSourceCode();

        Assert.That(graph.Dimension, Is.EqualTo(2));
        Assert.That(graph.Count, Is.EqualTo(new ComputeDispatchSize(16, 8, 1)));
        Assert.That(graph.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 1)));
        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(2, 2, 1)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 1)));
        Assert.That(split.SkipOutsideRange, Is.True);
        Assert.That(graph.Index, Is.TypeOf<Fuse.GetMember<Int3, Int2>>());
        Assert.That(graph.GetDispatcher().GetDispatchInfo(), Is.SameAs(graph.DispatchInfo));
        Assert.That(graph.GetComputeGraph().Ins, Does.Contain(shaderNode));
        Assert.That(graph.GetComputeGraph().Ins.OfType<SkipOutsideRange2D>(), Has.Exactly(1).Items);
        Assert.That(source, Does.Contain("streams.DispatchThreadId.xy >= uint2(16, 8)"));
        Assert.That(source, Does.Contain("return;"));
    }

    [Test]
    public void ComputeGraph3D_BuildsDispatchIndexAndSkipGuard()
    {
        var shaderNode = new EmptyVoid(null);
        var graph = ComputeGraph3D.Create(
            shaderNode,
            count: new Int3(16, 8, 4),
            threadGroupSize: new Int3(8, 4, 2));

        var split = graph.SplitDispatchInfo();
        var source = graph.GetComputeGraph().BuildSourceCode();

        Assert.That(graph.Dimension, Is.EqualTo(3));
        Assert.That(graph.Count, Is.EqualTo(new ComputeDispatchSize(16, 8, 4)));
        Assert.That(graph.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 2)));
        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(2, 2, 2)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 2)));
        Assert.That(split.SkipOutsideRange, Is.True);
        Assert.That(graph.Index, Is.TypeOf<DispatchThreadId>());
        Assert.That(graph.GetDispatcher().GetDispatchInfo(), Is.SameAs(graph.DispatchInfo));
        Assert.That(graph.GetComputeGraph().Ins, Does.Contain(shaderNode));
        Assert.That(graph.GetComputeGraph().Ins.OfType<SkipOutsideRange3D>(), Has.Exactly(1).Items);
        Assert.That(source, Does.Contain("streams.DispatchThreadId >= uint3(16, 8, 4)"));
        Assert.That(source, Does.Contain("return;"));
    }

    [Test]
    public void ComputeGraph_UpdateRebuildsGraphAndTicksChange()
    {
        var first = new EmptyVoid(null);
        var second = new EmptyVoid(null);
        var graph = ComputeGraph2D.Create(
            first,
            count: new Int2(16, 8),
            threadGroupSize: new Int2(8, 4));
        var initialTick = graph.GetChangedTick();

        graph.Update(
            second,
            count: new Int2(32, 16),
            threadGroupSize: new Int2(8, 8),
            skipOutsideRange: false);

        Assert.That(graph.GetChangedTick(), Is.GreaterThan(initialTick));
        Assert.That(graph.Count, Is.EqualTo(new ComputeDispatchSize(32, 16, 1)));
        Assert.That(graph.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 8, 1)));
        Assert.That(graph.SplitDispatchInfo().DispatchGroups, Is.EqualTo(new ComputeDispatchSize(4, 2, 1)));
        Assert.That(graph.SplitDispatchInfo().SkipOutsideRange, Is.False);
        Assert.That(graph.GetComputeGraph().Ins, Does.Not.Contain(first));
        Assert.That(graph.GetComputeGraph().Ins, Does.Contain(second));
        Assert.That(graph.GetComputeGraph().Ins.OfType<SkipOutsideRangeGuard>(), Is.Empty);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void ComputeGraph_GeneratedShaderCompilesWithStandaloneEffectCompiler(int dimension)
    {
        var graph = CreateGraphForDimension(dimension);
        graph.RegisterGeneratedShaderSource = false;

        var shaderSource = graph.GenerateShaderSource(new ShaderGeneratorContext(), null);
        var (result, errors) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(graph.LastShaderGenerator);

        Assert.That(shaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        Assert.That(graph.ShaderCode, Does.Contain("return;"));
        Assert.That(result.Bytecode, Is.Not.Null);
        Assert.That(result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
    }

    private static ComputeGraph CreateGraphForDimension(int dimension)
    {
        return dimension switch
        {
            1 => ComputeGraph1D.Create(new EmptyVoid(null), count: 16, threadGroupSize: 8),
            2 => ComputeGraph2D.Create(new EmptyVoid(null), count: new Int2(16, 8), threadGroupSize: new Int2(8, 4)),
            _ => ComputeGraph3D.Create(new EmptyVoid(null), count: new Int3(16, 8, 4), threadGroupSize: new Int3(8, 4, 2))
        };
    }

}
