using System;
using System.Linq;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeExecutionPlanTests
{
    [Test]
    public void BuildExecutionPlan_CreatesReadyStagePlanWithShaderSource()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle"));
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var plan = system.BuildExecutionPlan(new ShaderGeneratorContext(), generateShaderSources: true);

        Assert.That(plan.CanExecute, Is.True);
        Assert.That(plan.ReadyStages, Has.Count.EqualTo(1));
        Assert.That(plan.SkippedStages, Is.Empty);
        Assert.That(plan.Resources, Has.Count.EqualTo(1));
        var readyStage = plan.ReadyStages[0];
        Assert.That(readyStage.Status, Is.EqualTo(ComputeStageExecutionStatus.Ready));
        Assert.That(readyStage.DispatcherProvider, Is.SameAs(stage.GetDispatcherProvider()));
        Assert.That(readyStage.StageDispatcher, Is.SameAs(stage.GetDispatcher()));
        Assert.That(readyStage.DispatchInfo, Is.SameAs(stage.DispatchInfo));
        Assert.That(readyStage.ShaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(readyStage.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(2, 1, 1)));
        Assert.That(readyStage.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(64, 1, 1)));
        Assert.That(readyStage.IterationCount, Is.EqualTo(1));
        Assert.That(stage.ShaderCode, Does.Contain("RWStructuredBuffer<Particle>"));
    }

    [Test]
    public void BuildExecutionPlan_CanSkipShaderGeneration()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle"));
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var plan = system.BuildExecutionPlan(generateShaderSources: false);

        Assert.That(plan.CanExecute, Is.True);
        Assert.That(plan.ReadyStages, Has.Count.EqualTo(1));
        Assert.That(plan.ReadyStages[0].ShaderSource, Is.Null);
        Assert.That(stage.ShaderCode, Is.Null);
    }

    [Test]
    public void BuildExecutionPlan_ReportsInvalidDispatchAndDoesNotGenerateShader()
    {
        var invalidResource = CreateParticleResource(
            "InvalidParticle",
            elementCount: 65_536L * 64L,
            threadGroupSize: 64);
        var stage = ComputeStage.FromStructuredBufferResource(invalidResource);
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var plan = system.BuildExecutionPlan(new ShaderGeneratorContext());

        Assert.That(plan.CanExecute, Is.False);
        Assert.That(plan.ReadyStages, Is.Empty);
        Assert.That(plan.SkippedStages, Has.Count.EqualTo(1));
        Assert.That(plan.SkippedStages[0].Status, Is.EqualTo(ComputeStageExecutionStatus.InvalidDispatch));
        Assert.That(plan.SkippedStages[0].ShaderSource, Is.Null);
        Assert.That(stage.ShaderCode, Is.Null);
        Assert.That(
            plan.DispatchDiagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
    }

    [Test]
    public void BuildExecutionPlan_ExcludesDisabledStagesByDefault()
    {
        var disabled = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("Disabled"))
            .SetEnabled(false);
        var system = new ComputeSystemSpectral().AppendComputeStage(disabled);

        var plan = system.BuildExecutionPlan();

        Assert.That(plan.Stages, Is.Empty);
        Assert.That(plan.CanExecute, Is.False);
        Assert.That(plan.Resources, Has.Count.EqualTo(1));
    }

    [Test]
    public void BuildExecutionPlan_CanIncludeDisabledStagesAsSkipped()
    {
        var disabled = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("Disabled"))
            .SetEnabled(false);
        var system = new ComputeSystemSpectral().AppendComputeStage(disabled);

        var plan = system.BuildExecutionPlan(includeDisabledStages: true);

        Assert.That(plan.CanExecute, Is.False);
        Assert.That(plan.SkippedStages, Has.Count.EqualTo(1));
        Assert.That(plan.SkippedStages[0].Status, Is.EqualTo(ComputeStageExecutionStatus.Disabled));
        Assert.That(plan.SkippedStages[0].Reason, Is.EqualTo("Stage is disabled."));
        Assert.That(disabled.ShaderCode, Is.Null);
    }

    [Test]
    public void BuildExecutionPlan_ReportsMissingGraph()
    {
        var stage = new ComputeStage(null)
            .SetDispatchInfo(Buffer1DDispatchInfo.Create(128, 64));
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var plan = system.BuildExecutionPlan();

        Assert.That(plan.CanExecute, Is.False);
        Assert.That(plan.SkippedStages, Has.Count.EqualTo(1));
        Assert.That(plan.SkippedStages[0].Status, Is.EqualTo(ComputeStageExecutionStatus.MissingGraph));
        Assert.That(plan.SkippedStages[0].Reason, Is.EqualTo("Stage has no compute graph."));
    }

    [Test]
    public void BuildExecutionPlan_FlattensComputeStageGroupsToChildStagePlans()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("GroupPlanA"));
        first.RegisterGeneratedShaderSource = false;
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("GroupPlanB"));
        second.RegisterGeneratedShaderSource = false;
        var group = ComputeStageGroup.Create(computeStageProviders: new[] { first, second });
        var system = new ComputeSystemSpectral().AppendComputeStage(group);

        var plan = system.BuildExecutionPlan(new ShaderGeneratorContext());

        Assert.That(plan.CanExecute, Is.True);
        Assert.That(plan.ReadyStages.Select(stage => stage.Stage), Is.EqualTo(new[] { first, second }));
        Assert.That(plan.Resources.Select(resource => resource.Resource), Is.EquivalentTo(new[] { "GroupPlanA", "GroupPlanB" }));
        Assert.That(plan.SkippedStages, Is.Empty);
    }

    [Test]
    public void DrawResult_FlattensComputeStageGroupsToChildStageResults()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("GroupDrawA"));
        first.RegisterGeneratedShaderSource = false;
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("GroupDrawB"));
        second.RegisterGeneratedShaderSource = false;
        var group = ComputeStageGroup.Create(computeStageProviders: new[] { first, second });

        var result = group.DrawResult(new ShaderGeneratorContext());

        Assert.That(result.CanDispatch, Is.True);
        Assert.That(result.ReadyStages.Select(stage => stage.Stage), Is.EqualTo(new[] { first, second }));
        Assert.That(result.ShaderSources, Has.Count.EqualTo(2));
        Assert.That(result.DispatchCommands.Select(command => command.Stage), Is.EqualTo(new[] { first, second }));
        Assert.That(result.DispatchCommands.Select(command => command.IterationIndex), Is.EqualTo(new[] { 0, 0 }));
        Assert.That(result.Resources.Select(resource => resource.Resource), Is.EquivalentTo(new[] { "GroupDrawA", "GroupDrawB" }));
    }

    [Test]
    public void Builder_ThrowsForNullSystem()
    {
        var builder = new ComputeExecutionPlanBuilder();

        Assert.Throws<ArgumentNullException>(() => builder.Build((ComputeSystemSpectral)null));
    }

    private static StructuredBufferResource CreateParticleResource(
        string name,
        long elementCount = 128,
        long threadGroupSize = 64)
    {
        var resource = StructuredBufferResource.Create(name, elementCount, threadGroupSize);
        var map = new AttributeMap(
            AttributeType.StructuredBuffer,
            attribute => attribute.Name == "Position" ? 12 : 4);

        map.HandleAttribute(FakeComputeAttribute<float>.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute<Vector3>.Create("Position"));
        resource.SetAttributeMap(
            map,
            getGpuType: attribute => attribute.Name == "Position" ? "float3" : "float",
            getSizeInBytes: attribute => attribute.Name == "Position" ? 12 : 4);

        return resource;
    }

    private sealed class FakeComputeAttribute<T> : IAttribute
    {
        private FakeComputeAttribute(string name)
        {
            Name = name;
            AttributeType = AttributeType.StructuredBuffer;
            ShaderNode = new ShaderNode<T>(null, name, theCreateDefault: false);
        }

        public string Name { get; }

        public AttributeType AttributeType { get; set; }

        public AbstractShaderNode ShaderNode { get; }

        public AbstractShaderNode InputAbstract { get; set; }

        public ShaderNode<GpuVoid> WriteCall { get; set; }

        public AbstractShaderNode ReadCall { get; set; }

        public Int3 Resolution => new(1, 1, 1);

        public bool IsOverridden => false;

        public void Sync(IAttribute theAttribute)
        {
            InputAbstract = theAttribute.InputAbstract;
            ReadCall = theAttribute.ReadCall;
            WriteCall = theAttribute.WriteCall;
        }

        public static FakeComputeAttribute<T> Create(string name)
        {
            return new FakeComputeAttribute<T>(name);
        }
    }
}
