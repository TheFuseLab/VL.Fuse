using System;
using System.Collections.Generic;
using Fuse.function;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate1In2OutUpdate<TIn, TOut1,TOut2>( object stateInput, out object stateOutput, ShaderNode<TIn> Input, out ShaderNode<TOut1> Output1, out ShaderNode<TOut2> Output2);
    
public delegate void Delegate1In2OutCreate( out object stateOutput);

public class Delegate1In2Out<TIn, TOut1,TOut2> : DelegateStateful<Delegate<TOut1>>
{

    public Delegate1In2Out(NodeContext theNodeContext) : base(theNodeContext)
    { 
    }

    public void Update(Delegate1In2OutCreate create, Delegate1In2OutUpdate<TIn, TOut1,TOut2> update)
    {
        if(State == null)create(out State);
        
        var nodeSubContextFactory = new NodeSubContextFactory(NodeContext);

        var inputParameter = new FunctionParameter<TIn>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);

        update(State, out State, inputParameter, out var sdfArgument, out var valueArgument);
        
        var outputParameter = new FunctionParameter<TOut2>(nodeSubContextFactory.NextSubContext(), null, InputModifier.Out,1);
        var set = new AssignValue<TOut2>(nodeSubContextFactory.NextSubContext(),outputParameter,valueArgument);
        var do2 = new Do2<TOut1>(nodeSubContextFactory.NextSubContext(), sdfArgument, new List<AbstractShaderNode>{set});

        Delegate = new Delegate<TOut1>(
            nodeSubContextFactory.NextSubContext(),
            do2,
            new List<IFunctionParameter>
            {
                inputParameter,
                outputParameter
            }
        );
    }
}

public class Delegate1In2OutInvoke<TIn, TOut1,TOut2> : Invoke<TOut1>
{
    public Delegate1In2OutInvoke(
        NodeContext nodeContext, 
        Delegate1In2Out<TIn, TOut1,TOut2> theDelegate, 
        ShaderNode<TIn> theInput) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode> { theInput})
    {
        
    }
}