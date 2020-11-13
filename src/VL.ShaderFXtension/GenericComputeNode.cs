using Stride.Core.Extensions;
using Stride.Rendering.Materials;
using Stride.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class GenericComputeNode<TIn, TOut> : ComputeValue<TOut>
    {
        public GenericComputeNode(Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> getShaderSource,
            IEnumerable<KeyValuePair<string, IComputeValue<TIn>>> inputs)
        {
            Inputs = inputs?.Where(input => !string.IsNullOrWhiteSpace(input.Key) && input.Value != null).ToList();
            GetShaderSource = getShaderSource;
        }

        public Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> GetShaderSource { get; }

        public IEnumerable<KeyValuePair<string, IComputeValue<TIn>>> Inputs { get; }

        public ShaderClassCode ShaderClass => shaderClass;

        ShaderClassCode shaderClass;

        public string ResultType()
        {
            return ShaderFXUtils.GetNameForType(this.GetType().GenericTypeArguments[1]);
        }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            shaderClass = GetShaderSource(context, baseKeys);

            //compose if necessary
            if (Inputs != null && Inputs.Any())
            {
                var mixin = shaderClass.CreateMixin();

                foreach (var input in Inputs)
                {
                    mixin.AddComposition(input.Value, input.Key, context, baseKeys);
                }

                return mixin;
            }

            return shaderClass;
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
            return shaderClass?.ToString() ?? GetType().ToString();
        }
    }
}
