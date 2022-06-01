using System.Collections.Generic;
using Stride.Core.Extensions;

namespace Fuse.compute
{
    public class GroupVoid: ShaderNode<GpuVoid> 
    {
        public GroupVoid(IEnumerable<ShaderNode<GpuVoid>> theInputs) : base("GroupVoid")
        { 
            
            var abstractGpuValues = new List<ShaderNode<GpuVoid>>();
            theInputs.ForEach(input =>
            {
                if (input == null) return;
                abstractGpuValues.Add(input);
            });
            
            SetInputs(abstractGpuValues);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    
}