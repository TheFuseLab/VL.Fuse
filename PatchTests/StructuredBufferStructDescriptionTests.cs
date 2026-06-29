using System;
using System.Linq;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class StructuredBufferStructDescriptionTests
{
    [Test]
    public void FromAttributeMap_BuildsMembersInAttributeOrder()
    {
        var map = CreateMap(("Life", 4), ("Position", 12));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Position"));

        var description = StructuredBufferStructDescription.FromAttributeMap(
            "Particle",
            map,
            getGpuType: attribute => attribute.Name == "Position" ? "float3" : "float",
            getSizeInBytes: attribute => attribute.Name == "Position" ? 12 : 4);

        Assert.That(description.Name, Is.EqualTo("Particle"));
        Assert.That(description.Stride, Is.EqualTo(16));
        Assert.That(
            description.Members.Select(member => member.Declaration),
            Is.EqualTo(new[] { "float Life;", "float3 Position;" }));
    }

    [Test]
    public void BuildStructSource_UsesDynamicStructFormatting()
    {
        var description = new StructuredBufferStructDescription(
            "Particle",
            new[]
            {
                new StructuredBufferStructMember("Life", "float", 4),
                new StructuredBufferStructMember("Position", "float3", 12)
            });

        var source = description.BuildStructSource();

        Assert.That(source, Does.Contain("struct Particle"));
        Assert.That(source, Does.Contain("float Life;"));
        Assert.That(source, Does.Contain("float3 Position;"));
    }

    [Test]
    public void FromAttributeMap_CanDescribeArrayMember()
    {
        var map = CreateMap(("Samples", 16));
        map.HandleAttribute(FakeComputeAttribute.Create("Samples"));

        var description = StructuredBufferStructDescription.FromAttributeMap(
            "Particle",
            map,
            getGpuType: _ => "float4",
            getSizeInBytes: _ => 16,
            isArray: _ => true,
            getArrayCount: _ => 4);

        Assert.That(description.Members.Single().Declaration, Is.EqualTo("float4 Samples[4];"));
        Assert.That(description.Members.Single().TotalSizeInBytes, Is.EqualTo(64));
        Assert.That(description.Stride, Is.EqualTo(64));
    }

    [Test]
    public void StructuredBufferStructMember_RejectsInvalidLayout()
    {
        Assert.Throws<ArgumentException>(() =>
            new StructuredBufferStructMember("", "float", 4));
        Assert.Throws<ArgumentException>(() =>
            new StructuredBufferStructMember("Life", "", 4));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StructuredBufferStructMember("Life", "float", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new StructuredBufferStructMember("Samples", "float4", 16, isArray: true, arrayCount: 0));
    }

    [Test]
    public void FromAttributeMap_UsesPaddingLayoutMetadata()
    {
        var map = new AttributeMap(AttributeType.StructuredBuffer);
        map.HandleAttribute(PaddingAttribute.Create(2));

        var description = StructuredBufferStructDescription.FromAttributeMap("Particle", map);

        Assert.That(description.Stride, Is.EqualTo(8));
        Assert.That(description.Members.Single().Declaration, Is.EqualTo("float2 Padding;"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("struct")]
    public void SanitizeStructName_FallsBackToGpuStruct(string name)
    {
        Assert.That(StructuredBufferStructDescription.SanitizeStructName(name), Is.EqualTo("GpuStruct"));
    }

    [Test]
    public void BuildMemberDescriptions_PrefixesStructName()
    {
        var description = new StructuredBufferStructDescription(
            "Particle",
            new[] { new StructuredBufferStructMember("Life", "float", 4) });

        Assert.That(description.BuildMemberDescriptions(), Is.EqualTo(new[] { "Particle.float Life;" }));
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
        return new AttributeMap(
            AttributeType.StructuredBuffer,
            attribute => attribute is IAttributeLayout layout ? layout.SizeInBytes : lookup[attribute.Name]);
    }
}
