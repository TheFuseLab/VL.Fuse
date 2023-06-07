using System;
using System.Collections.Generic;
using Fuse.compute;
using Fuse.function;
using VL.Core;

namespace Fuse.Function;


public delegate void Delegate0In0OutUpdate<TOut>( object stateInput, out object stateOutput, out ShaderNode<TOut> Output);
    
public delegate void Delegate0In0OutCreate( out object stateOutput);

public class Delegate0In0Out<TOut> : DelegateStateful<Delegate<TOut>>
{

    public Delegate0In0Out(NodeContext theNodeContext) : base(theNodeContext)
    { 
    }

    public void Update(Delegate0In0OutCreate create, Delegate0In0OutUpdate<TOut> update)
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

public class Delegate0In0OutInvoke<TOut> : Invoke<TOut>
{
    public Delegate0In0OutInvoke(
        NodeContext nodeContext, 
        Delegate0In0Out<TOut> theDelegate) : base(nodeContext,theDelegate?.Delegate, new List<AbstractShaderNode>())
    {
        
    }
}