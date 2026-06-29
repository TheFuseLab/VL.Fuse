using System.Linq;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class TextureResourceStateTests
{
    [Test]
    public void Create_UsesVlSlots()
    {
        var resource = TextureResource.Create("Volume", new Int3(32, 16, 8));

        Assert.That(resource.Name, Is.EqualTo("Volume"));
        Assert.That(resource.AttributeType, Is.EqualTo(AttributeType.Texture));
        Assert.That(resource.GetSize(), Is.EqualTo(new Int3(32, 16, 8)));
        Assert.That(resource.GetDimension(), Is.EqualTo(new Int3(32, 16, 8)));
        Assert.That(resource.GetAttributeMap().AttributeType, Is.EqualTo(AttributeType.Texture));
        Assert.That(resource.TextureAInputs, Is.Empty);
        Assert.That(resource.TextureBInputs, Is.Empty);
        Assert.That(resource.GetName(), Is.EqualTo("Volume"));
        Assert.That(resource.GetAttributeType(), Is.EqualTo(AttributeType.Texture));
        Assert.That(resource.GetResource(), Is.EqualTo(resource.GetComputeResource()));
        Assert.That(resource.GetComputeResource().Resource, Is.EqualTo("Volume"));
        Assert.That(resource.GetComputeResource().Size, Is.EqualTo(new Int3(32, 16, 8)));
    }

    [Test]
    public void TextureDispatchInfo_SplitsDimensionIntoDispatchGroups()
    {
        var dispatchInfo = TextureDispatchInfo.Create(
            new Int3(17, 9, 3),
            new ComputeDispatchSize(8, 4, 2));

        var split = dispatchInfo.Split();

        Assert.That(dispatchInfo.GetCount(), Is.EqualTo(new ComputeDispatchSize(3, 3, 2)));
        Assert.That(split.DispatchGroups, Is.EqualTo(new ComputeDispatchSize(3, 3, 2)));
        Assert.That(split.ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 2)));
        Assert.That(split.SkipOutsideRange, Is.True);
        Assert.That(split.IsValid, Is.True);
    }

    [Test]
    public void TextureDispatchInfo_SplitFragmentOutputsMatchCurrentState()
    {
        var dispatchInfo = TextureDispatchInfo.Create(
            new Int3(17, 9, 3),
            new ComputeDispatchSize(8, 4, 2));

        var result = dispatchInfo.Split(
            out var preRenderCommand,
            out var dispatcher,
            out var threadGroupSize,
            out var skipOutsideRange);

        Assert.That(result, Is.SameAs(dispatchInfo));
        Assert.That(preRenderCommand, Is.Null);
        Assert.That(dispatcher, Is.Not.Null);
        Assert.That(threadGroupSize, Is.EqualTo(new ComputeDispatchSize(8, 4, 2)));
        Assert.That(skipOutsideRange, Is.True);
    }

    [Test]
    public void SetDimension_UpdatesComputeResourceAndDispatchInfo()
    {
        var resource = TextureResource.Create("Image");

        resource
            .SetThreadGroupSize(new ComputeDispatchSize(8, 8, 1))
            .SetDimension(new Int3(31, 17, 1));

        Assert.That(resource.GetSize(), Is.EqualTo(new Int3(31, 17, 1)));
        Assert.That(resource.GetComputeResource().Size, Is.EqualTo(new Int3(31, 17, 1)));
        Assert.That(resource.GetDispatchInfo().GetCount(), Is.EqualTo(new ComputeDispatchSize(4, 3, 1)));
    }

    [Test]
    public void PatchLifecycle_PrepareHandleFinishBuildsTextureAttributeMap()
    {
        var resource = TextureResource.Create("Textures");
        var color = FakeTextureAttribute<float>.Create("Color");
        var velocity = FakeTextureAttribute<Vector2>.Create("Velocity");

        resource
            .Prepare()
            .HandleAttribute(color)
            .HandleAttribute(velocity)
            .FinishAttributeMap();

        Assert.That(resource.GetAttributeMap().AttributeSet.Keys, Is.EquivalentTo(new[] { "Color", "Velocity" }));
        Assert.That(resource.ChangedAttributes, Is.True);
        Assert.That(resource.GetTicket(), Is.EqualTo(1));
    }

    [Test]
    public void PatchLifecycle_PrepareFinishRemovesUnusedTextureAttributesAndInputs()
    {
        var resource = TextureResource.Create("Textures");
        resource.HandleAttribute(FakeTextureAttribute<float>.Create("Color"));
        resource.FinishAttributeMap();
        resource.BindAttributes();

        resource.Prepare();
        resource.FinishAttributeMap();

        Assert.That(resource.GetAttributeMap().AttributeSet, Is.Empty);
        Assert.That(resource.TextureAInputs, Is.Empty);
        Assert.That(resource.ChangedAttributes, Is.True);
        Assert.That(resource.GetTicket(), Is.EqualTo(2));
    }

    [Test]
    public void BindAttributes_AssignsReadInputAndDoubleBufferWriteInput()
    {
        var resource = TextureResource.Create("Textures");
        var index = new ShaderNode<Int3>(null, "Index", theCreateDefault: false);
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);

        resource
            .Prepare()
            .HandleAttribute(color)
            .BindAttributes(readIndex: index);

        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureBInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(color.TextureInput, Is.SameAs(resource.TextureAInputs["Color"]));
        Assert.That(color.Index, Is.SameAs(index));
        Assert.That(color.ReadCall, Is.TypeOf<ComputeTextureGet<Int3, float>>());
        Assert.That(color.WriteCall, Is.TypeOf<ComputeTextureSet<Int3, float>>());
        Assert.That(color.ReadCall.SourceCode, Does.Contain(resource.TextureAInputs["Color"].ID));
        Assert.That(color.WriteCall.SourceCode, Does.Contain(resource.TextureBInputs["Color"].ID));
    }

    [Test]
    public void BindAttributes_NamesTextureInputsByResourceAttributeAndSlot()
    {
        var resource = TextureResource.Create("Textures");
        var index = new ShaderNode<Int3>(null, "Index", theCreateDefault: false);
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);

        resource
            .Prepare()
            .HandleAttribute(color)
            .BindAttributes(readIndex: index);

        Assert.That(resource.TextureAInputs["Color"].TextureName, Is.EqualTo("Textures_Color_A"));
        Assert.That(resource.TextureBInputs["Color"].TextureName, Is.EqualTo("Textures_Color_B"));
        Assert.That(resource.TextureAInputs["Color"].ID, Does.StartWith("Textures_Color_A_"));
        Assert.That(resource.TextureBInputs["Color"].ID, Does.StartWith("Textures_Color_B_"));
        Assert.That(resource.TextureAInputs["Color"].ID, Is.Not.EqualTo(resource.TextureBInputs["Color"].ID));
    }

    [Test]
    public void BindAttributes_NamesUnnamedTextureInputsByAttributeDimensionAndSlot()
    {
        var texture2D = TextureResource.Create(size: new Int3(64, 64, 1));
        var texture3D = TextureResource.Create(size: new Int3(64, 64, 64));
        var noise2D = FakeTextureAttribute<float>.Create("NoiseData", doubleBuffered: true);
        var noise3D = FakeTextureAttribute<float>.Create("NoiseData", doubleBuffered: false);

        texture2D
            .Prepare()
            .HandleAttribute(noise2D)
            .FinishAttributeMap()
            .BindAttributes();
        texture3D
            .Prepare()
            .HandleAttribute(noise3D)
            .FinishAttributeMap()
            .BindAttributes();

        Assert.That(texture2D.TextureAInputs["NoiseData"].TextureName, Is.EqualTo("NoiseData_64x64x1_A"));
        Assert.That(texture2D.TextureBInputs["NoiseData"].TextureName, Is.EqualTo("NoiseData_64x64x1_B"));
        Assert.That(texture3D.TextureAInputs["NoiseData"].TextureName, Is.EqualTo("NoiseData_64x64x64_A"));
        Assert.That(new[]
        {
            texture2D.TextureAInputs["NoiseData"].ID,
            texture2D.TextureBInputs["NoiseData"].ID,
            texture3D.TextureAInputs["NoiseData"].ID
        }, Is.Unique);
    }

    [Test]
    public void TextureInput_WithoutExplicitNameKeepsDefaultHashName()
    {
        var input = new TextureInput(null, new TextureTypeTracker(false));

        Assert.That(input.TextureName, Is.Null);
        Assert.That(input.ID, Does.StartWith("TextureInput_"));
    }

    [Test]
    public void UpdateTextures_WithoutGraphicsDeviceBuildsInputsAndReportsStatus()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);

        resource
            .Prepare()
            .HandleAttribute(color)
            .FinishAttributeMap()
            .UpdateTextures();

        Assert.That(resource.GetTextures().Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureBInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(color.TextureInput, Is.SameAs(resource.TextureAInputs["Color"]));
        Assert.That(resource.TextureStatuses["Color:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(resource.TextureStatuses["Color:B"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(resource.LastTextureStatus, Does.Contain("Color:A=Failed:GraphicsDevice=null"));
        Assert.That(resource.GetTextureStatuses(), Is.SameAs(resource.TextureStatuses));
        Assert.That(resource.GetLastTextureStatus(), Is.EqualTo(resource.LastTextureStatus));
    }

    [Test]
    public void UpdateTextures_UsesTextureAttributeShaderNodePixelFormat()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var color = FakeTextureAttribute<float>.Create("Color");

        resource
            .Prepare()
            .HandleAttribute(color)
            .FinishAttributeMap()
            .UpdateTextures();

        Assert.That(resource.TextureStatuses["Color:A"], Does.Contain($"Format={PixelFormat.R32_Float}"));
    }

    [Test]
    public void UpdateTextures_UnsupportedUnorderedAccessFormatReportsStatusAndKeepsInputs()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var position = FakeTextureAttribute<Vector3>.Create("Position", doubleBuffered: true);

        resource
            .Prepare()
            .HandleAttribute(position)
            .FinishAttributeMap()
            .UpdateTextures();

        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Position" }));
        Assert.That(resource.TextureBInputs.Keys, Is.EqualTo(new[] { "Position" }));
        Assert.That(position.TextureInput, Is.SameAs(resource.TextureAInputs["Position"]));
        Assert.That(resource.TextureStatuses["Position:A"], Does.StartWith("Failed:Description:ArgumentException"));
        Assert.That(resource.TextureStatuses["Position:B"], Does.StartWith("Failed:Description:ArgumentException"));
        Assert.That(resource.TextureStatuses["Position:A"], Does.Contain($"{PixelFormat.R32G32B32_Float}"));
        Assert.That(resource.TextureStatuses["Position:A"], Does.Contain("UnorderedAccess=True"));
        Assert.That(resource.LastTextureStatus, Does.Contain("Position:A=Failed:Description:ArgumentException"));
    }

    [Test]
    public void UpdateTextures_DimensionAboveD3D11LimitReportsStatusAndKeepsInputs()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 16, 2_049));
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);

        resource
            .Prepare()
            .HandleAttribute(color)
            .FinishAttributeMap()
            .UpdateTextures();

        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureBInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(color.TextureInput, Is.SameAs(resource.TextureAInputs["Color"]));
        Assert.That(resource.TextureStatuses["Color:A"], Does.StartWith("Failed:Description:ArgumentOutOfRangeException"));
        Assert.That(resource.TextureStatuses["Color:B"], Does.StartWith("Failed:Description:ArgumentOutOfRangeException"));
        Assert.That(resource.TextureStatuses["Color:A"], Does.Contain("Texture dimension Z"));
        Assert.That(resource.LastTextureStatus, Does.Contain("Color:A=Failed:Description:ArgumentOutOfRangeException"));
    }

    [Test]
    public void UpdateTextures_ShaderResourceOnlyAllowsUnsupportedUnorderedAccessFormat()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var position = FakeTextureAttribute<Vector3>.Create("Position");

        resource
            .Prepare()
            .HandleAttribute(position)
            .FinishAttributeMap()
            .UpdateTextures(unorderedAccess: false);

        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Position" }));
        Assert.That(resource.TextureStatuses["Position:A"], Does.StartWith("Failed:GraphicsDevice=null"));
        Assert.That(resource.TextureStatuses["Position:A"], Does.Contain($"{PixelFormat.R32G32B32_Float}"));
        Assert.That(resource.TextureStatuses["Position:A"], Does.Contain("Flags=ShaderResource"));
    }

    [Test]
    public void ReadCallAndWriteCall_CreateTypedTextureShaderOperations()
    {
        var resource = TextureResource.Create("Textures");
        var index = new ShaderNode<Int3>(null, "Index", theCreateDefault: false);
        var color = FakeTextureAttribute<Vector2>.Create("Velocity", doubleBuffered: true);
        resource
            .Prepare()
            .HandleAttribute(color)
            .UpdateTextures();
        var subContextFactory = new NodeSubContextFactory(null);

        var readCall = resource.ReadCall(color, subContextFactory, index);
        var writeCall = resource.WriteCall(color, subContextFactory, index);

        Assert.That(readCall, Is.TypeOf<ComputeTextureGet<Int3, Vector2>>());
        Assert.That(writeCall, Is.TypeOf<ComputeTextureSet<Int3, Vector2>>());
        Assert.That(color.ReadCall, Is.SameAs(readCall));
        Assert.That(color.WriteCall, Is.SameAs(writeCall));
        Assert.That(readCall.SourceCode, Does.Contain(resource.TextureAInputs["Velocity"].ID));
        Assert.That(writeCall.SourceCode, Does.Contain(resource.TextureBInputs["Velocity"].ID));
    }

    [Test]
    public void WriteCall_ForSingleBufferedAttributeUsesTextureAInput()
    {
        var resource = TextureResource.Create("Textures");
        var index = new ShaderNode<Int3>(null, "Index", theCreateDefault: false);
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: false);

        resource
            .Prepare()
            .HandleAttribute(color)
            .UpdateTextures()
            .BindAttributes(readIndex: index, writeIndex: index);

        Assert.That(resource.TextureAInputs.Keys, Is.EqualTo(new[] { "Color" }));
        Assert.That(resource.TextureBInputs, Is.Empty);
        Assert.That(color.ReadCall, Is.TypeOf<ComputeTextureGet<Int3, float>>());
        Assert.That(color.WriteCall, Is.TypeOf<ComputeTextureSet<Int3, float>>());
        Assert.That(color.ReadCall.SourceCode, Does.Contain(resource.TextureAInputs["Color"].ID));
        Assert.That(color.WriteCall.SourceCode, Does.Contain(resource.TextureAInputs["Color"].ID));
    }

    [Test]
    public void CreateRead_GroupsAttributeReadCalls()
    {
        var resource = TextureResource.Create("Textures");
        var index = new ShaderNode<Int3>(null, "Index", theCreateDefault: false);
        var color = FakeTextureAttribute<float>.Create("Color");
        var velocity = FakeTextureAttribute<Vector2>.Create("Velocity");
        resource
            .Prepare()
            .HandleAttribute(color)
            .HandleAttribute(velocity)
            .BindAttributes(readIndex: index, writeIndex: index);

        var readGroup = resource.CreateRead();

        Assert.That(readGroup, Is.SameAs(resource.ReadGroup));
        Assert.That(readGroup.Ins, Does.Contain(color.ReadCall));
        Assert.That(readGroup.Ins, Does.Contain(velocity.ReadCall));
        Assert.That(readGroup.Ins.Count, Is.EqualTo(2));
    }

    [Test]
    public void CreateWrite_GroupsWrittenAttributesTextureSwapAndPostGraphRenderer()
    {
        var resource = TextureResource.Create("Textures");
        var index = new ShaderNode<Int3>(null, "Index", theCreateDefault: false);
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);
        var velocity = FakeTextureAttribute<Vector2>.Create("Velocity", doubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        var postGraphRenderer = new EmptyVoid(null);
        resource
            .Prepare()
            .HandleAttribute(color)
            .HandleAttribute(velocity)
            .BindAttributes(readIndex: index, writeIndex: index);

        var writeGroup = resource.CreateWrite(postGraphRenderer: postGraphRenderer);

        Assert.That(writeGroup, Is.SameAs(resource.WriteGroup));
        Assert.That(writeGroup.Ins, Does.Contain(color.WriteCall));
        Assert.That(writeGroup.Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(color));
        Assert.That(writeGroup.Ins, Does.Contain(postGraphRenderer));
        Assert.That(writeGroup.Ins, Does.Not.Contain(velocity.WriteCall));
        Assert.That(writeGroup.Ins.OfType<TextureSwap>().Count(), Is.EqualTo(1));
    }

    [Test]
    public void BindComputeStage_BuildsReadAndWriteGroupsFromAttributeMap()
    {
        var resource = TextureResource.Create("Textures");
        var readIndex = new ShaderNode<Int3>(null, "ReadIndex", theCreateDefault: false);
        var writeIndex = new ShaderNode<Int3>(null, "WriteIndex", theCreateDefault: false);
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        var postGraphRenderer = new EmptyVoid(null);

        resource
            .Prepare()
            .HandleAttribute(color)
            .BindComputeStage(
                null,
                readIndex,
                writeIndex,
                postGraphRenderer: postGraphRenderer);

        Assert.That(resource.ReadGroup, Is.Not.Null);
        Assert.That(resource.WriteGroup, Is.Not.Null);
        Assert.That(resource.ReadGroup.Ins, Does.Contain(color.ReadCall));
        Assert.That(resource.WriteGroup.Ins, Does.Contain(color.WriteCall));
        Assert.That(resource.WriteGroup.Ins.OfType<TextureSwap>().Single().Attribute, Is.SameAs(color));
        Assert.That(resource.WriteGroup.Ins.Last(), Is.SameAs(postGraphRenderer));
        Assert.That(color.ReadCall.SourceCode, Does.Contain(readIndex.ID));
        Assert.That(color.WriteCall.SourceCode, Does.Contain(writeIndex.ID));
    }

    [Test]
    public void SwapTextures_SwapsReadAndWriteInputsForDoubleBufferedAttribute()
    {
        var resource = TextureResource.Create("Textures");
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);
        resource
            .Prepare()
            .HandleAttribute(color)
            .BindAttributes();
        var firstReadInput = resource.TextureAInputs["Color"];
        var firstWriteInput = resource.TextureBInputs["Color"];

        resource.SwapTextures("Color");

        Assert.That(resource.TextureAInputs["Color"], Is.SameAs(firstWriteInput));
        Assert.That(resource.TextureBInputs["Color"], Is.SameAs(firstReadInput));
        Assert.That(color.TextureInput, Is.SameAs(firstWriteInput));
    }

    [Test]
    public void Reset_WhenConditionIsTrueClearsTextureStateAndBumpsTicket()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);
        color.ShaderNode.WriteCounter = 1;
        resource
            .Prepare()
            .HandleAttribute(color)
            .FinishAttributeMap()
            .BindComputeStage(null)
            .UpdateTextures();
        var initialTicket = resource.GetTicket();

        resource.Reset();

        Assert.That(resource.TextureAInputs, Is.Empty);
        Assert.That(resource.TextureBInputs, Is.Empty);
        Assert.That(resource.TextureAs, Is.Empty);
        Assert.That(resource.TextureBs, Is.Empty);
        Assert.That(resource.TextureStatuses, Is.Empty);
        Assert.That(resource.ReadGroup, Is.Null);
        Assert.That(resource.WriteGroup, Is.Null);
        Assert.That(resource.GetLastTextureStatus(), Is.EqualTo("Reset"));
        Assert.That(resource.GetTicket(), Is.EqualTo(initialTicket + 1));
    }

    [Test]
    public void Reset_WhenConditionIsFalseKeepsTextureState()
    {
        var resource = TextureResource.Create("Textures", new Int3(16, 8, 1));
        var color = FakeTextureAttribute<float>.Create("Color", doubleBuffered: true);
        resource
            .Prepare()
            .HandleAttribute(color)
            .FinishAttributeMap()
            .UpdateTextures();
        var ticket = resource.GetTicket();
        var firstInput = resource.TextureAInputs["Color"];

        resource.Reset(false);

        Assert.That(resource.TextureAInputs["Color"], Is.SameAs(firstInput));
        Assert.That(resource.GetTicket(), Is.EqualTo(ticket));
        Assert.That(resource.GetLastTextureStatus(), Does.Contain("Color:A=Failed:GraphicsDevice=null"));
    }

    private sealed class FakeTextureAttribute<T> : ITextureAttribute where T : struct
    {
        private FakeTextureAttribute(string name, bool doubleBuffered)
        {
            Name = name;
            DoubleBuffered = doubleBuffered;
            ShaderNode = new ShaderNode<T>(null, name, theCreateDefault: false);
        }

        public string Name { get; }

        public AttributeType AttributeType { get; set; } = AttributeType.Texture;

        public AbstractShaderNode ShaderNode { get; }

        public AbstractShaderNode InputAbstract { get; set; }

        public ShaderNode<GpuVoid> WriteCall { get; set; }

        public AbstractShaderNode ReadCall { get; set; }

        public Int3 Resolution => TextureInput?.TextureSize() ?? new Int3(1, 1, 1);

        public bool IsOverridden => false;

        public bool DoubleBuffered { get; }

        public AbstractShaderNode Index { get; set; }

        public TextureInput TextureInput { get; set; }

        public void Sync(IAttribute theAttribute)
        {
            InputAbstract = theAttribute.InputAbstract;
            ReadCall = theAttribute.ReadCall;
            WriteCall = theAttribute.WriteCall;
            if (theAttribute is ITextureAttribute textureAttribute)
                TextureInput = textureAttribute.TextureInput;
        }

        public static FakeTextureAttribute<T> Create(string name, bool doubleBuffered = false)
        {
            return new FakeTextureAttribute<T>(name, doubleBuffered);
        }
    }
}
