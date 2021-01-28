using System.Collections.Generic;

namespace Fuse
{
    public class GpuValueMember<TIn, TOut> : ShaderNode<TOut>
    {

        public GpuValueMember(GpuValue<TIn> theInput, string theMember, ConstantValue<TOut> theDefault = null) : base("Member", theDefault)
        {
            
            Setup(
                "${resultType} ${resultName} = ${input}.${member};", 
                new List<AbstractGpuValue>{theInput}, 
                new Dictionary<string, string> {
                   {"input", theInput==null? "" : theInput.ID},
                   {"member", theMember}
               }
            );
        }
    }
}