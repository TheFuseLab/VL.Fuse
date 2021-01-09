using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class Float3Join : ShaderNode<Vector3>
    {
        private const string ShaderCode = "float3 ${resultName} = float3(${x},${y},${z});";

        public Float3Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z) : base("Float3Join")
        {

            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"x", x == null ? "0" : x.ID},
                {"y", y == null ? "0" : y.ID},
                {"z", z == null ? "0" : z.ID}
            });
            Setup(sourceCode, ShaderNodesUtil.BuildInputs(x,y,z));
        }
    }
}