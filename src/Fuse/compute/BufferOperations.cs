using System.Collections.Generic;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

public interface IBufferInput<T>
{
    BufferType BufferType { get; set; }
    string ID { get; }
    string TypeName();

    /// <summary>
    /// True while the buffer is declared as (RW)ByteAddressBuffer because a raw buffer is bound, see
    /// <see cref="TypeHelpers.UseByteAddressBuffer"/>. Access nodes then emit Load/Store instead of indexing.
    /// </summary>
    bool IsByteAddressBuffer { get; }
}

/// <summary>
/// A buffer input whose declaration can be forced to stay structured. Function-call nodes (mixin, custom, intrinsic,
/// region and delegate calls) request this for every buffer they pass on, because hand-written shader functions
/// declare their parameters as StructuredBuffer and cannot take a ByteAddressBuffer.
/// </summary>
public interface IBufferDeclaration
{
    void RequireStructuredDeclaration();
}

/// <summary>
/// Shader code for element access on a raw (ByteAddressBuffer) binding. Elements are addressed by byte offset
/// (index * 4 * components); Load/Store work on uint, so int and float go through asint/asuint/asfloat.
/// </summary>
public static class ByteAddressAccess
{
    private static readonly string[] Components = { "x", "y", "z", "w" };

    public static bool Supports(string theType)
    {
        if (string.IsNullOrEmpty(theType)) return false;
        var baseType = BaseType(theType);
        var dimension = Dimension(theType);
        if (theType != baseType && theType != baseType + dimension) return false; // no matrices (float3x3)
        return baseType switch
        {
            "int" or "uint" or "float" => dimension is >= 1 and <= 4,
            "bool" => dimension == 1,
            _ => false
        };
    }

    public static string BaseType(string theType)
    {
        return theType.TrimEnd('1', '2', '3', '4');
    }

    public static int Dimension(string theType)
    {
        var baseType = BaseType(theType);
        return theType.Length > baseType.Length ? theType[^1] - '0' : 1;
    }

    /// <summary>Byte offset of element <paramref name="theIndex"/>, optionally of one of its components.</summary>
    public static string Offset(string theIndex, string theType, int theComponent = 0)
    {
        var offset = $"(({theIndex}) * {4 * Dimension(theType)})";
        return theComponent == 0 ? offset : $"({offset} + {4 * theComponent})";
    }

    private static string Suffix(string theType)
    {
        var dimension = Dimension(theType);
        return dimension == 1 ? "" : dimension.ToString();
    }

    public static string Load(string theBuffer, string theIndex, string theType)
    {
        var raw = $"{theBuffer}.Load{Suffix(theType)}({Offset(theIndex, theType)})";
        return BaseType(theType) switch
        {
            "int" => $"asint({raw})",
            "float" => $"asfloat({raw})",
            "bool" => $"({raw} != 0)",
            _ => raw
        };
    }

    public static string Store(string theBuffer, string theIndex, string theType, string theValue)
    {
        var value = BaseType(theType) switch
        {
            "uint" => theValue,
            "bool" => $"({theValue} ? 1u : 0u)",
            _ => $"asuint({theValue})"
        };
        return $"{theBuffer}.Store{Suffix(theType)}({Offset(theIndex, theType)}, {value});";
    }

    /// <summary>
    /// Interlocked on a raw buffer: the ByteAddressBuffer method takes a byte offset and uint values, one component
    /// at a time. Only int and uint elements are meaningful, as for the structured variant.
    /// </summary>
    public static string Interlocked(string theFunction, string theBuffer, string theIndex, string theType,
        string theValue, string theLastValue)
    {
        var dimension = Dimension(theType);
        var isInt = BaseType(theType) == "int";
        var code = "";
        for (var i = 0; i < dimension; i++)
        {
            var component = dimension == 1 ? "" : "." + Components[i];
            var temp = $"{theLastValue}_raw{i}";
            var value = isInt ? $"asuint({theValue}{component})" : $"{theValue}{component}";
            code += $"uint {temp}; {theBuffer}.{theFunction}({Offset(theIndex, theType, i)}, {value}, {temp}); " +
                    $"{theLastValue}{component} = {(isInt ? $"asint({temp})" : temp)};\n";
        }

        return code;
    }
}

public class BufferGet<T> : ShaderNode<T>
{
    private readonly IBufferInput<T> _buffer;
    private readonly ShaderNode<int> _index;

    public BufferGet(
        NodeContext nodeContext, 
        IBufferInput<T> theBuffer, 
        ShaderNode<int> theIndex,
        ShaderNode<T> theType) : base(nodeContext, "getBuffer")
    {
        _buffer = theBuffer;
        _index = theIndex;

        // TODO: BUG - If theBuffer is null here, TypeOverride gets a default type.
        // But later at line 41, _buffer.TypeName() is called without null check.
        // Either theBuffer should never be null, or we need null handling in SourceTemplate().
        TypeOverride = theBuffer == null ? TypeHelpers.GetGpuType<T>() : theBuffer.TypeName();

        var myInputs = new List<AbstractShaderNode> { theBuffer as AbstractShaderNode, theIndex };
        if (theType != null) myInputs.Add(theType);
        SetInputs(myInputs);
    }

    protected override string SourceTemplate()
    {
        if (_buffer.IsByteAddressBuffer)
            return ShaderNodesUtil.Evaluate("${bufferType} ${resultName} = ${load};", new Dictionary<string, string>
            {
                { "bufferType", _buffer.TypeName() },
                { "load", ByteAddressAccess.Load(_buffer.ID, _index.ID, _buffer.TypeName()) }
            });

        const string shaderCode = "${bufferType} ${resultName} = ${bufferName}[${index}];";

        return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "bufferType", _buffer.TypeName() },
            { "bufferName", _buffer.ID },
            { "index", _index.ID }
        });
    }
}

public class BufferSet<T> : ShaderNode<GpuVoid>, IComputeVoid
{
    private readonly IBufferInput<T> _buffer;
    private readonly ShaderNode<int> _index;
    private readonly ShaderNode<T> _value;

    public BufferSet(
        NodeContext nodeContext, 
        IBufferInput<T> theBuffer, 
        ShaderNode<int> theIndex,
        ShaderNode<T> theValue) : base(nodeContext, "setBuffer")
    {
        _buffer = theBuffer;
        _buffer.BufferType = BufferType.RW;
        _index = theIndex;
        _value = theValue;

        SetInputs(new List<AbstractShaderNode> { theBuffer as AbstractShaderNode, theIndex, theValue });
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
        if (_buffer.IsByteAddressBuffer)
            return ByteAddressAccess.Store(_buffer.ID, _index.ID, _buffer.TypeName(), _value.ID);

        const string shaderCode = "${bufferName}[${index}] = ${value};";
        return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "bufferName", _buffer.ID },
            { "index", _index.ID },
            { "value", _value.ID }
        });
    }
}

public class BufferAppend<T> : ShaderNode<GpuVoid>
{
    private readonly IBufferInput<T> _buffer;
    private readonly ShaderNode<T> _value;

    public BufferAppend(NodeContext nodeContext, IBufferInput<T> theBuffer, ShaderNode<T> theValue) : base(nodeContext,
        "appendBuffer")
    {
        _buffer = theBuffer;
        _buffer.BufferType = BufferType.Append;
        _value = theValue;

        SetInputs(new List<AbstractShaderNode> { theBuffer as AbstractShaderNode, theValue });
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
        return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "bufferName", _buffer.ID },
            { "value", _value.ID }
        });
    }
}

public class BufferConsume<T> : ShaderNode<T>
{
    private readonly IBufferInput<T> _buffer;

    public BufferConsume(NodeContext nodeContext, IBufferInput<T> theBuffer, ShaderNode<T> theDefault) : base(
        nodeContext, "consumeBuffer", theDefault)
    {
        _buffer = theBuffer;
        _buffer.BufferType = BufferType.Consume;

        SetInputs(new List<AbstractShaderNode> { theBuffer as AbstractShaderNode });
    }

    protected override string SourceTemplate()
    {
        const string shaderCode = "${structType} ${resultName} = ${bufferName}.Consume();";
        return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "structType", TypeName() },
            { "bufferName", _buffer.ID }
        });
    }
}

public class BufferGetDimensions<T> : ShaderNode<GpuVoid>
{
    private readonly BufferInput<T> _buffer;

    public BufferGetDimensions(NodeContext nodeContext, BufferInput<T> theBuffer) : base(nodeContext,
        "bufferGetDimension")
    {
        OptionalOutputs = new List<AbstractShaderNode>();

        _buffer = theBuffer;

        var myInputs = new List<AbstractShaderNode> { _buffer };

        var myDeclareValue = new DeclareValue<float>(new NodeSubContextFactory(NodeContext).NextSubContext());
        myInputs.Add(myDeclareValue);
        OptionalOutputs.Add(myDeclareValue);

        SetInputs(myInputs);
    }

    protected override string SourceTemplate()
    {
        if (_buffer is { IsByteAddressBuffer: true })
        {
            // ByteAddressBuffer.GetDimensions reports the size in bytes, the node reports elements.
            var count = OptionalOutputs[0].ID;
            return $"uint {count}_bytes; {_buffer.ID}.GetDimensions({count}_bytes); " +
                   $"{count} = {count}_bytes / {4 * ByteAddressAccess.Dimension(_buffer.TypeName())};";
        }

        var result = new Dictionary<string, string>();
        if (_buffer != null) result["buffer"] = _buffer.ID;

        return ShaderNodesUtil.Evaluate("${buffer}.GetDimensions(${arguments});", result);
    }
}
