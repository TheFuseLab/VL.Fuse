using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferConsume : ShaderNode<GpuStruct> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<GpuStruct> _struct;
        
        public DynamicStructBufferConsume(ShaderNode<Buffer> theBuffer, ShaderNode<GpuStruct> theStruct, ShaderNode<GpuStruct> theDefault) : base( "consumeBuffer", theDefault)
        {
            _buffer = theBuffer;
            _struct = theStruct;
            
            Setup(new List<AbstractShaderNode>(){theBuffer, theStruct});

            TypeOverride = theStruct.TypeOverride;
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${structType} ${resultName} = ${bufferName}.Consume();";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structType", _struct.TypeOverride},
                {"bufferName", _buffer.ID},
            });
        }
    }
}