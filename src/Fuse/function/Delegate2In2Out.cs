using System;
using System.Collections.Generic;
using Fuse.function;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate2In2OutUpdate<TIn1, TIn2, TOut1,TOut2>( object stateInput, out object stateOutput, ShaderNode<TIn1> input1, ShaderNode<TIn2> input2, out ShaderNode<TOut1> output1, out ShaderNode<TOut2> output2);
    
public delegate void Delegate2In2OutCreate( out object stateOutput);

public class Delegate2In2Out<TIn1, TIn2, TOut1,TOut2> : DelegateStateful<Delegate<TOut1>>
{

    public Delegate2In2Out(NodeContext theNodeContext) : base(theNodeContext)
    { 
    }

    public void Update(Delegate2In2OutCreate create, Delegate2In2OutUpdate<TIn1, TIn2, TOut1,TOut2> update)
    {
        if(State == null)create(out State);
        
        var nodeSubContextFactory = new NodeSubContextFactory(NodeContext);

        var inputParameter1 = new FunctionParameter<TIn1>(nodeSubContextFactory.NextSubContext(), null);
        var inputParameter2 = new FunctionParameter<TIn2>(nodeSubContextFactory.NextSubContext(), null);

        update(State, out State, inputParameter1, inputParameter2, out var outArgument1, out var outArgument2);
        
        var outputParameter = new FunctionParameter<TOut2>(nodeSubContextFactory.NextSubContext(), null, InputModifier.Out,1);
        var set = new AssignValue<TOut2>(nodeSubContextFactory.NextSubContext(),outputParameter,outArgument2);
        var do2 = new Do2<TOut1>(nodeSubContextFactory.NextSubContext(), outArgument1, new List<AbstractShaderNode>{set});

        Delegate = new Delegate<TOut1>(
            nodeSubContextFactory.NextSubContext(),
            do2,
            new List<IFunctionParameter>
            {
                inputParameter1,
                inputParameter2,
                outputParameter
            }
        );
    }
}

public class Delegate2In2OutInvoke<TIn1, TIn2, TOut1,TOut2> : Invoke<TOut1>
{
    public Delegate2In2OutInvoke(
        NodeContext nodeContext, 
        Delegate2In2Out<TIn1, TIn2, TOut1,TOut2> theDelegate, 
        ShaderNode<TIn1> theInput1,
        ShaderNode<TIn2> theInput2) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode> { theInput1, theInput2})
    {
        
    }
}