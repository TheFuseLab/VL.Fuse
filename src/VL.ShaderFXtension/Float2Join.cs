using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{
    public class Float2Join : ShaderNode
    {
        private const string ShaderCode = "float2 ${resultName} = float2(${x},${y});";

        public GpuValue<Vector2> Output { get; }
        
        public Float2Join(GpuValue<float> x, GpuValue<float> y) : base()
        {
            Output = new GpuValue<Vector2>("result");

            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"x", x == null ? "0" : x.ID},
                {"y", y == null ? "0" : y.ID}
            });
            Setup(sourceCode, ShaderNodesUtil.BuildInputs(x,y),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }
    }
}