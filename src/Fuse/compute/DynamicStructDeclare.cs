using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructDeclare : ShaderNode<GpuStruct> 
    {
        private readonly ShaderNode<GpuStruct> _struct;
        
        public DynamicStructDeclare(ShaderNode<GpuStruct> theStruct, ShaderNode<GpuStruct> theDefault) : base( "getBuffer", theDefault)
        {
            _struct = theStruct;
            
            Setup(new List<AbstractShaderNode>(){theStruct});

            TypeOverride = theStruct.TypeOverride;
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${structType} ${resultName};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structType", _struct.TypeOverride}
            });
        }
    }
}