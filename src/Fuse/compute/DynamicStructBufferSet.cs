using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferSet<GpuStruct> : ShaderNode<GpuVoid> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<int> _index;
        private readonly ShaderNode<GpuStruct> _value;
    
        public DynamicStructBufferSet(ShaderNode<Buffer> theBuffer, ShaderNode<int> theIndex, ShaderNode<GpuStruct> theValue) : base( "setBuffer")
        {
            _buffer = theBuffer;
            _index = theIndex;
            _value = theValue;
            
            Setup(new List<AbstractShaderNode> {theBuffer,theIndex, theValue});
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