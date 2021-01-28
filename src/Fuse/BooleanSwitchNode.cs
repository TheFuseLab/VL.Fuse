using System.Collections.Generic;

namespace Fuse
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
            
            Setup(shaderCode, 
                new List<AbstractGpuValue>(){inCheck,inFalse,inTrue},
                new Dictionary<string, string>()
                {
                    {"check", inCheck.ID},
                    {"inFalse", inFalse.ID},
                    {"inTrue", inTrue.ID}
                });
        }
    }
}