using System.Collections.Generic;

namespace Fuse
{
    public class GpuValueMember<TIn, TOut> : ShaderNode<TOut>
    {

        public GpuValueMember(GpuValue<TIn> theInput, string theMember, ConstantValue<TOut> theDefault = null) : base("Member", theDefault)
        {
            const string shaderCode = "${resultType} ${resultName} = ${input}.${member};";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>
            {
                {"input",theInput}
            };
            var valueMap = new Dictionary<string, string>
            {
                {"member", theMember}
            };
            
           Setup(shaderCode, inputs, valueMap);
        }
    }
}