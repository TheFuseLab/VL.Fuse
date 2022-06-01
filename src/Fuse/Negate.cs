using System.Collections.Generic;

namespace Fuse
{
    public class Negate<T> : ShaderNode<T>
    {
        private readonly ShaderNode<T> _in;

        public Negate(ShaderNode<T> theIn) : base("negate")
        {
            _in = theIn ??  ConstantHelper.FromFloat<T>(0);
            
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