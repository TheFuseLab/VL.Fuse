using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    
    public class Semantic<T> : ShaderNode<T>
    {
        //;
        
        public Semantic(NodeContext nodeContext, string theName, bool define = false, string theSemantic = null) : base(nodeContext, "Semantic")
        {
            Name = theName;
            HasFixedName = true;
            SetInputs(new List<AbstractShaderNode>());

            if (!define) return;
            
            if(theSemantic == null){
                const string shaderCode = "    stage stream ${type} ${name};";
                var streamString = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
                {
                    {"type", TypeName()},
                    {"name", theName}
                });
            
                SetProperty(Streams, streamString);
                return;
            }
            
            const string shaderCodeSemantic = "    stage stream ${type} ${name} : ${semantic};";
            
            var semanticString = ShaderNodesUtil.Evaluate(shaderCodeSemantic,new Dictionary<string, string>()
            {
                {"type", TypeName()},
                {"name", theName},
                {"semantic", theSemantic}
            });
            
            SetProperty(Streams, semanticString);
        }

        public sealed override string TypeName()
        {
            return base.TypeName();
        }

        public override string ID => "streams." + Name;

        public override string DelegateID => base.ID;

        protected override string SourceTemplate()
        {
            return "";
        }
    }
}