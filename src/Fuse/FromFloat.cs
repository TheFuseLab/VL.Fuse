using System.Collections.Generic;

namespace Fuse
{
    
    public class FromFloat<T> : ShaderNode<T> where T : struct
    {

        private ShaderNode<float> _x;
        
        public FromFloat(ShaderNode<float> x) : base("fromFloat")
        {
            _x = x ?? new ConstantValue<float>(0);
            
            Setup(new List<AbstractShaderNode>{x});
        }

        protected override string SourceTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(T)) switch
            {
                1 => "float ${resultName} = ${x};",
                2 => "float2 ${resultName} = float2(${x},${x});",
                3 => "float3 ${resultName} = float3(${x},${x},${x});",
                4 => "float4 ${resultName} = float4(${x},${x},${x},${x});",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"x", _x.ID}
                });
        }
    }
}