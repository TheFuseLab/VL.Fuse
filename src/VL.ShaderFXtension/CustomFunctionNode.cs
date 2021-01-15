using System.Collections.Generic;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class CustomFunctionNode<T>: ShaderNode<T>
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${function}(${arguments});";
        
        public CustomFunctionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theFunction, string theCodeTemplate, ConstantValue<T> theDefault) : base(theFunction, theDefault)
        {
            
            var hasNullValue = false;
            inputs.ForEach(kv =>
            {
                if (kv.Value == null) hasNullValue = true;
            });
            
            var myCode = ShaderCode;
            if (hasNullValue)
            {
                var myKeyMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                    {"default", theDefault.ID}
                };
                myCode = ShaderTemplateEvaluator.Evaluate(DefaultShaderCode, myKeyMap);
            }
            else
            {
                myCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                    {"function",theFunction},
                    {"arguments",ShaderNodesUtil.BuildArguments(inputs)}
                });
            }
            Setup(myCode, inputs);
        }
        
        
    }
}