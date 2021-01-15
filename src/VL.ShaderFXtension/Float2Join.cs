using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class Float2Join : ShaderNode<Vector2>
    {

        public Float2Join(GpuValue<float> x, GpuValue<float> y) : base("float2Join")
        {
            const string shaderCode = "float2 ${resultName} = float2(${x},${y});";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"x", x ?? new ConstantValue<float>(0)},
                {"y", y ?? new ConstantValue<float>(0)}
            };
            Setup(shaderCode, inputs);
        }
    }
}