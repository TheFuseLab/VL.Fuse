using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class GpuArray<T> where T :struct
    {
        
    } 
    public class Array<T> : ShaderNode<GpuArray<T>> where T :struct
    {

        private readonly string _sourceTemplate = "";
        
        public Array(NodeContext nodeContext, ICollection<T> theInputs) : base(nodeContext, "Array")
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
        
        public Array(NodeContext nodeContext, int theSize) : base(nodeContext, "Array")
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
        public Array(NodeContext nodeContext, ICollection<ShaderNode<T>> theInputs) : base(nodeContext, "Array")
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

        public int Size { get; }
    }
    
    public class ArrayGet<T> : ShaderNode<T>  where T :struct
    {
        private readonly Array<T> _array;
        private readonly ShaderNode<int> _index;
        
        public ArrayGet(NodeContext nodeContext, Array<T> theArray, ShaderNode<int> theIndex) : base( nodeContext, "getItem")
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