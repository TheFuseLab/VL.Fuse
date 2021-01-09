﻿using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;

namespace VL.ShaderFXtension
{
    public class FunctionNode<T> : ShaderNode<T>
    {
        private const string ShaderCall = "${function}(${arguments})";
        private const string ShaderCode = "${resultType} ${resultName} = "+ShaderCall+";";

        private readonly string _myFunction;

        public FunctionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theFunctionName, IEnumerable<string> theMixins, OrderedDictionary<string,string> theFunctions = null) : base(theFunctionName)
        {
            _myFunction = theFunctionName;
            MixIns = theMixins;
            Functions = theFunctions;
            
            Output = new GpuValue<T>("result");

            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                {"function",theFunctionName},
                {"arguments",BuildArguments(inputs)}
            });
            Setup(sourceCode, inputs);
        }

        public override string ReferenceCall(Dictionary<string,AbstractGpuValue> theReplacements)
        {
            var myCopy = new Dictionary<string,AbstractGpuValue>(Ins);
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
            var myCopy = new Dictionary<string,AbstractGpuValue>(Ins);
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
            var myCopy = new Dictionary<string,AbstractGpuValue>(Ins);
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
            var myCopy = new Dictionary<string,AbstractGpuValue>(Ins);
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
        
        public override IEnumerable<string> MixIns { get; }

        public override IDictionary<string, string> Functions { get; }
    }
}