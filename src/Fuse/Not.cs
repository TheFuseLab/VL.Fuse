using System.Collections.Generic;

namespace Fuse
{
    public class Not: ShaderNode<bool>
    {
        private readonly GpuValue<bool> _in;

        public Not(GpuValue<bool> theIn) : base("not")
        {
            _in = theIn ??  new ConstantValue<bool>(false);
            
            Setup( new List<AbstractGpuValue>{_in});
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