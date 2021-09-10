using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Fuse
{

    public class FunctionParameterValue<T> : GpuValue<T>
    {

        public FunctionParameterValue(string theName) : base(theName)
        {
        }

        public override string ID => Name;

    }
    
    public interface IFunctionParameter : IShaderNode
    {
        string TypeName();

        string Name();

        void Remap(List<AbstractGpuValue> theParameters);

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

        public FunctionParameter(GpuValue<T> theType, int theId = 0): base("delegate", null,"delegate")
        {
            Output = new FunctionParameterValue<T>("arg_" + theId)
            {
                ParentNode = this
            };
            _name = "arg_" + theId;
            Ins = new List<AbstractGpuValue>();
            _id = theId;
        }

        public void Remap(List<AbstractGpuValue> theParameters)
        {
            if (_id >= theParameters.Count()) return;
            Output.Name = "arg_"+theParameters[_id].ID;
        }

        public void DeleteRemap()
        {
            Output.Name = _name;
        }

        public virtual string TypeName()
        {
            return TypeHelpers.GetGpuTypeForType<T>();
        }

        public string Name()
        {
            return Output.ID;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public interface IFunctionInvokeNode
    {
         string Name { get; }
         string FunctionName { get;  }
         
         IDictionary<string, string> Functions { get; }


         Dictionary<string, IList> ResourcesForTree();
    }

    public class FunctionInvokeNode<T> : ShaderNode<T>, IFunctionInvokeNode
    {
        public FunctionInvokeNode(AbstractGpuValue theDelegate, IEnumerable<AbstractGpuValue> theParameters, string theId, ConstantValue<T> theDefault = null, string outputName = "result") : base(theId, theDefault, outputName)
        {
            Functions = new Dictionary<string, string>();

            Name = theId;
            FunctionName = theId + (theDelegate?.HashCode ?? HashCode);
            
            var gpuValues = theParameters.ToList();
            
            AddFunctionInvoke(FunctionName,theDelegate, gpuValues);
            
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", FunctionName}
            };
            Setup(gpuValues, valueMap);
        }

        private void AddFunctionInvoke(string theFunctionName, AbstractGpuValue theDelegate, List<AbstractGpuValue> theParameters)
        {
            if (theDelegate == null) return;
            var delegates = theDelegate.ParentNode.Delegates();
            delegates.ForEach(input => input.Remap(theParameters));
            
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"functionName", theFunctionName},
                {"arguments", BuildArguments(theParameters)},
                {"functionImplementation", theDelegate.ParentNode.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";
            Functions.Add(theFunctionName, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            theDelegate.ParentNode.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
            theDelegate.ParentNode.ResourcesForTree().ForEach(kv =>
            {
                AddResources(kv.Key, kv.Value );
            });
            
            delegates.ForEach(input => input.DeleteRemap());
        }
        
        private static string BuildArguments(IEnumerable<AbstractGpuValue> inputs)
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
            return "${resultType} ${resultName} = ${functionName}(${arguments});";
        }

        public sealed override IDictionary<string, string> Functions { get; }
        public string FunctionName { get;  }

        public string Name { get; }
    }
}