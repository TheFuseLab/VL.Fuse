using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class OperatorNode<T> : ShaderNode
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${Call};";

        public GpuValue<T> Output { get; }

        public OperatorNode(IEnumerable<AbstractGpuValue> inputs, string theOperator) : base("Operator")
        {
            Output = new GpuValue<T>("result");

            var gpuValues = inputs.ToList();
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                {"Call",BuildCall(gpuValues,theOperator)}
            });
           Setup(sourceCode, ShaderNodesUtil.BuildInputs(gpuValues),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }

        private static string BuildCall(IEnumerable<AbstractGpuValue> inputs, string theOperator)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(" " + theOperator + " ");
            });
            stringBuilder.Remove(stringBuilder.Length - 3, 3);
            return stringBuilder.ToString();
        }
    }
    
    
}