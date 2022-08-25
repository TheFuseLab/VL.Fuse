using System.Collections.Generic;

namespace Fuse
{
    public class GetMember<TIn, TOut> : ShaderNode<TOut>
    {

        private readonly ShaderNode<TIn> _input;
        private readonly string _member;
        public GetMember(ShaderNode<TIn> theInput, string theMember, ShaderNode<TOut> theDefault = null) : base(theMember, theDefault)
        {
            _input = theInput;
            _member = theMember;
            SetInputs(new List<AbstractShaderNode>{theInput});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${input}.${member};",new Dictionary<string, string> {
                {"input", _input == null? "" : _input.ID},
                {"member", _member}
            });
        }
        
        
    }
}