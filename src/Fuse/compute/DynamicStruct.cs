using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VL.Core;

namespace Fuse.compute
{
    
    public class DynamicStruct<T>: ShaderNode<T>
    {
        private readonly string _structName;
        
        public DynamicStruct(NodeContext nodeContext,IEnumerable<AbstractShaderNode> theInputs, string theName, T instance) : base(nodeContext, "GPUAttributeStruct")
        {
            _structName = theName;
            
            const string shaderCode = 
                @"    struct ${structName}{
${structMembers}
    };" ;
            
            var myStride = 0;
            var call = new StringBuilder();
            theInputs.ForEach(input =>
            {
                call.Append("        "+TypeHelpers.GetGpuType(input) + " " + input.Name+";"+Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(input);

                
            });
            var structString = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structName", theName},
                {"structMembers", call.ToString()}
            });
            
            SetProperty(Structs, structString);
            Stride = myStride;

            TypeOverride = theName;
            
            SetInputs(theInputs);
        }

        private string _sourceTemplate = "";
        
        public DynamicStruct(NodeContext nodeContext,Dictionary<string,AbstractShaderNode> theInputs, T instance, bool AddTypesToName = false) : base(nodeContext, "GPUAttributeStruct")
        {
            var _name  = TypeHelpers.GetGpuType<T>();
            Name = ShaderNodesUtil.FirstLetterToLower(_name);

            if (AddTypesToName)
            {
                _name = theInputs.Aggregate(_name, (current, input) => current + TypeHelpers.GetGpuType(input.Value));
            }

            _structName = _name;
            
            const string shaderCode = 
                """
                    struct ${structName}{
                ${structMembers}
                    };
                """ ;
            
            var myStride = 0;
            var call = new StringBuilder();
            foreach (var kv in theInputs)
            {
                call.Append("        "+TypeHelpers.GetGpuType(kv.Value) + " " + kv.Key+";"+Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(kv.Value);
            }
            var structString = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structName", _structName},
                {"structMembers", call.ToString()}
            });
            
            var template = new StringBuilder();
            template.Append("${resultType} ${resultName};"+Environment.NewLine);
            foreach (var kv in theInputs)
            {
                template.Append("        ${resultName}." + kv.Key + " = "+ kv.Value.ID+";"+Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(kv.Value);
            }
            
            _sourceTemplate = ShaderNodesUtil.Evaluate(template.ToString(),
                new Dictionary<string, string>
                {
                    {"resultType", _structName}
                });
            
            SetProperty(Structs, structString);
            Stride = myStride;

            TypeOverride = _structName;
            
            SetInputs(theInputs.Values);
        }

        protected override string SourceTemplate()
        {
            return _sourceTemplate;
           
        }

        public List<string> Description {
            get
            {
                var result = new List<string>();
                Ins.ForEach(input => result.Add(_structName + "." + TypeHelpers.GetDescription(input)));
                return result;
            }
        }

        public int Stride { get; private set; }
    }
}