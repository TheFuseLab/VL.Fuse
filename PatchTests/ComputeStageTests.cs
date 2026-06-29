using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Rendering.ComputeEffect;
using VL.Stride.Shaders.ShaderFX;
using FragmentAttribute = VL.Core.Import.FragmentAttribute;
using FragmentSelection = VL.Core.Import.FragmentSelection;
using ImportTypeAttribute = VL.Core.Import.ImportTypeAttribute;
using ProcessNodeAttribute = VL.Core.Import.ProcessNodeAttribute;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeStageTests
{
    [Test]
    public void ComputeReplacementClasses_ArePreparedAsExplicitProcessNodes()
    {
        var importedTypes = typeof(ComputeStage).Assembly
            .GetCustomAttributes<ImportTypeAttribute>()
            .Select(attribute => attribute.Type)
            .ToArray();

        Assert.That(typeof(ComputeStage).Assembly.GetName().Name, Is.EqualTo("Fuse.Compute"));
        Assert.That(importedTypes, Is.EquivalentTo(new[]
        {
            typeof(ComputeStage),
            typeof(Buffer1DDispatchInfo),
            typeof(ComputeGraph),
            typeof(ComputeGraph1D),
            typeof(ComputeGraph2D),
            typeof(ComputeGraph3D),
            typeof(ComputeStageGroupSpectral),
            typeof(ComputeStageGroup),
            typeof(ComputeSystemSpectral),
            typeof(ComputeSystem),
            typeof(StructuredBufferResourceDispatchInfo),
            typeof(StructuredBufferResource),
            typeof(TextureDispatchInfo),
            typeof(TextureResource),
            typeof(ToComputeStage),
            typeof(Average<,>),
            typeof(Laplace2DKarlSims<>)
        }));
        Assert.That(importedTypes, Does.Not.Contain(typeof(TextureNeighborhoodNode<,>)));

        var expectedNodes = new[]
        {
            new ProcessNodeExpectation(
                typeof(Buffer1DDispatchInfo),
                "Buffer1DDispatchInfo",
                new[]
                {
                    nameof(Buffer1DDispatchInfo.SetElementCount),
                    nameof(Buffer1DDispatchInfo.SetThreadGroupSize),
                    nameof(Buffer1DDispatchInfo.Split),
                    nameof(Buffer1DDispatchInfo.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeGraph),
                "ComputeGraph",
                new[]
                {
                    nameof(ComputeGraph.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeGraph1D),
                "ComputeGraph1D",
                new[]
                {
                    nameof(ComputeGraph1D.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeGraph2D),
                "ComputeGraph2D",
                new[]
                {
                    nameof(ComputeGraph2D.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeGraph3D),
                "ComputeGraph3D",
                new[]
                {
                    nameof(ComputeGraph3D.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeStageGroup),
                "Group (ComputeStage)",
                new[]
                {
                    nameof(ComputeStageGroup.SetEnabled),
                    nameof(ComputeStageGroup.SetIterationCount),
                    nameof(ComputeStageGroup.SetResourceProvider),
                    nameof(ComputeStageGroup.SetWriteAttributes),
                    nameof(ComputeStageGroup.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeStageGroupSpectral),
                "Group (ComputeStage,Spectral)",
                new[]
                {
                    nameof(ComputeStageGroupSpectral.AddDispatchProvider),
                    nameof(ComputeStageGroupSpectral.BuildComputeGraph),
                    nameof(ComputeStageGroupSpectral.HandleAttributes),
                    nameof(ComputeStageGroupSpectral.ProcessMainResource),
                    nameof(ComputeStageGroupSpectral.SetEnabled),
                    nameof(ComputeStageGroupSpectral.SetIterationCount),
                    nameof(ComputeStageGroupSpectral.SetPreGraphRenderer),
                    nameof(ComputeStageGroupSpectral.SetResourceProvider),
                    nameof(ComputeStageGroupSpectral.SetWriteAttributes),
                    nameof(ComputeStageGroupSpectral.Update)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeSystemSpectral),
                "ComputeSystem (Spectral Advanced)",
                new[]
                {
                    nameof(ComputeSystemSpectral.AddDispatcherProvider),
                    nameof(ComputeSystemSpectral.AppendComputeStage),
                    nameof(ComputeSystemSpectral.BuildComputeGraph),
                    nameof(ComputeSystemSpectral.FinishResources),
                    nameof(ComputeSystemSpectral.GetResources),
                    nameof(ComputeSystemSpectral.HandleAttributes),
                    nameof(ComputeSystemSpectral.PrepareResources),
                    nameof(ComputeSystemSpectral.ProcessMainResource),
                    nameof(ComputeSystemSpectral.SetEnabled),
                    nameof(ComputeSystemSpectral.SetInput),
                    nameof(ComputeSystemSpectral.SetPreGraphRenderer),
                    nameof(ComputeSystemSpectral.Update),
                    nameof(ComputeSystemSpectral.WriteAttributesComputeStage)
                }),
            new ProcessNodeExpectation(
                typeof(ComputeSystem),
                "ComputeSystem",
                new[]
                {
                    nameof(ComputeSystem.AppendComputeStage),
                    nameof(ComputeSystem.BuildComputeGraph),
                    nameof(ComputeSystem.Clear),
                    nameof(ComputeSystem.ProcessMainResource),
                    nameof(ComputeSystem.SetEnabled),
                    nameof(ComputeSystem.SetInput),
                    nameof(ComputeSystem.Update)
                }),
            new ProcessNodeExpectation(
                typeof(StructuredBufferResource),
                "StructuredBufferResource",
                new[]
                {
                    nameof(StructuredBufferResource.BindAttributes),
                    nameof(StructuredBufferResource.BindComputeStage),
                    nameof(StructuredBufferResource.Finish),
                    nameof(StructuredBufferResource.FinishAttributeMap),
                    nameof(StructuredBufferResource.HandleAttribute),
                    nameof(StructuredBufferResource.Prepare),
                    nameof(StructuredBufferResource.Reset),
                    nameof(StructuredBufferResource.SetAttributeMap),
                    nameof(StructuredBufferResource.SetDispatchGroupSize),
                    nameof(StructuredBufferResource.SetElementCount),
                    nameof(StructuredBufferResource.SetName),
                    nameof(StructuredBufferResource.SetStructSize),
                    nameof(StructuredBufferResource.SyncAttributes),
                    nameof(StructuredBufferResource.Update),
                    nameof(StructuredBufferResource.UpdateBuffer),
                    nameof(StructuredBufferResource.UpdateStruct)
                }),
            new ProcessNodeExpectation(
                typeof(StructuredBufferResourceDispatchInfo),
                "StructuredBufferResourceDispatchInfo",
                new[]
                {
                    nameof(StructuredBufferResourceDispatchInfo.SetResource),
                    nameof(StructuredBufferResourceDispatchInfo.SetThreadGroupSize),
                    nameof(StructuredBufferResourceDispatchInfo.Split),
                    nameof(StructuredBufferResourceDispatchInfo.Update)
                }),
            new ProcessNodeExpectation(
                typeof(TextureDispatchInfo),
                "TextureDispatchInfo",
                new[]
                {
                    nameof(TextureDispatchInfo.SetDimension),
                    nameof(TextureDispatchInfo.SetThreadGroupSize),
                    nameof(TextureDispatchInfo.Split),
                    nameof(TextureDispatchInfo.Update)
                }),
            new ProcessNodeExpectation(
                typeof(TextureResource),
                "TextureResource",
                new[]
                {
                    nameof(TextureResource.BindAttributes),
                    nameof(TextureResource.BindComputeStage),
                    nameof(TextureResource.CreateRead),
                    nameof(TextureResource.CreateWrite),
                    nameof(TextureResource.Finish),
                    nameof(TextureResource.FinishAttributeMap),
                    nameof(TextureResource.HandleAttribute),
                    nameof(TextureResource.Prepare),
                    nameof(TextureResource.Reset),
                    nameof(TextureResource.SetDimension),
                    nameof(TextureResource.SetName),
                    nameof(TextureResource.SetThreadGroupSize),
                    nameof(TextureResource.SwapTextures),
                    nameof(TextureResource.SyncAttributes),
                    nameof(TextureResource.UpdateTextures)
                }),
            new ProcessNodeExpectation(
                typeof(ToComputeStage),
                "ToComputeStage",
                new[]
                {
                    nameof(ToComputeStage.SetEnabled),
                    nameof(ToComputeStage.Update)
                })
        };

        foreach (var expectedNode in expectedNodes)
            AssertExplicitProcessNode(expectedNode, importedTypes);

        AssertShaderNodeProcessSurface(typeof(Average<,>), "Average", "Fuse.Compute.Texture");
        AssertShaderNodeProcessSurface(typeof(Laplace2DKarlSims<>), "Laplace2D (8 Karl Sims)", "Fuse.Compute.Texture");
        AssertFragmentHasOutPins<StructuredBufferResource>(
            nameof(StructuredBufferResource.BindComputeStage),
            nameof(StructuredBufferResource.BindComputeStage),
            "read",
            "write");
        AssertFragmentHasOutPins<TextureResource>(
            nameof(TextureResource.BindComputeStage),
            nameof(TextureResource.BindComputeStage),
            "read",
            "write");
        AssertDispatchSplitPins<Buffer1DDispatchInfo>();
        AssertDispatchSplitPins<StructuredBufferResourceDispatchInfo>();
        AssertDispatchSplitPins<TextureDispatchInfo>();
        AssertFragmentHasOutPins<ComputeSystemSpectral>(
            nameof(ComputeSystemSpectral.Update),
            "ComputeSystem (Spectral Advanced).Update",
            "globalAttributes",
            "hasChanged");
        AssertFragmentHasOutPins<ComputeSystem>(
            nameof(ComputeSystem.Update),
            nameof(ComputeSystem.Update),
            "hasChanged");
    }

    [Test]
    public void ComputeStage_IsPreparedAsExplicitProcessNode()
    {
        var processNode = typeof(ComputeStage).GetCustomAttribute<ProcessNodeAttribute>();
        Assert.That(processNode, Is.Not.Null);
        Assert.That(processNode.Name, Is.EqualTo("ComputeStage"));
        Assert.That(processNode.Category, Is.EqualTo("Fuse.Compute"));
        Assert.That(processNode.FragmentSelection, Is.EqualTo(FragmentSelection.Explicit));

        var importedTypes = typeof(ComputeStage).Assembly
            .GetCustomAttributes<ImportTypeAttribute>()
            .Select(attribute => attribute.Type)
            .ToArray();
        Assert.That(typeof(ComputeStage).Assembly.GetName().Name, Is.EqualTo("Fuse.Compute"));
        Assert.That(importedTypes, Does.Contain(typeof(ComputeStage)));

        var fragmentMethods = typeof(ComputeStage)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<FragmentAttribute>() != null)
            .Select(method => method.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.That(fragmentMethods, Is.EquivalentTo(new[]
        {
            nameof(ComputeStage.SetComputeGraph),
            nameof(ComputeStage.SetDispatchInfo),
            nameof(ComputeStage.SetEnabled),
            nameof(ComputeStage.SetIndexProvider),
            nameof(ComputeStage.SetIterationCount),
            nameof(ComputeStage.SetPreGraphRenderer),
            nameof(ComputeStage.SetResourceProvider),
            nameof(ComputeStage.SetWriteAttributes),
            nameof(ComputeStage.Update)
        }));
        Assert.That(fragmentMethods, Does.Not.Contain(nameof(ComputeStage.DrawResult)));
        Assert.That(fragmentMethods, Does.Not.Contain(nameof(ComputeStage.GenerateShaderSource)));

        var fragmentProperties = typeof(ComputeStage)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(property => property.GetCustomAttribute<FragmentAttribute>() != null)
            .Select(property => property.Name)
            .ToArray();
        Assert.That(fragmentProperties, Is.EqualTo(new[] { nameof(ComputeStage.Output) }));

        var fragmentConstructors = typeof(ComputeStage)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Where(constructor => constructor.GetCustomAttribute<FragmentAttribute>() != null)
            .ToArray();
        Assert.That(fragmentConstructors, Has.Length.EqualTo(1));
    }

    private static void AssertExplicitProcessNode(
        ProcessNodeExpectation expectedNode,
        IReadOnlyCollection<Type> importedTypes)
    {
        Assert.That(importedTypes, Does.Contain(expectedNode.Type));

        var processNode = expectedNode.Type.GetCustomAttribute<ProcessNodeAttribute>();
        Assert.That(processNode, Is.Not.Null, expectedNode.Type.Name);
        Assert.That(processNode.Name, Is.EqualTo(expectedNode.Name), expectedNode.Type.Name);
        Assert.That(processNode.Category, Is.EqualTo("Fuse.Compute"), expectedNode.Type.Name);
        Assert.That(processNode.FragmentSelection, Is.EqualTo(FragmentSelection.Explicit), expectedNode.Type.Name);

        var fragmentMethods = expectedNode.Type
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<FragmentAttribute>() != null)
            .Select(method => method.Name)
            .Distinct()
            .OrderBy(name => name)
            .ToArray();
        Assert.That(fragmentMethods, Is.EquivalentTo(expectedNode.Methods), expectedNode.Type.Name);

        var fragmentProperties = expectedNode.Type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(property => property.GetCustomAttribute<FragmentAttribute>() != null)
            .Select(property => property.Name)
            .ToArray();
        Assert.That(fragmentProperties, Is.EqualTo(new[] { "Output" }), expectedNode.Type.Name);

        var fragmentConstructors = expectedNode.Type
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Where(constructor => constructor.GetCustomAttribute<FragmentAttribute>() != null)
            .ToArray();
        Assert.That(fragmentConstructors, Has.Length.EqualTo(1), expectedNode.Type.Name);
    }

    private static void AssertShaderNodeProcessSurface(
        Type type,
        string name,
        string category)
    {
        var processNode = type.GetCustomAttribute<ProcessNodeAttribute>();
        Assert.That(processNode, Is.Not.Null, type.Name);
        Assert.That(processNode.Name, Is.EqualTo(name), type.Name);
        Assert.That(processNode.Category, Is.EqualTo(category), type.Name);
        Assert.That(processNode.FragmentSelection, Is.EqualTo(FragmentSelection.Explicit), type.Name);

        var fragmentMembers = type
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(member => member.GetCustomAttribute<FragmentAttribute>() != null)
            .Select(member => member.Name)
            .ToArray();
        Assert.That(fragmentMembers, Is.Empty, type.Name);
    }

    private static void AssertFragmentHasOutPins<T>(
        string methodName,
        string assertionName,
        params string[] outParameterNames)
    {
        var fragment = typeof(T)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Single(method =>
                method.Name == methodName
                && method.GetCustomAttribute<FragmentAttribute>() != null
                && outParameterNames.All(name => method.GetParameters().Any(parameter =>
                    parameter.IsOut && parameter.Name == name)));

        var actualOutNames = fragment
            .GetParameters()
            .Where(parameter => parameter.IsOut)
            .Select(parameter => parameter.Name)
            .ToArray();
        Assert.That(actualOutNames, Is.EquivalentTo(outParameterNames), assertionName);
    }

    private static void AssertDispatchSplitPins<T>()
    {
        AssertFragmentHasOutPins<T>(
            "Split",
            $"{typeof(T).Name}.Split",
            "preRenderCommand",
            "dispatcher",
            "threadGroupSize",
            "skipOutsideRange");
    }

    private sealed record ProcessNodeExpectation(
        Type Type,
        string Name,
        IReadOnlyCollection<string> Methods);

    [Test]
    public void FromStructuredBufferResource_BindsResourceDispatchAndGraph()
    {
        var resource = CreateParticleResource();

        var stage = ComputeStage.FromStructuredBufferResource(resource, name: "ParticleUpdate");

        Assert.That(stage.GetComputeStage(), Is.SameAs(stage));
        Assert.That(((IComputeStage)stage).GetName(), Is.EqualTo("ParticleUpdate"));
        Assert.That(stage.Name, Is.EqualTo("ParticleUpdate"));
        Assert.That(stage.Enabled, Is.True);
        Assert.That(stage.IterationCount, Is.EqualTo(1));
        Assert.That(stage.WriteAttributes, Is.True);
        Assert.That(stage.ResourceProvider, Is.SameAs(resource));
        Assert.That(stage.Resource, Is.EqualTo(resource.GetComputeResource()));
        Assert.That(stage.DispatchInfo, Is.SameAs(resource.GetDispatchInfo()));
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph1D>());
        Assert.That(stage.ComputeGraph, Is.SameAs(stage.ComputeGraphNode.GetComputeGraph()));
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(stage.StructuredBufferBindings.WriteGroup));
        Assert.That(stage.StructuredBufferBindings.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void SplitDispatchInfo_UsesBoundResourceDispatchInfo()
    {
        var resource = CreateParticleResource(elementCount: 1_000, threadGroupSize: 128);
        var stage = ComputeStage.FromStructuredBufferResource(resource);

        var split = stage.SplitDispatchInfo();

        Assert.That(split.IsValid, Is.True);
        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(8, 1, 1)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(128, 1, 1)));
        Assert.That(split.Diagnostics, Is.Empty);
        Assert.That(stage.GetDispatcher(), Is.Not.Null);
        Assert.That(stage.GetDispatcher().GetDispatchInfo(), Is.SameAs(resource.GetDispatchInfo()));
    }

    [Test]
    public void SplitDispatchInfo_ReportsInvalidWhenNoDispatchInfoIsBound()
    {
        var stage = new ComputeStage(null);

        var split = stage.SplitDispatchInfo();

        Assert.That(split.IsValid, Is.False);
        Assert.That(split.DispatchGroups, Is.EqualTo(ComputeDispatchSize.Zero));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(ComputeDispatchSize.Zero));
    }

    [Test]
    public void GenerateShaderSource_ProducesHeadlessComputeShader()
    {
        var resource = CreateParticleResource();
        var stage = ComputeStage.FromStructuredBufferResource(resource);
        stage.RegisterGeneratedShaderSource = false;

        var shaderSource = stage.GenerateShaderSource(new ShaderGeneratorContext(), null);

        Assert.That(shaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(stage.ShaderCode, Does.Contain("shader Shader_"));
        Assert.That(stage.ShaderCode, Does.Contain("override void Compute()"));
        Assert.That(stage.ShaderCode, Does.Contain("RWStructuredBuffer<Particle>"));
        Assert.That(stage.ShaderCode, Does.Contain("streams.DispatchThreadId.x"));
        Assert.That(stage.LastShaderGenerator, Is.Not.Null);
        Assert.That(stage.LastDiagnosticContext, Is.Not.Null);
        Assert.That(
            stage.LastDiagnosticContext.Timings.Select(timing => timing.Name),
            Does.Contain("AddShaderSourceSkipped"));
    }

    [Test]
    public void GenerateShaderSource_ReturnsNullWhenStageIsDisabled()
    {
        var stage = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource())
            .SetEnabled(false);

        var shaderSource = stage.GenerateShaderSource(new ShaderGeneratorContext(), null);

        Assert.That(shaderSource, Is.Null);
        Assert.That(stage.ShaderCode, Is.Null);
        Assert.That(stage.LastDiagnosticContext, Is.Null);
    }

    [Test]
    public void GenerateShaderSource_ThrowsWhenGraphIsMissing()
    {
        var stage = new ComputeStage(null);

        Assert.Throws<InvalidOperationException>(() => stage.GenerateShaderSource(new ShaderGeneratorContext(), null));
    }

    [Test]
    public void BindStructuredBufferResource_UsesCustomIndexProvider()
    {
        var stage = ComputeStage.FromStructuredBufferResource(
            CreateParticleResource(),
            indexProvider: new VertexIdIndexProvider());

        AbstractShaderNode.ResetBuildSourceCodeCache();
        var source = stage.ComputeGraph.BuildSourceCode();

        Assert.That(stage.IndexProvider, Is.TypeOf<VertexIdIndexProvider>());
        Assert.That(source, Does.Contain("streams.VertexId"));
        Assert.That(source, Does.Contain(".x"));
    }

    [Test]
    public void Setters_NormalizeStageState()
    {
        var stage = new ComputeStage(null)
            .SetName("")
            .SetIterationCount(-12)
            .SetWriteAttributes(false)
            .SetIndexProvider(null);

        Assert.That(stage, Is.TypeOf<ComputeStage>());
        Assert.That(stage.GetName(), Is.EqualTo("ComputeStage"));
        Assert.That(stage.GetEnabled(), Is.True);
        Assert.That(stage.GetTicket(), Is.GreaterThanOrEqualTo(0));

        Assert.That(stage.Name, Is.EqualTo("ComputeStage"));
        Assert.That(stage.IterationCount, Is.EqualTo(1));
        Assert.That(stage.WriteAttributes, Is.False);
        Assert.That(stage.IndexProvider, Is.TypeOf<DispatchIdIndexProvider>());
    }

    [Test]
    public void Update_StoresGraphShaderNodeNameAndBumpsTicket()
    {
        var stage = new ComputeStage(null);
        var graph = new EmptyVoid(null);
        var initialTicket = stage.GetTicket();

        stage.Update(
            computeGraph: graph,
            shaderNode: graph,
            forceRecompile: true,
            name: "ParticleUpdate");

        Assert.That(stage.Name, Is.EqualTo("ParticleUpdate"));
        Assert.That(stage.GetComputeGraph(), Is.SameAs(graph));
        Assert.That(stage.GetShaderNode(), Is.SameAs(graph));
        Assert.That(stage.GetTicket(), Is.GreaterThan(initialTicket));
    }

    [Test]
    public void DispatcherResourceAndShaderAccessorsFollowVlOperationNames()
    {
        var resource = CreateParticleResource();
        var stage = ComputeStage.FromStructuredBufferResource(resource);
        stage.RegisterGeneratedShaderSource = false;

        stage.DrawStage(new ShaderGeneratorContext());

        Assert.That(stage.GetComputeStage(), Is.SameAs(stage));
        Assert.That(((IComputeStage)stage).GetResource(), Is.EqualTo(resource.GetComputeResource()));
        Assert.That(stage.GetResource(), Is.EqualTo(resource.GetComputeResource()));
        Assert.That(stage.GetResources(), Does.Contain(resource.GetComputeResource()));
        Assert.That(stage.GetDispatcherProvider(), Is.Not.Null);
        Assert.That(stage.GetDispatcher(), Is.SameAs(stage.GetDispatcherProvider().GetDispatcher()));
        Assert.That(stage.GetDispatcher().GetDispatchInfo(), Is.SameAs(resource.GetDispatchInfo()));
        Assert.That(stage.GetShaderCode(), Does.Contain("RWStructuredBuffer<Particle>"));
        Assert.That(stage.GetLastError(), Is.Null);
    }

    [Test]
    public void Dispose_ClearsStageStateWithoutDisposingResource()
    {
        var resource = CreateParticleResource();
        var stage = ComputeStage.FromStructuredBufferResource(resource);
        var initialTicket = stage.GetTicket();

        stage.Dispose();

        Assert.That(stage.GetTicket(), Is.GreaterThan(initialTicket));
        Assert.That(stage.GetResource(), Is.Null);
        Assert.That(stage.GetDispatcherProvider(), Is.Null);
        Assert.That(stage.GetComputeGraph(), Is.Null);
        Assert.That(stage.GetShaderNode(), Is.Null);
        Assert.That(stage.GetShaderCode(), Is.Null);
        Assert.That(stage.GetLastError(), Is.Null);
        Assert.That(resource.GetBufferInput(), Is.Not.Null);
    }

    [Test]
    public void GetResources_MergesInputResourcesAndStageResourceByTarget()
    {
        var resource = CreateParticleResource();
        var stage = ComputeStage.FromStructuredBufferResource(resource);
        var previousSameTarget = new ComputeResource(
            AttributeType.StructuredBuffer,
            resource.GetComputeResource().Resource,
            new Int3(1, 1, 1));
        var otherResource = new ComputeResource(
            AttributeType.StructuredBuffer,
            "Other",
            new Int3(2, 1, 1));

        var merged = stage.GetResources(new[] { previousSameTarget, otherResource }).ToArray();

        Assert.That(merged, Has.Length.EqualTo(2));
        Assert.That(merged.Single(item => item.Resource == "Other"), Is.SameAs(otherResource));
        Assert.That(merged.Single(item => item.Resource == resource.GetComputeResource().Resource), Is.SameAs(stage.Resource));
    }

    [Test]
    public void GetResources_KeepsUnnamedResourcesWithDifferentSizesSeparate()
    {
        var first = new ComputeResource(
            AttributeType.Texture,
            null,
            new Int3(64, 64, 1));
        var second = new ComputeResource(
            AttributeType.Texture,
            null,
            new Int3(64, 64, 64));

        var merged = ComputeResource.MergeResources(new[] { first }, new[] { second }).ToArray();

        Assert.That(merged, Has.Length.EqualTo(2));
        Assert.That(merged.Select(resource => resource.Size), Is.EquivalentTo(new[]
        {
            new Int3(64, 64, 1),
            new Int3(64, 64, 64)
        }));
    }

    [Test]
    public void DrawStage_WithRenderContextRunsPatchDispatchSteps()
    {
        var stage = ComputeStage
            .FromStructuredBufferResource(CreateParticleResource(elementCount: 256, threadGroupSize: 64))
            .SetIterationCount(2);
        stage.RegisterGeneratedShaderSource = false;

        var executionResults = stage.DrawStage(
            (RenderDrawContext)null,
            new ShaderGeneratorContext());

        Assert.That(executionResults, Has.Count.EqualTo(2));
        Assert.That(executionResults.Select(result => result.Command.Stage), Is.EqualTo(new[] { stage, stage }));
        Assert.That(executionResults.Select(result => result.Command.IterationIndex), Is.EqualTo(new[] { 0, 1 }));
        Assert.That(executionResults.All(result => result.IterationIndexSetApplied), Is.True);
        Assert.That(executionResults.All(result => !result.PreRenderCommandExecuted), Is.True);
        Assert.That(executionResults.All(result => !result.TextureResourceUpdated), Is.True);
        Assert.That(executionResults.All(result => !result.Dispatched), Is.True);
        Assert.That(executionResults[0].Steps[2].Reason, Is.EqualTo("Stage has no texture resource."));
        Assert.That(executionResults[0].Steps[3].Reason, Is.EqualTo("RenderDrawContext is not assigned."));
    }

    [Test]
    public void DrawStage_WithRenderContextExecutesPreRenderBeforeDispatch()
    {
        var preRenderCommand = new FakePreRenderCommand();
        var dispatcher = new FakeComputeEffectDispatcher();
        var dispatchInfo = new TestDispatchInfo(
            Buffer1DDispatchInfo.Create(128, 64),
            preRenderCommand,
            dispatcher);
        var stage = new ComputeStage(null, new EmptyVoid(null), dispatchInfo)
            .SetIterationCount(1);
        var renderDrawContext = (RenderDrawContext)RuntimeHelpers.GetUninitializedObject(typeof(RenderDrawContext));

        var executionResults = stage.DrawStage(renderDrawContext, new ShaderGeneratorContext());

        Assert.That(executionResults, Has.Count.EqualTo(1));
        Assert.That(executionResults.Single().IterationIndexSetApplied, Is.True);
        Assert.That(executionResults.Single().PreRenderCommandExecuted, Is.True);
        Assert.That(executionResults.Single().TextureResourceUpdated, Is.False);
        Assert.That(executionResults.Single().Dispatched, Is.True);
        Assert.That(executionResults.Single().PostDispatchGraphExecuted, Is.False);
        Assert.That(preRenderCommand.DrawCount, Is.EqualTo(1));
        Assert.That(preRenderCommand.LastContext, Is.SameAs(renderDrawContext));
        Assert.That(dispatcher.DispatchCount, Is.EqualTo(1));
        Assert.That(dispatcher.LastContext, Is.SameAs(renderDrawContext));
        Assert.That(
            executionResults.Single().Steps.Select(step => step.Kind),
            Is.EqualTo(new[]
            {
                ComputeDispatchExecutionStepKind.IterationIndexSet,
                ComputeDispatchExecutionStepKind.PreRenderCommand,
                ComputeDispatchExecutionStepKind.TextureResourceUpdate,
                ComputeDispatchExecutionStepKind.Dispatch,
                ComputeDispatchExecutionStepKind.PostDispatchGraph
            }));
        Assert.That(
            executionResults.Single().Steps.Last().Reason,
            Is.EqualTo("Compute graph has no post-dispatch texture swaps."));
    }

    [Test]
    public void SetDispatcherProvider_UsesDispatcherDispatchInfoForSplit()
    {
        var stage = new ComputeStage(null)
            .SetComputeGraph(new EmptyVoid(null));
        var dispatchInfo = Buffer1DDispatchInfo.Create(1_024, 128);
        var dispatcherProvider = new DirectDispatcher(dispatchInfo, new VertexIdIndexProvider());

        stage.SetDispatcherProvider(dispatcherProvider);

        var split = stage.SplitDispatchInfo();

        Assert.That(stage.GetDispatcherProvider(), Is.SameAs(dispatcherProvider));
        Assert.That(stage.GetDispatcher(), Is.SameAs(dispatcherProvider));
        Assert.That(stage.DispatchInfo, Is.SameAs(dispatchInfo));
        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(8, 1, 1)));
        dispatcherProvider.Index(null, out var readIndex, out var writeIndex);
        Assert.That(readIndex, Is.SameAs(writeIndex));
        Assert.That(readIndex.BuildSourceCode(), Does.Contain("streams.VertexId"));
    }

    [Test]
    public void SetPreGraphRenderer_IsUsedWhenBindingResource()
    {
        var resource = CreateParticleResource();
        var postGraph = new EmptyVoid(null);
        var stage = new ComputeStage(null)
            .SetPreGraphRenderer(postGraph)
            .BindStructuredBufferResource(resource);

        Assert.That(stage.StructuredBufferBindings.PostGraphRenderer, Is.SameAs(postGraph));
        Assert.That(stage.StructuredBufferBindings.WriteGroup.Ins.Last(), Is.SameAs(postGraph));
    }

    [Test]
    public void ProcessMainResource_UsesMainResourceOnlyWhenStageResourceIsMissing()
    {
        var stageResource = CreateParticleResource();
        var mainResource = CreateParticleResource();
        mainResource.SetName("MainParticle");
        var stage = ComputeStage.FromStructuredBufferResource(stageResource);
        var originalBindings = stage.StructuredBufferBindings;

        stage.ProcessMainResource(mainResource);

        Assert.That(stage.ResourceProvider, Is.SameAs(stageResource));
        Assert.That(stage.Resource, Is.EqualTo(stageResource.GetComputeResource()));
        Assert.That(stage.DispatchInfo, Is.SameAs(stageResource.GetDispatchInfo()));
        Assert.That(stage.StructuredBufferBindings, Is.SameAs(originalBindings));

        var emptyStage = new ComputeStage(null)
            .ProcessMainResource(mainResource);

        Assert.That(emptyStage.ResourceProvider, Is.SameAs(mainResource));
        Assert.That(emptyStage.Resource, Is.EqualTo(mainResource.GetComputeResource()));
        Assert.That(emptyStage.DispatchInfo, Is.SameAs(mainResource.GetDispatchInfo()));
        Assert.That(emptyStage.ComputeGraph, Is.Not.Null);
    }

    [Test]
    public void BuildComputeGraph_RebindsStructuredBufferResourceWithCurrentPreGraphRenderer()
    {
        var resource = CreateParticleResource();
        var firstPostGraph = new EmptyVoid(null);
        var secondPostGraph = new EmptyVoid(null);
        var stage = ComputeStage
            .FromStructuredBufferResource(resource)
            .SetPreGraphRenderer(firstPostGraph)
            .BuildComputeGraph();
        var firstBindings = stage.StructuredBufferBindings;

        stage
            .SetPreGraphRenderer(secondPostGraph)
            .BuildComputeGraph();

        Assert.That(stage.StructuredBufferBindings, Is.Not.SameAs(firstBindings));
        Assert.That(stage.StructuredBufferBindings.PostGraphRenderer, Is.SameAs(secondPostGraph));
        Assert.That(stage.StructuredBufferBindings.WriteGroup.Ins.Last(), Is.SameAs(secondPostGraph));
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph1D>());
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(stage.StructuredBufferBindings.WriteGroup));
    }

    [Test]
    public void BuildComputeGraph_RespectsWriteAttributesFlag()
    {
        var resource = CreateParticleResource();
        var stage = ComputeStage
            .FromStructuredBufferResource(resource)
            .SetWriteAttributes(false)
            .BuildComputeGraph();

        Assert.That(stage.WriteAttributes, Is.False);
        Assert.That(stage.StructuredBufferBindings.WriteAttributes, Is.False);
        Assert.That(stage.StructuredBufferBindings.BufferWrite, Is.Null);
        Assert.That(stage.StructuredBufferBindings.WriteValue, Is.Null);
        Assert.That(stage.StructuredBufferBindings.Items.All(item => item.WriteCall == null), Is.True);
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph1D>());
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(stage.StructuredBufferBindings.WriteGroup));
    }

    [Test]
    public void BuildComputeGraph_CallsChangeGraphAndBumpsTicket()
    {
        var stage = ComputeStage.FromStructuredBufferResource(CreateParticleResource());
        var listener = new RecordingChangeGraph();
        var initialTicket = stage.GetTicket();

        stage
            .AddChangeGraph(listener)
            .BuildComputeGraph();

        Assert.That(stage.GetTicket(), Is.GreaterThan(initialTicket));
        Assert.That(listener.Nodes, Is.EqualTo(new[] { stage.ComputeGraph }));

        stage.RemoveChangeGraph(listener);
        stage.SetComputeGraph(new EmptyVoid(null));

        Assert.That(listener.Nodes, Has.Count.EqualTo(1));
    }

    [Test]
    public void BuildComputeGraph_BindsTextureResourceThroughReadWriteGroups()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var postGraphRenderer = new EmptyVoid(null);
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource)
            .SetPreGraphRenderer(postGraphRenderer);

        stage.BuildComputeGraph();

        Assert.That(stage.DispatchInfo, Is.SameAs(resource.GetDispatchInfo()));
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph2D>());
        Assert.That(stage.ComputeGraph, Is.SameAs(stage.ComputeGraphNode.GetComputeGraph()));
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(resource.WriteGroup));
        Assert.That(stage.ShaderNode, Is.SameAs(stage.ComputeGraph));
        Assert.That(resource.ReadGroup.Ins, Does.Contain(color.ReadCall));
        Assert.That(resource.WriteGroup.Ins, Does.Contain(color.WriteCall));
        Assert.That(resource.WriteGroup.Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(color));
        Assert.That(resource.WriteGroup.Ins.Last(), Is.SameAs(postGraphRenderer));
    }

    [Test]
    public void GenerateShaderSource_ForTextureResourceStageEmitsComputeTextureShaderCode()
    {
        var (stage, resource, color) = CreateTextureComputeStage();

        var shaderSource = stage.GenerateShaderSource(new ShaderGeneratorContext(), null);

        Assert.That(shaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph2D>());
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(resource.WriteGroup));
        Assert.That(stage.LastShaderGenerator, Is.Not.Null);
        Assert.That(stage.ShaderCode, Does.Contain("shader Shader_"));
        Assert.That(stage.ShaderCode, Does.Contain("override void Compute()"));
        Assert.That(stage.ShaderCode, Does.Contain("RWTexture2D<float>"));
        Assert.That(stage.ShaderCode, Does.Contain("Texture2D<float>"));
        Assert.That(stage.ShaderCode, Does.Contain(color.ReadCall.ID));
        Assert.That(stage.ShaderCode, Does.Contain(resource.TextureAInputs["Color"].ID));
        Assert.That(stage.ShaderCode, Does.Contain(resource.TextureBInputs["Color"].ID));
        Assert.That(stage.ShaderCode, Does.Contain("Textures_Color_A_"));
        Assert.That(stage.ShaderCode, Does.Contain("Textures_Color_B_"));
        var textureDeclarationNames = Regex.Matches(
                stage.ShaderCode,
                @"stage\s+(?:RW)?Texture\S+\s+(?<name>\w+);")
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        Assert.That(textureDeclarationNames, Is.Unique);
        Assert.That(stage.ShaderCode, Does.Contain("streams.DispatchThreadId.xy"));
        Assert.That(stage.ShaderCode, Does.Contain($"{resource.TextureBInputs["Color"].ID}["));
        Assert.That(stage.ShaderCode, Does.Contain($"] = {color.ReadCall.ID};"));
        Assert.That(
            ShaderNodesUtil.ValidateGeneratedShaderCode(stage.ShaderCode, out var validationReason),
            Is.True,
            validationReason);
    }

    [Test]
    public void GenerateShaderSource_ForTextureResourceStageCompilesWithStandaloneEffectCompiler()
    {
        var (stage, _, _) = CreateTextureComputeStage();
        stage.GenerateShaderSource(new ShaderGeneratorContext(), null);

        var (result, errors) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(stage.LastShaderGenerator);

        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        Assert.That(result.Bytecode, Is.Not.Null);
        Assert.That(result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Bytecode.Reflection, Is.Not.Null);
    }

    [Test]
    public void GenerateShaderSource_ForTextureResourceStageUsesComputeGraph2DAndCompiles()
    {
        var (stage, _, _) = CreateTextureComputeStage();
        stage.RegisterGeneratedShaderSource = false;

        var shaderSource = stage.GenerateShaderSource(new ShaderGeneratorContext(), null);
        var (result, errors) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(stage.LastShaderGenerator);

        Assert.That(stage.ComputeGraphNode, Is.TypeOf<ComputeGraph2D>());
        Assert.That(shaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        Assert.That(stage.ShaderCode, Does.Contain("streams.DispatchThreadId.xy >= uint2(16, 8)"));
        Assert.That(stage.ShaderCode, Does.Contain("return;"));
        Assert.That(stage.ShaderCode, Does.Contain("RWTexture2D<float>"));
        Assert.That(result.Bytecode, Is.Not.Null);
        Assert.That(result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(16, 1, 1, typeof(ComputeGraph1D), "RWTexture1D<float>", "Texture1D<float>", "streams.DispatchThreadId.x >= 16")]
    [TestCase(16, 8, 1, typeof(ComputeGraph2D), "RWTexture2D<float>", "Texture2D<float>", "streams.DispatchThreadId.xy >= uint2(16, 8)")]
    [TestCase(16, 8, 4, typeof(ComputeGraph3D), "RWTexture3D<float>", "Texture3D<float>", "streams.DispatchThreadId >= uint3(16, 8, 4)")]
    public void GenerateShaderSource_ForTextureResourceStagesCompileAcrossDimensions(
        int x,
        int y,
        int z,
        Type graphType,
        string rwTextureType,
        string textureType,
        string skipGuard)
    {
        var (stage, resource, color) = CreateTextureComputeStage(new Int3(x, y, z));
        stage.RegisterGeneratedShaderSource = false;

        var shaderSource = stage.GenerateShaderSource(new ShaderGeneratorContext(), null);
        var (result, errors) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(stage.LastShaderGenerator);

        Assert.That(stage.ComputeGraphNode, Is.TypeOf(graphType));
        Assert.That(stage.ComputeGraph.Ins, Does.Contain(resource.WriteGroup));
        Assert.That(shaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        Assert.That(stage.ShaderCode, Does.Contain(rwTextureType));
        Assert.That(stage.ShaderCode, Does.Contain(textureType));
        Assert.That(stage.ShaderCode, Does.Contain(skipGuard));
        Assert.That(stage.ShaderCode, Does.Contain(color.ReadCall.ID));
        Assert.That(result.Bytecode, Is.Not.Null);
        Assert.That(result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
    }

    private static StructuredBufferResource CreateParticleResource(
        long elementCount = 128,
        long threadGroupSize = 64)
    {
        var resource = StructuredBufferResource.Create("Particle", elementCount, threadGroupSize);
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

    private static (ComputeStage Stage, TextureResource Resource, TextureAttribute<float> Color) CreateTextureComputeStage()
    {
        return CreateTextureComputeStage(new Int3(16, 8, 1));
    }

    private static (ComputeStage Stage, TextureResource Resource, TextureAttribute<float> Color) CreateTextureComputeStage(
        Int3 size)
    {
        var resource = TextureResource.Create("Textures", size);
        var color = new TextureAttribute<float>(null, "Color", theIsDoubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color);
        var stage = new ComputeStage(null)
            .SetResourceProvider(resource);
        stage.RegisterGeneratedShaderSource = false;
        stage.BuildComputeGraph();

        return (stage, resource, color);
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

    private sealed class RecordingChangeGraph : IComputeChangeGraph
    {
        private readonly System.Collections.Generic.List<AbstractShaderNode> _nodes = new();

        public System.Collections.Generic.IReadOnlyList<AbstractShaderNode> Nodes => _nodes;

        public void ChangeGraph(AbstractShaderNode node)
        {
            _nodes.Add(node);
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

    private sealed class FakePreRenderCommand : IGraphicsRendererBase
    {
        public int DrawCount { get; private set; }

        public RenderDrawContext LastContext { get; private set; }

        public void Draw(RenderDrawContext context)
        {
            DrawCount++;
            LastContext = context;
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
}
