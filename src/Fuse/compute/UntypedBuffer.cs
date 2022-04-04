using System.Collections.Generic;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse.compute
{

    public class UntypedBufferGet<T> : AbstractTypedFunction<T,T> where T : struct
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<int> _index;
        
        public UntypedBufferGet(GpuValue<Buffer> theBuffer, GpuValue<int> theIndex, GpuValue<T> theDefault) : base( "getBuffer", theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            Setup(new List<AbstractGpuValue> {theBuffer,theIndex});
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
    
    public class UntypedBufferSet<TIn> : AbstractTypedFunction<TIn, GpuVoid> where TIn : struct
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<int> _index;
        private readonly GpuValue<TIn> _value;
    
        public UntypedBufferSet(GpuValue<Buffer> theBuffer, GpuValue<int> theIndex, GpuValue<TIn> theValue) : base( "setBuffer")
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
    
    public class UntypedBufferAppend<T> : AbstractTypedFunction<T, GpuVoid> where T : struct
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<T> _value;
    
        public UntypedBufferAppend(GpuValue<Buffer> theBuffer, GpuValue<T> theValue) : base( "appendBuffer")
        {
            _buffer = theBuffer;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theValue});
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
            const string shaderCode = "${bufferName}.Append(${value});";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID},
                {"value", _value.ID}
            });
        }
    }
    
    public class UntypedBufferConsume<T> : AbstractTypedFunction<T,T> where T : struct
    {
        private readonly GpuValue<Buffer> _buffer;
        
        public UntypedBufferConsume(GpuValue<Buffer> theBuffer, GpuValue<T> theDefault) : base( "consumeBuffer", theDefault)
        {
            _buffer = theBuffer;
            Setup(new List<AbstractGpuValue> {theBuffer});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${bufferName}.Consume();";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID}
            });
        }
    }
}