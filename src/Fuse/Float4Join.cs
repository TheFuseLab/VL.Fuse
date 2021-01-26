using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float4Join : ShaderNode<Vector4>
    {

        public Float4Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z, GpuValue<float> w) : base("Float4Join")
        {
            const string shaderCode = "float4 ${resultName} = float4(${x},${y},${z},${w});";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"x", x ?? new ConstantValue<float>(0)},
                {"y", y ?? new ConstantValue<float>(0)},
                {"z", z ?? new ConstantValue<float>(0)},
                {"w", w ?? new ConstantValue<float>(1)}
            };
            Setup(shaderCode, inputs);
        }
    }
}