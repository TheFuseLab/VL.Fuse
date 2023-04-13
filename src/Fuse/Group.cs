using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class Group: ShaderNode<GpuVoid>, IComputeVoid
    {
        public Group(NodeContext nodeContext, IEnumerable<AbstractShaderNode> theInputs, string name = "Group2") : base(nodeContext, name)
        {
            NullInputMode = HandleNullInputMode.Remove;
            SetInputs(theInputs);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public class Do2<T>: ShaderNode<T>
    {
        
        private readonly ShaderNode<T> _input;
        public Do2(NodeContext nodeContext, ShaderNode<T> theOne, IEnumerable<AbstractShaderNode> theInputs, string name = "Do2") : base(nodeContext, name)
        {
            NullInputMode = HandleNullInputMode.Remove;
            _input = theOne;
            var ins = new List<AbstractShaderNode>();
            foreach (var input in theInputs)
            {
                ins.Add(input);
            }
            ins.Add(theOne);
            SetInputs(ins);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        public override string ID => _input.ID;
    }
}