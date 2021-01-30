using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using Stride.Engine;

namespace Fuse
{
    internal static class DelegateUtil
    {

        public static string BuildArguments(List<IDelegateParameter> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(" ");
                stringBuilder.Append(input.Name());
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
    }
    
    public class DelegateValue<T> : GpuValue<T>
    {

        public DelegateValue(string theName) : base(theName)
        {
        }

        public override string ID => name;

    }
    
    public interface IDelegateParameter : IShaderNode
    {
        string TypeName();

        string Name();
    }
    
    public class DelegateParameter<T> : ShaderNode<T> , IDelegateParameter
    {

        public DelegateParameter(GpuValue<T> theType,  GpuValue<T> theOverride = null, int id = 0): base("delegate", null,"delegate")
        {
            Output = theOverride??new DelegateValue<T>("val"+GetHashCode());
            Output.ParentNode = this;
            Ins = new List<AbstractGpuValue>();
        }


        public virtual string TypeName()
        {
            return TypeHelpers.GetNameForType<T>().ToLower();
        }

        public string Name()
        {
            return Output.ID;
        }
    }
    
    public interface IDelegateNode
    {
    }

    public class DelegateNode<T> : ShaderNode<T> ,IDelegateNode
    {
        
        
        public DelegateNode(AbstractGpuValue theDelegate, IEnumerable<AbstractGpuValue> theParameters, ConstantValue<T> theDefault = null) : base("Delegate", theDefault)
        {
            FunctionName = "delegateFunction" + GetHashCode();
            MixIns = new List<string>();
            Functions = new Dictionary<string, string>();


            var gpuValues = theParameters.ToList();
            
            var delegates = theDelegate.ParentNode.Delegates();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"functionName", FunctionName},
                {"arguments", DelegateUtil.BuildArguments(delegates)},
                {"functionImplementation", theDelegate.ParentNode.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";
            Functions.Add(FunctionName, ShaderTemplateEvaluator.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            theDelegate.ParentNode.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
            MixIns.AddRange(theDelegate.ParentNode.MixinList());
            
            const string shaderCode = "${resultType} ${resultName} = ${functionName}(${arguments});";
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", FunctionName},
                {"arguments", ShaderNodesUtil.BuildArguments(gpuValues.ToList())},
            };
            Setup(shaderCode, gpuValues, valueMap);
        }

        public sealed override List<string> MixIns { get; }
        
        public string FunctionName { get;  }
        
        public sealed override IDictionary<string, string> Functions { get; }
    }
}