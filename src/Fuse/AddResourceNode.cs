using System.Collections;
using System.Collections.Generic;

namespace Fuse
{
    public class AddResourceNode<T> : ShaderNode<T>
    {
        
        public AddResourceNode(GpuValue<T> theIn, string theResourceId, IList theResources) : base("AddResource")
        {
            Setup(new List<AbstractGpuValue>{theIn});
            AddResources(theResourceId, theResources);

            Output = new GpuValuePassThrough<T>(theIn);
            Output.ParentNode = this;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
    }
}