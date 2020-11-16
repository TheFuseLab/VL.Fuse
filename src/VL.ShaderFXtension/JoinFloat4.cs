using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class JoinFloat4 : GenericComputeNode<float,Vector4>
    {
        public JoinFloat4(Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> getShaderSource,
            IEnumerable<KeyValuePair<string, IComputeValue<float>>> inputs) : base(getShaderSource, inputs)
        {
        }
    }
}