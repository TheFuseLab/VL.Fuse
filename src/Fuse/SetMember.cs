using System.Collections.Generic;
using Fuse.compute;

namespace Fuse
{
    public class SetMember<T, TMember> : ShaderNode<GpuVoid>
    {

        private readonly ShaderNode<T> _object;
        private readonly ShaderNode<TMember> _value;
        private readonly string _member;
        public SetMember(ShaderNode<T> theObject, string theMember, ShaderNode<TMember> theValue) : base("Member")
        {
            _object = theObject;
            _value = theValue;
            _member = theMember;
            Setup(new List<AbstractShaderNode>{theObject, theValue});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${object}.${member} = ${value};",new Dictionary<string, string> {
                {"object", _object==null? "" : _object.ID},
                {"value", _object==null? "" : _value.ID},
                {"member", _member}
            });
        }
    }
}