using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float3Join : ShaderNode<Vector3>
    {

        public Float3Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z) : base("Float3Join")
        {
            const string shaderCode = "float3 ${resultName} = float3(${x},${y},${z});";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"x", x ?? new ConstantValue<float>(0)},
                {"y", y ?? new ConstantValue<float>(0)},
                {"z", z ?? new ConstantValue<float>(0)}
            };
            Setup(shaderCode, inputs);
        }
    }
}