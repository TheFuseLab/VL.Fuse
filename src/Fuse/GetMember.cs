using System.Collections.Generic;

namespace Fuse
{
    public class GetMember<TIn, TOut> : ShaderNode<TOut>
    {

        private readonly GpuValue<TIn> _input;
        private readonly string _member;
        public GetMember(GpuValue<TIn> theInput, string theMember, GpuValue<TOut> theDefault = null) : base("Member", theDefault)
        {
            _input = theInput;
            _member = theMember;
            Setup(new List<AbstractGpuValue>{theInput});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${input}.${member};",new Dictionary<string, string> {
                {"input", _input==null? "" : _input.ID},
                {"member", _member}
            });
        }
        
        
    }
}