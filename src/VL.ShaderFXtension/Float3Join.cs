using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class Float3Join : ShaderNode
    {
        private const string ShaderCode = "float3 ${resultName} = float3(${x},${y},${z});";

        public GpuValue<Vector3> Output { get; }
        
        public Float3Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z) : base("Float3Join")
        {
            Output = new GpuValue<Vector3>("result");

            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"x", x == null ? "0" : x.ID},
                {"y", y == null ? "0" : y.ID},
                {"z", z == null ? "0" : z.ID}
            });
            Setup(sourceCode, ShaderNodesUtil.BuildInputs(x,y,z),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }
    }
}