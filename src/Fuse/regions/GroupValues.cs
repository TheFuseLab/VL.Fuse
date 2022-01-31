using System.Collections.Generic;
using Fuse.compute;

namespace Fuse.regions
{
    public class GroupValues: ShaderNode<GpuVoid> 
    {
        public GroupValues(IEnumerable<AbstractGpuValue> theInputs) : base("CallStack")
        { 
            
            var abstractGpuValues = new List<AbstractGpuValue>();
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