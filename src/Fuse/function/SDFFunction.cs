using Stride.Core.Mathematics;

namespace Fuse.function;

public static class FuseDelegates
{
    public delegate void SDFDelegate(in ShaderNode<Vector3> _domain, out ShaderNode<float> _distance);
    
    public static void InvokeSDF(SDFDelegate _delegate, ShaderNode<Vector3> _domain, out ShaderNode<float> _distance)
    {
        _delegate.Invoke(_domain, out _distance);
    }
}