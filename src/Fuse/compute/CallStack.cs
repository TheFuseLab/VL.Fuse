using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
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