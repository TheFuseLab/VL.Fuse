using System.Collections.Generic;
using Fuse.function;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate1In1OutUpdate<TIn, TOut>( object stateInput, out object stateOutput, ShaderNode<TIn> Input, out ShaderNode<TOut> Output);
    
public delegate void Delegate1In1OutCreate( out object stateOutput);

public class Delegate1In1Out<TIn, TOut> : DelegateStateful<Delegate<TOut>>
{

    public Delegate1In1Out(NodeContext theNodeContext) : base(theNodeContext)
    {
    }

    public void Update(Delegate1In1OutCreate create, Delegate1In1OutUpdate<TIn, TOut> update)
    {
        if(State == null)create(out State);
        
        var nodeSubContextFactory = new NodeSubContextFactory(NodeContext);

        var inputParameter = new FunctionParameter<TIn>(nodeSubContextFactory.NextSubContext(), null, InputModifier.In,0);

        update(State, out State, inputParameter, out var outArgument);

        Delegate = new Delegate<TOut>(
            nodeSubContextFactory.NextSubContext(),
            outArgument,
            new List<IFunctionParameter>
            {
                inputParameter
            }
        );
    }
}

public class Delegate1In1OutInvoke<TIn, TOut> : Invoke<TOut>
{
    public Delegate1In1OutInvoke(
        NodeContext nodeContext, 
        Delegate1In1Out<TIn, TOut> theDelegate, 
        ShaderNode<TIn> theInput) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode> { theInput})
    {
        
    }
}