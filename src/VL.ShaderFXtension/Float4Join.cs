using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class Float4Join : ShaderNode
    {
        private const string ShaderCode = "float4 ${resultName} = float4(${x},${y},${z},${w});";

        public GpuValue<Vector4> Output { get; }
        
        public Float4Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z, GpuValue<float> w) : base("Float4Join")
        {
            Output = new GpuValue<Vector4>("result");

            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"x", x == null ? "0" : x.ID},
                {"y", y == null ? "0" : y.ID},
                {"z", z == null ? "0" : z.ID},
                {"w", w == null ? "1" : w.ID}
            });
            Setup(sourceCode, ShaderNodesUtil.BuildInputs(x,y,z,w),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }
    }
}