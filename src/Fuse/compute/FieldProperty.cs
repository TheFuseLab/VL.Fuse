using VL.Core;

namespace Fuse.compute;

public class FieldProperty<T> : PassThroughNode<float>
{
    private ShaderNode<T> _property;
    
    public FieldProperty(NodeContext nodeContext, ShaderNode<float> theSDF, ShaderNode<T> theProperty) : base(nodeContext, theSDF, "FieldProperty")
    {
        _property = theProperty;
    }

    public ShaderNode<T> GetProperty()
    {
        return _property;
    }
}