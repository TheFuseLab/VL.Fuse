using System.Collections.Generic;
using Stride.Core.Extensions;

namespace Fuse.compute
{
    public class GroupVoid: ShaderNode<GpuVoid> 
    {
        public GroupVoid(IEnumerable<GpuValue<GpuVoid>> theInputs) : base("GroupVoid")
        { 
            
            var abstractGpuValues = new List<GpuValue<GpuVoid>>();
            theInputs.ForEach(input =>
            {
                if (input == null) return;
                abstractGpuValues.Add(input);
            });
            
            Setup(abstractGpuValues);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    
}