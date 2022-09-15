using System.Collections.Generic;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class Group: ShaderNode<GpuVoid>, IComputeVoid
    {
        public Group(NodeContext nodeContext, string name = "Group2") : base(nodeContext, name)
        {
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
}