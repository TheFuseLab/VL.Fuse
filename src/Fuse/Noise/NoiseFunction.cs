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
    private object State;

    private NodeContext NodeContext;

    public Delegate<float> Delegate { get; private set; }

    public NoiseDerivative(NodeContext theNodeContext) 
    {
          NodeContext = theNodeContext;  
    }

    public void Update(NoiseDerivativeCreate create, NoiseDerivativeUpdate<T> update)
    {
        if(State == null)create(out State);
        
        var NodeSubContextFactory = new NodeSubContextFactory(NodeContext);

        var domainParameter = new FunctionParameter<T>(NodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);
        ShaderNode<float> valueArgument;
        ShaderNode<T> DerivativeArgument;
        
        //update(State,out State, new FunctionParameter<MarchSurface>())
        update(State, out State, domainParameter, out valueArgument, out DerivativeArgument);
        
        var derivativeParameter = new FunctionParameter<T>(NodeSubContextFactory.NextSubContext(), null, InputModifier.Out,1);
        var set = new AssignValue<T>(NodeSubContextFactory.NextSubContext(),derivativeParameter,DerivativeArgument);
        var do2 = new Do2<float>(NodeSubContextFactory.NextSubContext(), valueArgument, new List<AbstractShaderNode>{set});

        Delegate = new Delegate<float>(
            NodeSubContextFactory.NextSubContext(),
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
        if(State is IDisposable disposable) disposable.Dispose();
    }
}

public class NoiseDerivativeInvoke<T> : Invoke<float>
{
    public NoiseDerivativeInvoke(
        NodeContext nodeContext, 
        NoiseDerivative<T> theDelegate, 
        ShaderNode<T> domain) : base(nodeContext,theDelegate.Delegate, new List<AbstractShaderNode> { domain})
    {
        
    }
}