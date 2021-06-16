using System.Collections.Generic;

namespace Fuse
{
    public class ReferenceNode<T> : ShaderNode<T>
    { 
        
        public ReferenceNode(GpuValue<T> theIn) : base("Reference")
        {
            Setup(new List<AbstractGpuValue>{theIn});

            
            Output = theIn == null ? new GpuValue<T>(""):new GpuValuePassThrough<T>(theIn);
            Output.ParentNode = this;
        }

        public void SetInput(GpuValue<T> theIn)
        {
            Setup(new List<AbstractGpuValue>{theIn});

            
            Output = theIn == null ? new GpuValue<T>(""):new GpuValuePassThrough<T>(theIn);
            Output.ParentNode = this;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
    }
}