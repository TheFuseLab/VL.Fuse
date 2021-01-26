using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
{
    public class NumericSwitchNode<T> : ShaderNode<T>
    {
        private const string ShaderCode = @"
    ${resultType} ${resultName}; 
    switch(${check}){
${cases}
    }
        ";
        public NumericSwitchNode(GpuValue<int> theCheck, IEnumerable<GpuValue<T>> inputs, GpuValue<T> theDefault) : base("numericSwitch")
        {

            
            
            var myIns = new OrderedDictionary<string, AbstractGpuValue>();
            var c = 0;
            var abstractGpuValues = inputs.ToList();
            abstractGpuValues.ForEach(input => {
                myIns.Add("val" + c, input);
                c++;
            });
            if (theDefault != null)
            {
                myIns.Add("default" + c, theDefault);
            }
            
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>{{"cases", BuildCases(abstractGpuValues, theDefault)}});
            
            var myKeyMap = new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"check", theCheck.ID}
            };
            sourceCode = ShaderTemplateEvaluator.Evaluate(sourceCode, myKeyMap);
            Setup(sourceCode, myIns);
            
        }
        
        private static string BuildCases(IEnumerable<AbstractGpuValue> inputs, GpuValue<T> theDefault)
        {
            var stringBuilder = new StringBuilder();
            var c = 0;
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append("    case " + c + ":"+ Environment.NewLine);
                stringBuilder.Append("        ${resultName} = " + input.ID + ";"+ Environment.NewLine);
                stringBuilder.Append("        break;" + Environment.NewLine);
                c++;
            });
            if (theDefault != null)
            {
                stringBuilder.Append("    default:"+ Environment.NewLine);
                stringBuilder.Append("        ${resultName} = " + theDefault.ID + ";"+ Environment.NewLine);
                stringBuilder.Append("        break;" );
            }
            return stringBuilder.ToString();
        }
    }
}