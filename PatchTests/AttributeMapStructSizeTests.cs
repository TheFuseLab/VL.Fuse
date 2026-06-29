using System.Linq;
using Fuse;
using Fuse.compute;
using Fuse.ComputeSystem;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class AttributeMapStructSizeTests
{
    [Test]
    public void GetStructSize_SumsShaderNodeByteSizes()
    {
        var map = CreateMap(("Life", 4), ("Position", 12));

        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Position"));

        Assert.That(map.AttributeSet.Keys, Is.EquivalentTo(new[] { "Life", "Position" }));
        Assert.That(map.GetStructSize(), Is.EqualTo(16));
        Assert.That(map.GetPaddedStructSize(), Is.EqualTo(16));
    }

    [Test]
    public void GetPaddedStructSize_AlignsToSixteenBytes()
    {
        var map = CreateMap(("Life", 4), ("Velocity", 8));

        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Velocity"));

        Assert.That(map.GetStructSize(), Is.EqualTo(12));
        Assert.That(map.GetPaddedStructSize(), Is.EqualTo(16));
        Assert.That(AttributeMap.GetPaddingFloatCount(12), Is.EqualTo(1));
        Assert.That(AttributeMap.GetPaddingByteCount(12), Is.EqualTo(4));
    }

    [Test]
    public void ApplyPadding_AddsPaddingAttributeToAlignStruct()
    {
        var map = CreateMap(("Life", 4), ("Velocity", 8));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Velocity"));

        var changed = map.ApplyPadding();

        Assert.That(changed, Is.True);
        Assert.That(map.AttributeSet[PaddingAttribute.DefaultName], Is.TypeOf<PaddingAttribute>());
        Assert.That(((PaddingAttribute)map.AttributeSet[PaddingAttribute.DefaultName]).FloatCount, Is.EqualTo(1));
        Assert.That(map.GetStructSize(), Is.EqualTo(16));
    }

    [Test]
    public void ApplyPadding_ReplacesStalePaddingWhenBaseSizeChanges()
    {
        var map = CreateMap(("Life", 4), ("Velocity", 8));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.ApplyPadding();

        Assert.That(((PaddingAttribute)map.AttributeSet[PaddingAttribute.DefaultName]).FloatCount, Is.EqualTo(3));

        map.HandleAttribute(FakeComputeAttribute.Create("Velocity"));
        map.ApplyPadding();

        Assert.That(((PaddingAttribute)map.AttributeSet[PaddingAttribute.DefaultName]).FloatCount, Is.EqualTo(1));
        Assert.That(map.GetStructSizeWithoutPadding(), Is.EqualTo(12));
        Assert.That(map.GetStructSize(), Is.EqualTo(16));
    }

    [Test]
    public void ApplyPadding_RemovesPaddingWhenStructBecomesAligned()
    {
        var map = CreateMap(("Life", 4), ("Velocity", 8), ("Age", 4));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));
        map.HandleAttribute(FakeComputeAttribute.Create("Velocity"));
        map.ApplyPadding();

        map.HandleAttribute(FakeComputeAttribute.Create("Age"));
        var changed = map.ApplyPadding();

        Assert.That(changed, Is.True);
        Assert.That(map.AttributeSet.ContainsKey(PaddingAttribute.DefaultName), Is.False);
        Assert.That(map.GetStructSize(), Is.EqualTo(16));
    }

    [Test]
    public void GetPaddingByteCount_ReturnsZeroForAlignedSize()
    {
        Assert.That(AttributeMap.GetPaddingByteCount(16), Is.EqualTo(0));
        Assert.That(AttributeMap.GetPaddingFloatCount(16), Is.EqualTo(0));
    }

    [Test]
    public void HandleAttribute_SkipsOverriddenAttributes()
    {
        var map = CreateMap(("Overridden", 4));

        map.HandleAttribute(FakeComputeAttribute.Create("Overridden", isOverridden: true));

        Assert.That(map.AttributeSet, Is.Empty);
        Assert.That(map.GetStructSize(), Is.EqualTo(0));
    }

    [Test]
    public void Finish_RemovesUnusedAttributesAfterPrepare()
    {
        var map = CreateMap(("Life", 4));
        map.HandleAttribute(FakeComputeAttribute.Create("Life"));

        map.Prepare();
        var changed = map.Finish();

        Assert.That(changed, Is.True);
        Assert.That(map.AttributeSet, Is.Empty);
    }

    private sealed class FakeComputeAttribute : IAttribute
    {
        private FakeComputeAttribute(string name, bool isOverridden)
        {
            Name = name;
            IsOverridden = isOverridden;
            AttributeType = AttributeType.StructuredBuffer;
        }

        public string Name { get; }

        public AttributeType AttributeType { get; set; }

        public AbstractShaderNode ShaderNode { get; }

        public AbstractShaderNode InputAbstract { get; set; }

        public ShaderNode<GpuVoid> WriteCall { get; set; }

        public AbstractShaderNode ReadCall { get; set; }

        public Int3 Resolution => new(1, 1, 1);

        public bool IsOverridden { get; }

        public void Sync(IAttribute theAttribute)
        {
            InputAbstract = theAttribute.InputAbstract;
            ReadCall = theAttribute.ReadCall;
            WriteCall = theAttribute.WriteCall;
        }

        public static FakeComputeAttribute Create(string name, bool isOverridden = false)
        {
            return new FakeComputeAttribute(name, isOverridden);
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
