using System.Collections.Generic;

namespace Fuse.compute
{
    public class DynamicStructDeclare : ShaderNode<GpuStruct> 
    {
        private readonly GpuValue<GpuStruct> _struct;
        
        public DynamicStructDeclare(GpuValue<GpuStruct> theStruct, ConstantValue<GpuStruct> theDefault) : base( "getBuffer", theDefault)
        {
            _struct = theStruct;
            
            Setup(new List<AbstractGpuValue>(){theStruct});

            Output.TypeOverride = theStruct.TypeOverride;
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