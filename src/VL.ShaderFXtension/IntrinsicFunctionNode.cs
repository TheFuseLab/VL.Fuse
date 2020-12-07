using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class IntrinsicFunctionNode<T> : ShaderNode
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${function}(${arguments});";

        public GPUValue<T> Output { get; }

        public IntrinsicFunctionNode(IEnumerable<AbstractGPUValue> inputs, string theFunction) : base()
        {
            Output = new GPUValue<T>("result");

            var gpuValues = inputs.ToList();
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                {"function",theFunction},
                {"arguments",BuildArguments(gpuValues)}
            });
           Setup(sourceCode, ShaderNodesUtil.BuildInputs(gpuValues),new Dictionary<string, AbstractGPUValue> {{"result", Output}});
        }

        private static string BuildArguments(IEnumerable<AbstractGPUValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(", ");
            });
            stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
    }
    
    
}