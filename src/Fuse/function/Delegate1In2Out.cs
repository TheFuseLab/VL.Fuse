using System;
using System.Collections.Generic;
using Fuse.function;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate1In2OutUpdate<TIn, TOut1,TOut2>( object stateInput, out object stateOutput, ShaderNode<TIn> Input, out ShaderNode<TOut1> Output1, out ShaderNode<TOut2> Output2);
    
public delegate void Delegate1In2OutCreate( out object stateOutput);

public class Delegate1In2Out<TIn, TOut1,TOut2> : IDisposable
{
    private object _state;

    private readonly NodeContext _nodeContext;

    public Delegate<TOut1> Delegate { get; private set; }

    public Delegate1In2Out(NodeContext theNodeContext) 
    {
          _nodeContext = theNodeContext;  
    }

    public void Update(Delegate1In2OutCreate create, Delegate1In2OutUpdate<TIn, TOut1,TOut2> update)
    {
        if(_state == null)create(out _state);
        
        var nodeSubContextFactory = new NodeSubContextFactory(_nodeContext);

        var inputParameter = new FunctionParameter<TIn>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);

        update(_state, out _state, inputParameter, out var sdfArgument, out var valueArgument);
        
        var valueParameter = new FunctionParameter<TOut2>(nodeSubContextFactory.NextSubContext(), null, InputModifier.Out,1);
        var set = new AssignValue<TOut2>(nodeSubContextFactory.NextSubContext(),valueParameter,valueArgument);
        var do2 = new Do2<TOut1>(nodeSubContextFactory.NextSubContext(), sdfArgument, new List<AbstractShaderNode>{set});

        Delegate = new Delegate<TOut1>(
            nodeSubContextFactory.NextSubContext(),
            do2,
            new List<IFunctionParameter>
            {
                inputParameter,
                valueParameter
            }
        );
    }

    public void Dispose()
    {
        if(_state is IDisposable disposable) disposable.Dispose();
    }
}

public class Delegate1In2OutInvoke<TIn, TOut1,TOut2> : Invoke<TOut1>
{
    public Delegate1In2OutInvoke(
        NodeContext nodeContext, 
        Delegate1In2Out<TIn, TOut1,TOut2> theDelegate, 
        ShaderNode<TIn> domain) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode> { domain})
    {
        
    }
}