using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float2Join : ShaderNode<Vector2>
    {

        public Float2Join(GpuValue<float> x, GpuValue<float> y) : base("float2Join")
        {
            x = x ?? new ConstantValue<float>(0);
            y = y ?? new ConstantValue<float>(0);
            
            Setup(
                "float2 ${resultName} = float2(${x},${y});", 
                new List<AbstractGpuValue>{x,y},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                    {"y", y.ID}
                }
            );
        }
    }
}