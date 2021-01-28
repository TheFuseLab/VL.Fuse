using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float4Join : ShaderNode<Vector4>
    {

        public Float4Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z, GpuValue<float> w) : base("Float4Join")
        {

            x = x ?? new ConstantValue<float>(0);
            y = y ?? new ConstantValue<float>(0);
            z = z ?? new ConstantValue<float>(0);
            w = w ?? new ConstantValue<float>(1);
            
            Setup(
                "float4 ${resultName} = float4(${x},${y},${z},${w});", 
                new List<AbstractGpuValue>{x,y,z,w},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                    {"y", y.ID},
                    {"z", z.ID},
                    {"w", w.ID}
                }
            );
        }
    }
}