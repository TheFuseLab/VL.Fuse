using Stride.Graphics;
using System;
using System.Diagnostics;
using VL.Core;

namespace Fuse
{
    public sealed class GpuValueMonadicFactory<T> : IMonadicFactory<T, GpuValue<T>>
    {
        // This field is accessed by the target code
        public static readonly GpuValueMonadicFactory<T> Default = new GpuValueMonadicFactory<T>();

        // Will be called once for each data source. The patch editor will also create instances as needed during interaction.
        public IMonadBuilder<T, GpuValue<T>> GetMonadBuilder()
        {
            // Can't call the constructor directly due to value type constraint
            if (typeof(T).IsValueType)
                return Activator.CreateInstance(typeof(GpuValueBuilder<>).MakeGenericType(typeof(T))) as IMonadBuilder<T, GpuValue<T>>;
            // Didn' test these ...
            if (typeof(T) == typeof(Texture))
                return new TextureGpuValueBuilder() as IMonadBuilder<T, GpuValue<T>>;
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

        // Called by deserialization and value editor
        public T Extract(GpuValue<T> sink)
        {
            if (sink?.ParentNode is GpuInput<T> gpuInput)
                return gpuInput.Value;
            return default;
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

        // Shouldn't be called as there's no editor for Texture
        public Texture Extract(GpuValue<Texture> sink)
        {
            if (!(sink?.ParentNode is TextureInput textureInput)) return default;
            Debug.Assert(textureInput == _textureInput);
            return textureInput.Value;
        }
    }
}
