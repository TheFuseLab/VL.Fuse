using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class FBMNode<TIn, TOut> : ShaderNode
    {
        private const string ShaderCode = @"
${resultType} fbmRidge${signature}${id}(${argumentType} p, ${referenceSignature}){
    ${resultType} noiseVal = abs(${referenceCall} - 0.5) * 2; 
    noiseVal = 1. - noiseVal;
    noiseVal = pow(noiseVal , 10.);
    return noiseVal;
}

${resultType} fbmTurbulence${signature}${id}(${argumentType} p, ${referenceSignature}){
    ${resultType} noiseVal = abs(${referenceCall} - 0.5) * 2; 
    noiseVal = pow(noiseVal , 1.);
    return noiseVal;
}

${resultType} fbmStandard${signature}${id}(${argumentType} p, ${referenceSignature}){
    return ${referenceCall};
}

${resultType} fbm${signature}${id}(${argumentType} p,float gain, float octaves, float lacunarity, ${referenceSignature})
{

    float myScale = 1;
    float myFallOff = gain;

    int iOctaves = int(floor(octaves)); 
    ${resultType} myResult = 0.;  
    float myAmp = 0.;

    for(int i = 0; i < iOctaves;i++){
        myResult += fbm${fbmType}${signature}${id}(p * myScale,${referenceArguments}) * myFallOff;
        myAmp += myFallOff;
        myFallOff *= gain;
        myScale *= lacunarity;
    }

    float myBlend = octaves - float(iOctaves);
    myResult += fbm${fbmType}${signature}${id}(p * myScale,${referenceArguments}) * myFallOff * myBlend;    
    myAmp += myFallOff * myBlend;
    
    if(myAmp > 0.0){
        myResult /= myAmp;
    }
 
    return myResult;
}";

        public string ShaderCall;
        public GpuValue<TOut> Output { get; }

        private GPUReference<TIn> _myReference;
        private FractalType _myType;

        public FBMNode(GPUReference<TIn> theReference, 
            OrderedDictionary<string, AbstractGpuValue> inputs,
            FractalType theType = FractalType.Standard) : base("fbm")
        {
            _myReference = theReference;
            _myType = theType;
            Output = new GpuValue<TOut>("result");
            inputs.Add("function", theReference);

            var sourceCode =
                ShaderTemplateEvaluator.Evaluate("${resultType} ${resultName} = fbm${signature}${id}(${arguments});",
                    new Dictionary<string, string>
                    {
                        {"resultName", Output.ID},
                        {"resultType", TypeHelpers.GetNameForType<TOut>().ToLower()},
                        {"signature", $"{TypeHelpers.GetNameForType<TIn>()}To{TypeHelpers.GetNameForType<TOut>()}"},
                        {"id", _myReference.ParentNode.ID},
                        {"arguments", BuildArguments(inputs)}
                    });
            Setup(sourceCode, inputs, new OrderedDictionary<string, AbstractGpuValue> {{"result", Output}});
        }

        private string BuildArguments(OrderedDictionary<string, AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            var replacement = new Dictionary<string, AbstractGpuValue>
                {{_myReference.ArguemntKey, new GPUReferenceOverride("p")}};
            inputs.ForEach(input =>
            {
                if (input.Value == null) return;
                if (input.Value is AbstractGPUReference) return;
                stringBuilder.Append(input.Value.ID);
                stringBuilder.Append(", ");
            });
            var myReferenceArguments = _myReference.ParentNode.ReferenceArguments(replacement);
            if (!myReferenceArguments.IsNullOrEmpty())
            {
                stringBuilder.Append(myReferenceArguments);
            }
            else
            {
                if (stringBuilder.Length > 2) stringBuilder.Remove(stringBuilder.Length - 2, 2);
            }
            return stringBuilder.ToString();
        }
/*
        public override IEnumerable<string> MixIn
        {

            get
            {
                var result = new List<string>(base.MixIn);
                result.AddRange(_myReference.ParentNode.MixIn);
                return result;
            }
        }
        */

        public override string Function
        {
            get
            {
                var replacement = new Dictionary<string, AbstractGpuValue>
                    {{_myReference.ArguemntKey, new GPUReferenceOverride("p")}};
                return ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<TOut>().ToLower()},
                    {"argumentType", TypeHelpers.GetNameForType<TIn>().ToLower()},
                    {"signature", $"{TypeHelpers.GetNameForType<TIn>()}To{TypeHelpers.GetNameForType<TOut>()}"},
                    {"id", _myReference.ParentNode.ID},
                    {"referenceCall", _myReference.ParentNode.ReferenceCall(replacement)},
                    {"referenceSignature", _myReference.ParentNode.ReferenceSignature(replacement)},
                    {"referenceArguments", _myReference.ParentNode.ReferenceCallArguments(replacement)},
                    {"fbmType", _myType.ToString()}
                });
            }
        }
    }
}