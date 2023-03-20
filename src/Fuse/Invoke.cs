using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse
{

    public interface IFunctionParameter : IComputeNode
    {
        string TypeName();

        string ID { get; }

        InputModifier Modifier { get; }

        string ModifierString();

        int ArgumentNumber { get; }
        uint HashCode { get; set; }
    }

    public enum InputModifier
    {
        In,
        Out,
        InOut
    }
    
    public class FunctionParameter<T> : ShaderNode<T> , IFunctionParameter
    {

        public FunctionParameter(NodeContext nodeContext, ShaderNode<T> theType, InputModifier theInputModifier = InputModifier.In,  int theId = 0): base(nodeContext, "arg_" + theId)
        {
            Ins = new List<AbstractShaderNode>();
            Modifier = theInputModifier;
            ArgumentNumber = theId;
        }
        
        public override string ID => Name;

        public string ModifierString()
        {
            return Modifier switch
            {
                InputModifier.In => "in",
                InputModifier.InOut => "inout",
                InputModifier.Out => "out",
                _ => ""
            };
        }

        public int ArgumentNumber { get; }
        
        public InputModifier Modifier { get; }

        public override string TypeName()
        {
            return TypeHelpers.GetGpuType<T>();
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }

    public interface IDelegate
    {
        string Name { get; }
        string FunctionName { get; }
        
        IDictionary<string, string> Functions { get; }
        
        Dictionary<string, IList> PropertiesForTree();

        public void CheckContext(ShaderGeneratorContext theContext);
    }

    public class Delegate<T> : ShaderNode<T>, IDelegate
    {

        private AbstractShaderNode _delegate;

        public List<IFunctionParameter> Parameters { get; private set; }

        public Delegate(NodeContext nodeContext, AbstractShaderNode theDelegate, string theId = "Function", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
            if (theDelegate == null) return;
            
            
            
            BuildFunction(theDelegate);
        }

        protected void BuildFunction(AbstractShaderNode theDelegate)
        {
            _delegate = theDelegate;
            
            Parameters = theDelegate.FunctionParameters();
            
            Functions = new Dictionary<string, string>();
            
            

            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuType<T>()},
                {"functionName", FunctionName},
                {"arguments", BuildArguments(Parameters)},
                {"functionImplementation", theDelegate.BuildSourceCode()},
                {"result", theDelegate.ID}
            };
            string functionCode;
            if (TypeHelpers.IsVoidNode(theDelegate))
            {
                functionCode = @"${resultType} ${functionName}(${arguments}){
                ${functionImplementation}
            }";
            }
            else
            {
                functionCode = @"${resultType} ${functionName}(${arguments}){
                ${functionImplementation}
                return ${result};
            }";
            }
            Functions.Add(FunctionName, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            theDelegate.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
            theDelegate.PropertiesForTree().ForEach(kv =>
            {
                AddProperties(kv.Key, kv.Value );
            });
        }
        
        private static string BuildArguments(List<IFunctionParameter> inputs)
        {
            inputs.Sort((a,b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));
            var usedIDs = new HashSet<string>();
            
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (usedIDs.Contains(input.ID)) return;
                usedIDs.Add(input.ID);
                stringBuilder.Append(input.ModifierString());
                stringBuilder.Append(' ');
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(' ');
                stringBuilder.Append(ShaderNodesUtil.FixName(input.ID));
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }

        public override void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            _delegate ?. CheckContext(nodeContext);
        }
        
        public sealed override IDictionary<string, string> Functions { get; protected set; }

        protected override string SourceTemplate()
        {
            return "";
        }

        public virtual string FunctionName => Name + "_" + HashCode;
    }

    public class Invoke<T> : ShaderNode<T>
    {
        private readonly Delegate<T> _delegate;
        public Invoke(NodeContext nodeContext, Delegate<T> theDelegate, IEnumerable<AbstractShaderNode> theValues, string theId = "Invoke", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
            Functions = new Dictionary<string, string>();
            
            Name = theId;
            
            _delegate = theDelegate;
            
            OptionalOutputs = new List<AbstractShaderNode>();
         
            SetInputs(theDelegate, theValues);
        }

        private void SetInputs(Delegate<T> theDelegate, IEnumerable<AbstractShaderNode> theValues)
        {
            if (theDelegate == null) return;
            
            var inputs = new List<AbstractShaderNode> { theDelegate };
            
            var subContextFactory = new NodeSubContextFactory(NodeContext);
            var myValues = theValues.ToList();
            var countIns = 0;

            for (var i = 0; i < theDelegate.Parameters.Count; i++)
            {
                var myParameter = theDelegate.Parameters[i];
                var myModifier = myParameter.Modifier;
                AbstractShaderNode myValue;
                switch (myModifier)
                {
                    case InputModifier.In:
                        myValue = myValues[countIns++];
                        inputs.Add(myValue);
                        break;
                    case InputModifier.Out:
                        var myDeclareValue = AbstractCreation.AbstractDeclareValue(subContextFactory, myParameter as AbstractShaderNode);
                        inputs.Add(myDeclareValue);
                        var output = AbstractCreation.AbstractOutput(subContextFactory, this, myDeclareValue);
                        OptionalOutputs.Add(output);
                        break;
                    case InputModifier.InOut:
                        myValue = myValues[countIns++];
                        var myDeclareValueAssigned = AbstractCreation.AbstractDeclareValueAssigned(subContextFactory,myValue);
                        inputs.Add(myDeclareValueAssigned);
                        var outputAssigned = AbstractCreation.AbstractOutput(subContextFactory, this, myDeclareValueAssigned);
                        OptionalOutputs.Add(outputAssigned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }


            }
            
            SetInputs(inputs);
        }

        protected override string SourceTemplate()
        {
            if (_delegate == null)
            {
                return GenerateDefaultSource();
            }
            const string shader = "${resultType} ${resultName} = ${functionName}(${arguments});";

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"functionName", _delegate.FunctionName},
                    {"arguments", ShaderNodesUtil.BuildArguments(Ins.GetRange(1, Ins.Count - 1))}
                });
        }

        public sealed override IDictionary<string, string> Functions { get; protected set; }

    }
}