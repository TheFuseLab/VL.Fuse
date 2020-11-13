using System;
using System.Collections.Generic;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class BoolComputeNode<T> : GenericComputeNode<T,float>
    {
        public BoolComputeNode(Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> getShaderSource,
            IEnumerable<KeyValuePair<string, IComputeValue<T>>> inputs) : base(getShaderSource, inputs)
        {
        }
    }
}