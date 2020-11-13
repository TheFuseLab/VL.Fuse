using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class JoinFloat2 : GenericComputeNode<float,Vector2>
    {
        public JoinFloat2(Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> getShaderSource,
            IEnumerable<KeyValuePair<string, IComputeValue<float>>> inputs) : base(getShaderSource, inputs)
        {
        }
    }
}