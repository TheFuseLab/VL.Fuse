using Fuse.ComputeSystem;
using Stride.Core.Mathematics;

namespace Fuse.compute;

public interface IAttributeLayout
{
    string GpuType { get; }

    int SizeInBytes { get; }

    bool IsArray { get; }

    int ArrayCount { get; }
}

public sealed class PaddingAttribute : IAttribute, IAttributeLayout
{
    public const string DefaultName = "Padding";

    private PaddingAttribute(int floatCount)
    {
        FloatCount = floatCount;
        AttributeType = AttributeType.StructuredBuffer;
    }

    public int FloatCount { get; }

    public string Name => DefaultName;

    public AttributeType AttributeType { get; set; }

    public AbstractShaderNode ShaderNode => null;

    public AbstractShaderNode InputAbstract { get; set; }

    public ShaderNode<GpuVoid> WriteCall { get; set; }

    public AbstractShaderNode ReadCall { get; set; }

    public Int3 Resolution => new(1, 1, 1);

    public bool IsOverridden => false;

    public string GpuType => FloatCount switch
    {
        1 => "float",
        2 => "float2",
        3 => "float3",
        _ => "float"
    };

    public int SizeInBytes => FloatCount * 4;

    public bool IsArray => false;

    public int ArrayCount => 1;

    public void Sync(IAttribute theAttribute)
    {
        InputAbstract = theAttribute.InputAbstract;
        ReadCall = theAttribute.ReadCall;
        WriteCall = theAttribute.WriteCall;
    }

    public static PaddingAttribute Create(int floatCount)
    {
        if (floatCount < 1 || floatCount > 3)
            return null;

        return new PaddingAttribute(floatCount);
    }
}
