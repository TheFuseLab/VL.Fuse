

using System.Collections.Generic;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;
using static VL.Stride.Shaders.ShaderFX.ShaderFXUtils;

namespace VL.ShaderFXtension
{
   
        public class UnaryOperationT1InT2Out<TIn, TOut> : ComputeValue<TOut>
        {
            public UnaryOperationT1InT2Out(string operatorName, IComputeValue<TIn> value)
            {
                ShaderName = operatorName;
                Value = value;
            }

            public IComputeValue<TIn> Value { get; }

            public override IEnumerable<IComputeNode> GetChildren(object context = null)
            {
                return ReturnIfNotNull(Value);
            }

            public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
            {
                var shaderSource = GetShaderSourceForType2<TIn, TOut>(ShaderName);

                var mixin = shaderSource.CreateMixin();

                mixin.AddComposition(Value, "Value", context, baseKeys);

                return mixin;
            }

            public override string ToString()
            {
                return string.Format("Op {0} {1}", Value, ShaderName);
            }
        }
    
}