using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using VL.Core;

namespace Fuse.compute
{
    
    public class DynamicStruct: ShaderNode<GpuStruct>
    {
        private readonly string _name;
        
        public DynamicStruct(NodeContext nodeContext, IEnumerable<AbstractShaderNode> theInputs, string theName) : base(nodeContext, "GPUAttributeStruct")
        {
            SetInputs(theInputs);

            _name = theName;
            
            const string shaderCode = 
@"    struct ${structName}{
${structMembers}
    };" ;
            
            var myStride = 0;
            var call = new StringBuilder();
            Ins.ForEach(input =>
            {
                call.Append("        "+TypeHelpers.GetGpuTypeByValue(input) + " " + input.Name+";"+Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(input);

                
            });
            var _struct = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structName", theName},
                {"structMembers", call.ToString()}
            });
            
            AddProperty(Structs, _struct);
            Stride = myStride;

            TypeOverride = theName;
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

        public int Stride { get; }
    }
}