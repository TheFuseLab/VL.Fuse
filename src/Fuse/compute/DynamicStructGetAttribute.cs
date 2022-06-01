using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructGetAttribute<TOut> : ShaderNode<TOut>
    {

        private readonly ShaderNode<GpuStruct> _struct;
        private readonly ShaderNode<TOut> _member;
        
        public DynamicStructGetAttribute(ShaderNode<GpuStruct> theStruct, ShaderNode<TOut> theMember, ShaderNode<TOut> theDefault = null) : base("StructAttribute", theDefault)
        {
            _struct = theStruct;
            _member = theMember;
            SetInputs(new List<AbstractShaderNode>{theStruct});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${input}.${member};",new Dictionary<string, string> {
                {"input", _struct==null? "" : _struct.ID},
                {"member", _member.Name}
            });
        }
    }
}