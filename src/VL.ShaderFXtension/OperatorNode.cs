using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class OperatorNode<TIn, TOut> : ShaderNode<TOut>
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${Call};";

        public OperatorNode(IEnumerable<GpuValue<TIn>> inputs, ConstantValue<TOut> theDefault, string theOperator) : base("Operator", theDefault)
        {
            var gpuValues = System.Linq.Enumerable.ToList(inputs);
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<TOut>().ToLower()},
                {"Call",BuildCall(gpuValues,theOperator)}
            });
           Setup(sourceCode, ShaderNodesUtil.BuildInputs(gpuValues));
        }

        public OperatorNode(GpuValue<TIn> input0, GpuValue<TIn> input1, ConstantValue<TOut> theDefault, string theOperator) :
            this(new List<GpuValue<TIn>> {input0, input1}, theDefault, theOperator){
        }

        private string BuildCall(IEnumerable<AbstractGpuValue> inputs, string theOperator)
        {
            var abstractGpuValues = inputs.ToList();
            var hasNullValue = false;
            abstractGpuValues.ForEach(input =>
            {
                if (input == null) hasNullValue = true;
            });
            if (hasNullValue)
            {
                return Default.ID;
            }
            
            var stringBuilder = new StringBuilder();
            abstractGpuValues.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(" " + theOperator + " ");
            });
            if(stringBuilder.Length > 3)stringBuilder.Remove(stringBuilder.Length - 3, 3);
            return stringBuilder.ToString();
        }
    }
    
    
}