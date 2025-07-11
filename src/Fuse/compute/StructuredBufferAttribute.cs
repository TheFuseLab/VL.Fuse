using Fuse.ComputeSystem;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.compute;

public interface IStructureBufferAttribute : IAttribute
{
    public Buffer Buffer { get; set; }

    public int ArrayCount { get; }
}

public abstract class AbstractStructuredBufferAttribute<T> : Attribute<T>, IStructureBufferAttribute
{
    protected AbstractStructuredBufferAttribute(NodeContext nodeContext, string theName, AttributeType theType,
        int theArrayCount = 0) : base(nodeContext, theName, theType)
    {
        ArrayCount = theArrayCount;
    }

    public Buffer Buffer { get; set; }

    public int ArrayCount { get; }

    public override Int3 Resolution => new(Buffer.ElementCount, 1, 1);
}

public class StructuredBufferAttribute<T> : AbstractStructuredBufferAttribute<T>
{
    // public ShaderNode<T> Default;
    public StructuredBufferAttribute(NodeContext nodeContext, string theName, bool theDefineSemantic = true,
        ShaderNode<GpuStruct> theOverrideInstance = null, ShaderNode<T> theDefault = null,
        int theArrayCount = 0) : base(nodeContext, theName, AttributeType.StructuredBuffer, theArrayCount)
    {
        var myFactory = new NodeSubContextFactory(nodeContext);

        Default = theDefault ?? ConstantHelper.FromFloat<T>(0f);

        if (theOverrideInstance != null)
        {
            var getMember = new GetMember<GpuStruct, T>(myFactory.NextSubContext(), theOverrideInstance, theName);
            SetInput(getMember);
            IsOverridden = true;
        }
        else
        {
            SetInput(new Semantic<T>(myFactory.NextSubContext(), theName, theDefineSemantic, theName.ToUpper()));
            SetProperty("ComputeSystemAttribute", this);
            SetViewerID("ComputeAttribute");
        }
    }
}