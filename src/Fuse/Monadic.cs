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
        public static readonly ShaderNodeMonadicFactory<T> Default = new ShaderNodeMonadicFactory<T>();

        // Will be called once for each data source. The patch editor will also create instances as needed during interaction.
        public IMonadBuilder<T, ShaderNode<T>> GetMonadBuilder(bool isConstant)
        {
            // Can't call the constructor directly due to value type constraint
            if (typeof(T).IsValueType)
            {
                // Shader constants disabled for now
                var builderType = /*isConstant ? typeof(ConstantGpuValueBuilder<>) :*/ typeof(GpuValueBuilder<>);
                return Activator.CreateInstance(builderType.MakeGenericType(typeof(T))) as IMonadBuilder<T, ShaderNode<T>>;
            }
            
            // Read: if (T is Buffer)
            if (typeof(Buffer).IsAssignableFrom(typeof(T)))
            {
                // Can't call the constructor directly due to value type constraint
                var builderType = typeof(BufferGpuValueBuilder<>);
                return Activator.CreateInstance(builderType.MakeGenericType(typeof(T))) as IMonadBuilder<T, ShaderNode<T>>;
            }
            
            if (typeof(T) == typeof(Texture))
                return new TextureGpuValueBuilder() as IMonadBuilder<T, ShaderNode<T>>;
            
            if (typeof(T) == typeof(SamplerState))
                return new SamplerStateGpuValueBuilder() as IMonadBuilder<T, ShaderNode<T>>;
            
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
        private readonly GpuInput<T> _gpuInput = new GpuInput<T>();

        public ShaderNode<T> Return(T value)
        {
            _gpuInput.Value = value;
            return _gpuInput;
        }
    }

    internal sealed class ConstantGpuValueBuilder<T> : IMonadBuilder<T, ShaderNode<T>>
        where T : struct
    {
        private static readonly EqualityComparer<T> equalityComparer = EqualityComparer<T>.Default;

        private ConstantValue<T> _constantValue;
        private GpuInput<T> _gpuInput;

        public ShaderNode<T> Return(T value)
        {
            if (_gpuInput != null)
            {
                // The value changed in the past. Stick with the less performant constant buffer upload
                _gpuInput.Value = value;
                return _gpuInput;
            }

            if (_constantValue is null)
            {
                // First time
                _constantValue = new ConstantValue<T>(value);
                return _constantValue;
            }

            if (equalityComparer.Equals(value, _constantValue.Value))
            {
                // Value stayed the same
                return _constantValue;
            }

            // Value is changing - switch to different strategy where we upload via constant buffer
            (_gpuInput ??= new GpuInput<T>()).Value = value;
            return _gpuInput;
        }
    }

    // Not sure about this one, never tested it ..
    internal sealed class TextureGpuValueBuilder : IMonadBuilder<Texture, ShaderNode<Texture>>
    {
        private readonly TextureInput _textureInput = new TextureInput(null);

        public ShaderNode<Texture> Return(Texture value)
        {
            _textureInput.Value = value;
            return _textureInput;
        }
    }

    internal sealed class SamplerStateGpuValueBuilder : IMonadBuilder<SamplerState, ShaderNode<SamplerState>>
    {
        private readonly SamplerInput _samplerInput = new SamplerInput(null);

        public ShaderNode<SamplerState> Return(SamplerState value)
        {
            _samplerInput.Value = value;
            return _samplerInput;
        }
    }
    
    internal sealed class BufferGpuValueBuilder<T> : IMonadBuilder<Buffer<T>, ShaderNode<Buffer<T>>> where T : struct
    {
        private readonly BufferInput<T> _bufferInput = new BufferInput<T>(null);

        public ShaderNode<Buffer<T>> Return(Buffer<T> value)
        {
            _bufferInput.Value = value;
            return _bufferInput;
        }
    }
}
