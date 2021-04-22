using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructSetAttribute<T> : ShaderNode<GpuStruct>
    {

        private readonly GpuValue<GpuStruct> _struct;
        private readonly string _member;
        private readonly GpuValue<T> _value;
        
        public DynamicStructSetAttribute(GpuValue<GpuStruct> theStruct, string theMember, GpuValue<T> theValue) : base("StructAttribute")
        {
            _struct = theStruct;
            _member = theMember;
            _value = theValue;
            Setup(new List<AbstractGpuValue>{theStruct,theValue});
            Output = theStruct;
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${input}.${member} =  ${value};",new Dictionary<string, string> {
                {"input", _struct == null? "" : _struct.ID},
                {"member", _member},
                {"value", _value.ID}
            });
        }
    }
}