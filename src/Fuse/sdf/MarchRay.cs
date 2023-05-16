using System;
using System.Collections.Generic;
using Fuse.function;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.sdf;

public class MarchRay
{
    
}

public class MarchSurface
{
    
}

public delegate void ShadeSDFUpdate( object stateInput, out object stateOutput, ShaderNode<MarchSurface> Surface, ShaderNode<MarchRay> Ray, out ShaderNode<Vector4> Color);
    
public delegate void ShadeSDFCreate( out object stateOutput);

public class ShadeSDF : IDisposable
{
    private object State;

    private ShadeSDFUpdate UpdateDelegate;


    public ShadeSDF(NodeContext theNodeContext) 
    {
            
    }

    public void Update(ShadeSDFCreate create, ShadeSDFUpdate update)
    {
        if(State == null)create(out State);
        
        //update(State,out State, new FunctionParameter<MarchSurface>())
        UpdateDelegate = update;
    }

    public void Invoke(ShaderNode<MarchSurface> Surface, ShaderNode<MarchRay> Ray, out ShaderNode<Vector4> Color)
    {
        UpdateDelegate(State, out State, Surface,  Ray,out Color);
    }

    public void Dispose()
    {
        if(State is IDisposable disposable) disposable.Dispose();
    }
}

public class ShadeSDFInvoke : Invoke<Vector4>
{
    public ShadeSDFInvoke(
        NodeContext nodeContext, 
        ShadeSDF theDelegate, 
        ShaderNode<MarchSurface> surface, 
        ShaderNode<MarchRay> ray, 
        string theId = "Invoke", 
        ShaderNode<Vector4> theDefault = null
        ) : base(
        nodeContext, 
        null, 
        new List<AbstractShaderNode>{surface, ray}, 
        theId, 
        theDefault)
    {
    }
}