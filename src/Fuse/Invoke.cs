using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;

namespace Fuse
{

    public interface IFunctionParameter : IAtomicComputeNode
    {
        string TypeName();

        string Namer();

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

        public FunctionParameter(ShaderNode<T> theType, int theId = 0): base("delegate", null)
        {
            
            _name = "arg_" + theId;
            Ins = new List<AbstractShaderNode>();
            _id = theId;
        }

        public void Remap(List<AbstractShaderNode> theParameters)
        {
            if (_id >= theParameters.Count()) return;
            Name = "arg_"+theParameters[_id].ID;
        }

        public override string ID => Name;

        public void DeleteRemap()
        {
            Name = _name;
        }

        public override string TypeName()
        {
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        public string Namer()
        {
            return ID;
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


         Dictionary<string, IList> ResourcesForTree();

         public void SetHashCodes(ShaderGeneratorContext theContext);
    }

    public class Invoke<T> : ShaderNode<T>, IInvoke
    {
        private readonly AbstractShaderNode _delegate;
        public Invoke(AbstractShaderNode theDelegate, IEnumerable<AbstractShaderNode> theParameters, string theId, ShaderNode<T> theDefault = null) : base("Invoke", theDefault)
        {
            Functions = new Dictionary<string, string>();

            _delegate = theDelegate;

            SetInputs(theParameters);
            
            Name = theId;
        }

        protected override void OnGenerateCode(ShaderGeneratorContext theContext)
        {
            Functions.Clear();
            _delegate.SetHashCodes(theContext);
            AddFunctionInvoke(FunctionName,_delegate, Ins);
        }

        protected override Dictionary<string,string> CreateTemplateMap ()
        {
            var result = base.CreateTemplateMap();
            result["function"] = FunctionName;
            return result;
        }

        private void AddFunctionInvoke(string theFunctionName, AbstractShaderNode theDelegate, List<AbstractShaderNode> theParameters)
        {
            if (theDelegate == null) return;
            var delegates = theDelegate.Delegates();
            delegates.ForEach(input => input.Remap(theParameters));
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
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
            theDelegate.ResourcesForTree().ForEach(kv =>
            {
                AddResources(kv.Key, kv.Value );
            });
            
            delegates.ForEach(input => input.DeleteRemap());
        }
        
        private static string BuildArguments(IEnumerable<AbstractShaderNode> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                stringBuilder.Append(input.TypeName());
                stringBuilder.Append(" ");
                stringBuilder.Append("arg_"+input.ID);
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

        public sealed override IDictionary<string, string> Functions { get; }
        public string FunctionName => Name + HashCode;

    }
}