using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse.compute
{
    public class DynamicStructBufferAppend<GpuStruct> : ShaderNode<GpuVoid> 
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<GpuStruct> _value;
    
        public DynamicStructBufferAppend(GpuValue<Buffer> theBuffer, GpuValue<GpuStruct> theValue) : base( "appendBuffer")
        {
            _buffer = theBuffer;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theBuffer, theValue});
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