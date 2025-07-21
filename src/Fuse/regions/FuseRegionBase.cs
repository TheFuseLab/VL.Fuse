#nullable  enable
using System;
using System.Collections.Generic;

namespace Fuse.regions;

using VL.Core.PublicAPI;

public abstract class FuseRegionBase<T> : IRegion<T>
{
    protected Func<T>? _patchInlayFactory;
    protected readonly Dictionary<InputDescription, object>  _inputValues = new();
    protected readonly Dictionary<OutputDescription, object> _outputValues = new();
    
    public virtual void AcknowledgeInput(in InputDescription description, object outerValue)
    {
        _inputValues[description] = outerValue;
    }

    public virtual void AcknowledgeOutput(in OutputDescription description, T patchInlay, object innerValue)
    {
        _outputValues[description] = innerValue;
    }

    public virtual void RetrieveInput(in InputDescription description, T patchInlay, out object innerValue)
    {
        _inputValues.TryGetValue(description, out innerValue);
    }

    public virtual void RetrieveOutput(in OutputDescription description, out object outerValue)
    {
        _outputValues.TryGetValue(description, out outerValue);
    }

    public virtual void SetPatchInlayFactory(Func<T> patchInlayFactory)
    {
        this._patchInlayFactory = patchInlayFactory;
    }
}