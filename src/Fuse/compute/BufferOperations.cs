using System.Collections.Generic;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute
{

    public interface IBufferInput<T> 
    {
        
        bool Append { set; }
        string ID { get;  }
        string TypeName();
    }
    public class BufferGet<T> : ShaderNode<T> 
    {
        private IBufferInput<T> _buffer;
        private ShaderNode<int> _index;
        
        public BufferGet(NodeContext nodeContext) : base( nodeContext, "getBuffer")
        {
            
        }

        public void SetInputs(IBufferInput<T> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            
            TypeOverride = theBuffer == null ? TypeHelpers.GetGpuType<T>() : theBuffer.TypeName();
            
            SetInputs(new List<AbstractShaderNode>{theBuffer as AbstractShaderNode,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${bufferType} ${resultName} = ${bufferName}[${index}];";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferType", _buffer.TypeName()},
                {"bufferName", _buffer.ID},
                {"index", _index.ID}
            });
        }
    }
    
    public class BufferSet<T> : ShaderNode<GpuVoid>, IComputeVoid
    {
        private IBufferInput<T> _buffer;
        private ShaderNode<int> _index;
        private ShaderNode<T> _value;
    
        public BufferSet(NodeContext nodeContext) : base( nodeContext, "setBuffer")
        {
            
        }

        public void SetInputs(IBufferInput<T> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theValue)
        {
            _buffer = theBuffer;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode> {theBuffer as AbstractShaderNode,theIndex, theValue});
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
        private IBufferInput<T> _buffer;
        private ShaderNode<T> _value;
    
        public BufferAppend(NodeContext nodeContext) : base( nodeContext, "appendBuffer")
        {
            
        }

        public void SetInputs(IBufferInput<T> theBuffer, ShaderNode<T> theValue)
        {
            _buffer = theBuffer;
            _buffer.Append = true;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{theBuffer as AbstractShaderNode, theValue});
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
        private IBufferInput<T> _buffer;
        
        public BufferConsume(NodeContext nodeContext, ShaderNode<T> theDefault) : base( nodeContext, "consumeBuffer", theDefault)
        {
            

        }

        public void SetInput(IBufferInput<T> theBuffer)
        {
            _buffer = theBuffer;
            
            SetInputs(new List<AbstractShaderNode> {theBuffer as AbstractShaderNode});
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
    
    public class BufferGetDimensions<T> : ShaderNode<GpuVoid>
    {
        private BufferInput<T> _buffer;

        public BufferGetDimensions(NodeContext nodeContext) : base(nodeContext, "bufferGetDimension")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
        }

        public void SetInput(BufferInput<T> theBuffer)
        {
            _buffer = theBuffer;

            OptionalOutputs.Clear();

            var myInputs = new List<AbstractShaderNode> {_buffer};
            
            var myDeclareValue = new DeclareValue<float>(new NodeSubContextFactory(NodeContext).NextSubContext());
            myInputs.Add(myDeclareValue);
            OptionalOutputs.Add(myDeclareValue);
            
            SetInputs(myInputs);
        }

        protected override string SourceTemplate()
        {
            var result = new Dictionary<string, string>();
            if (_buffer != null) result["buffer"] = _buffer.ID;

            return ShaderNodesUtil.Evaluate("${buffer}.GetDimensions(${arguments});", result);
        }

        protected void Setup(IEnumerable<AbstractShaderNode> theArguments, IEnumerable<InputModifier> theModifiers,
            string theFunction)
        {
        }
    }
}