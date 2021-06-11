using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        private GpuValue<T> _default;
        private List<GpuValue<T>> _cases;
        public NumericSwitchNode(GpuValue<int> theCheck, IEnumerable<GpuValue<T>> inputs, GpuValue<T> theDefault) : base("numericSwitch")
        {
            _cases = inputs.ToList();
            _default = theDefault;
            var myIns = new List<AbstractGpuValue>(_cases);
            if (theDefault != null)
            {
                myIns.Add(theDefault);
            }
            
            var myKeyMap = new Dictionary<string, string>
            {
                {"check", theCheck.ID}
            };
            Setup(myIns,myKeyMap);
            
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

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"cases", BuildCases(_cases, _default)}});
        }
    }
}