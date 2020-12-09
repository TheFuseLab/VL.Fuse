using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class GpuValueMember<TIn, TOut> : ShaderNode
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${input}.${member};";

        public GpuValue<TOut> Output { get; }

        public GpuValueMember(GpuValue<TIn> theInput, string theMember) : base()
        {
            Output = new GpuValue<TOut>("member");

            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<TOut>().ToLower()},
                {"input",theInput.ID},
                {"member",theMember}
            });
           Setup(sourceCode, ShaderNodesUtil.BuildInputs(theInput),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }
    }
}