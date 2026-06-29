using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using Fuse.ShaderFX;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Core.Shaders.Utility;
using Stride.Rendering.Materials;
using Stride.Shaders;
using Stride.Shaders.Parser;
using Stride.Shaders.Parser.Mixins;
using VL.Stride.Shaders.ShaderFX;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class StructuredBufferResourceBindingTests
{
    [Test]
    public void CreateReadWriteBindings_AssignsReadInputAndWriteCalls()
    {
        var resource = CreateParticleResource();
        var map = resource.GetAttributeMap();
        var life = (FakeComputeAttribute<float>)map.AttributeSet["Life"];
        var position = (FakeComputeAttribute<Vector3>)map.AttributeSet["Position"];
        var index = CreateDispatchThreadIdXIndex();

        var bindings = resource.CreateReadWriteBindings(null, index);

        Assert.That(bindings.Items, Has.Count.EqualTo(2));
        Assert.That(bindings.BufferInput, Is.Not.Null);
        Assert.That(resource.GetBufferInput(), Is.SameAs(bindings.BufferInput));
        Assert.That(bindings.BufferRead, Is.Not.Null);
        Assert.That(bindings.WriteValue, Is.Not.Null);
        Assert.That(bindings.BufferWrite, Is.Not.Null);
        Assert.That(life.ReadCall, Is.TypeOf<Fuse.GetMember<GpuStruct, float>>());
        Assert.That(position.ReadCall, Is.TypeOf<Fuse.GetMember<GpuStruct, Vector3>>());
        Assert.That(life.InputAbstract, Is.SameAs(life.ReadCall));
        Assert.That(position.InputAbstract, Is.SameAs(position.ReadCall));
        Assert.That(life.WriteCall, Is.TypeOf<SetMember<GpuStruct>>());
        Assert.That(position.WriteCall, Is.TypeOf<SetMember<GpuStruct>>());
    }

    [Test]
    public void CreateReadWriteBindings_SkipsPaddingAttributes()
    {
        var resource = CreateParticleResource(usePadding: true);
        var index = CreateDispatchThreadIdXIndex();

        var bindings = resource.CreateReadWriteBindings(null, index);

        Assert.That(resource.GetAttributeMap().AttributeSet.ContainsKey(PaddingAttribute.DefaultName), Is.True);
        Assert.That(bindings.Items.Select(item => item.Attribute.Name), Is.EqualTo(new[] { "Life", "Velocity" }));
    }

    [Test]
    public void CreateReadWriteBindings_CanDisableAttributeWrites()
    {
        var resource = CreateParticleResource();
        var map = resource.GetAttributeMap();
        var life = (FakeComputeAttribute<float>)map.AttributeSet["Life"];
        var position = (FakeComputeAttribute<Vector3>)map.AttributeSet["Position"];
        var index = CreateDispatchThreadIdXIndex();

        var bindings = resource.CreateReadWriteBindings(null, index, writeAttributes: false);

        Assert.That(bindings.WriteAttributes, Is.False);
        Assert.That(bindings.BufferInput, Is.Not.Null);
        Assert.That(bindings.BufferRead, Is.Not.Null);
        Assert.That(bindings.WriteValue, Is.Null);
        Assert.That(bindings.BufferWrite, Is.Null);
        Assert.That(bindings.Items, Has.Count.EqualTo(2));
        Assert.That(bindings.Items.All(item => item.WriteCall == null), Is.True);
        Assert.That(bindings.WriteGroup.Ins, Is.EqualTo(new[] { life.ReadCall, position.ReadCall }));
        Assert.That(life.InputAbstract, Is.SameAs(life.ReadCall));
        Assert.That(position.InputAbstract, Is.SameAs(position.ReadCall));
        Assert.That(life.WriteCall, Is.Null);
        Assert.That(position.WriteCall, Is.Null);
    }

    [Test]
    public void CreateReadWriteBindings_SyncsAttributeInstances()
    {
        var resource = StructuredBufferResource.Create("Particle");
        var canonical = FakeComputeAttribute<float>.Create("Life");
        var instance = FakeComputeAttribute<float>.Create("Life");
        var map = new AttributeMap(AttributeType.StructuredBuffer, attribute => 4);
        map.HandleAttribute(canonical);
        map.HandleAttribute(instance);
        resource.SetAttributeMap(map, getGpuType: _ => "float", getSizeInBytes: _ => 4);
        var index = CreateDispatchThreadIdXIndex();

        resource.CreateReadWriteBindings(null, index);

        Assert.That(instance.ReadCall, Is.SameAs(canonical.ReadCall));
        Assert.That(instance.InputAbstract, Is.SameAs(canonical.InputAbstract));
        Assert.That(instance.WriteCall, Is.SameAs(canonical.WriteCall));
    }

    [Test]
    public void CreateReadWriteBindings_GeneratesMinimalComputeShaderSnapshot()
    {
        var resource = CreateParticleResource();
        var index = CreateDispatchThreadIdXIndex();
        var bindings = resource.CreateReadWriteBindings(null, index);

        AbstractShaderNode.ResetBuildSourceCodeCache();
        var snapshot = BuildShaderSnapshot(bindings.WriteGroup);

        Assert.That(snapshot, Does.Contain("RWStructuredBuffer<Particle>"));
        Assert.That(snapshot, Does.Contain("struct Particle"));
        Assert.That(snapshot, Does.Contain("float Life;"));
        Assert.That(snapshot, Does.Contain("float3 Position;"));
        Assert.That(snapshot, Does.Contain(".Life"));
        Assert.That(snapshot, Does.Contain(".Position"));
        Assert.That(snapshot, Does.Contain("DynamicBufferInput"));
        Assert.That(snapshot, Does.Contain("streams.DispatchThreadId.x"));
    }

    [Test]
    public void BindComputeStage_UsesDispatchThreadIdXByDefault()
    {
        var resource = CreateParticleResource();

        var bindings = resource.BindComputeStage(null);

        AbstractShaderNode.ResetBuildSourceCodeCache();
        var snapshot = BuildShaderSnapshot(bindings.WriteGroup);

        Assert.That(bindings.ReadGroup, Is.Not.Null);
        Assert.That(bindings.WriteGroup, Is.Not.Null);
        Assert.That(snapshot, Does.Contain("streams.DispatchThreadId.x"));
        Assert.That(snapshot, Does.Contain("RWStructuredBuffer<Particle>"));
    }

    [Test]
    public void BindAttributes_UsesPatchNamedResourceBindingPath()
    {
        var resource = CreateParticleResource();

        var bindings = resource.BindAttributes(null);

        AbstractShaderNode.ResetBuildSourceCodeCache();
        var snapshot = BuildShaderSnapshot(bindings.WriteGroup);

        Assert.That(bindings.ReadGroup, Is.Not.Null);
        Assert.That(bindings.WriteGroup, Is.Not.Null);
        Assert.That(bindings.Items.Select(item => item.Attribute.Name), Is.EqualTo(new[] { "Life", "Position" }));
        Assert.That(snapshot, Does.Contain("RWStructuredBuffer<Particle>"));
        Assert.That(snapshot, Does.Contain("streams.DispatchThreadId.x"));
    }

    [Test]
    public void BindComputeStage_AppendsPostGraphRendererToWriteGroup()
    {
        var resource = CreateParticleResource();
        var postGraphRenderer = new EmptyVoid(null);

        var bindings = resource.BindComputeStage(null, postGraphRenderer: postGraphRenderer);

        Assert.That(bindings.PostGraphRenderer, Is.SameAs(postGraphRenderer));
        Assert.That(bindings.WriteGroup.Ins.Last(), Is.SameAs(postGraphRenderer));
    }

    [Test]
    public void CreateReadWriteBindings_GeneratesToComputeFxShaderWithoutRegistration()
    {
        var (computeFx, shaderSource) = GenerateHeadlessComputeShader();

        Assert.That(shaderSource, Is.InstanceOf<ShaderClassSource>());
        Assert.That(computeFx.ShaderCode, Does.Contain("shader Shader_"));
        Assert.That(computeFx.ShaderCode, Does.Contain("override void Compute()"));
        Assert.That(computeFx.ShaderCode, Does.Contain("RWStructuredBuffer<Particle>"));
        Assert.That(computeFx.ShaderCode, Does.Contain("struct Particle"));
        Assert.That(computeFx.ShaderCode, Does.Contain(".Life"));
        Assert.That(computeFx.ShaderCode, Does.Contain(".Position"));
        Assert.That(computeFx.LastDiagnosticContext, Is.Not.Null);
        Assert.That(
            computeFx.LastDiagnosticContext.Timings.Select(timing => timing.Name),
            Does.Contain("AddShaderSourceSkipped"));
        Assert.That(
            ShaderNodesUtil.ValidateGeneratedShaderCode(computeFx.ShaderCode, out var validationReason),
            Is.True,
            validationReason);
    }

    [Test]
    public void CreateReadWriteBindings_LoadsGeneratedShaderWithStandaloneShaderLoader()
    {
        var (computeFx, _) = GenerateHeadlessComputeShader();
        var diagnostics = computeFx.LastDiagnosticContext;
        var sourceManager = GetStandaloneShaderSourceManager();
        sourceManager.AddShaderSource(diagnostics.ShaderName, computeFx.ShaderCode, diagnostics.SourcePath);

        var loader = new ShaderLoader(sourceManager);
        var log = new LoggerResult();
        var loadedShader = loader.LoadClassSource(
            new ShaderClassSource(diagnostics.ShaderName),
            Array.Empty<Stride.Core.Shaders.Parser.ShaderMacro>(),
            log,
            autoGenericInstances: false);

        Assert.That(log.HasErrors, Is.False, log.ToString());
        Assert.That(loadedShader, Is.Not.Null);
        Assert.That(loadedShader.Type, Is.Not.Null);
        Assert.That(loadedShader.Type.Name.Text, Is.EqualTo(diagnostics.ShaderName));
        Assert.That(loadedShader.SourcePath, Is.EqualTo(diagnostics.SourcePath));
    }

    [Test]
    public void CreateReadWriteBindings_CompilesGeneratedShaderWithStandaloneEffectCompiler()
    {
        var (computeFx, _) = GenerateHeadlessComputeShader();

        var (result, errors) = ShaderCompilerTestUtil
            .CompileGeneratedShaderWithStandaloneEffectCompiler(computeFx);

        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
        Assert.That(result.Bytecode, Is.Not.Null);
        Assert.That(result.Bytecode.Stages, Is.Not.Null.And.Not.Empty);
        Assert.That(result.Bytecode.Reflection, Is.Not.Null);
    }

    private static StructuredBufferResource CreateParticleResource(bool usePadding = false)
    {
        var resource = StructuredBufferResource.Create("Particle", elementCount: 128);
        var map = usePadding
            ? new AttributeMap(
                AttributeType.StructuredBuffer,
                attribute => attribute is IAttributeLayout layout ? layout.SizeInBytes : attribute.Name == "Velocity" ? 8 : 4)
            : new AttributeMap(
                AttributeType.StructuredBuffer,
                attribute => attribute.Name == "Position" ? 12 : 4);

        map.HandleAttribute(FakeComputeAttribute<float>.Create("Life"));
        map.HandleAttribute(usePadding
            ? FakeComputeAttribute<Vector2>.Create("Velocity")
            : FakeComputeAttribute<Vector3>.Create("Position"));

        if (usePadding)
            map.ApplyPadding();

        resource.SetAttributeMap(
            map,
            getGpuType: attribute => attribute is IAttributeLayout layout
                ? layout.GpuType
                : attribute.Name == "Velocity" ? "float2"
                : attribute.Name == "Position" ? "float3"
                : "float",
            getSizeInBytes: attribute => attribute is IAttributeLayout layout
                ? layout.SizeInBytes
                : attribute.Name == "Velocity" ? 8
                : attribute.Name == "Position" ? 12
                : 4);

        return resource;
    }

    private static string BuildShaderSnapshot(AbstractShaderNode node)
    {
        var builder = new StringBuilder();

        foreach (var declaration in node.DeclarationList())
            builder.AppendLine(declaration.GetDeclaration(theIsComputeShader: true));

        foreach (var gpuStruct in node.StructList())
            builder.AppendLine(gpuStruct);

        builder.AppendLine(node.BuildSourceCode());
        return builder.ToString();
    }

    private static (ToComputeFx<GpuVoid> ComputeFx, ShaderSource ShaderSource) GenerateHeadlessComputeShader()
    {
        var resource = CreateParticleResource();
        var index = CreateDispatchThreadIdXIndex();
        var bindings = resource.CreateReadWriteBindings(null, index);
        var computeFx = new ToComputeFx<GpuVoid>(bindings.WriteGroup)
        {
            RegisterGeneratedShaderSource = false
        };

        var shaderSource = computeFx.GenerateShaderSource(new ShaderGeneratorContext(), null);
        return (computeFx, shaderSource);
    }

    private static ShaderNode<int> CreateDispatchThreadIdXIndex()
    {
        return new DispatchThreadIdX(null);
    }

    private static ShaderSourceManager GetStandaloneShaderSourceManager()
    {
        var method = typeof(ShaderNodesUtil).GetMethod(
            "TryGetStandaloneShaderSourceManager",
            BindingFlags.Static | BindingFlags.NonPublic);
        var args = new object[] { null };

        Assert.That(method, Is.Not.Null);
        Assert.That((bool)method.Invoke(null, args), Is.True);
        return (ShaderSourceManager)args[0];
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
