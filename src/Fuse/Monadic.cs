using Fuse.compute;
using Stride.Graphics;
using VL.Core;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse;

internal sealed class ShaderNodeMonadicTypeFilter : IMonadicTypeFilter
{
    public bool Accepts(TypeDescriptor typeDescriptor)
    {
        if (typeDescriptor.IsUnmanaged)
            return true;

        var type = typeDescriptor.ClrType;
        if (type is null)
            return false;

        // Read: if (T is Buffer)
        if (typeof(Buffer).IsAssignableFrom(type))
            return true;

        if (type == typeof(Texture))
            return true;

        if (type == typeof(SamplerState))
            return true;

        if (type == typeof(GpuStruct))
            return true;

        if (type == typeof(GpuVoid))
            return true;

        return false;
    }
}

internal sealed class DelegatingTextureInput : ShaderNode<Texture>
{
    private readonly NodeContext _nodeContext;

    private readonly TextureTypeTracker _typeTracker;
    private TextureInput _textureInput;

    public DelegatingTextureInput(NodeContext nodeContext)
        : base(nodeContext, "DelegatingTextureInput", theCreateDefault: false)
    {
        _nodeContext = nodeContext;
        _typeTracker = new TextureTypeTracker(false);
    }

    protected override ShaderNode<Texture> SetValue(Texture value)
    {
        var changeDeclaration = _typeTracker.CheckDeclaration(value);
        if (changeDeclaration || _textureInput == null) _textureInput = new TextureInput(_nodeContext, _typeTracker);
        _textureInput.Value = value;
        return _textureInput;
    }
}

internal sealed class DelegatingBufferInput<T> : ShaderNode<Buffer> where T : unmanaged
{
    private readonly NodeContext _nodeContext;

    private readonly BufferTypeTracker<T> _typeTracker;
    private BufferInput<T> _bufferInput;

    public DelegatingBufferInput(NodeContext nodeContext)
        : base(nodeContext, "DelegatingBufferInput", theCreateDefault: false)
    {
        _nodeContext = nodeContext;
        _typeTracker = new BufferTypeTracker<T>(null);
    }

    protected override ShaderNode<Buffer> SetValue(Buffer value)
    {
        var changeDeclaration = _typeTracker.CheckDeclaration(value);
        if (changeDeclaration || _bufferInput == null)
            _bufferInput = new BufferInput<T>(_nodeContext, _typeTracker, null);
        _bufferInput.Value = value;
        return _bufferInput;
    }
}