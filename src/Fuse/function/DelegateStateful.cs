using System;
using Fuse.Function;
using VL.Core;

namespace Fuse.function;

public abstract class DelegateStateful<TDelegateType> : IDisposable, IDelegateProvider where TDelegateType : IDelegate
{
    protected readonly NodeContext NodeContext;
    protected object State;

    protected DelegateStateful(NodeContext theNodeContext)
    {
        NodeContext = theNodeContext;
    }

    public TDelegateType Delegate { get; protected set; }

    public IDelegate GetDelegate()
    {
        return Delegate;
    }

    public void Dispose()
    {
        if (State is IDisposable disposable) disposable.Dispose();
    }
}