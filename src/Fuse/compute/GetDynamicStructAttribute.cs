using System.Collections.Generic;

namespace Fuse
{
    public class GetDynamicStructAttribute<TOut> : ShaderNode<TOut>
    {

        private readonly GpuValue<GpuStruct> _struct;
        private readonly GpuValue<TOut> _member;
        
        public GetDynamicStructAttribute(GpuValue<GpuStruct> theStruct, GpuValue<TOut> theMember, ConstantValue<TOut> theDefault = null) : base("StructAttribute", theDefault)
        {
            _struct = theStruct;
            _member = theMember;
            Setup(new List<AbstractGpuValue>{theStruct});
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