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

        protected AbstractFunctionNode(string theFunction, GpuValue<T> theDefault, bool theIsGroupable = false) : base(theFunction, theDefault)
        {
            _isGroupable = theIsGroupable;
            OptionalOutputs = new List<AbstractGpuValue>();
        }

        protected void Setup(IEnumerable<AbstractGpuValue> theArguments, IEnumerable<InputModifier> theModifiers, string theFunction)
        {
            var abstractGpuValues = theArguments.ToList();
            if (theModifiers == null || theModifiers.Count() != abstractGpuValues.Count())
            {
                Setup(abstractGpuValues, new Dictionary<string, string> {{"function", theFunction}});
                return;
            }
            
            var myInputs = new List<AbstractGpuValue>();
            var myModifiers = theModifiers.ToList();

            for (var i = 0; i < abstractGpuValues.Count(); i++)
            {
                var myModifier = myModifiers[i];
                if (myModifier == InputModifier.In)
                {
                    myInputs.Add(abstractGpuValues[i]);
                    continue;
                }

                var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(abstractGpuValues[i]);
                myInputs.Add(myDeclareValue);
                var myPass = AbstractCreation.AbstractGpuValuePassThrough(myDeclareValue);
                myPass.ParentNode = this;
                OptionalOutputs.Add(myPass);
                _isGroupable = false;
            }
            Setup(myInputs, new Dictionary<string, string> {{"function", theFunction}});
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
        
        public IntrinsicFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            GpuValue<T> theDefault, 
            bool theIsGroupable = false, 
            IEnumerable<InputModifier> theModifiers = null
            ) : base(theFunction, theDefault, theIsGroupable)
        {
            Setup(theArguments, theModifiers, theFunction);
        }
    }
    
    public class MixinFunctionNode<T> : AbstractFunctionNode<T>
    {

        public MixinFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            GpuValue<T> theDefault, 
            string theMixin, 
            bool theIsGroupable = false, 
            IEnumerable<InputModifier> theModifiers = null
            ) : base(theFunction, theDefault, theIsGroupable)
        {
            AddResource(Mixins, theMixin);
            Setup(theArguments, theModifiers, theFunction);
        }
    }
    
    public sealed class CustomFunctionNode<T>: AbstractFunctionNode<T>
    {
        
        public CustomFunctionNode(
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            string theCodeTemplate, 
            GpuValue<T> theDefault, 
            IEnumerable<IFunctionInvokeNode> theDelegates = null, 
            IEnumerable<string> theMixins = null, 
            IDictionary<string,string> theFunctionValues = null,
            bool theIsGroupable = false,
            IEnumerable<InputModifier> theModifiers = null
        ) : base(theFunction, theDefault, theIsGroupable)
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

            var inputs = theArguments.ToList();
            Ins = inputs;
            HandleDelegates(theDelegates,functionValueMap);

            theCodeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            theFunctionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(signature, ShaderNodesUtil.Evaluate(theCodeTemplate, functionValueMap) + Environment.NewLine);
            //Setup(inputs, new Dictionary<string, string> {{"function", signature}});
            
            Setup(theArguments, theModifiers, signature);
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