using System;
using System.Collections.Generic;
using Fuse.Domain;
using Fuse.function;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.Function;


public delegate void ShadeUpdate( object stateInput, out object stateOutput, ShaderNode<FuseSurface> Surface, ShaderNode<FuseRay> Ray, out ShaderNode<Vector4> Color);
    
public delegate void ShadeCreate( out object stateOutput);

public class FuseRay { }

public class FuseSurface { }

public class Shade : IDisposable
{
    private object _state;

    private readonly NodeContext _nodeContext;

    public Delegate<Vector4> Delegate { get; private set; }

    public Shade(NodeContext theNodeContext) 
    {
          _nodeContext = theNodeContext;  
    }

    public void Update(ShadeCreate create, ShadeUpdate update)
    {
        if(_state == null)create(out _state);
        
        var nodeSubContextFactory = new NodeSubContextFactory(_nodeContext);

        var surfaceParameter = new FunctionParameter<FuseSurface>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);
        var rayParameter = new FunctionParameter<FuseRay>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,1);

        //update(State,out State, new FunctionParameter<MarchSurface>())
        update(_state, out _state, surfaceParameter, rayParameter, out var color);

        Delegate = new Delegate<Vector4>(
            nodeSubContextFactory.NextSubContext(),
            color,
            new List<IFunctionParameter>
            {
                surfaceParameter,
                rayParameter
            }
        );
    }

    public void Dispose()
    {
        if(_state is IDisposable disposable) disposable.Dispose();
    }
}

public class ShadeInvoke : Invoke<Vector4>
{
    public ShadeInvoke(
        NodeContext nodeContext, 
        Shade theShade, 
        ShaderNode<FuseSurface> theSurface,
        ShaderNode<FuseRay> theRay) : base(nodeContext,theShade.Delegate, new List<AbstractShaderNode> { theSurface, theRay})
    {
    }
}