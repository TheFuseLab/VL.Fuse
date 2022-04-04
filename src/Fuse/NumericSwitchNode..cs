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

        private readonly GpuValue<T> _default;
        private readonly List<GpuValue<T>> _cases;
        private readonly GpuValue<int> _check;
        public NumericSwitchNode(GpuValue<int> theCheck, IEnumerable<GpuValue<T>> inputs, GpuValue<T> theDefault) : base("numericSwitch")
        {
            _check = theCheck;
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
            Setup(myIns);
            
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

        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  base.CreateTemplateMap();
            result["check"] = _check.ID;
            return result;
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"cases", BuildCases(_cases, _default)}});
        }
    }
}