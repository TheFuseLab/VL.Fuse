using Stride.Graphics;
using System;
using System.Collections.Generic;
using VL.Core;
using VL.Stride.Rendering.ComputeEffect;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse
{
    public sealed class ShaderNodeMonadicFactory<T> : IMonadicFactory<T, ShaderNode<T>>
    {
        // This field is accessed by the target code
        public static readonly ShaderNodeMonadicFactory<T> Default = new();
        
        private readonly Dictionary<uint, uint> _hashInstances = new();

        IMonadBuilder<T, ShaderNode<T>> IMonadicFactory<T, ShaderNode<T>>.GetMonadBuilder(bool isConstant)
        {
            // Not called anymore in 2022.5
            throw new NotSupportedException();
        }

        // Will be called once for each data source. The patch editor will also create instances as needed during interaction.
        public IMonadBuilder<T, ShaderNode<T>> GetMonadBuilder(bool isConstant, NodeContext nodeContext)
        {
            // Can't call the constructor directly due to value type constraint
            if (typeof(T).IsValueType)
            {
                // Shader constants disabled for now
                var builderType = /*isConstant ? typeof(ConstantGpuValueBuilder<>) :*/ typeof(GpuValueBuilder<>);
                return Activator.CreateInstance(builderType.MakeGenericType( typeof(T)), nodeContext) as IMonadBuilder<T, ShaderNode<T>>;
            }
            
            // Read: if (T is Buffer)
            if (typeof(Buffer).IsAssignableFrom(typeof(T)))
            {
                // Can't call the constructor directly due to value type constraint
                var builderType = typeof(BufferShaderNodeBuilder<>);
                return Activator.CreateInstance(builderType.MakeGenericType(typeof(T)), nodeContext) as IMonadBuilder<T, ShaderNode<T>>;
            }
            
            if (typeof(T) == typeof(Texture))
                return new TextureShaderNodeBuilder(nodeContext) as IMonadBuilder<T, ShaderNode<T>>;
            
            if (typeof(T) == typeof(SamplerState))
                return new SamplerStateGpuValueBuilder(nodeContext) as IMonadBuilder<T, ShaderNode<T>>;
            
            /*
            if (typeof(T) == typeof(Buffer))
                return new BufferGpuValueBuilder<T>() as IMonadBuilder<T, GpuValue<T>>;
            */
            throw new NotImplementedException(typeof(T).FullName + "Not Implemented");
        }
    }

    // For each data source a builder will be kept
    internal sealed class GpuValueBuilder<T> : IMonadBuilder<T, ShaderNode<T>>
        where T : struct
    {
        private readonly ValueInput<T> _valueInput;
        public GpuValueBuilder(NodeContext nodeContext)
        {
            _valueInput = new ValueInput<T>(nodeContext);
        }

        public ShaderNode<T> Return(T value)
        {
            _valueInput.Value = value;
            return _valueInput;
        }
    }

    internal sealed class GpuConstantValueBuilder<T> : IMonadBuilder<T, ShaderNode<T>>
        where T : struct
    {
        private static readonly EqualityComparer<T> EqualityComparer = EqualityComparer<T>.Default;

        private ConstantValue<T> _constantValue;
        private ValueInput<T> _valueInput;

        private readonly NodeContext _context;

        public GpuConstantValueBuilder(NodeContext nodeContext)
        {
            _context = nodeContext;
        }

        public ShaderNode<T> Return(T value)
        {
            if (_valueInput != null)
            {
                // The value changed in the past. Stick with the less performant constant buffer upload
                _valueInput.Value = value;
                return _valueInput;
            }

            if (_constantValue is null)
            {
                // First time
                _constantValue = new ConstantValue<T>(value);
                return _constantValue;
            }

            if (EqualityComparer.Equals(value, _constantValue.Value))
            {
                // Value stayed the same
                return _constantValue;
            }

            // Value is changing - switch to different strategy where we upload via constant buffer
            (_valueInput ??= new ValueInput<T>(_context)).Value = value;
            return _valueInput;
        }
    }

    // Not sure about this one, never tested it ..
    internal sealed class TextureShaderNodeBuilder : IMonadBuilder<Texture, ShaderNode<Texture>>
    {
        private TextureInput _textureInput;

        private readonly NodeContext _nodeContext;

        private readonly TextureTypeTracker _typeTracker;

        public TextureShaderNodeBuilder(NodeContext nodeContext)
        {
            _nodeContext = nodeContext;
            _typeTracker = new TextureTypeTracker(false);
            
        }

        public ShaderNode<Texture> Return(Texture value)
        {
            var changeDeclaration = _typeTracker.CheckDeclaration(value);
            if (changeDeclaration || _textureInput == null)
            {
                _textureInput = new TextureInput(_nodeContext, _typeTracker);
            }
            _textureInput.Value = value;
            return _textureInput;
        }
    }
    
    internal sealed class BufferShaderNodeBuilder<T> : IMonadBuilder<Buffer, ShaderNode<Buffer>> where T : unmanaged
    {
        private BufferInput<T> _bufferInput;

        private readonly NodeContext _nodeContext;

        private readonly BufferTypeTracker<T> _typeTracker;
        
        public BufferShaderNodeBuilder(NodeContext nodeContext)
        {
            _nodeContext = nodeContext;
            _typeTracker = new BufferTypeTracker<T>(null);
        }

        public ShaderNode<Buffer> Return(Buffer value)
        {
            var changeDeclaration = _typeTracker.CheckDeclaration(value);
            if (changeDeclaration || _bufferInput == null)
            {
                _bufferInput = new BufferInput<T>(_nodeContext, _typeTracker, null);
            }
            _bufferInput.Value = value;
            return _bufferInput;
        }
    }

    internal sealed class SamplerStateGpuValueBuilder : IMonadBuilder<SamplerState, ShaderNode<SamplerState>>
    {
        private readonly SamplerInput _samplerInput;
        
        public SamplerStateGpuValueBuilder(NodeContext nodeContext)
        {
            _samplerInput = new SamplerInput(nodeContext);
        }

        public ShaderNode<SamplerState> Return(SamplerState value)
        {
            _samplerInput.Value = value;
            return _samplerInput;
        }
    }
}
