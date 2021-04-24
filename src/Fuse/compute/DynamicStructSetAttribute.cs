using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructSetAbstractAttribute : ShaderNode<GpuVoid>
    {

        private readonly GpuValue<GpuStruct> _struct;
        private readonly string _member;
        private readonly AbstractGpuValue _value;
        
        public DynamicStructSetAbstractAttribute(GpuValue<GpuStruct> theStruct, string theMember, AbstractGpuValue theValue) : base("StructAttribute")
        {
            _struct = theStruct;
            _member = theMember;
            _value = theValue;
            Setup(new List<AbstractGpuValue>{theStruct,theValue});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${input}.${member} =  ${value};",new Dictionary<string, string> {
                {"input", _struct == null? "" : _struct.ID},
                {"member", _member},
                {"value", _value.ID}
            });
        }
        
        public GpuValue<GpuStruct> Struct()
        {
            var result = _struct == null ? new GpuValue<GpuStruct>(""):new GpuValuePassThrough<GpuStruct>(_struct);
            result.ParentNode = this;
            return result;
        }
    }
    
    public class DynamicStructSetAttribute<T> : DynamicStructSetAbstractAttribute
    {
        
        public DynamicStructSetAttribute(GpuValue<GpuStruct> theStruct, string theMember, GpuValue<T> theValue) : base(theStruct, theMember, theValue)
        {
            
        }
    }
}