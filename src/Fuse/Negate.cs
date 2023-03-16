using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    public class Negate<T> : ResultNode<T>
    {
        private readonly ShaderNode<T> _in;

        public Negate(NodeContext nodeContext, ShaderNode<T> theIn) : base(nodeContext, "negate")
        {
            _in = theIn ??  Default;
            SetInputs( new List<AbstractShaderNode>{_in});
        }
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("-${in}", 
                new Dictionary<string, string>
                {
                    {"in", _in.ID},
                });
        }
    }
}