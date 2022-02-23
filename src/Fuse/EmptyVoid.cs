using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;

namespace Fuse
{
    public class SimpleKeyword : ShaderNode<GpuVoid>
    {

        private string _keyword;
    
        public SimpleKeyword(string theKeyword) : base( "void")
        {
            _keyword = theKeyword;
            Setup(new List<AbstractGpuValue>{null});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return _keyword;
        }

        protected override string SourceTemplate()
        {
            return _keyword;
        }
    }
    
    public class EmptyVoid : SimpleKeyword
    {
    
        public EmptyVoid() : base( "")
        {
        }
    }
    
    public class ReturnVoid : SimpleKeyword
    {
    
        public ReturnVoid() : base( "return;")
        {
        }
    }
    
    public class BreakVoid : SimpleKeyword
    {
    
        public BreakVoid() : base( "break;")
        {
        }
    }
}