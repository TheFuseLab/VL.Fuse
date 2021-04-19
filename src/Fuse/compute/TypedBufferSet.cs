using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class TypedBufferSet<TIn> : ShaderNode<GpuVoid> where TIn : struct
    {
        private readonly GpuValue<Buffer<TIn>> _buffer;
        private readonly GpuValue<int> _index;
        private readonly GpuValue<TIn> _value;
    
        public TypedBufferSet(GpuValue<Buffer<TIn>> theBuffer, GpuValue<int> theIndex, GpuValue<TIn> theValue) : base( "setBuffer")
        {
            _buffer = theBuffer;
            _index = theIndex;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theIndex,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${bufferName}[${index}] = ${value};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
}