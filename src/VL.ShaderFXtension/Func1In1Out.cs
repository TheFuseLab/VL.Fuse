using System;
using Stride.Core.Mathematics;
using System.Collections.Generic;
using System.Linq;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;
using VL.Stride.Shaders.ShaderFX.Functions;
using static VL.Stride.Shaders.ShaderFX.ShaderFXUtils;

namespace VL.ShaderFXtension
{
    public class Func1In1Out<TIn, TOut> : ComputeNode<TOut>
    {
        public Func1In1Out(string functionName, IEnumerable<KeyValuePair<string, IComputeNode>> inputs)
        {
            ShaderName = functionName;
            Inputs = inputs?.Where(input => !string.IsNullOrWhiteSpace(input.Key) && input.Value != null).ToList();
        }

        public IEnumerable<KeyValuePair<string, IComputeNode>> Inputs { get; private set; }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            var shaderSource = GetShaderSourceFunkForType2<TIn, TOut>(ShaderName);

            //compose if necessary
            if (Inputs == null || !Inputs.Any()) return shaderSource;
            
            var mixin = shaderSource.CreateMixin();

            foreach (var input in Inputs)
            {
                mixin.AddComposition(input.Value, input.Key, context, baseKeys);
            }

            return mixin;

        }

        public override IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            if (Inputs == null) yield break;
            
            foreach (var item in Inputs)
            {
                if (item.Value != null)
                    yield return item.Value;
            }
        }

        public override string ToString()
        {
            return ShaderName;
        }
    }
}
