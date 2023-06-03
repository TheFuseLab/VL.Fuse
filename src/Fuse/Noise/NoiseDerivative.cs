using System;
using System.Collections.Generic;
using Fuse.function;
using Fuse.sdf;
using VL.Core;

namespace Fuse.Noise;


public delegate void NoiseDerivativeUpdate<T>( object stateInput, out object stateOutput, ShaderNode<T> Domain, out ShaderNode<float> Value, out ShaderNode<T> Derivative);
    
public delegate void NoiseDerivativeCreate( out object stateOutput);

public class NoiseDerivative<T> : IDisposable
{
    private object _state;

    private readonly NodeContext _nodeContext;

    public Delegate<float> Delegate { get; private set; }

    public NoiseDerivative(NodeContext theNodeContext) 
    {
          _nodeContext = theNodeContext;  
    }

    public void Update(NoiseDerivativeCreate create, NoiseDerivativeUpdate<T> update)
    {
        if(_state == null)create(out _state);
        
        var nodeSubContextFactory = new NodeSubContextFactory(_nodeContext);

        var domainParameter = new FunctionParameter<T>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);

        //update(State,out State, new FunctionParameter<MarchSurface>())
        update(_state, out _state, domainParameter, out var valueArgument, out var derivativeArgument);
        
        var derivativeParameter = new FunctionParameter<T>(nodeSubContextFactory.NextSubContext(), null, InputModifier.Out,1);
        var set = new AssignValue<T>(nodeSubContextFactory.NextSubContext(),derivativeParameter,derivativeArgument);
        var do2 = new Do2<float>(nodeSubContextFactory.NextSubContext(), valueArgument, new List<AbstractShaderNode>{set});

        Delegate = new Delegate<float>(
            nodeSubContextFactory.NextSubContext(),
            do2,
            new List<IFunctionParameter>
            {
                domainParameter,
                derivativeParameter
            }
        );
    }

    public void Dispose()
    {
        if(_state is IDisposable disposable) disposable.Dispose();
    }
}

public class NoiseDerivativeInvoke<T> : Invoke<float>
{
    public NoiseDerivativeInvoke(
        NodeContext nodeContext, 
        NoiseDerivative<T> theDelegate, 
        ShaderNode<T> domain) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode> { domain})
    {
        
    }
}