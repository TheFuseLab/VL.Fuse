using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferConsume : ShaderNode<GpuStruct> 
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<GpuStruct> _struct;
        
        public DynamicStructBufferConsume(GpuValue<Buffer> theBuffer, GpuValue<GpuStruct> theStruct, GpuValue<GpuStruct> theDefault) : base( "consumeBuffer", theDefault)
        {
            _buffer = theBuffer;
            _struct = theStruct;
            
            Setup(new List<AbstractGpuValue>(){theBuffer, theStruct});

            Output.TypeOverride = theStruct.TypeOverride;
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