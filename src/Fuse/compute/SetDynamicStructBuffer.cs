using System.Collections.Generic;
using Stride.Graphics;

namespace Fuse
{
    public class SetDynamicStructBuffer<GpuStruct> : ShaderNode<GpuVoid> 
    {
        private readonly GpuValue<Buffer> _buffer;
        private readonly GpuValue<int> _index;
        private GpuValue<GpuStruct> _struct;
        private readonly GpuValue<GpuStruct> _value;
    
        public SetDynamicStructBuffer(GpuValue<Buffer> theBuffer, GpuValue<int> theIndex, GpuValue<GpuStruct> theStruct, GpuValue<GpuStruct> theValue) : base( "setBuffer")
        {
            _buffer = theBuffer;
            _index = theIndex;
            _struct = theStruct;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theIndex,theStruct, theValue});
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