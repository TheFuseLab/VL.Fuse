using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class Array<T> : ShaderNode<T> where T :struct
    {

        private string _sourceTemplate = "";
        
        public Array(NodeContext nodeContext) : base(nodeContext, "Array")
        {
        }
        
        public void SetConstantInput(ICollection<T> theInputs)
        {
            _sourceTemplate = "";
            
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
            Size = theInputs.Count; 
            SetProperty(ConstantArrays, constantArrayString);
        }

        public void SetDynamicInput(int theSize)
        {
            const string shaderCode = 
                @"    ${arrayType} ${arrayName}[${arraySize}];" ;
            
            _sourceTemplate = ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"arrayType", TypeHelpers.GetGpuType<T>()},
                {"arrayName", ID},
                {"arraySize", theSize.ToString()}
            });
            Size = theSize; 
        }

        public void SetDynamicInput(ICollection<ShaderNode<T>> theInputs)
        {
            
            var content = new StringBuilder("    ${arrayType} ${arrayName}[${arraySize}];"+Environment.NewLine);
            var i = 0;
            foreach (var input in theInputs)
            {
                content.Append("    ${arrayName}[" + i + "] =  " + input.ID + ";"+Environment.NewLine);
                i++;
            }
            theInputs.ForEach(input =>
            {
                
            });
            _sourceTemplate = ShaderNodesUtil.Evaluate(content.ToString(),new Dictionary<string, string>()
            {
                {"arrayType", TypeHelpers.GetGpuType<T>()},
                {"arrayName", ID},
                {"arraySize", theInputs.Count.ToString()}
            });
            SetInputs(new List<AbstractShaderNode>( theInputs));
        }
        
        protected override string SourceTemplate()
        {
            return _sourceTemplate;
        }

        public int Size { get; private set; }
    }
    
    public class ArrayGet<T> : ShaderNode<T>  where T :struct
    {
        private Array<T> _array;
        private ShaderNode<int> _index;
        
        public ArrayGet(NodeContext nodeContext) : base( nodeContext, "getItem")
        {
            
        }

        public void SetInputs(Array<T> theArray, ShaderNode<int> theIndex)
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