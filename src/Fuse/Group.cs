using System.Collections.Generic;
using Fuse.compute;

namespace Fuse.regions
{
    public class GroupValues: ShaderNode<GpuVoid> 
    {
        public GroupValues(IEnumerable<AbstractShaderNode> theInputs) : base("Group")
        { 
            
            var abstractGpuValues = new List<AbstractShaderNode>();
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