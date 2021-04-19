using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructSetAttribute<T> : ShaderNode<GpuVoid>
    {

        private readonly GpuValue<GpuStruct> _struct;
        private readonly GpuValue<T> _member;
        private readonly GpuValue<T> _value;
        
        public DynamicStructSetAttribute(GpuValue<GpuStruct> theStruct, GpuValue<T> theMember, GpuValue<T> theValue) : base("StructAttribute")
        {
            _struct = theStruct;
            _member = theMember;
            _value = theValue;
            Setup(new List<AbstractGpuValue>{theStruct,theValue});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${input}.${member} =  ${value};",new Dictionary<string, string> {
                {"input", _struct==null? "" : _struct.ID},
                {"member", _member.Name},
                {"value", _value.ID}
            });
        }
    }
}