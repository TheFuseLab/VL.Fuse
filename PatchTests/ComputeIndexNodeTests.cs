using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeIndexNodeTests
{
    [Test]
    public void DispatchThreadIdX_ReferencesComputeStream()
    {
        var index = new DispatchThreadIdX(null);

        Assert.That(index.ID, Is.EqualTo("streams.DispatchThreadId.x"));
        Assert.That(index.GetReference(), Is.EqualTo("streams.DispatchThreadId.x"));
        Assert.That(index.SourceCode, Is.Empty);
        Assert.That(index.TypeName(), Is.EqualTo("int"));
    }

    [Test]
    public void DispatchIdIndexProvider_CreatesHeadlessDispatchIndex()
    {
        new DispatchIdIndexProvider().Index(null, out var readIndex, out var writeIndex);

        Assert.That(readIndex, Is.SameAs(writeIndex));
        Assert.That(readIndex, Is.TypeOf<DispatchThreadId>());
        Assert.That(readIndex.ID, Is.EqualTo("streams.DispatchThreadId"));
        Assert.That(readIndex.TypeName(), Is.EqualTo("int3"));
    }

    [Test]
    public void DynamicIndex_WithDispatchProvider_ReferencesDispatchThreadId()
    {
        var dynamicIndex = new DynamicIndex(null, new DispatchIdIndexProvider());

        Assert.That(dynamicIndex.Input, Is.TypeOf<DispatchThreadId>());
        Assert.That(dynamicIndex.ID, Is.EqualTo("streams.DispatchThreadId"));
        Assert.That(dynamicIndex.SourceCode, Is.Empty);
        Assert.That(dynamicIndex.TypeName(), Is.EqualTo("int3"));
    }

    [Test]
    public void VertexIdIndexProvider_CreatesHeadlessInt3Index()
    {
        AbstractShaderNode.ResetBuildSourceCodeCache();

        new VertexIdIndexProvider().Index(null, out var readIndex, out var writeIndex);
        var source = readIndex.BuildSourceCode();

        Assert.That(readIndex, Is.SameAs(writeIndex));
        Assert.That(readIndex, Is.TypeOf<Int3Join>());
        Assert.That(source, Does.Contain("streams.VertexId"));
        Assert.That(source, Does.Contain("int3("));
        Assert.That(source, Does.Contain(",0,0"));
    }

    [Test]
    public void ConstantValue_Int3_IsHeadless()
    {
        var constant = new ConstantValue<Int3>(new Int3(1, 2, 3));

        Assert.That(constant.ID, Is.EqualTo("int3(1,2,3)"));
        Assert.That(constant.SourceCode, Is.Empty);
    }

    [Test]
    public void IterationIndexGlobal_UsesVlGlobalAttributeName()
    {
        var iterationIndex = IterationIndexGlobal.Create(iterationIndex: 7);

        Assert.That(iterationIndex.GetName(), Is.EqualTo("IterationIndex"));
        Assert.That(iterationIndex.Attribute.Name, Is.EqualTo("IterationIndex"));
        Assert.That(iterationIndex.GetGraph(), Is.SameAs(iterationIndex.Attribute));
        Assert.That(iterationIndex.GetValue(), Is.TypeOf<ConstantValue<int>>());
        Assert.That(((ConstantValue<int>)iterationIndex.GetValue()).Value, Is.EqualTo(7));
        Assert.That(iterationIndex.GetGraph().ID, Is.EqualTo("7"));
        Assert.That(iterationIndex.GetGraph().SourceCode, Is.Empty);
    }

    [Test]
    public void GlobalAttributeSet_UpdatesTargetAndReturnsGraph()
    {
        var iterationIndex = IterationIndexGlobal.Create(iterationIndex: 0);
        var source = new ConstantValue<int>(11);

        var set = new GlobalAttributeSet<int>(iterationIndex, source);

        Assert.That(set.Name, Is.EqualTo("IterationIndex"));
        Assert.That(set.Target, Is.SameAs(iterationIndex));
        Assert.That(set.Source, Is.SameAs(source));
        Assert.That(set.Graph, Is.SameAs(iterationIndex.GetGraph()));
        Assert.That(iterationIndex.GetValue(), Is.SameAs(source));
        Assert.That(iterationIndex.GetGraph().ID, Is.EqualTo("11"));
    }
}
