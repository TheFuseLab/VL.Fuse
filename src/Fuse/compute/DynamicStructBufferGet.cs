using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferGet : ShaderNode<GpuStruct> 
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<int> _index;
        private readonly GpuValue<GpuStruct> _struct;
        
        public DynamicStructBufferGet(GpuValue<Buffer> theBuffer, GpuValue<int> theIndex, GpuValue<GpuStruct> theStruct, GpuValue<GpuStruct> theDefault) : base( "getBuffer", theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            _struct = theStruct;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theIndex, theStruct});

            Output.TypeOverride = theStruct.TypeOverride;
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${structType} ${resultName} = ${bufferName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structType", _struct.TypeOverride},
                {"bufferName", _buffer.ID},
                {"index", _index.ID}
            });
        }
    }
}