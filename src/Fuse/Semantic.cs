using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    
    public class Semantic<T> : ShaderNode<T>
    {
        
        public Semantic(NodeContext nodeContext, string theSemantic) : base(nodeContext, "Semantic")
        {
            Name = theSemantic;
            SetInputs(new List<AbstractShaderNode>() );
        }
        
        public override string ID => "streams." + Name;

        protected override string SourceTemplate()
        {
            return "";
        }
    }
}