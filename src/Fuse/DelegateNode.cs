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

    public abstract class AbstractDelegateNode<T> : ShaderNode<T>, IDelegateNode
    {
        protected AbstractDelegateNode(string theId, ConstantValue<T> theDefault = null, string outputName = "result") : base(theId, theDefault, outputName)
        {
            
            MixIns = new List<string>();
            Functions = new Dictionary<string, string>();
            Declarations = new List<string>();
            Inputs = new List<IGpuInput>();
        }
        
        protected void AddDelegate(string theFunctionName, AbstractGpuValue theDelegate)
        {
            var delegates = theDelegate.ParentNode.Delegates();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"functionName", theFunctionName},
                {"arguments", DelegateUtil.BuildArguments(delegates)},
                {"functionImplementation", theDelegate.ParentNode.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";
            Functions.Add(theFunctionName, ShaderTemplateEvaluator.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            theDelegate.ParentNode.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
            MixIns.AddRange(theDelegate.ParentNode.MixinList());
            Declarations.AddRange(theDelegate.ParentNode.DeclarationList());
            Inputs.AddRange(theDelegate.ParentNode.InputList());
        }
        
        public sealed override IDictionary<string, string> Functions { get; }
        public sealed override List<string> MixIns { get; }
        public override List<string> Declarations { get; }
        public override List<IGpuInput> Inputs { get; }
        
    }

    public class DelegateNode<T> : AbstractDelegateNode<T>
    {
        
        public DelegateNode(AbstractGpuValue theDelegate, IEnumerable<AbstractGpuValue> theParameters, ConstantValue<T> theDefault = null) : base("Delegate", theDefault)
        {
            FunctionName = "delegateFunction" + GetHashCode();


            var gpuValues = theParameters.ToList();
            
            AddDelegate(FunctionName,theDelegate);
            
            const string shaderCode = "${resultType} ${resultName} = ${functionName}(${arguments});";
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", FunctionName},
                {"arguments", ShaderNodesUtil.BuildArguments(gpuValues.ToList())},
            };
            Setup(shaderCode, gpuValues, valueMap);
        }

        public string FunctionName { get;  }
        
        
    }
}