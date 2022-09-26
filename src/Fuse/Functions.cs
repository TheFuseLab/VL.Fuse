using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse
{
    public abstract class AbstractFunction<T> : ShaderNode<T>
    {
        private bool _isGroupable;

        private readonly IEnumerable<InputModifier> _modifiers;

        private string _functionName;

        public virtual string FunctionName
        {
            get => _functionName;
            
        }

        protected AbstractFunction(
            NodeContext nodeContext, 
            string theFunction, 
            IEnumerable<InputModifier> theModifiers,
            ShaderNode<T> theDefault, 
            bool theIsGroupable = false
            ) : base(nodeContext, theFunction, theDefault)
        {
            _functionName = theFunction;
            _modifiers = theModifiers;
            _isGroupable = theIsGroupable;
            OptionalOutputs = new List<AbstractShaderNode>();
        }
        
        public virtual void SetArguments(IEnumerable<AbstractShaderNode> theArguments)
        {
            Setup(theArguments);
        }

        public void SetFunctionName(string theName)
        {
            Name = theName;
            _functionName = theName;
            CallChangeEvent();
        }

        protected void Setup(IEnumerable<AbstractShaderNode> theArguments)
        {
            var abstractGpuValues = theArguments.ToList();

            if (_modifiers == null || _modifiers.Count() != abstractGpuValues.Count())
            {
                SetInputs(abstractGpuValues);
                return;
            }
            
            var myInputs = new List<AbstractShaderNode>();
            var myModifiers = _modifiers.ToList();

            for (var i = 0; i < abstractGpuValues.Count; i++)
            {
                var myModifier = myModifiers[i];
                if (myModifier == InputModifier.In)
                {
                    myInputs.Add(abstractGpuValues[i]);
                    continue;
                }

                var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(NodeContext,i * 2 + 1,abstractGpuValues[i]);
                myInputs.Add(myDeclareValue);

                var output = AbstractCreation.AbstractOutput(NodeContext,i * 2 + 2, this, myDeclareValue);
                OptionalOutputs.Add(output);
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

            for (var i = 2; i < inputList.Count;i++)
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
            NodeContext nodeContext,
            string theFunction,
            ShaderNode<T> theDefault,
            bool theIsGroupable = false,
            IEnumerable<InputModifier> theModifiers = null) : base(nodeContext, theFunction, theModifiers, theDefault, theIsGroupable)
        {
            
        }
    }
    
    public class MixinFunction<T> : AbstractFunction<T>
    {

        public MixinFunction(
            NodeContext nodeContext,
            string theFunction,
            ShaderNode<T> theDefault,
            string theMixin,
            bool theIsGroupable = false,
            IEnumerable<InputModifier> theModifiers = null) : base(nodeContext, theFunction, theModifiers, theDefault, theIsGroupable)
        {
            AddProperty(Mixins, theMixin);
        }
    }

    public class DelegateChangeListener<T> : IChangeGraph
    {

        private readonly CustomFunction<T> _function;
        
        public DelegateChangeListener(CustomFunction<T> theFunction)
        {
            _function = theFunction;
        }
        
        public void ChangeGraph(AbstractShaderNode theNode)
        {
            _function.UpdateDelegates();
            _function.CallChangeEvent();
        }
    }
    
    public sealed class CustomFunction<T>: AbstractFunction<T>
    {
        private IEnumerable<IInvoke> _delegates;

        private string _codeTemplate;

        private IEnumerable<string> _mixins;

        private IDictionary<string, string> _templateValues;

        private string Signature => base.FunctionName + HashCode;

        private readonly DelegateChangeListener<T> _delegateChangeListener;

        public CustomFunction(
            NodeContext nodeContext,
            string theFunction,
            string theCodeTemplate,
            ShaderNode<T> theDefault,
            IEnumerable<string> theMixins = null,
            IDictionary<string, string> theFunctionValues = null,
            bool theIsGroupable = false,
            IEnumerable<InputModifier> theModifiers = null) : base(nodeContext, theFunction, theModifiers, theDefault, theIsGroupable)
        {
            

            _mixins = theMixins;
            
            Functions = new Dictionary<string, string>();

            _delegateChangeListener = new DelegateChangeListener<T>(this);

            _codeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);

            _templateValues = theFunctionValues;
        }

        public void SetMixins(IEnumerable<string> theMixins)
        {
            _mixins = theMixins;
            CallChangeEvent();
        }

        public void SetCodeTemplate(string theCodeTemplate)
        {
            _codeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);
            CallChangeEvent();
        }

        public void SetTemplateValues(IDictionary<string,string> theFunctionValues)
        {
            _templateValues = theFunctionValues;
            CallChangeEvent();
        }

        public void SetDelegates(IEnumerable<IInvoke> theDelegates)
        {
            if (_delegates != null)
            {
                foreach (var myDelegate in _delegates)
                {
                    (myDelegate as AbstractShaderNode)?.ChangeGraphListener.Remove(_delegateChangeListener);
                }
            }
            _delegates = theDelegates;
            if (_delegates != null)
            {
                foreach (var myDelegate in _delegates)
                {
                    (myDelegate as AbstractShaderNode)?.ChangeGraphListener.Add(_delegateChangeListener);
                }
            }
            CallChangeEvent();
        }

        private void UpdateMixins()
        {
            if (_mixins == null) return;

            var mixinList = new ArrayList();
            foreach (var mixin in _mixins)
            {
                mixinList.Add(mixin);
            }
            AddProperties(Mixins, mixinList);
        }

        public void UpdateDelegates()
        {
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"signature", Signature}
            };
            
            Functions.Clear();
            Property.Clear();
            
            UpdateMixins();
            HandleDelegates(_delegates,functionValueMap);

            _templateValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            
            Functions.Add(Signature, ShaderNodesUtil.Evaluate(_codeTemplate, functionValueMap) + Environment.NewLine);
        }

        public override void SetArguments(IEnumerable<AbstractShaderNode> theArguments)
        {
            UpdateDelegates();
            
           // var inputs = theArguments.ToList();
          //  Ins = inputs;

            Setup(theArguments);
        }
        
        private void HandleDelegates(IEnumerable<IInvoke> theDelegates, IDictionary<string, string> theFunctionValueMap)
        {
            if (theDelegates == null) return;

            foreach (var delegateNode in theDelegates)
            {
                if (delegateNode == null) continue;
                
                theFunctionValueMap.Add(delegateNode.Name, delegateNode.FunctionName);
                delegateNode.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
                delegateNode.PropertiesForTree().ForEach(kv =>
                {
                    AddProperties(kv.Key, kv.Value);
                });
            }
        }

        public override void CheckContext(ShaderGeneratorContext theContext)
        {
            _delegates?.ForEach(delegateNode =>
            {
                if (delegateNode == null) return;
                
                delegateNode.CheckContext(theContext);
            });
            
            base.CheckContext(theContext);
        }

        public override IDictionary<string, string> Functions { get; protected set; }

        public override string FunctionName => Signature;
    }
    
    
}