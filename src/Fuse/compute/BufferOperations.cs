using System.Collections.Generic;
using Stride.Graphics;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute
{
    public class BufferGet<T> : ShaderNode<T> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<int> _index;
        
        public BufferGet(ShaderNode<Buffer> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theDefault) : base( "getBuffer", theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            
            SetInputs(new List<AbstractShaderNode>{theBuffer,theIndex});

            TypeOverride = theDefault.TypeOverride;
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${bufferType} ${resultName} = ${bufferName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferType", TypeName()},
                {"bufferName", _buffer.ID},
                {"index", _index.ID}
            });
        }
    }
    
    public class BufferSet<T> : ShaderNode<T> , IComputeVoid
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<int> _index;
        private readonly ShaderNode<T> _value;
    
        public BufferSet(ShaderNode<Buffer> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theValue) : base( "setBuffer")
        {
            _buffer = theBuffer;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode> {theBuffer,theIndex, theValue});
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
    
    public class BufferAppend<T> : ShaderNode<GpuVoid> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<T> _value;
    
        public BufferAppend(ShaderNode<Buffer> theBuffer, ShaderNode<T> theValue) : base( "appendBuffer")
        {
            _buffer = theBuffer;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>(){theBuffer, theValue});
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
    
    public class BufferConsume<T> : ShaderNode<T> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        
        public BufferConsume(ShaderNode<Buffer> theBuffer, ShaderNode<T> theDefault) : base( "consumeBuffer", theDefault)
        {
            _buffer = theBuffer;
            
            SetInputs(new List<AbstractShaderNode>{theBuffer});

        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${structType} ${resultName} = ${bufferName}.Consume();";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structType", TypeName()},
                {"bufferName", _buffer.ID},
            });
        }
    }
}