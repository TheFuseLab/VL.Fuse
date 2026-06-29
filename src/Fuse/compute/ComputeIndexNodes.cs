using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.compute;

public class DispatchThreadId : ShaderNode<Int3>
{
    public DispatchThreadId(NodeContext nodeContext)
        : base(nodeContext, "DispatchThreadId", theCreateDefault: false)
    {
        HasFixedName = true;
    }

    public override string ID => "streams.DispatchThreadId";

    public override string GetReference(int depth = 3)
    {
        return ID;
    }
}

public class DispatchThreadIdX : ShaderNode<int>
{
    public DispatchThreadIdX(NodeContext nodeContext)
        : base(nodeContext, "DispatchThreadIdX", theCreateDefault: false)
    {
        HasFixedName = true;
    }

    public override string ID => "streams.DispatchThreadId.x";

    public override string GetReference(int depth = 3)
    {
        return ID;
    }
}

public class VertexId : ShaderNode<int>
{
    public VertexId(NodeContext nodeContext)
        : base(nodeContext, "VertexId", theCreateDefault: false)
    {
        HasFixedName = true;
    }

    public override string ID => "streams.VertexId";

    public override string GetReference(int depth = 3)
    {
        return ID;
    }
}
