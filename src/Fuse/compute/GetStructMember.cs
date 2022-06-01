using System.Collections.Generic;

namespace Fuse.compute
{
    public class GetStructMember<TOut> : ShaderNode<TOut>
    {

        private readonly ShaderNode<GpuStruct> _input;
        private readonly string _member;
        public GetStructMember(ShaderNode<GpuStruct> theInput, string theMember, ShaderNode<TOut> theDefault = null) : base("Member", theDefault)
        {
            _input = theInput;
            _member = theMember;
            SetInputs(new List<AbstractShaderNode>{theInput});
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