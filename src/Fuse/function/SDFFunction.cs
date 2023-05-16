using System;
using Stride.Core.Mathematics;

namespace Fuse.function;

public static class FuseDelegates
{
    
    public delegate void SDFUpdate( object stateInput, out object stateOutput, ShaderNode<Vector3> _domain, out ShaderNode<float> _distance);
    
    public delegate void SDFCreate( out object stateOutput);

    public class SDFInvoke : IDisposable
    {
        private object State;

        private SDFUpdate UpdateDelegate;
        public SDFInvoke()
        {
            
        }

        public void Update(SDFCreate create, SDFUpdate update)
        {
            if(State == null)create(out State);
            UpdateDelegate = update;
        }

        public void Invoke(ShaderNode<Vector3> domain, out ShaderNode<float> distance)
        {
            UpdateDelegate(State, out State, domain, out distance);
        }

        public void Dispose()
        {
            if(State is IDisposable disposable) disposable.Dispose();
        }
    }
}