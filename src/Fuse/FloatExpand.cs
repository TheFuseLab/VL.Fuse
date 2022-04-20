using System.Collections.Generic;

namespace Fuse
{
    public class FloatExpand<TIn,TOut> : ShaderNode<TOut>
    {
        private readonly GpuValue<TIn> _in;
        private readonly GpuValue<float> _expand;

        public FloatExpand(GpuValue<TIn> x, GpuValue<float> y) : base("floatExpand")
        {
            _in = x ?? ConstantHelper.FromFloat<TIn>(0);
            _expand = y ?? new ConstantValue<float>(0);
            
            Setup( new List<AbstractGpuValue>{_in,_expand});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${resultType}(${in},${expand});", 
                new Dictionary<string, string>
                {
                    {"in", _in.ID},
                    {"expand", _expand.ID}
                });
        }
    }
}