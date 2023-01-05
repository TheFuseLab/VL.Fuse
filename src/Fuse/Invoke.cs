using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse
{

    public interface IFunctionParameter : IAtomicComputeNode
    {
        string TypeName();

        void Remap(List<AbstractShaderNode> theParameters);

        void DeleteRemap();
    }

    public enum InputModifier
    {
        In,
        Out,
        InOut
    }
    
    public class FunctionParameter<T> : ShaderNode<T> , IFunctionParameter
    {
        private readonly string _name;
        private readonly int _id;

        public FunctionParameter(NodeContext nodeContext, ShaderNode<T> theType, int theId = 0): base(nodeContext, "delegate")
        {
            
            _name = "arg_" + theId;
            Ins = new List<AbstractShaderNode>();
            _id = theId;
        }

        public void Remap(List<AbstractShaderNode> theParameters)
        {
            if (_id >= theParameters.Count()) return;
            Name = "arg_"+theParameters[_id].DelegateID;
        }

        public override string ID => Name;

        public void DeleteRemap()
        {
            Name = _name;
        }

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

            SetInputs(theParameters, false);
            AddFunctionInvoke(FunctionName,_delegate, Ins);
            
            CallChangeEvent();
        }

        public override void CheckContext(ShaderGeneratorContext theContext)
        {
            _delegate?.CheckContext(theContext);
            base.CheckContext(theContext);
        }

        public void UpdateInvoke()
        {
            if (_delegate == null) return;
            AddFunctionInvoke(FunctionName, _delegate, Ins);
        }

        private void AddFunctionInvoke(string theFunctionName, AbstractShaderNode theDelegate, List<AbstractShaderNode> theParameters)
        {
            if (theDelegate == null) return;
            Functions.Clear();
            Property.Clear();
            var functionParameters = theDelegate.FunctionParameters();
            functionParameters.ForEach(input => input.Remap(theParameters));
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuType<T>()},
                {"functionName", theFunctionName},
                {"arguments", BuildArguments(theParameters)},
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
            
            functionParameters.ForEach(input => input.DeleteRemap());
        }
        
        private static string BuildArguments(IEnumerable<AbstractShaderNode> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(" ");
                stringBuilder.Append("arg_"+input.DelegateID);
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