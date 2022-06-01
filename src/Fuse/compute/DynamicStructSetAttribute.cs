using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructSetAbstractAttribute : ShaderNode<GpuVoid>
    {

        private readonly ShaderNode<GpuStruct> _struct;
        private readonly string _member;
        private readonly AbstractShaderNode _value;
        
        public DynamicStructSetAbstractAttribute(ShaderNode<GpuStruct> theStruct, string theMember, AbstractShaderNode theValue) : base("StructAttribute")
        {
            _struct = theStruct;
            _member = theMember;
            _value = theValue;
            SetInputs(new List<AbstractShaderNode>{theStruct,theValue});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${input}.${member} =  ${value};",new Dictionary<string, string> {
                {"input", _struct == null ? "" : _struct.ID},
                {"member", _member},
                {"value", _value.ID}
            });
        }
        
        public ShaderNode<GpuStruct> Struct()
        {
            return _struct;
        }
    }
    
    public class DynamicStructSetAttribute<T> : DynamicStructSetAbstractAttribute
    {
        
        public DynamicStructSetAttribute(ShaderNode<GpuStruct> theStruct, string theMember, ShaderNode<T> theValue) : base(theStruct, theMember, theValue)
        {
            
        }
    }
}