using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferGet : ShaderNode<GpuStruct> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<int> _index;
        private readonly ShaderNode<GpuStruct> _struct;
        
        public DynamicStructBufferGet(ShaderNode<Buffer> theBuffer, ShaderNode<int> theIndex, ShaderNode<GpuStruct> theStruct, ShaderNode<GpuStruct> theDefault) : base( "getBuffer", theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            _struct = theStruct;
            
            Setup(new List<AbstractShaderNode>(){theBuffer,theIndex, theStruct});

            TypeOverride = theStruct.TypeOverride;
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