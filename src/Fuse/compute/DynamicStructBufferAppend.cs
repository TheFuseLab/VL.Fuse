using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferAppend<GpuStruct> : ShaderNode<GpuVoid> 
    {
        private readonly ShaderNode<Buffer> _buffer;
        private readonly ShaderNode<GpuStruct> _value;
    
        public DynamicStructBufferAppend(ShaderNode<Buffer> theBuffer, ShaderNode<GpuStruct> theValue) : base( "appendBuffer")
        {
            _buffer = theBuffer;
            _value = theValue;
            
            Setup(new List<AbstractShaderNode>(){theBuffer, theValue});
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
}