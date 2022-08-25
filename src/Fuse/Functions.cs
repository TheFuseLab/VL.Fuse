using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;

namespace Fuse
{
    public abstract class AbstractFunction<T> : ShaderNode<T>
    {
        private bool _isGroupable;
        public virtual string FunctionName { get;  }

        protected AbstractFunction(string theFunction, ShaderNode<T> theDefault, bool theIsGroupable = false) : base(theFunction, theDefault)
        {
            FunctionName = theFunction;
            _isGroupable = theIsGroupable;
            OptionalOutputs = new List<AbstractShaderNode>();
        }

        protected void Setup(IEnumerable<AbstractShaderNode> theArguments, IEnumerable<InputModifier> theModifiers)
        {
            var abstractGpuValues = theArguments.ToList();

            if (theModifiers == null || theModifiers.Count() != abstractGpuValues.Count())
            {
                SetInputs(abstractGpuValues);
                return;
            }
            
            var myInputs = new List<AbstractShaderNode>();
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
                OptionalOutputs.Add(myDeclareValue);
                _isGroupable = false;
            }
            SetInputs(myInputs);
        }
        
        protected override Dictionary<string,string> CreateTemplateMap ()
        {
            var result = base.CreateTemplateMap();
            result["function"] = FunctionName;
            return result;
        }


        protected override string SourceTemplate()
        {
            if(!_isGroupable || Ins.Count <= 2)return "${resultType} ${resultName} = ${function}(${arguments});";

            var inputList = new List<AbstractShaderNode>(Ins);
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
    
    public class IntrinsicFunction<T> : AbstractFunction<T>
    {
        
        public IntrinsicFunction(
            IEnumerable<AbstractShaderNode> theArguments, 
            string theFunction, 
            ShaderNode<T> theDefault, 
            bool theIsGroupable = false, 
            IEnumerable<InputModifier> theModifiers = null
            ) : base(theFunction, theDefault, theIsGroupable)
        {
            Setup(theArguments, theModifiers);
        }
    }
    
    public class MixinFunction<T> : AbstractFunction<T>
    {

        public MixinFunction(
            IEnumerable<AbstractShaderNode> theArguments, 
            string theFunction, 
            ShaderNode<T> theDefault, 
            string theMixin, 
            bool theIsGroupable = false, 
            IEnumerable<InputModifier> theModifiers = null
            ) : base(theFunction, theDefault, theIsGroupable)
        {
            AddResource(Mixins, theMixin);
            Setup(theArguments, theModifiers);
        }
    }
    
    public sealed class CustomFunction<T>: AbstractFunction<T>
    {
        private readonly IEnumerable<IInvoke> _delegates;

        private readonly IDictionary<string, string> _functionValues;

        private readonly string _codeTemplate;

        private string _signature;
        
        public CustomFunction(
            IEnumerable<AbstractShaderNode> theArguments, 
            string theFunction, 
            string theCodeTemplate, 
            ShaderNode<T> theDefault, 
            IEnumerable<IInvoke> theDelegates = null, 
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

            _delegates = theDelegates;

            _functionValues = theFunctionValues;
            
            var inputs = theArguments.ToList();
            Ins = inputs;
            
            _codeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            
            Setup(theArguments, theModifiers);
        }
        
        private void HandleDelegates(IEnumerable<IInvoke> theDelegates, IDictionary<string, string> theFunctionValueMap)
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

        protected override void OnGenerateCode(ShaderGeneratorContext theContext)
        {
            _delegates?.ForEach(delegateNode =>
            {
                if (delegateNode == null) return;
                
                delegateNode.SetHashCodes(theContext);
            });
            
            Functions.Clear();
            
            _signature = base.FunctionName + HashCode;

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"signature", _signature}
            };
            
            HandleDelegates(_delegates,functionValueMap);

            _functionValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            Functions.Add(_signature, ShaderNodesUtil.Evaluate(_codeTemplate, functionValueMap) + Environment.NewLine);
            
        }

        public override IDictionary<string, string> Functions { get; }

        public override string FunctionName => _signature;
    }
    
    
}