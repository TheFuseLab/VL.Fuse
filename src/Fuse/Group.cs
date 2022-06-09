using System.Collections.Generic;
using Fuse.compute;

namespace Fuse
{
    public class Group: ShaderNode<GpuVoid> 
    {
        public Group(IEnumerable<AbstractShaderNode> theInputs, string name = "Group") : base(name)
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