using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class OperatorNode<T> : ShaderNode
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${Call};";

        public GPUValue<T> Output { get; }

        public OperatorNode(IEnumerable<AbstractGPUValue> inputs, string theOperator) : base()
        {
            Output = new GPUValue<T>("result");

            var gpuValues = inputs.ToList();
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                {"Call",BuildCall(gpuValues,theOperator)}
            });
           Setup(sourceCode, ShaderNodesUtil.BuildInputs(gpuValues),new Dictionary<string, AbstractGPUValue> {{"result", Output}});
        }

        private static string BuildCall(IEnumerable<AbstractGPUValue> inputs, string theOperator)
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