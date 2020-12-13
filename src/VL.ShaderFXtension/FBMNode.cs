using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class FBMNode<TIn, TOut> : ShaderNode
    {
        private const string ShaderCode = @"
${resultType} fbmRidge${signature}${id}(${argumentType} p, ${referenceSignature}){{
    ${resultType} noiseVal = abs(${referenceCall} - 0.5) * 2; 
    noiseVal = 1. - noiseVal;
    noiseVal = pow(noiseVal , 10.);
    return noiseVal;
}

${resultType} fbmTurbulence${signature}${id}(${argumentType} p, ${referenceSignature}){{
    ${resultType} noiseVal = abs(${referenceCall} - 0.5) * 2; 
    noiseVal = pow(noiseVal , 1.);
    return noiseVal;
}

${resultType} fbmStandard${signature}${id}(${argumentType} p, ${referenceSignature}){{
    return ${referenceCall};
}

${resultType} fbm${signature}${id}(${argumentType} p,float gain, float octaves, float lacunarity, ${referenceSignature})
{

    float myScale = 1;
    float myFallOff = gain;

    int iOctaves = int(floor(octaves)); 
    ${argumentType} myResult = 0.;  
    float myAmp = 0.;

    for(int i = 0; i < iOctaves;i++){{
        myResult += ${fbmType}${signature}${id}(p * myScale,${referenceArguments}) * myFallOff;
        myAmp += myFallOff;
        myFallOff *= gain;
        myScale *= lacunarity;
    }

    float myBlend = octaves - float(iOctaves);
    myResult += ${fbmType}(p * myScale) * myFallOff * myBlend;    
    myAmp += myFallOff * myBlend;
    
    if(myAmp > 0.0){{
        myResult /= myAmp;
    }
 
    return myResult;
    }
};";

        public GpuValue<TOut> Output { get; }
        
        public FBMNode(GPUReference<TIn> theReference, IEnumerable<AbstractGpuValue> inputs, FractalType theType = FractalType.Standard) : base("fbm")
        {
            Output = new GpuValue<TOut>("result");

            var gpuValues = inputs.ToList();
            var replacement = new Dictionary<string, AbstractGpuValue>
                {{theReference.ArguemntKey, new GPUReferenceOverride("p")}};
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<TOut>().ToLower()},
                {"argumentType",TypeHelpers.GetNameForType<TIn>().ToLower()},
                {"signature",$"{TypeHelpers.GetNameForType<TIn>()}To{TypeHelpers.GetNameForType<TOut>()}"},
                {"id",theReference.ParentNode.ID},
                {"referenceCall", theReference.ParentNode.ReferenceCall(replacement)},
                {"referenceSignature", theReference.ParentNode.ReferenceSignature(replacement)},
                {"referenceArguments", theReference.ParentNode.ReferenceArguments(replacement)},
                {"fbmType", theType.ToString()}
            });
            Setup(sourceCode, ShaderNodesUtil.BuildInputs(gpuValues),new Dictionary<string, AbstractGpuValue> {{"result", Output}});
        }

        private void BuildReference()
        {
            
        }

        private static string BuildArguments(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
    }
}