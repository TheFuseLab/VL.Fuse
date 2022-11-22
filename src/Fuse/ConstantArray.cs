using System;
using System.Collections.Generic;
using System.Text;
using VL.Core;

namespace Fuse
{
    public class ConstantArray<T> : ShaderNode<T> where T :struct
    {
        public ConstantArray(NodeContext nodeContext) : base(nodeContext, "ConstantArray")
        {
        }
        
        public void SetInput(ICollection<T> theInputs)
        {
            
            
            const string shaderCode = 
                @"    static const ${arrayType} ${arrayName}[${arraySize}] = {
${arrayContent}
    };" ;
            
            var content = new StringBuilder();
            theInputs.ForEach(input =>
            {
                content.Append("        "+TypeHelpers.GetDefaultForType(input) + ","+Environment.NewLine);
            });
            var constantArrayString = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"arrayType", TypeHelpers.GetGpuType<T>()},
                {"arrayName", ID},
                {"arraySize", theInputs.Count.ToString()},
                {"arrayContent", content.ToString()}
            });
            
            SetProperty(ConstantArrays, constantArrayString);
        }
    }
    
    public class ConstantArrayGet<T> : ShaderNode<T>  where T :struct
    {
        private ConstantArray<T> _array;
        private ShaderNode<int> _index;
        
        public ConstantArrayGet(NodeContext nodeContext) : base( nodeContext, "getItem")
        {
            
        }

        public void SetInputs(ConstantArray<T> theArray, ShaderNode<int> theIndex)
        {
            _array = theArray;
            _index = theIndex;
            
            SetInputs(new List<AbstractShaderNode>{theArray,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${arrayName}[${index}];";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"arrayName", _array.ID},
                {"index", _index.ID}
            });
        }
    }
}