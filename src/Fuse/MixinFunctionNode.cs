using System.Collections.Generic;
using System.Linq;

namespace Fuse
{
    public class MixinFunctionNode<T> : ShaderNode<T>
    {

        public MixinFunctionNode(IEnumerable<AbstractGpuValue> inputs, string theFunction, ConstantValue<T> theDefault, string theMixin) : base(theFunction, theDefault)
        {
            MixIns = new List<string>(){theMixin};

            var abstractGpuValues = inputs.ToList();
          
            Setup(
                "${resultType} ${resultName} = ${function}(${arguments});", 
                abstractGpuValues, 
                new Dictionary<string, string> {
                    {"function", theFunction},
                    {"arguments", ShaderNodesUtil.BuildArguments(abstractGpuValues)}
                }
            );
        }
        public sealed override List<string> MixIns { get; }
    }
}