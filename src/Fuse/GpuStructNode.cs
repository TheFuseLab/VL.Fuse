using System.Collections.Generic;

namespace Fuse
{
    public class GpuStructNode<T> : ShaderNode<T>
    {
        
        public GpuStructNode(ShaderNode<T> theStruct, string theStructName) : base("GpuStruct")
        {
            theStruct.TypeOverride = theStructName;
            SetInputs(new List<AbstractShaderNode>());
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