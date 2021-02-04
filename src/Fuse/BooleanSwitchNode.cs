using System.Collections.Generic;

namespace Fuse
{
    public class BooleanSwitchNode<T> : ShaderNode<T>
    {
        private GpuValue<bool> _inCheck;
        private GpuValue<T> _inFalse;
        private GpuValue<T> _inTrue;
        public BooleanSwitchNode(GpuValue<bool> inCheck, GpuValue<T> inFalse, GpuValue<T> inTrue, ConstantValue<T> theDefault) : base( "expression", theDefault)
        {

            _inCheck = inCheck;
            _inFalse = inFalse;
            _inTrue = inTrue;
            
            Setup(new List<AbstractGpuValue>(){inCheck,inFalse,inTrue});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = @"
        ${resultType} ${resultName}; 
        if(${check}){
            ${resultName} = ${inTrue};
        }else{
            ${resultName} = ${inFalse};
        }
        ";
            return ShaderTemplateEvaluator.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"check", _inCheck.ID},
                {"inFalse", _inFalse.ID},
                {"inTrue", _inTrue.ID}
            });
        }
    }
}