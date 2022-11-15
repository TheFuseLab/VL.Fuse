using System.Collections.Generic;
using Stride.Graphics;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute
{
    public class BufferGet<T> : ShaderNode<T> 
    {
        private BufferInput<T> _buffer;
        private ShaderNode<int> _index;
        
        public BufferGet(NodeContext nodeContext) : base( nodeContext, "getBuffer")
        {
            
        }

        public void SetInputs(BufferInput<T> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            
            TypeOverride = theDefault == null ? TypeHelpers.GetGpuType<T>() : theDefault.TypeName();
            
            SetInputs(new List<AbstractShaderNode>{theBuffer,theIndex});
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
    
    public class BufferSet<T> : ShaderNode<GpuVoid>, IComputeVoid
    {
        private BufferInput<T> _buffer;
        private ShaderNode<int> _index;
        private ShaderNode<T> _value;
    
        public BufferSet(NodeContext nodeContext) : base( nodeContext, "setBuffer")
        {
            
        }

        public void SetInputs(BufferInput<T> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theValue)
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
        private BufferInput<T> _buffer;
        private ShaderNode<T> _value;
    
        public BufferAppend(NodeContext nodeContext) : base( nodeContext, "appendBuffer")
        {
            
        }

        public void SetInputs(BufferInput<T> theBuffer, ShaderNode<T> theValue)
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
        private BufferInput<T> _buffer;
        
        public BufferConsume(NodeContext nodeContext, ShaderNode<T> theDefault) : base( nodeContext, "consumeBuffer", theDefault)
        {
            

        }

        public void SetInput(BufferInput<T> theBuffer)
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