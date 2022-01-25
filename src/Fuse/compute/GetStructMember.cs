using System.Collections.Generic;

namespace Fuse.compute
{
    public class GetStructMember<TOut> : ShaderNode<TOut>
    {

        private readonly GpuValue<GpuStruct> _input;
        private readonly string _member;
        public GetStructMember(GpuValue<GpuStruct> theInput, string theMember, GpuValue<TOut> theDefault = null) : base("Member", theDefault)
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