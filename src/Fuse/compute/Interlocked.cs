using System.Collections.Generic;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

public class InterlockedBuffer<T> : ShaderNode<GpuVoid>, IComputeVoid
{
    private readonly IBufferInput<T> _buffer;
    private readonly ShaderNode<int> _index;
    private readonly ShaderNode<T> _value;
    private readonly ShaderNode<T> _lastValue;
    private readonly string _function;
    
    public InterlockedBuffer(NodeContext nodeContext, string theFunction, IBufferInput<T> theBuffer, ShaderNode<int> theIndex, ShaderNode<T> theValue) : base( nodeContext, "interlocked")
    {
        var factory = new NodeSubContextFactory(NodeContext);
        _lastValue = new DeclareValue<T>(factory.NextSubContext());
        OptionalOutputs = [_lastValue];
        
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
        if(TypeHelpers.GetDimension<T>() == 1){
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
        else
        {
            string[] components = { "x", "y", "z", "w" };
            var shaderCode = "";
            for (var i = 0; i < TypeHelpers.GetDimension<T>();i++)
            {
                shaderCode += "${function}(${bufferName}[${index}]."+components[i]+", ${value}."+components[i]+", ${lastValue}."+components[i]+");\n";
            }
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
}

public class InterlockedTexture<T,TIndex> : ShaderNode<GpuVoid>, IComputeVoid
{
    private readonly ITextureInput _texture;
    private readonly ShaderNode<TIndex> _index;
    private readonly ShaderNode<T> _value;
    private readonly ShaderNode<T> _lastValue;
    private readonly string _function;
    
    public InterlockedTexture(
        NodeContext nodeContext, 
        string theFunction, 
        ITextureInputProvider theTextureProvider,
        ShaderNode<TIndex> theIndex, 
        ShaderNode<T> theValue) : base( nodeContext, "interlocked")
    {
        var factory = new NodeSubContextFactory(NodeContext);
        _lastValue = new DeclareValue<T>(factory.NextSubContext());
        OptionalOutputs = [_lastValue];
        
        _function = theFunction;
        _texture = theTextureProvider?.GetTextureInput();
        _index = theIndex;
        _value = theValue;
            
        SetInputs(new List<AbstractShaderNode> {_texture as AbstractShaderNode,_index, _value,_lastValue});
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
    {/*
        const string shaderCode = "${function}(${textureName}[${index}], ${value}, ${lastValue});";
        return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>
        {
            {"function", _function},
            {"textureName", _texture.TextureID()},
            {"index", _index.ID},
            {"value", _value.ID},
            {"lastValue", _lastValue.ID}
        });
        */
        if(TypeHelpers.GetDimension<T>() == 1){
            const string shaderCode = "${function}(${textureName}[${index}], ${value}, ${lastValue});";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>
            {
                {"function", _function},
                {"textureName", _texture.TextureID()},
                {"index", _index.ID},
                {"value", _value.ID},
                {"lastValue", _lastValue.ID}
            });
        }
        else
        {
            string[] components = { "x", "y", "z", "w" };
            var shaderCode = "";
            for (var i = 0; i < TypeHelpers.GetDimension<T>();i++)
            {
                shaderCode += "${function}(${textureName}[${index}]."+components[i]+", ${value}."+components[i]+", ${lastValue}."+components[i]+");\n";
            }
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>
            {
                {"function", _function},
                {"textureName", _texture.TextureID()},
                {"index", _index.ID},
                {"value", _value.ID},
                {"lastValue", _lastValue.ID}
            });
        }
    }
}