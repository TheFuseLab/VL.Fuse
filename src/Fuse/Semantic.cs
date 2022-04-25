using System.Collections.Generic;

namespace Fuse
{
    
    public class Semantic<T> : ShaderNode<T>
    {
        
        public Semantic (string theSemantic) : base("Semantic")
        {
            Name = theSemantic;
            Setup(new List<AbstractShaderNode>() );
        }
        
        public override string ID => "streams." + Name;

        protected override string SourceTemplate()
        {
            return "";
        }
    }
}