using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Fuse.regions
{
    public class FunctionRegion
    {
        public sealed class RegionFunctionParameter<T> : ShaderNode<T> 
    {

        public RegionFunctionParameter(GpuValue<T> theType, int theId = 0): base("argument", null,"argument")
        {
            Output = new FunctionParameterValue<T>("val" + GetHashCode())
            {
                ParentNode = this
            };
            Ins = new List<AbstractGpuValue>();
            Output.Name = "arg_"+theId;
        }

        public string TypeName()
        {
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        public string Name()
        {
            return Output.ID;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public sealed class RegionFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public RegionFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            AbstractGpuValue theFunction,
            string theName,
            ConstantValue<T> theDefault, 
            IEnumerable<IFunctionInvokeNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null,
            bool theIsGroupable = false,
            IEnumerable<InputModifier> theModifiers = null
        ) : base("patchedFunction", theDefault, theIsGroupable)
        {
            if (theMixins != null)
            {
                var mixinList = new ArrayList();
                theMixins.ForEach(m => mixinList.Add(m));
                AddResources(Mixins, mixinList);
            }
            Functions = new Dictionary<string, string>();
            
            var signature = theName + BuildSignature(theArguments)  +"To" + TypeHelpers.GetSignatureTypeForType<T>();

            
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"functionName", signature},
                {"arguments", BuildArguments(theArguments)},
                {"functionImplementation", theFunction.ParentNode.BuildSourceCode()},
                {"result", theFunction.ID}
            };
            
            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";

            var inputs = theArguments.ToList();
            Ins = inputs;
            HandleDelegates(theDelegates,functionValueMap);

            Functions.Add(signature, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            Setup(inputs, new Dictionary<string, string> {{"function", signature}});
            
        }
        
        private void HandleDelegates(IEnumerable<IFunctionInvokeNode> theDelegates, IDictionary<string, string> theFunctionValueMap)
        {
            theDelegates?.ForEach(delegateNode =>
            {
                if (delegateNode == null) return;
                
                theFunctionValueMap.Add(delegateNode.Name, delegateNode.FunctionName);
                delegateNode.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
                delegateNode.ResourcesForTree().ForEach(kv =>
                {
                    AddResources(kv.Key, kv.Value);
                });
            });
        }
        
        private static string BuildSignature(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input => stringBuilder.Append(TypeHelpers.GetSignatureTypeForType(input.GetType().GetGenericArguments()[0])));
            return stringBuilder.ToString();
        }
        
        private static string BuildArguments(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            var c = 0;
            inputs.ForEach(input =>
            {
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(" ");
                stringBuilder.Append("arg_"+c);
                stringBuilder.Append(", ");
                c++;
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }
        
        public override IDictionary<string, string> Functions { get; }
    }
    }
}