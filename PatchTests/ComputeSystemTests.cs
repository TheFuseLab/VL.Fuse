using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Stride.Rendering.ComputeEffect;
using VL.Stride.Shaders.ShaderFX;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeSystemTests
{
    [Test]
    public void SpectralUpdate_ReturnsPatchOutputs()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle"));
        var system = new ComputeSystemSpectral();

        var result = system.Update(
            externalScheduler: null,
            computeStages: new[] { stage },
            enabled: true,
            globalAttributes: out var globalAttributes,
            hasChanged: out var hasChanged);

        Assert.That(result, Is.SameAs(system));
        Assert.That(globalAttributes, Is.SameAs(system.GlobalAttributes));
        Assert.That(hasChanged, Is.EqualTo(system.HasChanged));
        Assert.That(hasChanged, Is.True);
        Assert.That(system.Stages, Is.EqualTo(new[] { stage }));
    }

    [Test]
    public void ComputeSystemUpdate_ReturnsHasChangedPatchOutput()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle"));
        var system = new ComputeSystem();

        var result = system.Update(
            computeStage: stage,
            externalScheduler: null,
            enabled: true,
            hasChanged: out var hasChanged);

        Assert.That(result, Is.SameAs(system));
        Assert.That(hasChanged, Is.EqualTo(system.HasChanged));
        Assert.That(hasChanged, Is.True);
        Assert.That(system.Stages, Is.EqualTo(new[] { stage }));
    }

    [Test]
    public void AppendComputeStage_AddsStageAndResource()
    {
        var resource = CreateParticleResource("ParticleA");
        var stage = ComputeStage.FromStructuredBufferResource(resource, name: "UpdateA");
        var system = new ComputeSystemSpectral();

        system.AppendComputeStage("root/updateA", stage);

        Assert.That(system.Stages, Is.EqualTo(new[] { stage }));
        Assert.That(system.EnabledStages, Is.EqualTo(new[] { stage }));
        Assert.That(system.StageRegistrations[0].NodePath, Is.EqualTo("root/updateA"));
        Assert.That(system.StageRegistrations[0].StageProvider, Is.SameAs(stage));
        Assert.That(system.AppendedStages["root/updateA"], Is.SameAs(stage));
        Assert.That(system.Resources, Has.Count.EqualTo(1));
        Assert.That(system.Resources[0], Is.EqualTo(resource.GetComputeResource()));
        Assert.That(system.FindStage("UpdateA"), Is.SameAs(stage));
        Assert.That(system.IsValid, Is.True);
        Assert.That(system.HasChanged, Is.True);
    }

    [Test]
    public void AppendComputeStage_ReplacesProviderForExistingNodePath()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("First"), name: "First");
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Second"), name: "Second");
        var system = new ComputeSystemSpectral()
            .AppendComputeStage("root/update", first);

        system.AppendComputeStage("root/update", second);

        Assert.That(system.AppendedStages, Has.Count.EqualTo(1));
        Assert.That(system.StageRegistrations[0].StageProvider, Is.SameAs(second));
        Assert.That(system.Stages, Is.EqualTo(new[] { second }));
        Assert.That(system.Resources.Select(resource => resource.Resource), Is.EqualTo(new[] { "Second" }));
    }

    [Test]
    public void AppendComputeStage_HooksStageChangeGraphIntoSystem()
    {
        var stage = new ComputeStage(null);
        var listener = new RecordingChangeGraph();
        var node = new EmptyVoid(null);
        var system = new ComputeSystemSpectral()
            .AppendComputeStage("root/update", stage)
            .AddEventHook(listener);

        stage.SetComputeGraph(node);

        Assert.That(listener.Nodes, Is.EqualTo(new[] { node }));
        Assert.That(system.HasChanged, Is.True);

        system.RemoveEventHook(listener);
        stage.SetComputeGraph(new EmptyVoid(null));

        Assert.That(listener.Nodes, Has.Count.EqualTo(1));
    }

    [Test]
    public void AppendComputeStage_ReplacingNodePathDetachesOldStageChangeGraph()
    {
        var first = new ComputeStage(null);
        var second = new ComputeStage(null);
        var firstNode = new EmptyVoid(null);
        var secondNode = new EmptyVoid(null);
        var listener = new RecordingChangeGraph();
        var system = new ComputeSystemSpectral()
            .AddEventHook(listener)
            .AppendComputeStage("root/update", first)
            .AppendComputeStage("root/update", second);

        first.SetComputeGraph(firstNode);
        second.SetComputeGraph(secondNode);

        Assert.That(listener.Nodes, Is.EqualTo(new[] { secondNode }));
        Assert.That(system.Stages, Is.EqualTo(new[] { second }));
    }

    [Test]
    public void AppendComputeStage_MergesResourcesByTarget()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle"));
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle"));
        var system = new ComputeSystemSpectral();

        system
            .AppendComputeStage(first)
            .AppendComputeStage(second);

        Assert.That(system.Stages, Has.Count.EqualTo(2));
        Assert.That(system.Resources, Has.Count.EqualTo(1));
        Assert.That(system.Resources[0].Resource, Is.EqualTo("Particle"));
    }

    [Test]
    public void SetInput_ReplacesExistingStagesAndIncludesExplicitResource()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("First"));
        var secondResource = CreateParticleResource("Second");
        var second = ComputeStage.FromStructuredBufferResource(secondResource);
        var system = new ComputeSystemSpectral().AppendComputeStage(first);

        system.SetInput(secondResource, new[] { second });

        Assert.That(system.Stages, Is.EqualTo(new[] { second }));
        Assert.That(system.Resources, Has.Count.EqualTo(1));
        Assert.That(system.Resources[0].Resource, Is.EqualTo("Second"));
    }

    [Test]
    public void DispatchDiagnostics_AggregatesEnabledStageDiagnostics()
    {
        var invalidResource = CreateParticleResource(
            "Invalid",
            elementCount: 65_536L * 256L,
            threadGroupSize: 256);
        var invalidStage = ComputeStage.FromStructuredBufferResource(invalidResource);
        var disabledInvalidStage = ComputeStage
            .FromStructuredBufferResource(invalidResource)
            .SetEnabled(false);
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(invalidStage)
            .AppendComputeStage(disabledInvalidStage);

        Assert.That(system.IsValid, Is.False);
        Assert.That(
            system.DispatchDiagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
        Assert.That(system.DispatchDiagnostics, Has.Count.EqualTo(1));
    }

    [Test]
    public void GenerateShaderSources_GeneratesOnlyEnabledStagesByDefault()
    {
        var enabled = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Enabled"));
        enabled.RegisterGeneratedShaderSource = false;
        var disabled = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("Disabled"))
            .SetEnabled(false);
        disabled.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(enabled)
            .AppendComputeStage(disabled);

        var shaderSources = system.GenerateShaderSources(new ShaderGeneratorContext(), null);

        Assert.That(shaderSources, Has.Count.EqualTo(1));
        Assert.That(shaderSources[0], Is.InstanceOf<ShaderClassSource>());
        Assert.That(enabled.ShaderCode, Does.Contain("RWStructuredBuffer<Enabled>"));
        Assert.That(disabled.ShaderCode, Is.Null);
    }

    [Test]
    public void Clear_RemovesStagesAndResources()
    {
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(ComputeStage.FromStructuredBufferResource(CreateParticleResource("Particle")));

        system.Clear();

        Assert.That(system.Stages, Is.Empty);
        Assert.That(system.EnabledStages, Is.Empty);
        Assert.That(system.Resources, Is.Empty);
        Assert.That(system.DispatchDiagnostics, Is.Empty);
        Assert.That(system.IsValid, Is.True);
    }

    [Test]
    public void Dispose_ClearsSystemAndChildStageStateWithoutDisposingResources()
    {
        var resource = CreateParticleResource("DisposableSystem");
        var stage = ComputeStage.FromStructuredBufferResource(resource);
        var listener = new RecordingChangeGraph();
        var scheduler = new object();
        var system = new ComputeSystemSpectral()
            .AddEventHook(listener)
            .SetInternalScheduler(scheduler)
            .ProcessMainResource(resource)
            .AppendComputeStage("root/dispose", stage)
            .SetPreGraphRenderer(new EmptyVoid(null));

        system.Dispose();
        stage.SetComputeGraph(new EmptyVoid(null));

        Assert.That(system.Stages, Is.Empty);
        Assert.That(system.Resources, Is.Empty);
        Assert.That(system.AppendedStages, Is.Empty);
        Assert.That(system.LastLifecycleSteps, Is.Empty);
        Assert.That(system.MainResourceProvider, Is.Null);
        Assert.That(system.InternalScheduler, Is.Null);
        Assert.That(system.ExternalScheduler, Is.Null);
        Assert.That(system.IsScheduled, Is.False);
        Assert.That(system.HasChanged, Is.True);
        Assert.That(stage.GetResource(), Is.Null);
        Assert.That(listener.Nodes, Is.Empty);
        Assert.That(resource.GetBufferInput(), Is.Not.Null);
    }

    [Test]
    public void Update_RunsLifecycleAndReplacesStages()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("First"));
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Second"));
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(first)
            .SetEnabled(false);

        system.Update(computeStages: new[] { second }, enabled: true);

        Assert.That(system.Enabled, Is.True);
        Assert.That(system.Stages, Is.EqualTo(new[] { second }));
        Assert.That(system.Resources.Single().Resource, Is.EqualTo("Second"));
        Assert.That(system.HasChanged, Is.True);
    }

    [Test]
    public void Update_RunsPatchShapedLifecycleWithoutMainResource()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("LifecycleUpdate"));
        var system = new ComputeSystemSpectral();

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(
            system.LastLifecycleSteps,
            Is.EqualTo(new[]
            {
                ComputeSystemLifecycleStep.PrepareResources,
                ComputeSystemLifecycleStep.HandleAttributes,
                ComputeSystemLifecycleStep.WriteAttributesComputeStage,
                ComputeSystemLifecycleStep.AddDispatcherProvider,
                ComputeSystemLifecycleStep.SetPreGraphRenderer,
                ComputeSystemLifecycleStep.GetResources,
                ComputeSystemLifecycleStep.FinishResources
            }));
        Assert.That(stage.WriteAttributes, Is.True);
        Assert.That(stage.DispatcherProvider, Is.Not.Null);
    }

    [Test]
    public void Update_RunsPatchShapedLifecycleWithMainResource()
    {
        var resource = CreateParticleResource("MainLifecycle");
        var stage = new ComputeStage(null);
        var system = new ComputeSystemSpectral()
            .ProcessMainResource(resource);

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(
            system.LastLifecycleSteps,
            Is.EqualTo(new[]
            {
                ComputeSystemLifecycleStep.PrepareResources,
                ComputeSystemLifecycleStep.HandleAttributes,
                ComputeSystemLifecycleStep.WriteAttributesComputeStage,
                ComputeSystemLifecycleStep.AddDispatcherProvider,
                ComputeSystemLifecycleStep.SetPreGraphRenderer,
                ComputeSystemLifecycleStep.ProcessMainResource,
                ComputeSystemLifecycleStep.FinishResources
            }));
        Assert.That(system.MainResourceProvider, Is.SameAs(resource));
        Assert.That(stage.ResourceProvider, Is.SameAs(resource));
        Assert.That(stage.ComputeGraph, Is.Not.Null);
        Assert.That(system.Resources.Select(computeResource => computeResource.Resource), Does.Contain("MainLifecycle"));
    }

    [Test]
    public void Update_MainResourceDoesNotOverwriteStageResource()
    {
        var mainResource = CreateParticleResource("MainResource");
        var stageResource = CreateParticleResource("StageResource");
        var stage = ComputeStage.FromStructuredBufferResource(stageResource);
        var system = new ComputeSystemSpectral()
            .ProcessMainResource(mainResource);

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(system.MainResourceProvider, Is.SameAs(mainResource));
        Assert.That(stage.ResourceProvider, Is.SameAs(stageResource));
        Assert.That(stage.Resource.Resource, Is.EqualTo("StageResource"));
        Assert.That(stage.DispatchInfo, Is.SameAs(stageResource.GetDispatchInfo()));
        Assert.That(
            system.Resources.Select(resource => resource.Resource),
            Is.EquivalentTo(new[] { "MainResource", "StageResource" }));
        Assert.That(stage.ComputeGraph.BuildSourceCode(), Does.Contain("StageResource"));
    }

    [Test]
    public void Update_BindsGlobalAttributeValuesByAttributeName()
    {
        var value = new ConstantValue<float>(42f);
        var globalAttribute = new GlobalAttribute<float>(null, "Time", value);
        var computeAttribute = new TemporaryAttribute<float>(null, "Time");
        var graph = new Group(null, new[] { computeAttribute });
        var stage = new ComputeStage(null).Update(computeGraph: graph);
        var system = new ComputeSystemSpectral(new IGlobalAttribute[] { globalAttribute });

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(system.GlobalAttributes["Time"], Is.SameAs(value));
        Assert.That(system.AttributeHandler.GetAttributeSet().Keys, Does.Contain("Time"));
        Assert.That(system.AttributeHandler.GetAttributeInstances(), Does.Contain(computeAttribute));
        Assert.That(computeAttribute.InputAbstract, Is.SameAs(value));
        Assert.That(system.HasChanged, Is.True);
    }

    [Test]
    public void Update_BindsOnlyTemporaryAttributesAsGlobalAttributes()
    {
        var timeValue = new ConstantValue<float>(42f);
        var textureValue = new ConstantValue<float>(7f);
        var temporaryAttribute = new TemporaryAttribute<float>(null, "Time");
        var textureAttribute = FakeComputeAttribute<float>.Create("Albedo", AttributeType.Texture);
        var graph = new Group(null, Array.Empty<AbstractShaderNode>());
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, temporaryAttribute);
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, textureAttribute);
        var stage = new ComputeStage(null).Update(computeGraph: graph);
        var system = new ComputeSystemSpectral(new Dictionary<string, AbstractShaderNode>
        {
            ["Time"] = timeValue,
            ["Albedo"] = textureValue
        });

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(system.AttributeHandler.GetAttributeSet().Keys, Is.EqualTo(new[] { "Time" }));
        Assert.That(temporaryAttribute.InputAbstract, Is.SameAs(timeValue));
        Assert.That(textureAttribute.InputAbstract, Is.Null);
    }

    [Test]
    public void Update_RoutesTextureAttributesToStageTextureResource()
    {
        var resource = TextureResource.Create("TextureTarget", new Int3(32, 16, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        var graph = new Group(null, Array.Empty<AbstractShaderNode>());
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, color);
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .Update(computeGraph: graph);
        var system = new ComputeSystemSpectral(new Dictionary<string, AbstractShaderNode>
        {
            ["Color"] = new ConstantValue<float>(7f)
        });

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(resource.GetAttributeMap().AttributeSet.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureBInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(color.TextureInput, Is.SameAs(resource.TextureAInputs["Color"]));
        Assert.That(color.ReadCall, Is.TypeOf<ComputeTextureGet<Int2, float>>());
        Assert.That(color.WriteCall, Is.TypeOf<ComputeTextureSet<Int2, float>>());
        Assert.That(system.AttributeHandler.GetAttributeSet(), Is.Empty);
        Assert.That(system.Resources.Single().AttributeType, Is.EqualTo(AttributeType.Texture));
        Assert.That(system.HasChanged, Is.True);
    }

    [Test]
    public void Update_RemovesUnusedGlobalAttributesAfterPrepareFinish()
    {
        var value = new ConstantValue<float>(1f);
        var computeAttribute = new TemporaryAttribute<float>(null, "Transient");
        var stage = new ComputeStage(null).Update(computeGraph: new Group(null, new[] { computeAttribute }));
        var system = new ComputeSystemSpectral(new Dictionary<string, AbstractShaderNode>
        {
            ["Transient"] = value
        });

        system.Update(computeStages: new[] { stage }, enabled: true);
        system.Update(computeStages: Array.Empty<IComputeStageProvider>(), enabled: true);

        Assert.That(system.AttributeHandler.GetAttributeSet(), Is.Empty);
        Assert.That(system.HasChanged, Is.True);
    }

    [Test]
    public void Update_RoutesStructuredBufferAttributesToStageResource()
    {
        var resource = StructuredBufferResource.Create("Particle");
        var life = FakeComputeAttribute<float>.Create("Life");
        var position = FakeComputeAttribute<Vector3>.Create("Position");
        var graph = new Group(null, Array.Empty<AbstractShaderNode>());
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, life);
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, position);
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .Update(computeGraph: graph);
        var system = new ComputeSystemSpectral();

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(resource.GetAttributeMap().AttributeSet.Keys, Is.EquivalentTo(new[] { "Life", "Position" }));
        Assert.That(resource.ChangedAttributes, Is.True);
        Assert.That(resource.GetTicket(), Is.EqualTo(1));
        Assert.That(resource.GetStructSize(), Is.EqualTo(16));
        Assert.That(
            resource.GetStructDescription().Members.Select(member => member.Declaration),
            Is.EqualTo(new[] { "float Life;", "float3 Position;" }));
        Assert.That(system.AttributeHandler.GetAttributeSet(), Is.Empty);
        Assert.That(system.HasChanged, Is.True);
    }

    [Test]
    public void Update_BuildsStructuredBufferStageGraphThroughResourceBindings()
    {
        var resource = CreateParticleResource("SystemBinding");
        var postGraph = new EmptyVoid(null);
        var stage = ComputeStage
            .FromStructuredBufferResource(resource)
            .SetPreGraphRenderer(postGraph);
        var firstBindings = stage.StructuredBufferBindings;
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(stage);

        system.Update(enabled: true);

        Assert.That(stage.StructuredBufferBindings, Is.Not.SameAs(firstBindings));
        Assert.That(stage.StructuredBufferBindings.PostGraphRenderer, Is.SameAs(postGraph));
        Assert.That(stage.StructuredBufferBindings.WriteGroup.Ins.Last(), Is.SameAs(postGraph));
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph1D>());
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(stage.StructuredBufferBindings.WriteGroup));
        Assert.That(stage.ComputeGraph.BuildSourceCode(), Does.Contain("DynamicBufferInput"));
    }

    [Test]
    public void Draw_UsesEnabledStateAndStageDrawStage()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Drawable"));
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        Assert.That(system.Draw(new ShaderGeneratorContext()), Has.Count.EqualTo(1));
        Assert.That(stage.GetShaderCode(), Does.Contain("RWStructuredBuffer<Drawable>"));

        system.SetEnabled(false);
        Assert.That(system.Draw(new ShaderGeneratorContext()), Is.Empty);
    }

    [Test]
    public void Draw_UsesExecutionPlanAndSkipsInvalidDispatch()
    {
        var invalidResource = CreateParticleResource(
            "InvalidDraw",
            elementCount: 65_536L * 256L,
            threadGroupSize: 256);
        var invalidStage = ComputeStage.FromStructuredBufferResource(invalidResource);
        invalidStage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(invalidStage);

        var shaderSources = system.Draw(new ShaderGeneratorContext());
        var plan = system.BuildExecutionPlan(new ShaderGeneratorContext());

        Assert.That(shaderSources, Is.Empty);
        Assert.That(invalidStage.ShaderCode, Is.Null);
        Assert.That(plan.SkippedStages.Single().Status, Is.EqualTo(ComputeStageExecutionStatus.InvalidDispatch));
    }

    [Test]
    public void DrawResult_ExposesDispatchAndIterationDetails()
    {
        var stage = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("DrawResult", elementCount: 256, threadGroupSize: 64))
            .SetIterationCount(3);
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var result = system.DrawResult(new ShaderGeneratorContext());
        var stageResult = result.ReadyStages.Single();
        var commands = result.DispatchCommands;

        Assert.That(result.Enabled, Is.True);
        Assert.That(result.CanDispatch, Is.True);
        Assert.That(result.ShaderSources, Has.Count.EqualTo(1));
        Assert.That(stageResult.StageName, Is.EqualTo("ComputeStage"));
        Assert.That(stageResult.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(4, 1, 1)));
        Assert.That(stageResult.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(64, 1, 1)));
        Assert.That(stageResult.IterationCount, Is.EqualTo(3));
        Assert.That(stageResult.CanDispatch, Is.True);
        Assert.That(stageResult.StageDispatcher, Is.SameAs(stage.GetDispatcher()));
        Assert.That(stageResult.DispatchInfo, Is.SameAs(stage.DispatchInfo));
        Assert.That(commands, Has.Count.EqualTo(3));
        Assert.That(commands.Select(command => command.IterationIndex), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(commands.Select(command => command.IterationIndexGlobal.GetName()).Distinct(), Is.EqualTo(new[] { "IterationIndex" }));
        Assert.That(commands.Select(command => ((ConstantValue<int>)command.IterationIndexValue).Value), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(commands.All(command => command.IterationIndexGraph == command.IterationIndexGlobal.GetGraph()), Is.True);
        Assert.That(commands.All(command => command.IterationIndexSet.Target == command.IterationIndexGlobal), Is.True);
        Assert.That(commands.All(command => command.IterationIndexSet.Source == command.IterationIndexValue), Is.True);
        Assert.That(commands.All(command => command.IterationIndexSet.Graph == command.IterationIndexGraph), Is.True);
        Assert.That(commands.Select(command => command.DispatchGroups).Distinct(), Is.EqualTo(new[] { new ComputeDispatchSize(4, 1, 1) }));
        Assert.That(commands.Select(command => command.ThreadGroupSize).Distinct(), Is.EqualTo(new[] { new ComputeDispatchSize(64, 1, 1) }));
        Assert.That(commands.All(command => command.Stage == stage), Is.True);
        Assert.That(commands.All(command => command.StageDispatcher == stage.GetDispatcher()), Is.True);
        Assert.That(commands.All(command => command.DispatchInfo == stage.DispatchInfo), Is.True);
        Assert.That(commands.All(command => command.StageDispatcher.GetDispatchInfo() == stage.DispatchInfo), Is.True);
        Assert.That(commands.All(command => command.Dispatcher is IComputeEffectDispatcher), Is.True);
        Assert.That(
            commands.Select(command => GetThreadGroupCount(command.Dispatcher)).Distinct(),
            Is.EqualTo(new[] { new Int3(4, 1, 1) }));
        Assert.That(commands.All(command => command.PreRenderCommand == null), Is.True);
        Assert.That(commands.All(command => !command.SkipOutsideRange), Is.True);
    }

    [Test]
    public void DrawResult_ReportsInvalidDispatchDiagnostics()
    {
        var invalidResource = CreateParticleResource(
            "InvalidDrawResult",
            elementCount: 65_536L * 256L,
            threadGroupSize: 256);
        var stage = ComputeStage.FromStructuredBufferResource(invalidResource);
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var result = system.DrawResult(new ShaderGeneratorContext());
        var skippedStage = result.SkippedStages.Single();

        Assert.That(result.CanDispatch, Is.False);
        Assert.That(result.ShaderSources, Is.Empty);
        Assert.That(result.DispatchCommands, Is.Empty);
        Assert.That(skippedStage.Status, Is.EqualTo(ComputeStageExecutionStatus.InvalidDispatch));
        Assert.That(skippedStage.CanDispatch, Is.False);
        Assert.That(
            result.DispatchDiagnostics.Select(diagnostic => diagnostic.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.DispatchGroupCountExceeded));
    }

    [Test]
    public void DispatchExecutor_RunsHeadlessCommandStepsInPatchOrder()
    {
        var stage = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("ExecuteDraw", elementCount: 256, threadGroupSize: 64))
            .SetIterationCount(2);
        stage.RegisterGeneratedShaderSource = false;
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .DrawResult(new ShaderGeneratorContext());
        var executor = new ComputeDispatchExecutor();

        var executionResults = executor.Execute(result);

        Assert.That(executionResults, Has.Count.EqualTo(2));
        Assert.That(executionResults.Select(r => r.Command.IterationIndex), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(executionResults.All(r => r.IterationIndexSetApplied), Is.True);
        Assert.That(executionResults.All(r => !r.PreRenderCommandExecuted), Is.True);
        Assert.That(executionResults.All(r => !r.TextureResourceUpdated), Is.True);
        Assert.That(executionResults.All(r => !r.Dispatched), Is.True);
        Assert.That(executionResults.All(r => !r.PostDispatchGraphExecuted), Is.True);
        Assert.That(
            executionResults[0].Steps.Select(step => step.Kind),
            Is.EqualTo(new[]
            {
                ComputeDispatchExecutionStepKind.IterationIndexSet,
                ComputeDispatchExecutionStepKind.PreRenderCommand,
                ComputeDispatchExecutionStepKind.TextureResourceUpdate,
                ComputeDispatchExecutionStepKind.Dispatch,
                ComputeDispatchExecutionStepKind.PostDispatchGraph
            }));
        Assert.That(executionResults[0].Steps[2].Reason, Is.EqualTo("Stage has no texture resource."));
        Assert.That(executionResults[0].Steps[3].Reason, Is.EqualTo("RenderDrawContext is not assigned."));
        Assert.That(executionResults[0].Steps[4].Reason, Is.EqualTo("Dispatch was not executed."));
        Assert.That(
            executionResults.Select(r => ((ConstantValue<int>)r.Command.IterationIndexValue).Value),
            Is.EqualTo(new[] { 0, 1 }));
    }

    [Test]
    public void DrawResult_ExecuteUsesDispatchExecutor()
    {
        var stage = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("DrawResultExecute", elementCount: 256, threadGroupSize: 64))
            .SetIterationCount(2);
        stage.RegisterGeneratedShaderSource = false;
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .DrawResult(new ShaderGeneratorContext());

        var executionResults = result.Execute();

        Assert.That(executionResults, Has.Count.EqualTo(2));
        Assert.That(executionResults.All(r => r.IterationIndexSetApplied), Is.True);
        Assert.That(executionResults.All(r => !r.TextureResourceUpdated), Is.True);
        Assert.That(executionResults.All(r => !r.Dispatched), Is.True);
        Assert.That(executionResults.All(r => !r.PostDispatchGraphExecuted), Is.True);
        Assert.That(executionResults[0].Steps[3].Reason, Is.EqualTo("RenderDrawContext is not assigned."));
    }

    [Test]
    public void ComputeSystem_ExecuteBuildsDrawResultAndRunsDispatchExecutor()
    {
        var stage = ComputeStage.FromStructuredBufferResource(
            CreateParticleResource("SystemExecute", elementCount: 256, threadGroupSize: 64));
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var executionResults = system.Execute(null, new ShaderGeneratorContext());

        Assert.That(executionResults, Has.Count.EqualTo(1));
        Assert.That(executionResults.Single().Command.Stage, Is.SameAs(stage));
        Assert.That(executionResults.Single().IterationIndexSetApplied, Is.True);
        Assert.That(executionResults.Single().TextureResourceUpdated, Is.False);
        Assert.That(executionResults.Single().Dispatched, Is.False);
        Assert.That(executionResults.Single().PostDispatchGraphExecuted, Is.False);
    }

    [Test]
    public void DispatchExecutor_RunsTextureSwapAfterExecutedDispatch()
    {
        var resource = TextureResource.Create("Textures", new Int3(8, 8, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color)
            .BindComputeStage(null);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(
            null,
            resource.WriteGroup,
            new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher));
        var firstReadInput = resource.TextureAInputs["Color"];
        var firstWriteInput = resource.TextureBInputs["Color"];
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var executor = new ComputeDispatchExecutor();

        var skipped = executor.Execute(result).Single();

        Assert.That(skipped.TextureResourceUpdated, Is.False);
        Assert.That(skipped.Dispatched, Is.False);
        Assert.That(skipped.PostDispatchGraphExecuted, Is.False);
        Assert.That(resource.LastTextureStatus, Is.EqualTo("Uninitialized"));
        Assert.That(resource.TextureAInputs["Color"], Is.SameAs(firstReadInput));
        Assert.That(resource.TextureBInputs["Color"], Is.SameAs(firstWriteInput));

        executor.RenderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));
        var executed = executor.Execute(result).Single();

        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(executed.TextureResourceUpdated, Is.False);
        Assert.That(executed.Dispatched, Is.True);
        Assert.That(executed.PostDispatchGraphExecuted, Is.True);
        Assert.That(resource.TextureAInputs["Color"], Is.SameAs(firstWriteInput));
        Assert.That(resource.TextureBInputs["Color"], Is.SameAs(firstReadInput));
        Assert.That(color.TextureInput, Is.SameAs(firstWriteInput));
        Assert.That(executed.Steps.Last().Payload, Is.TypeOf<TextureSwap[]>());
    }

    [Test]
    public void DispatchExecutor_DefaultPolicyBlocksMissingGraphicsDeviceTextureResourceFailure()
    {
        var resource = TextureResource.Create("Textures", new Int3(8, 8, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var executor = new ComputeDispatchExecutor
        {
            RenderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext))
        };

        var executed = executor.Execute(result).Single();

        Assert.That(executed.TextureResourceUpdated, Is.True);
        Assert.That(executed.TextureResourceStatuses["Color:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executed.HasTextureResourceFailures, Is.True);
        Assert.That(executed.Dispatched, Is.False);
        Assert.That(executed.PostDispatchGraphExecuted, Is.False);
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(0));
        Assert.That(
            executed.Steps.Single(step => step.Kind == ComputeDispatchExecutionStepKind.Dispatch).Reason,
            Does.Contain("Color:A=Failed:GraphicsDevice=null"));
    }

    [Test]
    public void DispatchExecutor_CanAllowMissingGraphicsDeviceTextureResourceFailure()
    {
        var resource = TextureResource.Create("Textures", new Int3(8, 8, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var executor = new ComputeDispatchExecutor
        {
            RenderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext)),
            TextureResourceFailurePolicy = TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice
        };

        var executed = executor.Execute(result).Single();

        Assert.That(executed.TextureResourceUpdated, Is.True);
        Assert.That(executed.TextureResourceStatuses["Color:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executed.HasTextureResourceFailures, Is.True);
        Assert.That(executed.Dispatched, Is.True);
        Assert.That(executed.PostDispatchGraphExecuted, Is.True);
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
    }

    [Test]
    public void ComputeTextureStageFixture_RunsPatchOrderHeadlessAndSnapshotsResourceState()
    {
        var resource = TextureResource
            .Create("Textures", new Int3(17, 9, 1))
            .SetThreadGroupSize(new ComputeDispatchSize(8, 4, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        var velocity = new TextureAttribute<Vector2>(null, "Velocity", theIsDoubleBuffered: false);
        color.ShaderNode.WriteCounter = 1;
        velocity.ShaderNode.WriteCounter = 0;
        resource
            .Prepare()
            .HandleAttribute(color)
            .HandleAttribute(velocity)
            .FinishAttributeMap();
        var preRenderCommand = new FakeRenderer();
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null, name: "TextureUpdate")
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), preRenderCommand, dispatcher))
            .BuildComputeGraph();
        var drawResult = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var firstColorReadInput = resource.TextureAInputs["Color"];
        var firstColorWriteInput = resource.TextureBInputs["Color"];
        var velocityInput = resource.TextureAInputs["Velocity"];
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var execution = drawResult
            .Execute(renderDrawContext, TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice)
            .Single();

        Assert.That(drawResult.CanDispatch, Is.True);
        Assert.That(drawResult.ReadyStages.Single().StageName, Is.EqualTo("TextureUpdate"));
        Assert.That(drawResult.ReadyStages.Single().DispatchGroups, Is.EqualTo(new ComputeDispatchSize(3, 3, 1)));
        Assert.That(drawResult.ReadyStages.Single().ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 1)));
        Assert.That(drawResult.DispatchCommands, Has.Count.EqualTo(1));
        Assert.That(drawResult.Resources.Single().Resource, Is.EqualTo("Textures"));
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph2D>());
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(resource.WriteGroup));
        Assert.That(resource.ReadGroup.Ins, Does.Contain(color.ReadCall));
        Assert.That(resource.ReadGroup.Ins, Does.Contain(velocity.ReadCall));
        Assert.That(resource.WriteGroup.Ins, Does.Contain(color.WriteCall));
        Assert.That(resource.WriteGroup.Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(color));
        Assert.That(resource.WriteGroup.Ins, Does.Not.Contain(velocity.WriteCall));
        Assert.That(color.ReadCall.SourceCode, Does.Contain(firstColorReadInput.ID));
        Assert.That(color.WriteCall.SourceCode, Does.Contain(firstColorWriteInput.ID));
        Assert.That(velocity.ReadCall.SourceCode, Does.Contain(velocityInput.ID));
        Assert.That(
            execution.Steps.Select(step => step.Kind),
            Is.EqualTo(new[]
            {
                ComputeDispatchExecutionStepKind.IterationIndexSet,
                ComputeDispatchExecutionStepKind.PreRenderCommand,
                ComputeDispatchExecutionStepKind.TextureResourceUpdate,
                ComputeDispatchExecutionStepKind.Dispatch,
                ComputeDispatchExecutionStepKind.PostDispatchGraph
            }));
        Assert.That(execution.IterationIndexSetApplied, Is.True);
        Assert.That(execution.PreRenderCommandExecuted, Is.True);
        Assert.That(execution.TextureResourceUpdated, Is.True);
        Assert.That(execution.Dispatched, Is.True);
        Assert.That(execution.PostDispatchGraphExecuted, Is.True);
        Assert.That(preRenderCommand.DrawCount, Is.EqualTo(1));
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(execution.TextureResourceStatuses["Color:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(execution.TextureResourceStatuses["Color:B"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(execution.TextureResourceStatuses["Velocity:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(execution.HasTextureResourceFailures, Is.True);
        Assert.That(resource.TextureAInputs["Color"], Is.SameAs(firstColorWriteInput));
        Assert.That(resource.TextureBInputs["Color"], Is.SameAs(firstColorReadInput));
        Assert.That(color.TextureInput, Is.SameAs(firstColorWriteInput));
        Assert.That(resource.TextureAInputs["Velocity"], Is.SameAs(velocityInput));
        Assert.That(execution.Steps.Last().Payload, Is.TypeOf<TextureSwap[]>());
    }

    [Test]
    public void ComputeTextureHelpFixture_WriteToTextureRunsTwoTextureStages()
    {
        var texture2D = TextureResource
            .Create(size: new Int3(64, 64, 1))
            .SetThreadGroupSize(new ComputeDispatchSize(8, 8, 1));
        var texture3D = TextureResource
            .Create(size: new Int3(64, 64, 64))
            .SetThreadGroupSize(new ComputeDispatchSize(8, 8, 8));
        var noise2D = new TextureAttribute<float>(null, "NoiseData", theIsDoubleBuffered: true);
        var noise3D = new TextureAttribute<float>(null, "NoiseData", theIsDoubleBuffered: false);
        noise2D.ShaderNode.WriteCounter = 1;
        noise3D.ShaderNode.WriteCounter = 1;
        texture2D
            .Prepare()
            .HandleAttribute(noise2D)
            .FinishAttributeMap();
        texture3D
            .Prepare()
            .HandleAttribute(noise3D)
            .FinishAttributeMap();
        var dispatcher2D = new FakeComputeEffectDispatcher();
        var dispatcher3D = new FakeComputeEffectDispatcher();
        var stage2D = new ComputeStage(null)
            .SetResourceProvider(texture2D)
            .SetDispatchInfo(new TestDispatchInfo(texture2D.GetDispatchInfo(), null, dispatcher2D))
            .BuildComputeGraph();
        var stage3D = new ComputeStage(null)
            .SetResourceProvider(texture3D)
            .SetDispatchInfo(new TestDispatchInfo(texture3D.GetDispatchInfo(), null, dispatcher3D))
            .BuildComputeGraph();
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(stage2D)
            .AppendComputeStage(stage3D);
        var shaderSources = system.GenerateShaderSources(new ShaderGeneratorContext(), null);
        var (compile2D, errors2D) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(stage2D.LastShaderGenerator);
        var (compile3D, errors3D) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(stage3D.LastShaderGenerator);
        var drawResult = system
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var first2DReadInput = texture2D.TextureAInputs["NoiseData"];
        var first2DWriteInput = texture2D.TextureBInputs["NoiseData"];
        var texture3DInput = texture3D.TextureAInputs["NoiseData"];
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executions = drawResult.Execute(
            renderDrawContext,
            TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice);

        Assert.That(drawResult.CanDispatch, Is.True);
        Assert.That(shaderSources, Has.Count.EqualTo(2));
        Assert.That(errors2D, Is.Empty, string.Join(Environment.NewLine, errors2D));
        Assert.That(errors3D, Is.Empty, string.Join(Environment.NewLine, errors3D));
        Assert.That(compile2D.Bytecode, Is.Not.Null);
        Assert.That(compile3D.Bytecode, Is.Not.Null);
        Assert.That(drawResult.ReadyStages.Select(stage => stage.StageName), Is.EqualTo(new[] { "ComputeStage", "ComputeStage" }));
        Assert.That(drawResult.ReadyStages.Select(stage => stage.DispatchGroups), Is.EqualTo(new[]
        {
            new ComputeDispatchSize(8, 8, 1),
            new ComputeDispatchSize(8, 8, 8)
        }));
        Assert.That(drawResult.DispatchCommands, Has.Count.EqualTo(2));
        Assert.That(drawResult.Resources.Select(resource => resource.Resource), Is.EqualTo(new string[] { null, null }));
        Assert.That(drawResult.Resources.Select(resource => resource.Size), Is.EqualTo(new[]
        {
            new Int3(64, 64, 1),
            new Int3(64, 64, 64)
        }));
        Assert.That(stage2D.ShaderCode, Does.Contain("RWTexture2D<float>"));
        Assert.That(stage2D.ShaderCode, Does.Contain("Texture2D<float>"));
        Assert.That(stage2D.ShaderCode, Does.Contain("NoiseData_64x64x1_A_"));
        Assert.That(stage2D.ShaderCode, Does.Contain("NoiseData_64x64x1_B_"));
        Assert.That(stage2D.ShaderCode, Does.Contain("streams.DispatchThreadId.xy >= uint2(64, 64)"));
        Assert.That(stage3D.ShaderCode, Does.Contain("RWTexture3D<float>"));
        Assert.That(stage3D.ShaderCode, Does.Contain("Texture3D<float>"));
        Assert.That(stage3D.ShaderCode, Does.Contain("NoiseData_64x64x64_A_"));
        Assert.That(stage3D.ShaderCode, Does.Not.Contain("NoiseData_64x64x64_B_"));
        Assert.That(stage3D.ShaderCode, Does.Contain("streams.DispatchThreadId >= uint3(64, 64, 64)"));
        Assert.That(first2DReadInput.TextureName, Is.EqualTo("NoiseData_64x64x1_A"));
        Assert.That(first2DWriteInput.TextureName, Is.EqualTo("NoiseData_64x64x1_B"));
        Assert.That(texture3DInput.TextureName, Is.EqualTo("NoiseData_64x64x64_A"));
        Assert.That(
            new[] { first2DReadInput.ID, first2DWriteInput.ID, texture3DInput.ID },
            Is.Unique);
        Assert.That(executions, Has.Count.EqualTo(2));
        Assert.That(executions.All(execution => execution.TextureResourceUpdated), Is.True);
        Assert.That(executions.All(execution => execution.Dispatched), Is.True);
        Assert.That(executions[0].PostDispatchGraphExecuted, Is.True);
        Assert.That(executions[1].PostDispatchGraphExecuted, Is.False);
        Assert.That(executions[1].Steps.Last().Reason, Is.EqualTo("Compute graph has no post-dispatch texture swaps."));
        Assert.That(dispatcher2D.DispatchCount, Is.EqualTo(1));
        Assert.That(dispatcher3D.DispatchCount, Is.EqualTo(1));
        Assert.That(executions[0].TextureResourceStatuses["NoiseData:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executions[0].TextureResourceStatuses["NoiseData:B"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executions[1].TextureResourceStatuses["NoiseData:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executions[1].TextureResourceStatuses.ContainsKey("NoiseData:B"), Is.False);
        Assert.That(texture2D.WriteGroup.Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(noise2D));
        Assert.That(texture3D.WriteGroup.Ins.OfType<TextureSwap>(), Is.Empty);
        Assert.That(texture2D.TextureAInputs["NoiseData"], Is.SameAs(first2DWriteInput));
        Assert.That(texture2D.TextureBInputs["NoiseData"], Is.SameAs(first2DReadInput));
        Assert.That(noise2D.TextureInput, Is.SameAs(first2DWriteInput));
        Assert.That(texture3D.TextureAInputs["NoiseData"], Is.SameAs(texture3DInput));
        Assert.That(texture3D.TextureBInputs, Is.Empty);
        Assert.That(noise3D.TextureInput, Is.SameAs(texture3DInput));
    }

    [Test]
    public void ComputeTextureHelpFixture_AverageBuildsAndCompilesPatchShapedTextureStage()
    {
        var resource = TextureResource
            .Create(size: new Int3(64, 64, 1))
            .SetThreadGroupSize(new ComputeDispatchSize(8, 8, 1));
        var noiseData = new TextureAttribute<float>(null, "NoiseData", theIsDoubleBuffered: true);
        var sampleData = new TextureAttribute<float>(null, "SampleData", theIsDoubleBuffered: true);
        noiseData.SetInput(new ConstantValue<float>(1f));
        noiseData.ShaderNode.WriteCounter = 1;
        sampleData.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(noiseData)
            .HandleAttribute(sampleData)
            .FinishAttributeMap();
        var dynamicIndex = new DynamicIndex(null, new DispatchIdIndexProvider());
        var index = new Fuse.GetMember<Int3, Int2>(null, dynamicIndex, "xy");
        sampleData.SetInput(new Average<Int2, float>(
            null,
            noiseData,
            new ConstantValue<int>(1),
            index));
        var graph = new Group(null, new AbstractShaderNode[] { noiseData, sampleData }, "UseAverageTexture");
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, noiseData);
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, sampleData);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .Update(computeGraph: graph)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();

        stage.GenerateShaderSource(new ShaderGeneratorContext(), null);
        var (compile, errors) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(stage.LastShaderGenerator);
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(stage);
        var drawResult = system
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var firstNoiseReadInput = resource.TextureAInputs["NoiseData"];
        var firstNoiseWriteInput = resource.TextureBInputs["NoiseData"];
        var firstSampleReadInput = resource.TextureAInputs["SampleData"];
        var firstSampleWriteInput = resource.TextureBInputs["SampleData"];
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executions = drawResult.Execute(
            renderDrawContext,
            TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice);

        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        Assert.That(compile.Bytecode, Is.Not.Null);
        Assert.That(drawResult.CanDispatch, Is.True);
        Assert.That(drawResult.ReadyStages.Select(stageInfo => stageInfo.StageName), Is.EqualTo(new[] { "ComputeStage" }));
        Assert.That(drawResult.ReadyStages.Single().DispatchGroups, Is.EqualTo(new ComputeDispatchSize(8, 8, 1)));
        Assert.That(drawResult.Resources.Select(computeResource => computeResource.Resource), Is.EqualTo(new string[] { null }));
        Assert.That(drawResult.Resources.Single().Size, Is.EqualTo(new Int3(64, 64, 1)));
        Assert.That(stage.ShaderCode, Does.Contain("RWTexture2D<float>"));
        Assert.That(stage.ShaderCode, Does.Contain("Texture2D<float>"));
        Assert.That(stage.ShaderCode, Does.Contain("NoiseData_64x64x1_A_"));
        Assert.That(stage.ShaderCode, Does.Contain("NoiseData_64x64x1_B_"));
        Assert.That(stage.ShaderCode, Does.Contain("SampleData_64x64x1_B_"));
        Assert.That(stage.ShaderCode, Does.Contain("Average_"));
        Assert.That(stage.ShaderCode, Does.Contain("averageRadius_"));
        Assert.That(stage.ShaderCode, Does.Contain("averageCount_"));
        Assert.That(stage.ShaderCode, Does.Contain("int2(averageX_"));
        Assert.That(stage.ShaderCode, Does.Contain("streams.DispatchThreadId.xy >= uint2(64, 64)"));
        Assert.That(executions, Has.Count.EqualTo(1));
        Assert.That(executions.Single().TextureResourceUpdated, Is.True);
        Assert.That(executions.Single().Dispatched, Is.True);
        Assert.That(executions.Single().PostDispatchGraphExecuted, Is.True);
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(executions.Single().TextureResourceStatuses["NoiseData:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executions.Single().TextureResourceStatuses["NoiseData:B"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executions.Single().TextureResourceStatuses["SampleData:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(executions.Single().TextureResourceStatuses["SampleData:B"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(resource.TextureAInputs["NoiseData"], Is.SameAs(firstNoiseWriteInput));
        Assert.That(resource.TextureBInputs["NoiseData"], Is.SameAs(firstNoiseReadInput));
        Assert.That(resource.TextureAInputs["SampleData"], Is.SameAs(firstSampleWriteInput));
        Assert.That(resource.TextureBInputs["SampleData"], Is.SameAs(firstSampleReadInput));
        Assert.That(noiseData.TextureInput, Is.SameAs(firstNoiseWriteInput));
        Assert.That(sampleData.TextureInput, Is.SameAs(firstSampleWriteInput));
    }

    [Test]
    public void ComputeTextureHelpFixture_GameOfLifeGroupsThreeStagesOnSharedTextureResource()
    {
        var resource = TextureResource
            .Create("CellData", new Int3(256, 256, 1))
            .SetThreadGroupSize(new ComputeDispatchSize(8, 8, 1));
        var initAttribute = new TextureAttribute<int>(null, "CellData", theIsDoubleBuffered: true);
        var updateAttribute = new TextureAttribute<int>(null, "CellData", theIsDoubleBuffered: true);
        var alterAttribute = new TextureAttribute<int>(null, "CellData", theIsDoubleBuffered: true);
        initAttribute.ShaderNode.WriteCounter = 1;
        updateAttribute.ShaderNode.WriteCounter = 1;
        alterAttribute.ShaderNode.WriteCounter = 1;
        var initStage = CreateTextureAttributeStage("InitializeCells", initAttribute);
        var updateStage = CreateTextureAttributeStage("UpdateCells", updateAttribute);
        var alterStage = CreateTextureAttributeStage("AlterCells", alterAttribute);
        var initDispatcher = new FakeComputeEffectDispatcher();
        var updateDispatcher = new FakeComputeEffectDispatcher();
        var alterDispatcher = new FakeComputeEffectDispatcher();
        var group = ComputeStageGroup.Create(
            resourceProvider: resource,
            computeStageProviders: new[] { initStage, updateStage, alterStage });
        initStage.SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, initDispatcher));
        updateStage.SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, updateDispatcher));
        alterStage.SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, alterDispatcher));
        var system = new ComputeSystemSpectral();

        system.Update(computeStages: new[] { group }, enabled: true);
        var drawResult = system
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var firstReadInput = resource.TextureAInputs["CellData"];
        var firstWriteInput = resource.TextureBInputs["CellData"];
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executions = drawResult.Execute(
            renderDrawContext,
            TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice);

        Assert.That(group.Stages, Is.EqualTo(new[] { initStage, updateStage, alterStage }));
        Assert.That(group.ResourceProvider, Is.SameAs(resource));
        Assert.That(group.Stages.All(stage => stage.ResourceProvider == resource), Is.True);
        Assert.That(resource.GetAttributeMap().AttributeSet.Keys, Is.EqualTo(new[] { "CellData" }));
        Assert.That(resource.GetAttributeMap().GetAttributeInstances(), Has.Count.EqualTo(3));
        Assert.That(drawResult.ReadyStages.Select(stage => stage.StageName), Is.EqualTo(new[]
        {
            "InitializeCells",
            "UpdateCells",
            "AlterCells"
        }));
        Assert.That(drawResult.ReadyStages.Select(stage => stage.DispatchGroups).Distinct(), Is.EqualTo(new[]
        {
            new ComputeDispatchSize(32, 32, 1)
        }));
        Assert.That(drawResult.Resources.Select(computeResource => computeResource.Resource), Is.EqualTo(new[] { "CellData" }));
        Assert.That(executions, Has.Count.EqualTo(3));
        Assert.That(executions.All(execution => execution.TextureResourceUpdated), Is.True);
        Assert.That(executions.All(execution => execution.Dispatched), Is.True);
        Assert.That(executions.All(execution => execution.PostDispatchGraphExecuted), Is.True);
        Assert.That(initDispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(updateDispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(alterDispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(executions.All(execution => execution.TextureResourceStatuses["CellData:A"].StartsWith("Failed:GraphicsDevice=null")), Is.True);
        Assert.That(executions.All(execution => execution.TextureResourceStatuses["CellData:B"].StartsWith("Failed:GraphicsDevice=null")), Is.True);
        Assert.That(initStage.ComputeGraph.Ins.OfType<Group>().Single().Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(initAttribute));
        Assert.That(updateStage.ComputeGraph.Ins.OfType<Group>().Single().Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(updateAttribute));
        Assert.That(alterStage.ComputeGraph.Ins.OfType<Group>().Single().Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(alterAttribute));
        Assert.That(resource.TextureAInputs["CellData"], Is.SameAs(firstWriteInput));
        Assert.That(resource.TextureBInputs["CellData"], Is.SameAs(firstReadInput));
        Assert.That(alterAttribute.TextureInput, Is.SameAs(firstWriteInput));
    }

    [Test]
    public void ComputeTextureHelpFixture_ReactionDiffusionBuildsAndCompilesTwoStageTextureSystem()
    {
        var resource = TextureResource
            .Create("ReactionData", new Int3(1024, 1024, 1))
            .SetThreadGroupSize(new ComputeDispatchSize(8, 8, 1));
        var seedAttribute = new TextureAttribute<Vector2>(null, "ReactionData", theIsDoubleBuffered: true);
        var updateAttribute = new TextureAttribute<Vector2>(null, "ReactionData", theIsDoubleBuffered: true);
        seedAttribute.ShaderNode.WriteCounter = 1;
        updateAttribute.ShaderNode.WriteCounter = 1;
        var seedFormula = new ReactionDiffusionSeedFormulaNode(null, seedAttribute);
        seedAttribute.SetInput(seedFormula);
        var reactionTexture = resource.AddInput(null, "ReactionData");
        var laplace = new Laplace2DKarlSims<Vector2>(
            null,
            reactionTexture,
            new ConstantValue<float>(0.2f),
            new ConstantValue<float>(0.05f),
            new ConstantValue<float>(1f));
        var updateFormula = new ReactionDiffusionFormulaNode(
            null,
            reactionTexture,
            updateAttribute,
            laplace);
        updateAttribute.SetInput(updateFormula);
        var seedStage = CreateTextureAttributeStage("Initialize", seedAttribute)
            .SetEnabled(false);
        var updateStage = CreateTextureAttributeStage("Update", updateAttribute)
            .SetIterationCount(20)
            .SetEnabled(true);
        seedStage.RegisterGeneratedShaderSource = false;
        updateStage.RegisterGeneratedShaderSource = false;
        var seedDispatcher = new FakeComputeEffectDispatcher();
        var updateDispatcher = new FakeComputeEffectDispatcher();
        var group = ComputeStageGroup.Create(
            resourceProvider: resource,
            computeStageProviders: new[] { seedStage, updateStage });
        seedStage.SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, seedDispatcher));
        updateStage.SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, updateDispatcher));
        var system = new ComputeSystemSpectral();

        system.Update(computeStages: new[] { group }, enabled: true);
        var drawResult = system
            .BuildExecutionPlan(new ShaderGeneratorContext(), generateShaderSources: true)
            .ToDrawResult();
        var firstReadInput = resource.TextureAInputs["ReactionData"];
        var firstWriteInput = resource.TextureBInputs["ReactionData"];
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        seedStage.SetEnabled(true);
        seedStage.GenerateShaderSource(new ShaderGeneratorContext(), null);
        var seedCompile = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(seedStage.LastShaderGenerator);
        seedStage.SetEnabled(false);
        var updateCompile = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(updateStage.LastShaderGenerator);
        var executions = drawResult.Execute(
            renderDrawContext,
            TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice);

        Assert.That(resource.GetDimension(), Is.EqualTo(new Int3(1024, 1024, 1)));
        Assert.That(group.Stages, Is.EqualTo(new[] { seedStage, updateStage }));
        Assert.That(group.EnabledStages, Is.EqualTo(new[] { updateStage }));
        Assert.That(group.ResourceProvider, Is.SameAs(resource));
        Assert.That(group.Stages.All(stage => stage.ResourceProvider == resource), Is.True);
        Assert.That(resource.GetAttributeMap().AttributeSet.Keys, Is.EqualTo(new[] { "ReactionData" }));
        Assert.That(resource.GetAttributeMap().GetAttributeInstances(), Has.Count.EqualTo(2));
        Assert.That(seedStage.IterationCount, Is.EqualTo(1));
        Assert.That(seedStage.Enabled, Is.False);
        Assert.That(updateStage.IterationCount, Is.EqualTo(20));
        Assert.That(updateStage.Enabled, Is.True);
        Assert.That(drawResult.CanDispatch, Is.True);
        Assert.That(drawResult.ShaderSources, Has.Count.EqualTo(1));
        Assert.That(drawResult.ReadyStages.Select(stage => stage.StageName), Is.EqualTo(new[]
        {
            "Update"
        }));
        Assert.That(drawResult.ReadyStages.Select(stage => stage.DispatchGroups).Distinct(), Is.EqualTo(new[]
        {
            new ComputeDispatchSize(128, 128, 1)
        }));
        Assert.That(drawResult.DispatchCommands, Has.Count.EqualTo(20));
        Assert.That(drawResult.DispatchCommands.Count(command => command.Stage == seedStage), Is.EqualTo(0));
        Assert.That(drawResult.DispatchCommands.Count(command => command.Stage == updateStage), Is.EqualTo(20));
        Assert.That(drawResult.DispatchCommands.Where(command => command.Stage == updateStage).Select(command => command.IterationIndex), Is.EqualTo(Enumerable.Range(0, 20)));
        Assert.That(drawResult.Resources.Select(computeResource => computeResource.Resource), Is.EqualTo(new[] { "ReactionData" }));
        Assert.That(executions, Has.Count.EqualTo(20));
        Assert.That(executions.All(execution => execution.TextureResourceUpdated), Is.True);
        Assert.That(executions.All(execution => execution.Dispatched), Is.True);
        Assert.That(executions.All(execution => execution.PostDispatchGraphExecuted), Is.True);
        Assert.That(seedDispatcher.DispatchCount, Is.EqualTo(0));
        Assert.That(updateDispatcher.DispatchCount, Is.EqualTo(20));
        Assert.That(executions.All(execution => execution.TextureResourceStatuses["ReactionData:A"].StartsWith("Failed:GraphicsDevice=null")), Is.True);
        Assert.That(executions.All(execution => execution.TextureResourceStatuses["ReactionData:B"].StartsWith("Failed:GraphicsDevice=null")), Is.True);
        Assert.That(seedStage.ShaderCode, Does.Contain("RWTexture2D<float2>"));
        Assert.That(seedStage.ShaderCode, Does.Not.Contain("stage Texture2D<float2>"));
        Assert.That(seedStage.ShaderCode, Does.Contain("reactionSeedMask"));
        Assert.That(seedStage.ShaderCode, Does.Contain("step(400.0"));
        Assert.That(seedStage.ShaderCode, Does.Contain("step(reactionSeedIndex"));
        Assert.That(updateStage.ShaderCode, Does.Contain("RWTexture2D<float2>"));
        Assert.That(updateStage.ShaderCode, Does.Contain("Texture2D<float2>"));
        Assert.That(updateStage.ShaderCode, Does.Contain("ReactionData_A_"));
        Assert.That(updateStage.ShaderCode, Does.Contain("ReactionData_B_"));
        Assert.That(updateStage.ShaderCode, Does.Not.Contain("TextureInput_"));
        Assert.That(updateStage.ShaderCode, Does.Contain("Laplace2DKarlSims_"));
        Assert.That(updateStage.ShaderCode, Does.Contain("laplaceCenter_"));
        Assert.That(updateStage.ShaderCode, Does.Contain("int2(-1, 0)"));
        Assert.That(updateStage.ShaderCode, Does.Contain("int2(1, 1)"));
        Assert.That(updateStage.ShaderCode, Does.Contain("reactionLaplacian"));
        Assert.That(updateStage.ShaderCode, Does.Contain("reactionRate"));
        Assert.That(updateStage.ShaderCode, Does.Contain("int2(-1, 0)"));
        Assert.That(updateStage.ShaderCode, Does.Contain("float2(reactionNextA"));
        Assert.That(seedCompile.Errors, Is.Empty, string.Join(Environment.NewLine, seedCompile.Errors));
        Assert.That(updateCompile.Errors, Is.Empty, string.Join(Environment.NewLine, updateCompile.Errors));
        Assert.That(seedCompile.Result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
        Assert.That(updateCompile.Result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
        Assert.That(seedStage.ComputeGraph.Ins.OfType<Group>().Single().Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(seedAttribute));
        Assert.That(updateStage.ComputeGraph.Ins.OfType<Group>().Single().Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(updateAttribute));
        Assert.That(resource.TextureAInputs["ReactionData"], Is.SameAs(firstReadInput));
        Assert.That(resource.TextureBInputs["ReactionData"], Is.SameAs(firstWriteInput));
        Assert.That(updateAttribute.TextureInput, Is.SameAs(firstReadInput));
    }

    [Test]
    public void DrawResult_ExecuteCanPassTextureResourceFailurePolicy()
    {
        var resource = TextureResource.Create("Textures", new Int3(8, 8, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executionResults = result.Execute(
            renderDrawContext,
            TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice);

        Assert.That(executionResults.Single().Dispatched, Is.True);
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
    }

    [Test]
    public void ComputeSystem_ExecuteCanPassTextureResourceFailurePolicy()
    {
        var resource = TextureResource.Create("Textures", new Int3(8, 8, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executionResults = system.Execute(
            renderDrawContext,
            new ShaderGeneratorContext(),
            textureResourceFailurePolicy: TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice);

        Assert.That(executionResults.Single().Dispatched, Is.True);
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
    }

    [Test]
    public void DispatchExecutor_ExposesTextureResourceUpdateStatuses()
    {
        var resource = TextureResource.Create("Textures", new Int3(8, 8, 1));
        var position = new TextureAttribute<Vector3>(null, "Position", theIsDoubleBuffered: true);
        position.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(position);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var executor = new ComputeDispatchExecutor
        {
            RenderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext))
        };

        var executed = executor.Execute(result).Single();
        var textureUpdate = executed.Steps
            .Single(step => step.Kind == ComputeDispatchExecutionStepKind.TextureResourceUpdate)
            .Payload as TextureResourceUpdateResult;

        Assert.That(executed.TextureResourceUpdated, Is.True);
        Assert.That(executed.Dispatched, Is.False);
        Assert.That(executed.PostDispatchGraphExecuted, Is.False);
        Assert.That(executed.HasTextureResourceFailures, Is.True);
        Assert.That(executed.TextureResourceStatuses["Position:A"], Does.StartWith("Failed:Description:ArgumentException"));
        Assert.That(executed.TextureResourceStatuses["Position:B"], Does.StartWith("Failed:Description:ArgumentException"));
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(0));
        Assert.That(
            executed.Steps.Single(step => step.Kind == ComputeDispatchExecutionStepKind.Dispatch).Reason,
            Does.StartWith("Texture resource update failed:"));
        Assert.That(
            executed.Steps.Single(step => step.Kind == ComputeDispatchExecutionStepKind.PostDispatchGraph).Reason,
            Is.EqualTo("Dispatch was not executed."));
        Assert.That(textureUpdate, Is.Not.Null);
        Assert.That(textureUpdate.HasFailures, Is.True);
        Assert.That(textureUpdate.Statuses["Position:A"], Is.EqualTo(executed.TextureResourceStatuses["Position:A"]));
    }

    [Test]
    public void DispatchExecutor_BlocksTextureResourceDimensionsAboveD3D11Limits()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 16, 2_049));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var dispatcher = new FakeComputeEffectDispatcher();
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetDispatchInfo(new TestDispatchInfo(resource.GetDispatchInfo(), null, dispatcher))
            .BuildComputeGraph();
        var result = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .BuildExecutionPlan(generateShaderSources: false)
            .ToDrawResult();
        var executor = new ComputeDispatchExecutor
        {
            RenderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext)),
            TextureResourceFailurePolicy = TextureResourceFailureDispatchPolicy.AllowMissingGraphicsDevice
        };

        var executed = executor.Execute(result).Single();

        Assert.That(executed.TextureResourceUpdated, Is.True);
        Assert.That(executed.Dispatched, Is.False);
        Assert.That(executed.PostDispatchGraphExecuted, Is.False);
        Assert.That(executed.HasTextureResourceFailures, Is.True);
        Assert.That(executed.TextureResourceStatuses["Color:A"], Does.StartWith("Failed:Description:ArgumentOutOfRangeException"));
        Assert.That(executed.TextureResourceStatuses["Color:B"], Does.StartWith("Failed:Description:ArgumentOutOfRangeException"));
        Assert.That(executed.TextureResourceStatuses["Color:A"], Does.Contain("Texture dimension Z"));
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(0));
        Assert.That(
            executed.Steps.Single(step => step.Kind == ComputeDispatchExecutionStepKind.Dispatch).Reason,
            Does.Contain("Color:A=Failed:Description:ArgumentOutOfRangeException"));
    }

    [Test]
    public void ComputeSystem_DrawWithRenderContextRunsStageDispatchSteps()
    {
        var stage = ComputeStage.FromStructuredBufferResource(
            CreateParticleResource("SystemDrawRenderContext", elementCount: 256, threadGroupSize: 64));
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);

        var executionResults = system.Draw(
            (RenderDrawContext)null,
            new ShaderGeneratorContext());

        Assert.That(executionResults, Has.Count.EqualTo(1));
        Assert.That(executionResults.Single().Command.Stage, Is.SameAs(stage));
        Assert.That(executionResults.Single().IterationIndexSetApplied, Is.True);
        Assert.That(executionResults.Single().Dispatched, Is.False);

        system.SetEnabled(false);
        Assert.That(system.Draw((RenderDrawContext)null, new ShaderGeneratorContext()), Is.Empty);
    }

    [Test]
    public void ComputeSystem_DrawWithRenderContextDrawsRendererComputeStage()
    {
        var renderer = new FakeRenderer();
        var stage = ToComputeStage.Create(input: renderer);
        var system = new ComputeSystemSpectral().AppendComputeStage(stage);
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executionResults = system.Draw(renderDrawContext, new ShaderGeneratorContext());

        Assert.That(executionResults, Is.Empty);
        Assert.That(renderer.DrawCount, Is.EqualTo(1));
        Assert.That(renderer.LastContext, Is.SameAs(renderDrawContext));

        stage.SetEnabled(false);
        system.Draw(renderDrawContext, new ShaderGeneratorContext());
        Assert.That(renderer.DrawCount, Is.EqualTo(1));
    }

    [Test]
    public void Update_SchedulesSystemInternallyWhenNoExternalSchedulerIsAssigned()
    {
        var scheduler = new FakeScheduler();
        var stage = ToComputeStage.Create(input: new FakeRenderer());
        var system = new ComputeSystemSpectral()
            .SetInternalScheduler(scheduler);

        system.Update(computeStages: new[] { stage }, enabled: true);

        Assert.That(system.ExternalScheduler, Is.Null);
        Assert.That(system.IsExternallyScheduled, Is.False);
        Assert.That(system.IsScheduled, Is.True);
        Assert.That(scheduler.Scheduled, Is.EqualTo(new IGraphicsRendererBase[] { system }));
        Assert.That(scheduler.Removed, Is.Empty);
    }

    [Test]
    public void Update_ExternalSchedulerSuppressesInternalRendererScheduler()
    {
        var internalScheduler = new FakeScheduler();
        var externalScheduler = new FakeScheduler();
        var stage = ToComputeStage.Create(input: new FakeRenderer());
        var system = new ComputeSystemSpectral()
            .SetInternalScheduler(internalScheduler);

        system.Update(externalScheduler: externalScheduler, computeStages: new[] { stage }, enabled: true);

        Assert.That(system.ExternalScheduler, Is.SameAs(externalScheduler));
        Assert.That(system.IsExternallyScheduled, Is.True);
        Assert.That(system.IsScheduled, Is.False);
        Assert.That(internalScheduler.Scheduled, Is.Empty);
        Assert.That(externalScheduler.Scheduled, Is.Empty);
    }

    [Test]
    public void SetEnabledFalse_RemovesInternalSchedulerRegistration()
    {
        var scheduler = new FakeScheduler();
        var stage = ToComputeStage.Create(input: new FakeRenderer());
        var system = new ComputeSystemSpectral()
            .SetInternalScheduler(scheduler);

        system.Update(computeStages: new[] { stage }, enabled: true);
        system.SetEnabled(false);

        Assert.That(system.IsScheduled, Is.False);
        Assert.That(scheduler.Scheduled, Is.EqualTo(new IGraphicsRendererBase[] { system }));
        Assert.That(scheduler.Removed, Is.EqualTo(new IGraphicsRendererBase[] { system }));
    }

    [Test]
    public void ComputeDispatchRenderer_RequiresVlAppHostInHeadlessTests()
    {
        var stage = ComputeStage.FromStructuredBufferResource(
            CreateParticleResource("RendererExecute", elementCount: 256, threadGroupSize: 64));
        stage.RegisterGeneratedShaderSource = false;
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(stage);

        var created = system.TryCreateRenderer(out var renderer, new ShaderGeneratorContext());

        Assert.That(created, Is.False);
        Assert.That(renderer, Is.Null);
    }

    [Test]
    public void DrawResult_IsDisabledWhenSystemIsDisabled()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("DisabledDrawResult"));
        var system = new ComputeSystemSpectral()
            .AppendComputeStage(stage)
            .SetEnabled(false);

        var result = system.DrawResult(new ShaderGeneratorContext());

        Assert.That(result.Enabled, Is.False);
        Assert.That(result.CanDispatch, Is.False);
        Assert.That(result.Stages, Is.Empty);
        Assert.That(result.ShaderSources, Is.Empty);
    }

    [Test]
    public void VlLifecycleOperationsForwardToStages()
    {
        var resource = CreateParticleResource("Lifecycle");
        var stage = ComputeStage.FromStructuredBufferResource(resource);
        var postGraph = new EmptyVoid(null);
        var system = new ComputeSystemSpectral()
            .AppendComputeStage("lifecycle", stage)
            .SetPreGraphRenderer(postGraph)
            .WriteAttributesComputeStage()
            .AddDispatcherProvider()
            .BuildComputeGraph()
            .ProcessMainResource(resource);

        Assert.That(stage.WriteAttributes, Is.True);
        Assert.That(stage.PreGraphRenderer, Is.SameAs(postGraph));
        Assert.That(stage.ComputeGraph, Is.Not.Null);
        Assert.That(system.GetTicket(), Is.EqualTo(stage.GetTicket()));
        Assert.That(system.GetResources().Select(r => r.Resource), Does.Contain("Lifecycle"));
    }

    [Test]
    public void PublicComputeSystem_WrapsSingleStageUpdate()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource("Public"));
        var system = new ComputeSystem();

        system.Update(stage, enabled: true);

        Assert.That(system.Stages, Is.EqualTo(new[] { stage }));
        Assert.That(system.Enabled, Is.True);
        Assert.That(system.Resources.Single().Resource, Is.EqualTo("Public"));
    }

    [Test]
    public void Update_AcceptsComputeStageProviders()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("ProviderA"));
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("ProviderB"));
        var system = new ComputeSystemSpectral();

        system.Update(computeStages: new IComputeStageProvider[] { first, null, second }, enabled: true);

        Assert.That(system.Stages, Is.EqualTo(new[] { first, second }));
        Assert.That(system.AppendedStages, Has.Count.EqualTo(2));
        Assert.That(system.Resources.Select(resource => resource.Resource), Is.EquivalentTo(new[] { "ProviderA", "ProviderB" }));
    }

    [Test]
    public void ComputeStageGroup_FiltersProvidersAndExposesChildStages()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("GroupA"));
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("GroupB"));

        var group = ComputeStageGroup.Create(
            computeStageProviders: new IComputeStageProvider[] { first, null, second });

        Assert.That(group.Stages, Is.EqualTo(new[] { first, second }));
        Assert.That(group.EnabledStages, Is.EqualTo(new[] { first, second }));
        Assert.That(group.GetComputeStage(), Is.SameAs(group));
        Assert.That(group.GetResources().Select(resource => resource.Resource), Is.EquivalentTo(new[] { "GroupA", "GroupB" }));
    }

    [Test]
    public void ComputeStageGroup_ForwardsVlStageOperations()
    {
        var resource = CreateParticleResource("GroupResource");
        var stage = ComputeStage.FromStructuredBufferResource(resource).SetWriteAttributes(false);
        var postGraph = new EmptyVoid(null);
        var group = ComputeStageGroup.Create(computeStageProviders: new[] { stage });

        group
            .SetPreGraphRenderer(postGraph)
            .SetWriteAttributes(true)
            .AddDispatchProvider()
            .BuildComputeGraph()
            .ProcessMainResource(resource);

        Assert.That(stage.PreGraphRenderer, Is.SameAs(postGraph));
        Assert.That(stage.WriteAttributes, Is.True);
        Assert.That(stage.ComputeGraph, Is.Not.Null);
        Assert.That(group.GetName(), Is.EqualTo("Group (ComputeStage)"));
        Assert.That(group.GetResource(), Is.EqualTo(resource.GetComputeResource()));
        Assert.That(group.GetTicket(), Is.EqualTo(stage.GetTicket()));
        Assert.That(group.GetResources().Select(computeResource => computeResource.Resource), Does.Contain("GroupResource"));
    }

    [Test]
    public void ComputeStageGroup_ProcessMainResourceUsesGroupResourceForChildren()
    {
        var groupResource = CreateParticleResource("GroupMain");
        var externalMain = CreateParticleResource("ExternalMain");
        var child = new ComputeStage(null);
        var group = ComputeStageGroup.Create(
            resourceProvider: groupResource,
            computeStageProviders: new[] { child });

        group.ProcessMainResource(externalMain);

        Assert.That(group.ResourceProvider, Is.SameAs(groupResource));
        Assert.That(group.GetResource(), Is.EqualTo(groupResource.GetComputeResource()));
        Assert.That(child.ResourceProvider, Is.SameAs(groupResource));
        Assert.That(child.Resource.Resource, Is.EqualTo("GroupMain"));
        Assert.That(child.DispatchInfo, Is.SameAs(groupResource.GetDispatchInfo()));
        Assert.That(group.GetResources().Select(resource => resource.Resource), Does.Contain("GroupMain"));
        Assert.That(group.GetResources().Select(resource => resource.Resource), Does.Not.Contain("ExternalMain"));
    }

    [Test]
    public void ComputeStageGroup_DisposeClearsGroupAndChildStageState()
    {
        var resource = CreateParticleResource("DisposableGroup");
        var child = ComputeStage.FromStructuredBufferResource(resource);
        var group = ComputeStageGroup.Create(
            resourceProvider: resource,
            computeStageProviders: new[] { child });

        group.Dispose();

        Assert.That(group.Stages, Is.Empty);
        Assert.That(group.StageProviders, Is.Empty);
        Assert.That(group.GetResources(), Is.Empty);
        Assert.That(group.GetResource(), Is.Null);
        Assert.That(child.GetResource(), Is.Null);
        Assert.That(resource.GetBufferInput(), Is.Not.Null);
    }

    [Test]
    public void ComputeSystem_DrawsAllEnabledStagesInsideGroup()
    {
        var first = ComputeStage.FromStructuredBufferResource(CreateParticleResource("DrawGroupA"));
        first.RegisterGeneratedShaderSource = false;
        var second = ComputeStage.FromStructuredBufferResource(CreateParticleResource("DrawGroupB"));
        second.RegisterGeneratedShaderSource = false;
        var disabled = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource("DrawGroupDisabled"))
            .SetEnabled(false);
        disabled.RegisterGeneratedShaderSource = false;

        var group = ComputeStageGroup.Create(computeStageProviders: new IComputeStageProvider[] { first, second, disabled });
        var system = new ComputeSystemSpectral().AppendComputeStage(group);

        var shaderSources = system.GenerateShaderSources(new ShaderGeneratorContext(), null);

        Assert.That(shaderSources, Has.Count.EqualTo(2));
        Assert.That(first.ShaderCode, Does.Contain("RWStructuredBuffer<DrawGroupA>"));
        Assert.That(second.ShaderCode, Does.Contain("RWStructuredBuffer<DrawGroupB>"));
        Assert.That(disabled.ShaderCode, Is.Null);

        group.SetEnabled(false);
        Assert.That(system.GenerateShaderSources(new ShaderGeneratorContext(), null), Is.Empty);
    }

    [Test]
    public void ComputeStageGroup_DrawStageWithRenderContextFlattensChildStages()
    {
        var first = ComputeStage.FromStructuredBufferResource(
            CreateParticleResource("DrawGroupRenderA", elementCount: 256, threadGroupSize: 64));
        first.RegisterGeneratedShaderSource = false;
        var second = ComputeStage.FromStructuredBufferResource(
            CreateParticleResource("DrawGroupRenderB", elementCount: 128, threadGroupSize: 64));
        second.RegisterGeneratedShaderSource = false;
        var group = ComputeStageGroup.Create(computeStageProviders: new[] { first, second });

        var executionResults = group.DrawStage(
            (RenderDrawContext)null,
            new ShaderGeneratorContext());

        Assert.That(executionResults, Has.Count.EqualTo(2));
        Assert.That(executionResults.Select(result => result.Command.Stage), Is.EqualTo(new[] { first, second }));
        Assert.That(executionResults.All(result => result.IterationIndexSetApplied), Is.True);
        Assert.That(executionResults.All(result => !result.Dispatched), Is.True);
    }

    [Test]
    public void ComputeStageGroup_DrawStageWithRenderContextDrawsRendererComputeStage()
    {
        var renderer = new FakeRenderer();
        var rendererStage = ToComputeStage.Create(input: renderer);
        var group = ComputeStageGroup.Create(computeStageProviders: new[] { rendererStage });
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executionResults = group.DrawStage(renderDrawContext, new ShaderGeneratorContext());

        Assert.That(executionResults, Is.Empty);
        Assert.That(renderer.DrawCount, Is.EqualTo(1));
        Assert.That(renderer.LastContext, Is.SameAs(renderDrawContext));
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

    private static ComputeStage CreateTextureAttributeStage(string name, IAttribute attribute)
    {
        var graph = new Group(null, Array.Empty<AbstractShaderNode>(), name);
        graph.AddProperty(GlobalAttributeHandler.ComputeSystemAttributeProperty, attribute);
        return new ComputeStage(null, name: name)
            .Update(computeGraph: graph)
            .SetPreGraphRenderer(graph);
    }

    private sealed class ReactionDiffusionSeedFormulaNode : ShaderNode<Vector2>
    {
        private readonly TextureAttribute<Vector2> _attribute;

        public ReactionDiffusionSeedFormulaNode(
            NodeContext nodeContext,
            TextureAttribute<Vector2> attribute)
            : base(nodeContext, "reactionDiffusionSeed")
        {
            _attribute = attribute;
            SetInputs(Array.Empty<AbstractShaderNode>());
        }

        protected override string SourceTemplate()
        {
            var indexId = _attribute.Index?.ID;
            if (string.IsNullOrWhiteSpace(indexId))
                return GenerateDefaultSource();

            const string shaderCode = @"
float2 reactionSeedIndex_${resultName} = float2(${index});
float reactionSeedMask_${resultName} =
    step(400.0, reactionSeedIndex_${resultName}.x)
    * step(reactionSeedIndex_${resultName}.x, 624.0)
    * step(reactionSeedIndex_${resultName}.y, 624.0)
    * step(400.0, reactionSeedIndex_${resultName}.y);
${resultType} ${resultName} = float2(reactionSeedMask_${resultName}, reactionSeedMask_${resultName});";

            return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
            {
                { "index", indexId }
            });
        }
    }

    private sealed class ReactionDiffusionFormulaNode : ShaderNode<Vector2>
    {
        private readonly TextureInput _readTexture;
        private readonly TextureAttribute<Vector2> _attribute;
        private readonly ShaderNode<Vector2> _laplace;

        public ReactionDiffusionFormulaNode(
            NodeContext nodeContext,
            TextureInput readTexture,
            TextureAttribute<Vector2> attribute,
            ShaderNode<Vector2> laplace)
            : base(nodeContext, "reactionDiffusionStep")
        {
            _readTexture = readTexture;
            _attribute = attribute;
            _laplace = laplace;
            SetInputs(new AbstractShaderNode[] { readTexture, laplace });
        }

        protected override string SourceTemplate()
        {
            var textureId = _readTexture.TextureID();
            var indexId = _attribute.Index?.ID;
            if (string.IsNullOrWhiteSpace(textureId) || string.IsNullOrWhiteSpace(indexId))
                return GenerateDefaultSource();

            const string shaderCode = @"
${resultType} reactionCenter_${resultName} = ${textureName}[${index}];
${resultType} reactionLaplacian_${resultName} = ${laplace};
float reactionRate_${resultName} = reactionCenter_${resultName}.x * reactionCenter_${resultName}.y * reactionCenter_${resultName}.y;
float reactionNextA_${resultName} = reactionCenter_${resultName}.x + (1.0 * reactionLaplacian_${resultName}.x - reactionRate_${resultName} + 0.062 * (1.0 - reactionCenter_${resultName}.x));
float reactionNextB_${resultName} = reactionCenter_${resultName}.y + (0.5 * reactionLaplacian_${resultName}.y + reactionRate_${resultName} - (0.062 + 0.062) * reactionCenter_${resultName}.y);
${resultType} ${resultName} = float2(reactionNextA_${resultName}, reactionNextB_${resultName});";

            return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
            {
                { "textureName", textureId },
                { "index", indexId },
                { "laplace", _laplace.ID }
            });
        }
    }

    private static Int3 GetThreadGroupCount(IComputeEffectDispatcher dispatcher)
    {
        return (Int3)dispatcher
            .GetType()
            .GetProperty("ThreadGroupCount")
            .GetValue(dispatcher);
    }

    private sealed class FakeComputeAttribute<T> : IAttribute
    {
        private FakeComputeAttribute(string name, AttributeType attributeType = AttributeType.StructuredBuffer)
        {
            Name = name;
            AttributeType = attributeType;
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

        public static FakeComputeAttribute<T> Create(string name, AttributeType attributeType)
        {
            return new FakeComputeAttribute<T>(name, attributeType);
        }
    }

    private sealed class TestDispatchInfo : IDispatchInfo
    {
        private readonly IDispatchInfo _inner;
        private readonly IGraphicsRendererBase _preRenderCommand;
        private readonly IComputeEffectDispatcher _dispatcher;

        public TestDispatchInfo(
            IDispatchInfo inner,
            IGraphicsRendererBase preRenderCommand,
            IComputeEffectDispatcher dispatcher)
        {
            _inner = inner;
            _preRenderCommand = preRenderCommand;
            _dispatcher = dispatcher;
        }

        public ComputeDispatchRequest Request => _inner.Request;

        public ComputeDispatchSize ThreadGroupSize => _inner.ThreadGroupSize;

        public ComputeDispatchSize DispatchGroups => _inner.DispatchGroups;

        public ComputeDispatchValidationResult Validation => _inner.Validation;

        public System.Collections.Generic.IReadOnlyList<ComputeDispatchDiagnostic> Diagnostics => _inner.Diagnostics;

        public bool IsValid => _inner.IsValid;

        public ComputeDispatchSize GetCount()
        {
            return _inner.GetCount();
        }

        public ComputeDispatchInfoSplit Split()
        {
            var split = _inner.Split();
            return split with
            {
                PreRenderCommand = _preRenderCommand,
                Dispatcher = _dispatcher
            };
        }
    }

    private sealed class FakeComputeEffectDispatcher : IComputeEffectDispatcher
    {
        public int DispatchCount { get; private set; }

        public RenderDrawContext LastContext { get; private set; }

        public void UpdateParameters(ParameterCollection parameters, Int3 threadGroupCount)
        {
        }

        public void Dispatch(RenderDrawContext context)
        {
            DispatchCount++;
            LastContext = context;
        }
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

    private sealed class RecordingChangeGraph : IComputeChangeGraph
    {
        private readonly System.Collections.Generic.List<AbstractShaderNode> _nodes = new();

        public System.Collections.Generic.IReadOnlyList<AbstractShaderNode> Nodes => _nodes;

        public void ChangeGraph(AbstractShaderNode node)
        {
            _nodes.Add(node);
        }
    }

    private sealed class FakeScheduler : IComputeRendererScheduler
    {
        private readonly System.Collections.Generic.List<IGraphicsRendererBase> _scheduled = new();
        private readonly System.Collections.Generic.List<IGraphicsRendererBase> _removed = new();

        public System.Collections.Generic.IReadOnlyList<IGraphicsRendererBase> Scheduled => _scheduled;

        public System.Collections.Generic.IReadOnlyList<IGraphicsRendererBase> Removed => _removed;

        public void Schedule(IGraphicsRendererBase renderer)
        {
            _scheduled.Add(renderer);
        }

        public void Remove(IGraphicsRendererBase renderer)
        {
            _removed.Add(renderer);
        }
    }
}
