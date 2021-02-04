using System.Collections.Generic;

namespace Fuse
{
    
    public class FromFloat<T> : ShaderNode<T>
    {

        public FromFloat(GpuValue<float> x) : base("fromFloat")
        {
            x = x ?? new ConstantValue<float>(0);

            
            
            Setup(
                new List<AbstractGpuValue>{x},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                });
        }

        protected override string SourceTemplate()
        {
            switch (TypeHelpers.GetDimension(typeof(T)))
            {
                case 1:
                    return "float ${resultName} = ${x};";
                case 2:
                    return "float2 ${resultName} = float2(${x},${x});";
                case 3:
                    return "float3 ${resultName} = float3(${x},${x},${x});";
                case 4:
                    return "float4 ${resultName} = float4(${x},${x},${x},${x});";
            }

            return "";
        }
    }
}