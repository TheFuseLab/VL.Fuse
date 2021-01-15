using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class GpuValueMember<TIn, TOut> : ShaderNode<TOut>
    {
        private const string ShaderCode = "${resultType} ${resultName} = ${input}.${member};";

        public GpuValueMember(GpuValue<TIn> theInput, string theMember, ConstantValue<TOut> theDefault = null) : base("Member", theDefault)
        {
            Output = new GpuValue<TOut>("member");

            var myCode = ShaderCode;
            if (theInput == null)
            {
                var myKeyMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<TOut>().ToLower()},
                    {"default", theDefault.ID}
                };
                myCode = ShaderTemplateEvaluator.Evaluate(DefaultShaderCode, myKeyMap);
            }
            else
            {
                myCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType",TypeHelpers.GetNameForType<TOut>().ToLower()},
                    {"input",theInput.ID},
                    {"member",theMember}
                });
            }
           Setup(myCode, ShaderNodesUtil.BuildInputs(theInput));
        }
    }
}