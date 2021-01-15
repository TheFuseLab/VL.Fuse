using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace VL.ShaderFXtension
{
    public class SampleTextureNode : ShaderNode<Vector4>
    {
        private const string ShaderCode = @"
    ${resultType} ${resultName} = ${texture}.Sample(${sampler},${texCoord}); 
        ";
        
        public SampleTextureNode(GpuValue<Texture> theTexture, GpuValue<SamplerState> theSampler, GpuValue<Vector2> theTexCoords, ConstantValue<Vector4> theDefault) : base( "sampleTexture", theDefault)
        {
            var myIns = new OrderedDictionary<string, AbstractGpuValue>() {{"texture", theTexture}, {"sampler", theSampler}, {"texCoords", theTexCoords}};
            

            var myCode = ShaderCode;
            if (theTexture == null || theSampler == null || theTexCoords == null)
            {
                var myKeyMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<Vector4>().ToLower()},
                    {"default", theDefault.ID}
                };
                myCode = ShaderTemplateEvaluator.Evaluate(DefaultShaderCode, myKeyMap);
            }
            else
            {
                var myKeyMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<Vector4>().ToLower()},
                    {"texture", theTexture.ID},
                    {"sampler", theSampler.ID},
                    {"texCoord", theTexCoords.ID}
                };
                myCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, myKeyMap);
            }
            
            
            Setup(myCode, myIns);
            
        }
    }
}