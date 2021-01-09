using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class ReferenceFunctionNode<TIn, TOut> : ShaderNode<TOut>
    {
        

        public string ShaderCall;

        private readonly GPUReference<TIn> _myReference;
        private readonly string _myShaderCode;
        private readonly IDictionary<string,string> _myValues;

        public ReferenceFunctionNode(
            string theName,
            string theShaderCode,
            IDictionary<string,string> theValues,
            GPUReference<TIn> theReference, 
            OrderedDictionary<string, AbstractGpuValue> inputs) : base(theName)
        {
            _myReference = theReference;
            _myShaderCode = theShaderCode;
            _myValues = theValues;
            inputs.Add("function", theReference);

            var sourceCode =
                ShaderTemplateEvaluator.Evaluate("${resultType} ${resultName} = ${functionName}${signature}${id}(${arguments});",
                    new Dictionary<string, string>
                    {
                        {"resultType", TypeHelpers.GetNameForType<TOut>().ToLower()},
                        {"resultName", Output.ID},
                        {"functionName", theName},
                        {"signature", $"{TypeHelpers.GetNameForType<TIn>()}To{TypeHelpers.GetNameForType<TOut>()}"},
                        {"id", _myReference.ParentNode.ID},
                        {"arguments", BuildArguments(inputs)}
                    });
            Setup(sourceCode, inputs);
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

        public override IDictionary<string,string> Functions
        {
            get
            {
                var replacement = new Dictionary<string, AbstractGpuValue> {{_myReference.ArguemntKey, new GPUReferenceOverride("p")}};

                var valueMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<TOut>().ToLower()},
                    {"argumentType", TypeHelpers.GetNameForType<TIn>().ToLower()},
                    {"signature", $"{TypeHelpers.GetNameForType<TIn>()}To{TypeHelpers.GetNameForType<TOut>()}"},
                    {"id", _myReference.ParentNode.ID},
                    {"referenceCall", _myReference.ParentNode.ReferenceCall(replacement)},
                    {"referenceSignature", _myReference.ParentNode.ReferenceSignature(replacement)},
                    {"referenceArguments", _myReference.ParentNode.ReferenceCallArguments(replacement)}
                };
                _myValues.ForEach(kv => valueMap.Add(kv.Key, kv.Value));
                return new Dictionary<string, string>() {
                {
                    ID+Output.ID,
                    ShaderTemplateEvaluator.Evaluate(_myShaderCode, valueMap)} };
            }
        }
    }
}