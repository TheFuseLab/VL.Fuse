using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;

namespace Fuse.regions;

public interface IForRegionInlay
{
    void Update(ShaderNode<int> index);
}

[ProcessNode]
public class ForRegion2 : FuseRegionBase<IForRegionInlay>
{
    private ShaderNode<int> _end;
    private readonly NodeContext _nodeContext;

    public ForRegion2(NodeContext nodeContext)
    {
        _nodeContext = nodeContext;
    }
    
    public void Update(ShaderNode<int> end)
    {
        if (end != _end)
        {
            _end = end;
            
            //new ForRegion(_nodeContext,)
        }
    }
}