using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core.Extensions;

namespace Fuse
{
    public abstract class AbstractFunctionNode<T> : ShaderNode<T>
    {
        protected AbstractFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault) : base(theFunction, theDefault)
        {
            Setup(theArguments, new Dictionary<string, string> {{"function", theFunction}});
        }


        protected override string SourceTemplate()
        {
            return "${resultType} ${resultName} = ${function}(${arguments});";
        }
        
    }
    
    public class IntrinsicFunctionNode<T> : AbstractFunctionNode<T>
    {
        
        public IntrinsicFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault) : base(theArguments, theFunction, theDefault)
        {
            
        }
    }
    
    public class MixinFunctionNode<T> : AbstractFunctionNode<T>
    {

        public MixinFunctionNode(IEnumerable<AbstractGpuValue> theArguments, string theFunction, ConstantValue<T> theDefault, string theMixin) : base(theArguments,theFunction, theDefault)
        {
            MixIns = new List<string>(){theMixin};
        }

        public sealed override List<string> MixIns { get; }
    }
    
    public sealed class CustomFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public CustomFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            string theCodeTemplate, 
            ConstantValue<T> theDefault, 
            IEnumerable<IDelegateNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null
        ) : base(theArguments, theFunction, theDefault)
        {
            MixIns = new List<string>();
            if(theMixins!=null)MixIns.AddRange(theMixins);
            Functions = new Dictionary<string, string>();
            Declarations = new List<string>();
            Inputs = new List<IGpuInput>();
            
            var arguments = theArguments.ToList();
            var signature = theFunction + GetHashCode();

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"signature", signature}
            };

            var inputs = new List<AbstractGpuValue>(arguments);
            Ins = inputs;
            HandleDelegates(theDelegates,functionValueMap);

            theCodeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderTemplateEvaluator.Evaluate(theCodeTemplate, functionValueMap) + Environment.NewLine);
            Setup(inputs, new Dictionary<string, string> {{"function", signature}});
        }
        
        private void HandleDelegates(IEnumerable<IDelegateNode> theDelegates, IDictionary<string, string> theFunctionValueMap)
        {
            theDelegates?.ForEach(delegateNode =>
            {
                if (delegateNode == null) return;
                
                theFunctionValueMap.Add(delegateNode.Name, delegateNode.FunctionName);
                delegateNode.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
                MixIns.AddRange(delegateNode.MixIns);
                Declarations.AddRange(delegateNode.Declarations);
                Inputs.AddRange(delegateNode.Inputs);
            });
        }
        
        public override IDictionary<string, string> Functions { get; }
        public override List<string> MixIns { get; }
        public override List<string> Declarations { get; }
        public override List<IGpuInput> Inputs { get; }
    }
}