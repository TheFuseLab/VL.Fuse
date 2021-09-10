using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;

namespace Fuse.regions
{
    public sealed class IfRegionNode : FuseRegion
    {
        private readonly GpuValue<bool> _inCheck;
        
        public IfRegionNode(
            GpuValue<bool> inCheck, 
            List<AbstractGpuValue> theRegionInputs, 
            List<AbstractGpuValue> theOutputs
        ) :base (theRegionInputs, theOutputs)
        {
            _inCheck = inCheck;
        }

    }
}