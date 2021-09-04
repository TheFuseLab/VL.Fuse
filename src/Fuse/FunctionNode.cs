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
        protected AbstractFunctionNode(IEnumerable<AbstractGpuValue> theRegionInputs, string theFunction, ConstantValue<T> theDefault, bool theIsGroupable = false) : base(theFunction, theDefault)
        {
            _isGroupable = theIsGroupable;
            _functionName = theFunction;
            Setup(theRegionInputs, new Dictionary<string, string> {{"function", theFunction}});
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
        
        public IntrinsicFunctionNode(IEnumerable<AbstractGpuValue> theRegionInputs, string theFunction, ConstantValue<T> theDefault, bool theIsGroupable = false) : base(theRegionInputs, theFunction, theDefault, theIsGroupable)
        {
            
        }
    }
    
    public class MixinFunctionNode<T> : AbstractFunctionNode<T>
    {

        public MixinFunctionNode(IEnumerable<AbstractGpuValue> theRegionInputs, string theFunction, ConstantValue<T> theDefault, string theMixin, bool theIsGroupable = false) : base(theRegionInputs,theFunction, theDefault, theIsGroupable)
        {
            AddResource(Mixins, theMixin);
        }
    }
    
    public sealed class CustomFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public CustomFunctionNode(
            IEnumerable<AbstractGpuValue> theRegionInputs, 
            string theFunction, 
            string theCodeTemplate, 
            ConstantValue<T> theDefault, 
            IEnumerable<IFunctionInvokeNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null,
            bool theIsGroupable = false
        ) : base(theRegionInputs, theFunction, theDefault, theIsGroupable)
        {

            if (theMixins != null)
            {
                var mixinList = new ArrayList();
                theMixins.ForEach(m => mixinList.Add(m));
                AddResources(Mixins, mixinList);
            }
            Functions = new Dictionary<string, string>();
            
            var signature = theFunction + HashCode;

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"signature", signature}
            };

            var inputs = theRegionInputs.ToList();
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
}