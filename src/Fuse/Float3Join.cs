using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float3Join : ShaderNode<Vector3>
    {

        public Float3Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z) : base("Float3Join")
        {
            
            x = x ?? new ConstantValue<float>(0);
            y = y ?? new ConstantValue<float>(0);
            z = z ?? new ConstantValue<float>(0);
            
            Setup(
                "float3 ${resultName} = float3(${x},${y},${z});", 
                new List<AbstractGpuValue>{x,y,z},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                    {"y", y.ID},
                    {"z", z.ID}
                }
            );
        }
    }
}