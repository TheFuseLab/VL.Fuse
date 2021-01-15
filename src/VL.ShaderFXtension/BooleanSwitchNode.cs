using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class BooleanSwitchNode<T> : ShaderNode<T>
    {
        private const string ShaderCode = @"
    ${resultType} ${resultName}; 
    if(${check}){
        ${resultName} = ${inTrue};
    }else{
        ${resultName} = ${inFalse};
    }
        ";

        public BooleanSwitchNode(GpuValue<bool> inCheck, GpuValue<T> inFalse, GpuValue<T> inTrue, ConstantValue<T> theDefault) : base( "expression", theDefault)
        {
            var myIns = new OrderedDictionary<string, AbstractGpuValue>() {{"check", inCheck}, {"inFalse", inFalse}, {"inTrue", inTrue}};
            

            var myCode = ShaderCode;
            if (inCheck == null || inFalse == null || inTrue == null)
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
                var myKeyMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                    {"check", inCheck.ID},
                    {"inFalse", inFalse.ID},
                    {"inTrue", inTrue.ID}
                };
                myCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, myKeyMap);
            }
            
            
            Setup(myCode, myIns);
            
        }
    }
}