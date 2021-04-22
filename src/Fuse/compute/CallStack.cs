using System.Collections.Generic;
using Stride.Core.Extensions;

namespace Fuse.compute
{
    public class CallStack: ShaderNode<GpuVoid> 
    {
        public CallStack(IEnumerable<GpuValue<GpuVoid>> theInputs, string theOperator) : base("CallStack")
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