using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    public class GetMember<TIn, TOut> : ShaderNode<TOut>
    {

        private ShaderNode<TIn> _input;
        private string _member;
        public GetMember(NodeContext nodeContext, ShaderNode<TOut> theDefault = null) : base(nodeContext, "GetMember", theDefault)
        {
            
        }
        
        public GetMember(
            NodeContext nodeContext,
            ShaderNode<TIn> theInput, 
            string theMember, 
            ShaderNode<TOut> theDefault = null) : base(nodeContext, "GetMember", theDefault)
        {
            SetInput(theMember, theInput);
        }

        public void SetInput(string theMember, ShaderNode<TIn> theInput)
        {
            _member = theMember;
            _input = theInput;
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