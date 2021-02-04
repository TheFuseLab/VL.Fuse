using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using Stride.Engine;

namespace Fuse
{

    public class DelegateValue<T> : GpuValue<T>
    {

        public DelegateValue(string theName) : base(theName)
        {
        }

        public override string ID => name;

    }
    
    public interface IDelegateParameter : IShaderNode
    {
        string TypeName();

        string Name();

        void Remap(List<AbstractGpuValue> theParameters);

        void DeleteRemap();
    }
    
    public class DelegateParameter<T> : ShaderNode<T> , IDelegateParameter
    {
        private string _name = "";
        private int id;

        public DelegateParameter(GpuValue<T> theType, int theId = 0): base("delegate", null,"delegate")
        {
            Output = new DelegateValue<T>("val" + GetHashCode())
            {
                ParentNode = this
            };
            _name = "val" + GetHashCode();
            Ins = new List<AbstractGpuValue>();
            id = theId;
        }

        public void Remap(List<AbstractGpuValue> theParameters)
        {
            if (id >= theParameters.Count()) return;
            Console.WriteLine(Output.ID + " : " + theParameters[id].ID);
            Output.name = "arg_"+theParameters[id].ID;
            Console.WriteLine(Output.ID + " : " + theParameters[id].ID);
        }

        public void DeleteRemap()
        {
            Console.WriteLine("Delete Remap");
            Output.name = _name;
        }

        public virtual string TypeName()
        {
            return TypeHelpers.GetNameForType<T>().ToLower();
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
    
    public interface IDelegateNode
    {
         string Name { get; }
         string FunctionName { get;  }
         
         IDictionary<string, string> Functions { get; }
         List<string> MixIns { get; }
         List<string> Declarations { get; }
         List<IGpuInput> Inputs { get; }
    }

    public class DelegateNode<T> : ShaderNode<T>, IDelegateNode
    {
        public DelegateNode(AbstractGpuValue theDelegate, IEnumerable<AbstractGpuValue> theParameters, string theId, ConstantValue<T> theDefault = null, string outputName = "result") : base(theId, theDefault, outputName)
        {
            
            MixIns = new List<string>();
            Functions = new Dictionary<string, string>();
            Declarations = new List<string>();
            Inputs = new List<IGpuInput>();

            Name = theId;
            FunctionName = theId + GetHashCode();
            
            var gpuValues = theParameters.ToList();
            
            AddDelegate(FunctionName,theDelegate, gpuValues);
            
            var valueMap = new Dictionary<string, string>
            {
                {"functionName", FunctionName}
            };
            Setup(gpuValues, valueMap);
        }

        private void AddDelegate(string theFunctionName, AbstractGpuValue theDelegate, List<AbstractGpuValue> theParameters)
        {
            var delegates = theDelegate.ParentNode.Delegates();
            delegates.ForEach(input => input.Remap(theParameters));
            
            Console.WriteLine(theDelegate.ParentNode.BuildSourceCode());
            var functionValueMap = new Dictionary<string, string>
            {
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"functionName", theFunctionName},
                {"arguments", BuildArguments(theParameters)},
                {"functionImplementation", theDelegate.ParentNode.BuildSourceCode()},
                {"result", theDelegate.ID}
            };

            const string functionCode = @"    ${resultType} ${functionName}(${arguments}){
${functionImplementation}
        return ${result};
    }";
            Functions.Add(theFunctionName, ShaderTemplateEvaluator.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
            theDelegate.ParentNode.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
            MixIns.AddRange(theDelegate.ParentNode.MixinList());
            Declarations.AddRange(theDelegate.ParentNode.DeclarationList());
            Inputs.AddRange(theDelegate.ParentNode.InputList());
            
            
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
        public sealed override List<string> MixIns { get; }
        public override List<string> Declarations { get; }
        public override List<IGpuInput> Inputs { get; }

        public string FunctionName { get;  }

        public string Name { get; }
    }
}