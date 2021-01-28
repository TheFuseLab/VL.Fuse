using System.Collections.Generic;
using System.Linq;

namespace Fuse
{
    public class IntrinsicFunctionNode<T> : ShaderNode<T>
    {
        
        public IntrinsicFunctionNode(IEnumerable<AbstractGpuValue> theInputs, string theFunction, ConstantValue<T> theDefault) : base(theFunction, theDefault)
        {
            const string shaderCode = "${resultType} ${resultName} = ${function}(${arguments});";
            var abstractGpuValues = theInputs.ToList();
            var valueMap = new Dictionary<string, string>
            {
                {"function", theFunction},
                {"arguments", ShaderNodesUtil.BuildArguments(abstractGpuValues)}
            };
            Setup(shaderCode, abstractGpuValues, valueMap);
        }
        
        
    }
}