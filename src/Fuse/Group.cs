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
        public Group(NodeContext nodeContext, string name = "Group2") : base(nodeContext, name)
        {
            NullInputMode = HandleNullInputMode.Remove;
        }

        public void SetInput(IEnumerable<AbstractShaderNode> theInputs, bool theCallChangeEvent = true)
        {
            SetInputs(theInputs, theCallChangeEvent);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public class Do2<T>: ShaderNode<T>
    {
        
        private ShaderNode<T> _input;
        public Do2(NodeContext nodeContext, string name = "Do2") : base(nodeContext, name)
        {
            NullInputMode = HandleNullInputMode.Remove;
        }

        public void SetInput(ShaderNode<T> theOne, IEnumerable<AbstractShaderNode> theInputs, bool theCallChangeEvent = true)
        {
            _input = theOne;
            var ins = new List<AbstractShaderNode>();
            foreach (var input in theInputs)
            {
                ins.Add(input);
            }
            ins.Add(theOne);
            SetInputs(ins, theCallChangeEvent);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        public override string ID => _input.ID;
    }
}