using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    public class Negate<T> : ShaderNode<T>
    {
        private ShaderNode<T> _in;

        public Negate(NodeContext nodeContext) : base(nodeContext, "negate")
        {
            _in = Default;
            
            SetInputs( new List<AbstractShaderNode>{_in});
        }

        public void SetInput(ShaderNode<T> theIn)
        {
            _in = theIn ??  Default;
            
            SetInputs( new List<AbstractShaderNode>{_in});
        }
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = -${in};", 
                new Dictionary<string, string>
                {
                    {"in", _in.ID},
                });
        }
    }
}