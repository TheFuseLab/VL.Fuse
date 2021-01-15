using System.Collections.Generic;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    /*
     *
     * void BlendHardLightFloat4(float4 Base, float4 Blend, float Opacity, out float4 Out)
{
    float4 result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
    float4 result2 = 2.0 * Base * Blend;
    float4 zeroOrOne = step(Blend, 0.5);
    Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    Out = lerp(Base, Out, Opacity);
}

${resultType} ${signature}(${resultType} Base, ${resultType} Blend, float Opacity)
{
    ${resultType} result1 = 1.0 - 2.0 * (1.0 - Base) * (1.0 - Blend);
    ${resultType} result2 = 2.0 * Base * Blend;
    ${resultType} zeroOrOne = step(Blend, 0.5);
    Out = result2 * zeroOrOne + (1 - zeroOrOne) * result1;
    Out = lerp(Base, Out, Opacity);
}
     */
    public class CustomFunctionNode<T>: ShaderNode<T>
    {
        
        public CustomFunctionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theFunction, string theCodeTemplate, ConstantValue<T> theDefault, IEnumerable<string> theMixins, IDictionary<string,string> theFunctionValues = null) : base(theFunction, theDefault)
        {
            var signature = theFunction + TypeHelpers.GetNameForType<T>();
            MixIns = theMixins;
            Functions = new Dictionary<string, string>();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"signature", signature}
            };
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderTemplateEvaluator.Evaluate(theCodeTemplate, functionValueMap));
            
            const string shaderCode = "${resultType} ${resultName} = ${function}(${arguments});";
            var valueMap = new Dictionary<string, string>
            {
                {"function", signature},
                {"arguments", ShaderNodesUtil.BuildArguments(inputs)}
            };
            Setup(shaderCode, inputs, valueMap);
        }
        
        public sealed override IDictionary<string, string> Functions { get; }
        public sealed override IEnumerable<string> MixIns { get; }
    }
}