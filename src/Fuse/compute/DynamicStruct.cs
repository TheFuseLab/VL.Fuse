using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using VL.Core;

namespace Fuse.compute
{
    
    public class DynamicStruct: ShaderNode<GpuStruct>
    {
        private string _name;
        
        public DynamicStruct(NodeContext nodeContext) : base(nodeContext, "GPUAttributeStruct")
        {
            
        }

        public void SetInput(IEnumerable<AbstractShaderNode> theInputs, string theName)
        {
            _name = theName;
            
            const string shaderCode = 
                @"    struct ${structName}{
${structMembers}
    };" ;
            
            var myStride = 0;
            var call = new StringBuilder();
            theInputs.ForEach(input =>
            {
                call.Append("        "+TypeHelpers.GetGpuTypeByValue(input) + " " + input.Name+";"+Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(input);

                
            });
            var structString = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structName", theName},
                {"structMembers", call.ToString()}
            });
            
            AddProperty(Structs, structString);
            Stride = myStride;

            TypeOverride = theName;
            
            SetInputs(theInputs);
        }

        protected override string SourceTemplate()
        {
            return "";
        }

        public List<string> Description {
            get
            {
                var result = new List<string>();
                Ins.ForEach(input => result.Add(_name + "." + TypeHelpers.GetDescription(input)));
                return result;
            }
        }

        public int Stride { get; private set; }
    }
}