using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
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