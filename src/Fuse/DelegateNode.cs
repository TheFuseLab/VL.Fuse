using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using Stride.Engine;

namespace Fuse
{
    class DelegateUtil
    {

        public delegate void BuildInputData(StringBuilder theBuilder, KeyValuePair<string,AbstractGpuValue> theInput);
        
        internal static string BuildInput(IDictionary<string,AbstractGpuValue> inputs, BuildInputData theInputBuilder)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input => { theInputBuilder(stringBuilder, input); });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        internal static string BuildArguments(IDictionary<string,AbstractGpuValue> inputs)
        {
            return BuildInput(inputs, (stringBuilder, input) =>
            {
                stringBuilder.Append(TypeHelpers.GetType(input.Value).ToLower() + " ");
                stringBuilder.Append(input.Key);
                stringBuilder.Append(", ");
            });
        }
        internal static string BuildArgumentsWithId(IDictionary<string,AbstractGpuValue> inputs)
        {
            return BuildInput(inputs, (stringBuilder, input) =>
            {
                stringBuilder.Append(TypeHelpers.GetType(input.Value).ToLower() + " ");
                stringBuilder.Append(input.Value.ID);
                stringBuilder.Append(", ");
            });
        }

        internal static string BuildCallWithId(IDictionary<string,AbstractGpuValue> inputs)
        {
            return BuildInput(inputs, (stringBuilder, input) =>
            {
                stringBuilder.Append(input.Value.ID);
                stringBuilder.Append(", ");
            });
        }
        
        internal static string BuildCall(IDictionary<string,AbstractGpuValue> inputs)
        {
            return BuildInput(inputs, (stringBuilder, input) =>
            {
                stringBuilder.Append(input.Key);
                stringBuilder.Append(", ");
            });
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
        string FunctionName { get;  }
    }

    public class DelegateNode<T> : ShaderNode<T> ,IDelegateNode
    {
        public DelegateNode(AbstractGpuValue theDelegate, IEnumerable<AbstractGpuValue> theParameters, bool theUseInTemplate, ConstantValue<T> theDefault = null) : base("Delegate", theDefault)
        {
            FunctionName = "delegateFunction" + GetHashCode();
            
            Functions = new Dictionary<string, string>();
            
            var myParameterMap = new Dictionary<string, AbstractGpuValue>();
            var c = 0;
            var abstractGpuValues = theParameters.ToList();
            abstractGpuValues.ForEach(parameter =>
            {
                myParameterMap.Add("val" + c++, parameter);
            });

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"functionName", FunctionName},
                {"arguments", DelegateUtil.BuildArguments(myParameterMap)},
                {"functionImplementation", theDelegate.ParentNode.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"${resultType} ${functionName}(${arguments}){
${functionImplementation}
    return ${result};
}";
            Functions.Add(FunctionName, ShaderTemplateEvaluator.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            
            var shaderCode = !theUseInTemplate ? "${resultType} ${resultName} = ${functionName}(${arguments});" : "";
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", FunctionName},
                {"arguments", DelegateUtil.BuildCallWithId(myParameterMap)},
            };
            Setup(shaderCode, abstractGpuValues, valueMap);
        }

        public DelegateNode() : base("Delegate")
        {
            
        }
        
        public string FunctionName { get;  }
        
        public sealed override IDictionary<string, string> Functions { get; }
    }
    
    public class DelegateShaderNode<T> : ShaderNode<T> ,IDelegateNode
    {
        
        public DelegateShaderNode(AbstractGpuValue theDelegate, IEnumerable<AbstractGpuValue> theParameters, bool theUseInTemplate, ConstantValue<T> theDefault = null) : base("Delegate", theDefault)
        {
            FunctionName = "delegateFunction" + GetHashCode();
            
            Functions = new Dictionary<string, string>();
            
            var myParameterMap = new Dictionary<string, AbstractGpuValue>();
            var c = 0;
            var abstractGpuValues = theParameters.ToList();
            abstractGpuValues.ForEach(parameter =>
            {
                myParameterMap.Add("val" + c++, parameter);
            });
            
            var shaderValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"functionName", FunctionName},
                {"arguments", DelegateUtil.BuildArguments(myParameterMap)},
                {"functionImplementation", theDelegate.ParentNode.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string shaderTemplateCode = @"
shader ${shaderID} : ${mixins}{
    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
    return ${result};
    }
}";
            ShaderCode = ShaderTemplateEvaluator.Evaluate(shaderTemplateCode, shaderValueMap);
            ShaderName = "Shader_" + ShaderCode.GetHashCode();

            ShaderCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string> {{"shaderID", ShaderName}});
            const string shaderComposition = "${shaderID} composition;";
            Functions.Add(FunctionName, ShaderTemplateEvaluator.Evaluate(shaderComposition, new Dictionary<string, string> {{"ShaderID", ShaderName}}) + Environment.NewLine);
            
            var shaderCallCode = !theUseInTemplate ? "${resultType} ${resultName} = ${shaderID}.${functionName}(${arguments});" : "";
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", FunctionName},
                {"arguments", DelegateUtil.BuildCallWithId(myParameterMap)},
                {"ShaderID", ShaderName}
            };
            Setup(shaderCallCode, abstractGpuValues, valueMap);
        }

        public void RegisterShader(Game theGame)
        {
            theGame.EffectSystem.AddShaderSource(ShaderName, ShaderCode, "shaders\\" + ShaderName+".sdsl");
        }
        
        public string FunctionName { get;  }
        
        public string ShaderName { get;  }
        
        public string ShaderCode { get;  }
        
        public sealed override IDictionary<string, string> Functions { get; }
    }
    
    public class Delegate1Node<TIn, TOut> : ShaderNode<TOut> 
    {
        public delegate GpuValue<TOut> Delegate1(GpuValue<TIn> theArg1);
        
        public Delegate1Node(Delegate1 theDelegate, GpuValue<TIn> theParameter, bool theUseInTemplate, ConstantValue<TOut> theDefault = null) : base("Delegate", theDefault)
        {
            var inputs = new List<AbstractGpuValue>()
            {
                theParameter
            };
            
            var functionName = "delegateFunction" + GetHashCode();
            
            Functions = new Dictionary<string, string>();
            
            var myParameterMap = new Dictionary<string, AbstractGpuValue>();
            var c = 0;
            var abstractGpuValues = inputs.ToList();
            abstractGpuValues.ForEach(parameter =>
            {
                myParameterMap.Add("val" + c++, parameter);
            });
            
            var delegateNode = theDelegate(theParameter);
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<TOut>().ToLower()},
                {"functionName", functionName},
                {"arguments", DelegateUtil.BuildArgumentsWithId(myParameterMap)},
                {"functionImplementation", delegateNode.ParentNode.BuildSourceCode()},
                {"result", delegateNode.ID}
            };

            const string functionCode = @"${resultType} ${functionName}(${arguments}){
${functionImplementation}
    return ${result};
}";
            Functions.Add(functionName, ShaderTemplateEvaluator.Evaluate(functionCode, functionValueMap) + Environment.NewLine);

            var shaderCode = !theUseInTemplate ? "${resultType} ${resultName}" : "" + " = ${functionName}(${arguments});";
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", functionName},
                {"arguments", DelegateUtil.BuildCallWithId(myParameterMap)},
            };
            Setup(shaderCode, inputs, valueMap);
        }
        
        public sealed override IDictionary<string, string> Functions { get; }
    }
}