using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class TypedBufferGet<T> : ShaderNode<T> where T : struct
    {
        private readonly GpuValue<Buffer<T>> _buffer;
        private readonly GpuValue<int> _index;
        
        public TypedBufferGet(GpuValue<Buffer<T>> theBuffer, GpuValue<int> theIndex, ConstantValue<T> theDefault) : base( "getBuffer", theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${bufferName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID},
                {"index", _index.ID}
            });
        }
    }
}