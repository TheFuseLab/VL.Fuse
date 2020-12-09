using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class CustomFunctionNode<T> : ShaderNode
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${function}(${arguments});";

        private IEnumerable<string> _myMixins = new List<string>();

        public GpuValue<T> Output { get; }

        public CustomFunctionNode(IEnumerable<AbstractGpuValue> inputs, string theFunctionName, IEnumerable<string> theMixins) : base()
        {
            Output = new GpuValue<T>("result");
            _myMixins = theMixins;

            var gpuValues = inputs.ToList();
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                {"function",theFunctionName},
                {"arguments",BuildArguments(gpuValues)}
            });
            Setup(sourceCode, ShaderNodesUtil.BuildInputs(gpuValues),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }

        private static string BuildArguments(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public override IEnumerable<string> MixIns => _myMixins;
    }
}