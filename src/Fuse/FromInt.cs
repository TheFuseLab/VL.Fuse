using System.Collections.Generic;

namespace Fuse
{
    public class FromInt<T> : ShaderNode<T> where T : struct
    {
        
        private GpuValue<int> _x;

        public FromInt(GpuValue<int> x) : base("fromInt")
        {
            _x = x ?? new ConstantValue<int>(0);
            
            Setup(new List<AbstractGpuValue>{x});
        }

        protected override string SourceTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(T)) switch
            {
                1 => "int ${resultName} = ${x};",
                2 => "int2 ${resultName} = int2(${x},${x});",
                3 => "int3 ${resultName} = int3(${x},${x},${x});",
                4 => "int4 ${resultName} = int4(${x},${x},${x},${x});",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, new Dictionary<string, string> {{"x", _x.ID}});
        }
    }
}