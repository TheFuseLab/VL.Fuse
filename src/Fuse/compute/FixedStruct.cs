using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse.compute
{
    public class FixedStruct<T>: ShaderNode<GpuStruct>
    {
        private readonly int _stride;
        
        public FixedStruct() : base("GPUFixedStruct")
        {
            var name = typeof(T).Name;
            Setup(new List<AbstractGpuValue>());
            
            const string shaderCode = 
                @"    struct ${structName}{
${structMembers}
    };" ;
            var myStride = 0;
            var call = new StringBuilder();
            typeof(T).GetProperties().ForEach(property =>
            {
                call.Append("        "+TypeHelpers.GetGpuTypeForType(property.PropertyType) + " " + property.Name+";"+Environment.NewLine);
                myStride += TypeHelpers.GetGpuTypeSizeForType(property.PropertyType);
            });
            Struct = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structName", name},
                {"structMembers", call.ToString()}
            });
            
            Structs = new List<string>(){Struct};
            _stride = myStride;

            Output.TypeOverride = name;
        }
        
        public sealed override List<string> Structs { get; }
        
        public string Struct { get; }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        public int Stride()
        {
            return _stride;
        }
    }
}