using System.Collections.Generic;

namespace Fuse
{
    public class Negate<T> : ShaderNode<T>
    {
        private readonly GpuValue<T> _in;

        public Negate(GpuValue<T> theIn) : base("negate")
        {
            _in = theIn ??  ConstantHelper.FromFloat<T>(0);
            
            Setup( new List<AbstractGpuValue>{_in});
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