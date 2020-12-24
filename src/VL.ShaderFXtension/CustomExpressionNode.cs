using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class CustomExpressionNode<T> : ShaderNode
    {

        public GpuValue<T> Output { get; }

        public CustomExpressionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theTemplate, OrderedDictionary<string,string> theParameters) : base("expression")
        {
            Output = new GpuValue<T>("result");

            var myKeyMap = new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()}
            };
            
            theParameters.ForEach(param => myKeyMap.Add(param.Key, param.Value));
            inputs.ForEach(input => myKeyMap.Add(input.Key, input.Value.ID));
            
            var sourceCode = ShaderTemplateEvaluator.Evaluate(theTemplate, myKeyMap);
           Setup(sourceCode, inputs,new OrderedDictionary<string, AbstractGpuValue> {{"result", Output}});
        }

       
    }
    
    
}