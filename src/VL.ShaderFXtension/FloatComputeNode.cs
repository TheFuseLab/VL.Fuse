using System;
using System.Collections.Generic;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class FloatComputeNode : GenericComputeNode<float>
    {
        public FloatComputeNode(Func<ShaderGeneratorContext, ShaderClassCode> getShaderSource, IEnumerable<KeyValuePair<string, IComputeNode>> inputs) : base(getShaderSource, inputs)
        {
        }
    }
}