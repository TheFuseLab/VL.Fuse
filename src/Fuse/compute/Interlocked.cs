using System.Collections.Generic;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

public class Interlocked : ShaderNode<GpuVoid>, IComputeVoid
{
    private readonly IBufferInput<int> _buffer;
    private readonly ShaderNode<int> _index;
    private readonly ShaderNode<int> _value;
    private readonly ShaderNode<int> _lastValue;
    private readonly string _function;
    
    public Interlocked(NodeContext nodeContext, string theFunction, IBufferInput<int> theBuffer, ShaderNode<int> theIndex, ShaderNode<int> theValue) : base( nodeContext, "interlocked")
    {
        var factory = new NodeSubContextFactory(NodeContext);
        _lastValue = new DeclareValue<int>(factory.NextSubContext());
        OptionalOutputs = new List<AbstractShaderNode> { _lastValue} ;
        
        _function = theFunction;
        _buffer = theBuffer;
        _index = theIndex;
        _value = theValue;
            
        SetInputs(new List<AbstractShaderNode> {_buffer as AbstractShaderNode,_index, _value,_lastValue});
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
        const string shaderCode = "${function}(${bufferName}[${index}], ${value}, ${lastValue});";
        return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>
        {
            {"function", _function},
            {"bufferName", _buffer.ID},
            {"index", _index.ID},
            {"value", _value.ID},
            {"lastValue", _lastValue.ID}
        });
    }
}