using System.Collections.Generic;

namespace Fuse
{
    public class MixinFunctionNode<T> : ShaderNode<T>
    {

        public MixinFunctionNode(OrderedDictionary<string, AbstractGpuValue> inputs, string theFunction, ConstantValue<T> theDefault, IEnumerable<string> theMixins) : base(theFunction, theDefault)
        {
            MixIns = theMixins;

            const string shaderCode = "${resultType} ${resultName} = ${function}(${arguments});";
            var valueMap = new Dictionary<string, string>
            {
                {"function", theFunction},
                {"arguments", ShaderNodesUtil.BuildArguments(inputs)}
            };
            Setup(shaderCode, inputs, valueMap);
        }
        public sealed override IEnumerable<string> MixIns { get; }
    }
}