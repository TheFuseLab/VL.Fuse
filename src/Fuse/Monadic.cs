using Stride.Graphics;
using System;
using System.Collections.Generic;
using VL.Core;

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
            // Didn' test these ...
            if (typeof(T) == typeof(Texture))
                return new TextureGpuValueBuilder() as IMonadBuilder<T, GpuValue<T>>;
            
            if (typeof(T) == typeof(SamplerInput))
                return new SamplerStateGpuValueBuilder() as IMonadBuilder<T, GpuValue<T>>;
            
            throw new NotImplementedException();
        }
    }

    // For each data source a builder will be kept
    sealed class GpuValueBuilder<T> : IMonadBuilder<T, GpuValue<T>>
        where T : struct
    {
        private readonly GpuInput<T> _gpuInput = new GpuInput<T>();

        public GpuValue<T> Return(T value)
        {
            _gpuInput.Value = value;
            return _gpuInput.Output;
        }
    }

    sealed class ConstantGpuValueBuilder<T> : IMonadBuilder<T, GpuValue<T>>
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
                return _constantValue = new ConstantValue<T>(value);
            }
            else if (equalityComparer.Equals(value, _constantValue.Value))
            {
                // Value stayed the same
                return _constantValue;
            }
            else
            {
                // Value is changing - switch to different strategy where we upload via constant buffer
                (_gpuInput ??= new GpuInput<T>()).Value = value;
                return _gpuInput.Output;
            }
        }
    }

    // Not sure about this one, never tested it ..
    sealed class TextureGpuValueBuilder : IMonadBuilder<Texture, GpuValue<Texture>>
    {
        private readonly TextureInput _textureInput = new TextureInput(null);

        public GpuValue<Texture> Return(Texture value)
        {
            _textureInput.Value = value;
            return _textureInput.Output;
        }
    }
    
    sealed class SamplerStateGpuValueBuilder : IMonadBuilder<SamplerState, GpuValue<SamplerState>>
    {
        private readonly SamplerInput _samplerInput = new SamplerInput(null);

        public GpuValue<SamplerState> Return(SamplerState value)
        {
            _samplerInput.Value = value;
            return _samplerInput.Output;
        }
    }
}
