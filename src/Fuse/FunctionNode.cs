using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fuse
{
    public abstract class AbstractFunctionNode<T> : ShaderNode<T>
    {
        private bool _isGroupable;

        private string _functionName;
        protected AbstractFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault, bool theIsGroupable = false) : base(theFunction, theDefault)
        {
            _isGroupable = theIsGroupable;
            _functionName = theFunction;
            Setup(theArguments, new Dictionary<string, string> {{"function", theFunction}});
        }


        protected override string SourceTemplate()
        {
            if(!_isGroupable || Ins.Count() <= 2)return "${resultType} ${resultName} = ${function}(${arguments});";

            var inputList = new List<AbstractGpuValue>(Ins);
            var call = new StringBuilder("${function}(" + inputList[0].ID + ", " + inputList[1].ID + ")");

            for (var i = 2; i < inputList.Count();i++)
            {
                call.Insert(0, "${function}(");
                call.Append(", ");
                call.Append(inputList[i].ID);
                call.Append(")");
            }
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${Call};",new Dictionary<string,string>
            {
                {"Call",call.ToString()}
            });
        }
        
    }
    
    public class IntrinsicFunctionNode<T> : AbstractFunctionNode<T>
    {
        
        public IntrinsicFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault, bool theIsGroupable = false) : base(theArguments, theFunction, theDefault, theIsGroupable)
        {
            
        }
    }
    
    public class MixinFunctionNode<T> : AbstractFunctionNode<T>
    {

        public MixinFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault, string theMixin, bool theIsGroupable = false) : base(theArguments,theFunction, theDefault, theIsGroupable)
        {
            AddResource(Mixins, theMixin);
        }
    }
    
    public sealed class CustomFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public CustomFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            string theCodeTemplate, 
            ConstantValue<T> theDefault, 
            IEnumerable<IFunctionInvokeNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null,
            bool theIsGroupable = false
        ) : base(theArguments, theFunction, theDefault, theIsGroupable)
        {

            if (theMixins != null)
            {
                var mixinList = new ArrayList();
                theMixins.ForEach(m => mixinList.Add(m));
                AddResources(Mixins, mixinList);
            }
            Functions = new Dictionary<string, string>();
            
            var signature = theFunction + GetHashCode();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"signature", signature}
            };

            var inputs = theArguments.ToList();
            Ins = inputs;
            HandleDelegates(theDelegates,functionValueMap);

            theCodeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderNodesUtil.Evaluate(theCodeTemplate, functionValueMap) + Environment.NewLine);
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
        
        public override IDictionary<string, string> Functions { get; }
    }
    
    public sealed class PatchedFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public PatchedFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            AbstractGpuValue theFunction,
            string theName,
            ConstantValue<T> theDefault, 
            IEnumerable<IFunctionInvokeNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null
        ) : base(theArguments, "patchedFunction", theDefault)
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