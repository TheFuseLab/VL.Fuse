using System.Linq;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class StructuredBufferResourceStateTests
{
    [Test]
    public void Create_UsesVlDefaults()
    {
        var resource = StructuredBufferResource.Create();

        Assert.That(resource.Name, Is.Null);
        Assert.That(resource.AttributeType, Is.EqualTo(AttributeType.StructuredBuffer));
        Assert.That(resource.ElementCount, Is.EqualTo(1_024));
        Assert.That(resource.ThreadGroupSize, Is.EqualTo(256));
        Assert.That(resource.StructSize, Is.EqualTo(0));
        Assert.That(resource.GetSize(), Is.EqualTo(new Int3(1_024, 1, 1)));
        Assert.That(resource.GetDispatchInfo().GetCount(), Is.EqualTo(new ComputeDispatchSize(4, 1, 1)));
    }

    [Test]
    public void SetElementCount_RebuildsDispatchInfo()
    {
        var resource = StructuredBufferResource.Create();

        resource.SetElementCount(1_000);

        Assert.That(resource.GetElementCount(), Is.EqualTo(1_000));
        Assert.That(resource.GetSize(), Is.EqualTo(new Int3(1_000, 1, 1)));
        Assert.That(resource.GetDispatchInfo().GetCount(), Is.EqualTo(new ComputeDispatchSize(4, 1, 1)));
        Assert.That(resource.GetDispatchInfo().IsValid, Is.True);
    }

    [Test]
    public void SetDispatchGroupSize_RebuildsDispatchInfo()
    {
        var resource = StructuredBufferResource.Create(elementCount: 1_000);

        resource.SetDispatchGroupSize(128);

        Assert.That(resource.ThreadGroupSize, Is.EqualTo(128));
        Assert.That(resource.GetDispatchInfo().ThreadGroupSize, Is.EqualTo(new ComputeDispatchSize(128, 1, 1)));
        Assert.That(resource.GetDispatchInfo().GetCount(), Is.EqualTo(new ComputeDispatchSize(8, 1, 1)));
    }

    [Test]
    public void SetStructSize_UpdatesResourceSize()
    {
        var resource = StructuredBufferResource.Create(elementCount: 1_000);

        resource.SetStructSize(32);

        Assert.That(resource.GetStructSize(), Is.EqualTo(32));
        Assert.That(resource.GetResourceSize(), Is.EqualTo(32_000));
    }

    [Test]
    public void SetAttributeMap_UpdatesStructSizeFromAttributes()
    {
        var resource = StructuredBufferResource.Create(elementCount: 1_000);
        var map = CreateMap(("Life", 4), ("Position", 12));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Position"));

        resource.SetAttributeMap(
            map,
            getGpuType: attribute => attribute.Name == "Position" ? "float3" : "float",
            getSizeInBytes: attribute => attribute.Name == "Position" ? 12 : 4);

        Assert.That(resource.GetAttributeMap(), Is.SameAs(map));
        Assert.That(resource.GetStructSize(), Is.EqualTo(16));
        Assert.That(resource.GetStride(), Is.EqualTo(16));
        Assert.That(resource.GetResourceSize(), Is.EqualTo(16_000));
        Assert.That(resource.GetStructDescription().Stride, Is.EqualTo(16));
        Assert.That(resource.GetStruct(), Is.SameAs(resource.GetStructDescription()));
        Assert.That(resource.GetStructInstance().TypeName(), Is.EqualTo("GpuStruct"));
        Assert.That(resource.GetSemantics(), Is.EqualTo(new[] { "LIFE", "POSITION" }));
        Assert.That(
            resource.GetVertexDeclaration(),
            Is.EqualTo(new[] { "GpuStruct.float Life;", "GpuStruct.float3 Position;" }));
        Assert.That(
            resource.GetStructDescription().Members.Select(member => member.Declaration),
            Is.EqualTo(new[] { "float Life;", "float3 Position;" }));
    }

    [Test]
    public void SetAttributeMap_CanUsePaddedStructSize()
    {
        var resource = StructuredBufferResource.Create(elementCount: 1_000);
        var map = CreateMap(("Life", 4), ("Velocity", 8));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Velocity"));

        resource.SetAttributeMap(
            map,
            usePaddedStructSize: true,
            getGpuType: attribute => attribute.Name == "Velocity" ? "float2" : "float",
            getSizeInBytes: attribute => attribute.Name == "Velocity" ? 8 : 4);

        Assert.That(resource.GetStructSize(), Is.EqualTo(16));
        Assert.That(resource.GetResourceSize(), Is.EqualTo(16_000));
    }

    [Test]
    public void PatchLifecycle_PrepareHandleFinishBuildsAttributeMapAndStruct()
    {
        var resource = StructuredBufferResource.Create("Particle", elementCount: 1_000);
        var life = FakeComputeAttribute.Create("Life");
        var position = FakeComputeAttribute.Create("Position");

        resource
            .Prepare()
            .HandleAttribute(life)
            .HandleAttribute(position)
            .FinishAttributeMap();

        resource.UpdateStruct(
            getGpuType: attribute => attribute.Name == "Position" ? "float3" : "float",
            getSizeInBytes: attribute => attribute.Name == "Position" ? 12 : 4);

        Assert.That(resource.GetAttributeMap().AttributeSet.Keys, Is.EquivalentTo(new[] { "Life", "Position" }));
        Assert.That(resource.ChangedAttributes, Is.True);
        Assert.That(resource.GetTicket(), Is.EqualTo(1));
        Assert.That(resource.GetStructSize(), Is.EqualTo(16));
        Assert.That(resource.GetStructDescription().Members.Select(member => member.Declaration), Is.EqualTo(new[] { "float Life;", "float3 Position;" }));
    }

    [Test]
    public void PatchLifecycle_PrepareFinishRemovesUnusedAttributes()
    {
        var resource = StructuredBufferResource.Create("Particle");
        resource.HandleAttribute(FakeComputeAttribute.Create("Life"));
        resource.FinishAttributeMap();

        resource.Prepare();
        resource.FinishAttributeMap();

        Assert.That(resource.GetAttributeMap().AttributeSet, Is.Empty);
        Assert.That(resource.ChangedAttributes, Is.True);
        Assert.That(resource.GetTicket(), Is.EqualTo(2));
    }

    [Test]
    public void GetResourceSize_SaturatesOnOverflow()
    {
        var resource = StructuredBufferResource.Create(
            elementCount: long.MaxValue,
            structSize: 2);

        Assert.That(resource.GetResourceSize(), Is.EqualTo(long.MaxValue));
    }

    [Test]
    public void InvalidElementCount_IsReportedThroughDispatchInfo()
    {
        var resource = StructuredBufferResource.Create(elementCount: 0);

        Assert.That(resource.GetDispatchInfo().IsValid, Is.False);
        Assert.That(
            resource.GetDispatchInfo().Diagnostics.Select(d => d.Code),
            Does.Contain(ComputeDispatchDiagnosticCode.ElementCountNonPositive));
    }

    [Test]
    public void GetComputeResource_UsesNameAttributeTypeAndSize()
    {
        var resource = StructuredBufferResource.Create("Particles", elementCount: 128);

        var computeResource = resource.GetComputeResource();

        Assert.That(resource.GetName(), Is.EqualTo("Particles"));
        Assert.That(resource.GetAttributeType(), Is.EqualTo(AttributeType.StructuredBuffer));
        Assert.That(computeResource.AttributeType, Is.EqualTo(AttributeType.StructuredBuffer));
        Assert.That(computeResource.Resource, Is.EqualTo("Particles"));
        Assert.That(computeResource.Size, Is.EqualTo(new Int3(128, 1, 1)));
    }

    [Test]
    public void UpdateBuffer_WithoutGraphicsDeviceBuildsDescriptionAndReportsStatus()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: 128)
            .SetStructSize(16);

        resource.UpdateBuffer();

        Assert.That(resource.GetBuffer(), Is.Null);
        Assert.That(resource.LastBufferDescription.ElementCount, Is.EqualTo(128));
        Assert.That(resource.LastBufferDescription.ElementSizeInBytes, Is.EqualTo(16));
        Assert.That(resource.LastBufferDescription.SizeInBytes, Is.EqualTo(2048));
        Assert.That(resource.LastBufferStatus, Is.EqualTo("Failed:GraphicsDevice=null"));
    }

    [Test]
    public void UpdateBuffer_StrideAboveD3D11LimitReportsStatus()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: 16)
            .SetStructSize(ComputeResourceLimits.D3D11.MaxStructuredBufferStride + 1L);

        resource.UpdateBuffer();

        Assert.That(resource.GetBuffer(), Is.Null);
        Assert.That(resource.LastBufferDescription, Is.Null);
        Assert.That(resource.LastBufferStatus, Does.StartWith("Failed:Description:ArgumentOutOfRangeException"));
        Assert.That(resource.LastBufferStatus, Does.Contain("Structured buffer stride"));
        Assert.That(resource.LastBufferStatus, Does.Contain("Stride=2049"));
    }

    [Test]
    public void UpdateBuffer_ElementCountAboveD3D11LimitReportsStatus()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: ComputeResourceLimits.D3D11.MaxBufferElementCount + 1L)
            .SetStructSize(16);

        resource.UpdateBuffer();

        Assert.That(resource.GetBuffer(), Is.Null);
        Assert.That(resource.LastBufferDescription, Is.Null);
        Assert.That(resource.LastBufferStatus, Does.StartWith("Failed:Description:ArgumentOutOfRangeException"));
        Assert.That(resource.LastBufferStatus, Does.Contain("Structured buffer element count"));
        Assert.That(resource.LastBufferStatus, Does.Contain("ElementCount=134217729"));
    }

    [Test]
    public void UpdateBuffer_SizeInBytesOverflowReportsStatus()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: int.MaxValue / 16L + 1L)
            .SetStructSize(16);

        resource.UpdateBuffer();

        Assert.That(resource.GetBuffer(), Is.Null);
        Assert.That(resource.LastBufferDescription, Is.Null);
        Assert.That(resource.LastBufferStatus, Does.StartWith("Failed:Description:OverflowException"));
        Assert.That(resource.LastBufferStatus, Does.Contain("sizeInBytes"));
    }

    [Test]
    public void DisposeBuffer_ReportsDisposed()
    {
        var resource = StructuredBufferResource.Create("Particles");

        resource.DisposeBuffer();

        Assert.That(resource.GetBuffer(), Is.Null);
        Assert.That(resource.LastBufferStatus, Is.EqualTo("Disposed"));
    }

    [Test]
    public void Reset_WhenConditionIsTrueClearsBufferStateAndBumpsTicket()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: 128)
            .SetStructSize(16);
        resource.UpdateBuffer();
        var initialTicket = resource.GetTicket();

        resource.Reset();

        Assert.That(resource.GetBuffer(), Is.Null);
        Assert.That(resource.LastBufferDescription, Is.Null);
        Assert.That(resource.GetBufferInput(), Is.Null);
        Assert.That(resource.LastBufferStatus, Is.EqualTo("Reset"));
        Assert.That(resource.GetTicket(), Is.EqualTo(initialTicket + 1));
    }

    [Test]
    public void Reset_WhenConditionIsFalseKeepsState()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: 128)
            .SetStructSize(16);
        resource.UpdateBuffer();
        var description = resource.LastBufferDescription;
        var ticket = resource.GetTicket();

        resource.Reset(false);

        Assert.That(resource.LastBufferDescription, Is.SameAs(description));
        Assert.That(resource.GetTicket(), Is.EqualTo(ticket));
    }

    [Test]
    public void SetName_UpdatesComputeResourceName()
    {
        var resource = StructuredBufferResource.Create("A");

        resource.SetName("B");

        Assert.That(resource.Name, Is.EqualTo("B"));
        Assert.That(resource.GetComputeResource().Resource, Is.EqualTo("B"));
    }

    private sealed class FakeComputeAttribute : IAttribute
    {
        private FakeComputeAttribute(string name)
        {
            Name = name;
            AttributeType = AttributeType.StructuredBuffer;
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

        public static FakeComputeAttribute Create(string name)
        {
            return new FakeComputeAttribute(name);
        }
    }

    private static AttributeMap CreateMap(params (string Name, int Size)[] sizes)
    {
        var lookup = sizes.ToDictionary(size => size.Name, size => size.Size);
        return new AttributeMap(AttributeType.StructuredBuffer, attribute => lookup[attribute.Name]);
    }
}
