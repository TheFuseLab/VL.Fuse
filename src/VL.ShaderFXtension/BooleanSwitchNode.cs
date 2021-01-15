using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class BooleanSwitchNode<T> : ShaderNode<T>
    {

        public BooleanSwitchNode(GpuValue<bool> inCheck, GpuValue<T> inFalse, GpuValue<T> inTrue, ConstantValue<T> theDefault) : base( "expression", theDefault)
        {
            const string shaderCode = @"
        ${resultType} ${resultName}; 
        if(${check}){
            ${resultName} = ${inTrue};
        }else{
            ${resultName} = ${inFalse};
        }
        ";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>()
            {
                {"check", inCheck},
                {"inFalse", inFalse},
                {"inTrue", inTrue}
            };
            Setup(shaderCode, inputs);
        }
    }
}