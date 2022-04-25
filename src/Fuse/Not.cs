using System.Collections.Generic;

namespace Fuse
{
    public class Not: ShaderNode<bool>
    {
        private readonly ShaderNode<bool> _in;

        public Not(ShaderNode<bool> theIn) : base("not")
        {
            _in = theIn ??  new ConstantValue<bool>(false);
            
            Setup( new List<AbstractShaderNode>{_in});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = !${in};", 
                new Dictionary<string, string>
                {
                    {"in", _in.ID},
                });
        }
    }
}