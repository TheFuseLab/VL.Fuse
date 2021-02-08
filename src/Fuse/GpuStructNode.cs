using System.Collections.Generic;

namespace Fuse
{
    public class GpuStructNode : ShaderNode<GpuStruct>
    {
        
        public GpuStructNode(GpuValue<GpuStruct> theStruct, string theStructName) : base("GpuStruct")
        {
            theStruct.TypeOverride = theStructName;
            Setup(new List<AbstractGpuValue>{});

            Output = theStruct;
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