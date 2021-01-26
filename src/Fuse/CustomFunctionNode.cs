using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
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
        
        public CustomFunctionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theFunction, string theCodeTemplate, ConstantValue<T> theDefault, IEnumerable<string> theArguments = null, IEnumerable<string> theMixins = null, IDictionary<string,string> theFunctionValues = null) : base(theFunction, theDefault)
        {
            var signature = theFunction + TypeHelpers.GetNameForType<T>();
            MixIns = theMixins??new List<string>();
            Functions = new Dictionary<string, string>();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"signature", signature}
            };
            inputs.ForEach(input =>
            {
                if (input.Value == null) return;
                var inputType =  input.Value.ParentNode.GetType();
                if (inputType.GetGenericTypeDefinition() == typeof(DelegateNode<>))
                {
                    var DelegateNode = input.Value.ParentNode as IDelegateNode;
                    functionValueMap.Add(input.Key, DelegateNode.FunctionName);
                }
            });
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderTemplateEvaluator.Evaluate(theCodeTemplate, functionValueMap) + Environment.NewLine);

            var shaderCode = "${resultType} ${resultName} = ${function}(" +  BuildArguments(theArguments??inputs.Keys) +");";
            var valueMap = new Dictionary<string, string>
            {
                {"function", signature}
            };
            Setup(shaderCode, inputs, valueMap);
        }
        
        public static string BuildArguments(IEnumerable<string> theArguments)
        {
            var stringBuilder = new StringBuilder();
            theArguments.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append("${"+input+"}");
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public sealed override IDictionary<string, string> Functions { get; }
        public sealed override IEnumerable<string> MixIns { get; }
    }
}