using System;
using System.Collections.Generic;
using System.Linq;
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

    public class CustomFunctionNode<T>: AbstractDelegateNode<T>
    {
        
        public CustomFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            string theCodeTemplate, 
            ConstantValue<T> theDefault, 
            IDictionary<string,AbstractGpuValue> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null
        ) : base(theFunction, theDefault)
        {
            var arguments = theArguments.ToList();
            var signature = theFunction + GetHashCode();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"signature", signature}
            };

            var inputs = new List<AbstractGpuValue>(arguments);
            Ins = inputs;
            HandleDelegates(theDelegates,inputs,functionValueMap);

            theCodeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderTemplateEvaluator.Evaluate(theCodeTemplate, functionValueMap) + Environment.NewLine);
            
            Setup(
                "${resultType} ${resultName} = ${function}(${arguments});", 
                inputs, 
                new Dictionary<string, string> {
                    {"function", signature},
                    {"arguments", ShaderNodesUtil.BuildArguments(arguments)}
                }
            );
        }

        

        private void HandleDelegates(IDictionary<string,AbstractGpuValue> theDelegates, List<AbstractGpuValue> theInputs, Dictionary<string, string> theFunctionValueMap)
        {
            theDelegates?.ForEach(kv =>
            {
                var delegateNode = kv.Value;
                if (delegateNode == null) return;
                var functionName = kv.Key + "Delegate"+delegateNode.GetHashCode();
                theFunctionValueMap.Add(kv.Key, functionName);
                AddDelegate(functionName,delegateNode);
            });
        }
    }
}