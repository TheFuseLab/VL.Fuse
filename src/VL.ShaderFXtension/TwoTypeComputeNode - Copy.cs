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
    public class TwoTypeComputeNode<TIn, TOut> : ComputeValue<TOut>
    {
        public TwoTypeComputeNode(
            Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> getShaderSource,
            IEnumerable<KeyValuePair<string, IComputeValue<TIn>>> inputs1,
            IEnumerable<KeyValuePair<string, IComputeValue<float>>> inputs2)
        {
            Inputs1 = inputs1?.Where(input => !string.IsNullOrWhiteSpace(input.Key) && input.Value != null).ToList();
            Inputs2 = inputs2?.Where(input => !string.IsNullOrWhiteSpace(input.Key) && input.Value != null).ToList();
            GetShaderSource = getShaderSource;
            
            
        }

        public Func<ShaderGeneratorContext, MaterialComputeColorKeys, ShaderClassCode> GetShaderSource { get; }

        public IEnumerable<KeyValuePair<string, IComputeValue<TIn>>> Inputs1 { get; }
        public IEnumerable<KeyValuePair<string, IComputeValue<float>>> Inputs2 { get; }

        public ShaderClassCode ShaderClass { get; private set; }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            ShaderClass = GetShaderSource(context, baseKeys);

            //compose if necessary
            if (Inputs1 == null || !Inputs1.Any()) return ShaderClass;
            if (Inputs2 == null || !Inputs2.Any()) return ShaderClass;
            var mixin = ShaderClass.CreateMixin();

            Inputs1.ForEach(input =>mixin.AddComposition(input.Value, input.Key, context, baseKeys));
            Inputs2.ForEach(input =>mixin.AddComposition(input.Value, input.Key, context, baseKeys));

            return mixin;

        }

        public override IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            if (Inputs1 == null) yield break;
            if (Inputs2 == null) yield break;
            
            foreach (var item in Inputs1)
            {
                if (item.Value != null)
                    yield return item.Value;
            }
            foreach (var item in Inputs2)
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
