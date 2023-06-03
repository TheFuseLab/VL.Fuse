using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.function;
using Fuse.Function;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse
{
    public abstract class AbstractFunction<T> : ShaderNode<T>
    {
        private bool _isGroupable;

        private readonly int _groupOptions;

        private readonly IEnumerable<InputModifier> _modifiers;

        public virtual string FunctionName { get; }

        protected AbstractFunction(
            NodeContext nodeContext, 
            string theFunction, 
            IEnumerable<AbstractShaderNode> theArguments, 
            IEnumerable<InputModifier> theModifiers,
            ShaderNode<T> theDefault, 
            bool theIsGroupable = false,
            int theGroupOptions = 0
            ) : base(nodeContext, theFunction, theDefault)
        {
            FunctionName = theFunction;
            _modifiers = theModifiers;
            _isGroupable = theIsGroupable;
            _groupOptions = theGroupOptions;
            OptionalOutputs = new List<AbstractShaderNode>();
            Setup(theArguments);
        }

        protected void Setup(IEnumerable<AbstractShaderNode> theArguments)
        {
            var abstractGpuValues = theArguments.ToList();

            if (_modifiers == null || _modifiers.Count() != abstractGpuValues.Count())
            {
                SetInputs(abstractGpuValues);
                return;
            }
            
            var subContextFactory = new NodeSubContextFactory(NodeContext);
            
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

                var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(subContextFactory,abstractGpuValues[i]);
                myInputs.Add(myDeclareValue);

                var output = AbstractCreation.AbstractOutput(subContextFactory, this, myDeclareValue);
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
            var resultPart = "";
            if (!TypeHelpers.IsVoidNode(this))
            {
                resultPart = "${resultType} ${resultName} = ";
            }
            if(!_isGroupable || Ins.Count <= 2)return resultPart + "${function}(${arguments});";

            var inputList = new List<AbstractShaderNode>(Ins);
            var call = new StringBuilder("${function}(" + inputList[0].ID + ", " + inputList[1].ID);

            var optionIndex = inputList.Count - _groupOptions;
            for (var index = optionIndex; index < inputList.Count ; index++)
            {
                call.Append(", ");
                call.Append(inputList[index].ID);
            }

            call.Append(')');

            for (var i = 2; i < optionIndex;i++)
            {
                call.Insert(0, "${function}(");
                call.Append(", ");
                call.Append(inputList[i].ID);
                
                for (var index = optionIndex; index < inputList.Count ; index++)
                {
                    call.Append(", ");
                    call.Append(inputList[index].ID);
                }
                
                call.Append(")");
            }
            return ShaderNodesUtil.Evaluate(resultPart + "${Call};",new Dictionary<string,string>
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
            IEnumerable<AbstractShaderNode> theArguments,
            bool theIsGroupable = false, 
            IEnumerable<InputModifier> theModifiers = null) : base(nodeContext, theFunction, theArguments, theModifiers, theDefault, theIsGroupable)
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
            IEnumerable<AbstractShaderNode> theArguments, 
            bool theIsGroupable = false,
            int theGroupOptions = 0,
            IEnumerable<InputModifier> theModifiers = null) : base(nodeContext, theFunction, theArguments, theModifiers, theDefault, theIsGroupable, theGroupOptions)
        {
            AddProperty(Mixins, theMixin);
        }
    }
    
    public sealed class CustomFunction<T>: AbstractFunction<T>
    {
        private readonly Dictionary<string, IDelegate> _delegates;

        private readonly string _codeTemplate;

        private readonly IDictionary<string, string> _templateValues;

        private string Signature => base.FunctionName + HashCode;

        public CustomFunction(
            NodeContext nodeContext,
            string theFunction,
            string theCodeTemplate,
            ShaderNode<T> theDefault,
            IEnumerable<AbstractShaderNode> theArguments, 
            Dictionary<string, IDelegate> theDelegates,
            IEnumerable<string> theMixins = null,
            IDictionary<string, string> theFunctionValues = null,
            bool theIsGroupable = false,
            IEnumerable<InputModifier> theModifiers = null) : base(nodeContext, theFunction,theArguments, theModifiers, theDefault, theIsGroupable)
        {
            

            UpdateMixins(theMixins);
            
            Functions = new Dictionary<string, string>();

            _delegates = theDelegates;

            _codeTemplate = ShaderNodesUtil.IndentCode(theCodeTemplate);

            _templateValues = theFunctionValues;

            BuildFunction();
        }

        private void UpdateMixins(IEnumerable<string> theMixins)
        {
            if (theMixins == null) return;

            var mixinList = new ArrayList();
            foreach (var mixin in theMixins)
            {
                mixinList.Add(mixin);
            }
            AddProperties(Mixins, mixinList);
        }

        private void BuildFunction()
        {
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuType<T>()},
                {"signature", Signature}
            };

            HandleDelegates(_delegates,functionValueMap);

            _templateValues?.ForEach(kv => functionValueMap.Add(kv.Key, kv.Value));
            
            Functions.Add(Signature, ShaderNodesUtil.Evaluate(_codeTemplate, functionValueMap) + Environment.NewLine);
        }

        
        private void HandleDelegates(Dictionary<string,IDelegate> theDelegates, IDictionary<string, string> theFunctionValueMap)
        {
            if (theDelegates == null) return;

            foreach (var delegateNode in theDelegates)
            {
                if (delegateNode.Value == null) continue;
                
                theFunctionValueMap.Add(delegateNode.Key, delegateNode.Value.FunctionName);
                delegateNode.Value.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
                delegateNode.Value.PropertiesForTree().ForEach(kv =>
                {
                    AddProperties(kv.Key, kv.Value);
                });
            }
        }

        public override void OnPassContext(ShaderGeneratorContext theContext)
        {
            _delegates?.ForEach(delegateNode =>
            {
                if (delegateNode.Value == null) return;
                
                delegateNode.Value.CheckContext(theContext);
            });
        }

        public override IDictionary<string, string> Functions { get; protected set; }

        public override string FunctionName => Signature;
    }
    
    
}