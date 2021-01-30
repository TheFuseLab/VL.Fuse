using System.Collections.Generic;

namespace Fuse
{
    
    public class FromFloat<T> : ShaderNode<T>
    {

        public FromFloat(GpuValue<float> x) : base("fromFloat")
        {
            x = x ?? new ConstantValue<float>(0);

            var code = "";
            switch (TypeHelpers.GetDimension(typeof(T)))
            {
                case 1:
                    code = "float ${resultName} = ${x};";
                    break;
                case 2:
                    code = "float2 ${resultName} = float2(${x},${x});";
                    break;
                case 3:
                    code = "float3 ${resultName} = float3(${x},${x},${x});";
                    break;
                case 4:
                    code = "float4 ${resultName} = float4(${x},${x},${x},${x});";
                    break;
            }
            
            Setup(
                code, 
                new List<AbstractGpuValue>{x},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                });
        }
    }
}