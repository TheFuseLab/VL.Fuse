using Stride.Graphics;
using System;
using System.Collections.Generic;
using VL.Core;
using VL.Stride.Rendering.ComputeEffect;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse
{
    public sealed class GpuValueMonadicFactory<T> : IMonadicFactory<T, GpuValue<T>> 
    {
        // This field is accessed by the target code
        public static readonly GpuValueMonadicFactory<T> Default = new GpuValueMonadicFactory<T>();

        // Will be called once for each data source. The patch editor will also create instances as needed during interaction.
        public IMonadBuilder<T, GpuValue<T>> GetMonadBuilder(bool isConstant)
        {
            // Can't call the constructor directly due to value type constraint
            if (typeof(T).IsValueType)
            {
                // Shader constants disabled for now
                var builderType = /*isConstant ? typeof(ConstantGpuValueBuilder<>) :*/ typeof(GpuValueBuilder<>);
                return Activator.CreateInstance(builderType.MakeGenericType(typeof(T))) as IMonadBuilder<T, GpuValue<T>>;
            }
            
            // Read: if (T is Buffer)
            if (typeof(Buffer).IsAssignableFrom(typeof(T)))
            {
                // Can't call the constructor directly due to value type constraint
                var builderType = typeof(BufferGpuValueBuilder<>);
                return Activator.CreateInstance(builderType.MakeGenericType(typeof(T))) as IMonadBuilder<T, GpuValue<T>>;
            }
            
            if (typeof(T) == typeof(Texture))
                return new TextureGpuValueBuilder() as IMonadBuilder<T, GpuValue<T>>;
            
            if (typeof(T) == typeof(SamplerState))
                return new SamplerStateGpuValueBuilder() as IMonadBuilder<T, GpuValue<T>>;
            /*
            if (typeof(T) == typeof(Buffer))
                return new BufferGpuValueBuilder<T>() as IMonadBuilder<T, GpuValue<T>>;
            */
            throw new NotImplementedException(typeof(T).FullName + "Not Implemented");
        }
    }

    // For each data source a builder will be kept
    internal sealed class GpuValueBuilder<T> : IMonadBuilder<T, GpuValue<T>>
        where T : struct
    {
        private readonly GpuInput<T> _gpuInput = new GpuInput<T>();

        public GpuValue<T> Return(T value)
        {
            _gpuInput.Value = value;
            return _gpuInput.Output;
        }
    }

    internal sealed class ConstantGpuValueBuilder<T> : IMonadBuilder<T, GpuValue<T>>
        where T : struct
    {
        private static readonly EqualityComparer<T> equalityComparer = EqualityComparer<T>.Default;

        private ConstantValue<T> _constantValue;
        private GpuInput<T> _gpuInput;

        public GpuValue<T> Return(T value)
        {
            if (_gpuInput != null)
            {
                // The value changed in the past. Stick with the less performant constant buffer upload
                _gpuInput.Value = value;
                return _gpuInput.Output;
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
            return _gpuInput.Output;
        }
    }

    // Not sure about this one, never tested it ..
    internal sealed class TextureGpuValueBuilder : IMonadBuilder<Texture, GpuValue<Texture>>
    {
        private readonly TextureInput _textureInput = new TextureInput(null);

        public GpuValue<Texture> Return(Texture value)
        {
            _textureInput.Value = value;
            return _textureInput.Output;
        }
    }

    internal sealed class SamplerStateGpuValueBuilder : IMonadBuilder<SamplerState, GpuValue<SamplerState>>
    {
        private readonly SamplerInput _samplerInput = new SamplerInput(null);

        public GpuValue<SamplerState> Return(SamplerState value)
        {
            _samplerInput.Value = value;
            return _samplerInput.Output;
        }
    }
    
    internal sealed class BufferGpuValueBuilder<T> : IMonadBuilder<Buffer<T>, GpuValue<Buffer<T>>> where T : struct
    {
        private readonly BufferInput<T> _bufferInput = new BufferInput<T>(null);

        public GpuValue<Buffer<T>> Return(Buffer<T> value)
        {
            _bufferInput.Value = value;
            return _bufferInput.Output;
        }
    }
}
