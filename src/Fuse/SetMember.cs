using System.Collections.Generic;
using Fuse.compute;

namespace Fuse
{
    public class SetMember<T, TMember> : ShaderNode<GpuVoid>
    {

        private readonly GpuValue<T> _object;
        private readonly GpuValue<TMember> _value;
        private readonly string _member;
        public SetMember(GpuValue<T> theObject, string theMember, GpuValue<TMember> theValue) : base("Member")
        {
            _object = theObject;
            _value = theValue;
            _member = theMember;
            Setup(new List<AbstractGpuValue>{theObject, theValue});
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