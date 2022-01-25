using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;

namespace Fuse
{
    /*
    public class EmptyVoid : GpuValue<GpuVoid>
    {
        
        public EmptyVoid() : base("constant")
        {
        }

        
        public override string ID => "";
    }*/
    
    public class EmptyVoid : ShaderNode<GpuVoid>
    {
    
        public EmptyVoid() : base( "void")
        {
            
            Setup(new List<AbstractGpuValue>(){null});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public class ReturnVoid : ShaderNode<GpuVoid>
    {
    
        public ReturnVoid() : base( "returnVoid")
        {
            
            Setup(new List<AbstractGpuValue> {null});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "return;";
        }

        protected override string SourceTemplate()
        {
            return "return;";
        }
    }
}