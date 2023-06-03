using System;
using System.Collections.Generic;
using Fuse.function;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate0In1OutUpdate<TOut>( object stateInput, out object stateOutput, out ShaderNode<TOut> Output);
    
public delegate void Delegate0In1OutCreate( out object stateOutput);

public class Delegate0In1Out<TOut> : DelegateStateful<Delegate<TOut>>
{

    public Delegate0In1Out(NodeContext theNodeContext) : base(theNodeContext)
    { 
    }

    public void Update(Delegate0In1OutCreate create, Delegate0In1OutUpdate<TOut> update)
    {
        if(State == null)create(out State);
        
        var nodeSubContextFactory = new NodeSubContextFactory(NodeContext);

        update(State, out State, out var outArgument);

        Delegate = new Delegate<TOut>(
            nodeSubContextFactory.NextSubContext(),
            outArgument,
            new List<IFunctionParameter>());
    }
}

public class Delegate0In1OutInvoke<TOut> : Invoke<TOut>
{
    public Delegate0In1OutInvoke(
        NodeContext nodeContext, 
        Delegate0In1Out<TOut> theDelegate) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode>())
    {
        
    }
}