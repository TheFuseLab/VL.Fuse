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

        public ShaderClassCode ShaderClass { get; private set; }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            ShaderClass = GetShaderSource(context, baseKeys);

            //compose if necessary
            if (Inputs == null || !Inputs.Any()) return ShaderClass;
            var mixin = ShaderClass.CreateMixin();

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
            return ShaderClass?.ToString() ?? GetType().ToString();
        }
    }
}
