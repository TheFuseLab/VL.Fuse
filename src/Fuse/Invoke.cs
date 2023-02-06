using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse
{

    public interface IFunctionParameter : IComputeNode
    {
        string TypeName();

        string ID { get; }

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

        public FunctionParameter(NodeContext nodeContext, ShaderNode<T> theType, int theId = 0): base(nodeContext, "arg_" + theId)
        {
            Ins = new List<AbstractShaderNode>();
            ArgumentNumber = theId;
        }

        public void Remap(List<AbstractShaderNode> theParameters)
        {
            if (ArgumentNumber >= theParameters.Count) return;
            Name = "arg_"+ShaderNodesUtil.FixName(theParameters[ArgumentNumber].DelegateID);
        }
        
        public override string ID => Name;

        public int ArgumentNumber { get; }

        public override string TypeName()
        {
            return TypeHelpers.GetGpuType<T>();
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public interface IInvoke
    {
         string Name { get; }
         string FunctionName { get;  }
         
         IDictionary<string, string> Functions { get; }


         Dictionary<string, IList> PropertiesForTree();

         public void CheckContext(ShaderGeneratorContext theContext);
    }

    public class InvokeChangeLister<T> : IChangeGraph
    {

        private readonly Invoke<T> _invoke;

        public InvokeChangeLister(Invoke<T> theInvoke)
        {
            _invoke = theInvoke;
        }
        public void ChangeGraph(AbstractShaderNode theNode)
        {
            _invoke.UpdateInvoke();
        }
    }

    public class Invoke<T> : ShaderNode<T>, IInvoke
    {
        private AbstractShaderNode _delegate;
        public Invoke(NodeContext nodeContext, string theId = "Invoke", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
            Functions = new Dictionary<string, string>();
            
            Name = theId;
            
            ChangeGraphListener.Add(new InvokeChangeLister<T>(this));
        }

        public void SetInputs(AbstractShaderNode theDelegate, IEnumerable<AbstractShaderNode> theParameters)
        {
            _delegate?.Outs.Remove(this);
            _delegate = theDelegate;

            if (theDelegate == null)
            {
                CallChangeEvent();
                return;
            }
            _delegate?.Outs.Add(this);
            
            
            Functions.Clear();
            Property.Clear();

            SetInputs(theParameters, false);
            
            UpdateInvoke();
            
            CallChangeEvent();
        }

        public override void CheckContext(ShaderGeneratorContext theContext)
        {
            _delegate?.CheckContext(theContext);
            base.CheckContext(theContext);
        }

        public void UpdateInvoke()
        {
            
            Functions.Clear();
            Property.Clear();

            var i = 0;
            foreach (var abstractShaderNode in Ins)
            {
                var myFunctionParameters = abstractShaderNode.FunctionParameters();
                if (myFunctionParameters.Count <= 0)
                {
                    i++;
                    continue;
                }
                AddFunctionInvoke(Name + "_" + abstractShaderNode.HashCode,abstractShaderNode);
                foreach (var parameter in _delegate.FunctionParameters())
                {
                    if (parameter.ArgumentNumber == i)
                    {
                        parameter.HashCode = abstractShaderNode.HashCode;
                    }
                }
                i++;
            }
            
            if (_delegate == null) return;
            AddFunctionInvoke(FunctionName, _delegate);
        }

        private void AddFunctionInvoke(string theFunctionName, AbstractShaderNode theDelegate)
        {
            if (theDelegate is null or IFunctionParameter) return;
            var functionParameters = theDelegate.FunctionParameters();
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuType<T>()},
                {"functionName", theFunctionName},
                {"arguments", BuildArguments(functionParameters)},
                {"functionImplementation", theDelegate.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";
            Functions.Add(theFunctionName, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
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
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(' ');
                stringBuilder.Append(ShaderNodesUtil.FixName(input.ID));
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
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
                    {"functionName", FunctionName}
                });
        }

        public sealed override IDictionary<string, string> Functions { get; protected set; }
        public string FunctionName => Name + "_" + _delegate.HashCode;

    }
}