using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class FunctionNode<T> : ShaderNode
    {
        private const string ShaderCall = "${function}(${arguments})";
        private const string ShaderCode = "${resultType} ${resultName} = "+ShaderCall+";";

        private IEnumerable<string> _myMixins = new List<string>();

        public GpuValue<T> Output { get; }

        private string _myFunction;

        public FunctionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theFunctionName, IEnumerable<string> theMixins) : base(theFunctionName)
        {
            _myFunction = theFunctionName;
            Output = new GpuValue<T>("result");
            _myMixins = theMixins;

            var gpuValues = inputs.ToList().Select(k => k.Value);
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                {"function",theFunctionName},
                {"arguments",BuildArguments(inputs)}
            });
            Setup(sourceCode, inputs,new OrderedDictionary<string, AbstractGpuValue> {{"result", Output}});
        }

        public override string ReferenceCall(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            var myCopy = new Dictionary<string,AbstractGpuValue>(_ins);
            theReplacements.ForEach(kv => myCopy[kv.Key] = kv.Value);
            return ShaderTemplateEvaluator.Evaluate(
                ShaderCall,
                new Dictionary<string, string>
                {
                    {"function",_myFunction}, 
                    {"arguments",BuildCallArguments(myCopy)}
                });
        }

        public override string ReferenceSignature(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            var myCopy = new Dictionary<string,AbstractGpuValue>(_ins);
            theReplacements.ForEach(kv => myCopy.Remove(kv.Key));
            
            var stringBuilder = new StringBuilder();
            myCopy.ForEach(input =>
            {
                if (input.Value == null) return;
                if (input.Value is GPUReferenceOverride) return;
                stringBuilder.Append(TypeHelpers.GetType(input.Value).ToLower() + " ");
                stringBuilder.Append(input.Key+Math.Abs(input.GetHashCode()));
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public override string ReferenceCallArguments(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            var myCopy = new Dictionary<string,AbstractGpuValue>(_ins);
            theReplacements.ForEach(kv => myCopy.Remove(kv.Key));
            
            var stringBuilder = new StringBuilder();
            myCopy.ForEach(input =>
            {
                if (input.Value == null) return;
                if (input.Value is GPUReferenceOverride) return;
                stringBuilder.Append(input.Key+Math.Abs(input.GetHashCode()));
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public override string ReferenceArguments(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            var myCopy = new Dictionary<string,AbstractGpuValue>(_ins);
            theReplacements.ForEach(kv => myCopy.Remove(kv.Key));
            
            var stringBuilder = new StringBuilder();
            myCopy.ForEach(input =>
            {
                if (input.Value == null) return;
                if (input.Value is GPUReferenceOverride) return;
                stringBuilder.Append(input.Value.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        private static string BuildCallArguments(Dictionary<string,AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input.Value == null) return;
                if (input.Value is GPUReferenceOverride)
                {
                    stringBuilder.Append(input.Value.ID);
                }
                else
                {
                    stringBuilder.Append(input.Key+Math.Abs(input.GetHashCode()));
                }
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }

        private static string BuildArguments(OrderedDictionary<string,AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input.Value == null) return;
                stringBuilder.Append(input.Value.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public override IEnumerable<string> MixIns => _myMixins;
    }
}