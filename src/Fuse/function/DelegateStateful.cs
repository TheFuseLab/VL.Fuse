using System;
using System.Collections.Generic;
using Fuse.Function;
using VL.Core;

namespace Fuse.function;

public abstract class DelegateStateful<TDelegateType> : IDisposable, IDelegateProvider where TDelegateType : IDelegate
{
    protected object State;

    protected readonly NodeContext NodeContext;

    public TDelegateType Delegate { get; protected set; }

    protected DelegateStateful(NodeContext theNodeContext) 
    {
        NodeContext = theNodeContext;  
    }

    public void Dispose()
    {
        if(State is IDisposable disposable) disposable.Dispose();
    }

    public IDelegate GetDelegate()
    {
        return Delegate;
    }
}