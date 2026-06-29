using System.Runtime.CompilerServices;
using Fuse.compute;
using NUnit.Framework;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ToComputeStageTests
{
    [Test]
    public void Create_ExposesRendererAsComputeStageProvider()
    {
        var renderer = new FakeRenderer();
        var stage = ToComputeStage.Create(input: renderer);

        Assert.That(stage.GetComputeStage(), Is.SameAs(stage));
        Assert.That(stage.Name, Is.EqualTo("ToComputeStage (IRenderer)"));
        Assert.That(stage.Input, Is.SameAs(renderer));
        Assert.That(stage.Enabled, Is.True);
        Assert.That(stage.GetEnabled(), Is.True);
        Assert.That(stage.ComputeGraph, Is.Null);
        Assert.That(stage.DispatchInfo, Is.Null);
        Assert.That(stage.GetResources(), Is.Empty);
    }

    [Test]
    public void DrawStage_DrawsAssignedRendererWhenEnabled()
    {
        var renderer = new FakeRenderer();
        var stage = ToComputeStage.Create(input: renderer);
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executionResults = stage.DrawStage(renderDrawContext, new ShaderGeneratorContext());

        Assert.That(executionResults, Is.Empty);
        Assert.That(renderer.DrawCount, Is.EqualTo(1));
        Assert.That(renderer.LastContext, Is.SameAs(renderDrawContext));
    }

    [Test]
    public void DrawStage_SkipsWhenDisabledUnassignedOrContextMissing()
    {
        var renderer = new FakeRenderer();
        var stage = ToComputeStage.Create(input: renderer, enabled: false);
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        stage.DrawStage(renderDrawContext, new ShaderGeneratorContext());
        Assert.That(renderer.DrawCount, Is.EqualTo(0));

        stage.SetEnabled(true);
        stage.Update(null);
        stage.DrawStage(renderDrawContext, new ShaderGeneratorContext());
        Assert.That(renderer.DrawCount, Is.EqualTo(0));

        stage.Update(renderer);
        stage.DrawStage(null, new ShaderGeneratorContext());
        Assert.That(renderer.DrawCount, Is.EqualTo(0));
    }

    private sealed class FakeRenderer : IGraphicsRendererBase
    {
        public int DrawCount { get; private set; }

        public RenderDrawContext LastContext { get; private set; }

        public void Draw(RenderDrawContext context)
        {
            DrawCount++;
            LastContext = context;
        }
    }
}
