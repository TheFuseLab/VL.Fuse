using System;
using System.Collections.Generic;
using Fuse.Domain;
using Fuse.function;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate2In1OutUpdate<TIn1, TIn2,TOut>( object stateInput, out object stateOutput, ShaderNode<TIn1> Input1, ShaderNode<TIn2> Input2, out ShaderNode<TOut> Output);
    
public delegate void Delegate2In1OutCreate( out object stateOutput);

public class Delegate2In1Out<TIn1, TIn2,TOut> : DelegateStateful<Delegate<TOut>>
{

    public Delegate2In1Out(NodeContext theNodeContext) : base(theNodeContext)
    { 
    }

    public void Update(Delegate2In1OutCreate create, Delegate2In1OutUpdate<TIn1, TIn2,TOut> update)
    {
        if(State == null)create(out State);
        
        var nodeSubContextFactory = new NodeSubContextFactory(NodeContext);

        var input1Parameter = new FunctionParameter<TIn1>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);
        var input2Parameter = new FunctionParameter<TIn2>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,1);

        //update(State,out State, new FunctionParameter<MarchSurface>())
        update(State, out State, input1Parameter, input2Parameter, out var outArgument);

        Delegate = new Delegate<TOut>(
            nodeSubContextFactory.NextSubContext(),
            outArgument,
            new List<IFunctionParameter>
            {
                input1Parameter,
                input2Parameter
            }
        );
    }
}

public class Delegate2In1OutInvoke<TIn1, TIn2,TOut> : Invoke<TOut>
{
    public Delegate2In1OutInvoke(
        NodeContext nodeContext, 
        Delegate2In1Out<TIn1, TIn2,TOut> theDelegate, 
        ShaderNode<TIn1> theInput1,
        ShaderNode<TIn2> theInput2) : base(nodeContext,theDelegate.Delegate, new List<AbstractShaderNode> { theInput1, theInput2})
    {
    }
}