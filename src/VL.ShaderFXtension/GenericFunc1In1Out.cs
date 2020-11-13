using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class GenericFunc1In1Out<TIn, TOut> : ComputeNode<TOut>
    {
        public GenericFunc1In1Out(string functionName, IEnumerable<KeyValuePair<string, IComputeNode>> inputs)
        {
            ShaderName = functionName;
            Inputs = inputs?.Where(input => !string.IsNullOrWhiteSpace(input.Key) && input.Value != null).ToList();
        }

        public IEnumerable<KeyValuePair<string, IComputeNode>> Inputs { get; }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            var shaderSource = new ShaderClassSource(ShaderName);

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
            if (Inputs != null)
            {
                foreach (var item in Inputs)
                {
                    if (item.Value != null)
                        yield return item.Value;
                }
            }
        }

        public override string ToString()
        {
            return ShaderName;
        }
    }
}