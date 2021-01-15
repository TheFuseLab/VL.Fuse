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

        public FunctionNode(OrderedDictionary<string,AbstractGpuValue> inputs, string theFunctionName, IEnumerable<string> theMixins, ConstantValue<T> theDefault, OrderedDictionary<string,string> theFunctions = null) : base(theFunctionName, theDefault)
        {
            _myFunction = ShaderTemplateEvaluator.Evaluate(theFunctionName, new Dictionary<string, string>
            {
                {"resultType",TypeHelpers.GetNameForType<T>().ToLower()}
            });
            MixIns = theMixins;
            Functions = new Dictionary<string, string>();
            theFunctions.ForEach(kv => {
                Functions.Add(kv.Key + TypeHelpers.GetNameForType<T>().ToLower(), ShaderTemplateEvaluator.Evaluate(kv.Value, new Dictionary<string, string>
                {
                    {"resultType",TypeHelpers.GetNameForType<T>().ToLower()}
                }));
            });
            
            Output = new GpuValue<T>("result");
            var hasNullValue = false;
            inputs.ForEach(kv =>
            {
                if (kv.Value == null) hasNullValue = true;
            });
            
            var myCode = ShaderCode;
            if (hasNullValue)
            {
                var myKeyMap = new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                    {"default", theDefault.ID}
                };
                myCode = ShaderTemplateEvaluator.Evaluate(DefaultShaderCode, myKeyMap);
            }
            else
            {
                myCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>
                {
                    {"resultName", Output.ID},
                    {"resultType",TypeHelpers.GetNameForType<T>().ToLower()},
                    {"function",theFunctionName},
                    {"arguments",BuildArguments(inputs)}
                });
            }
            Setup(myCode, inputs);
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