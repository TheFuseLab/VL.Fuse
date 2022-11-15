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
        
        public override void BuildChildrenSource( StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            //Console.WriteLine(Name);
            foreach (var child in Children)
            {
                if (child is not ShaderTree input)
                {
                    return;
                }
               
                input.Node.BuildSource( theSourceBuilder, theHashes);
            }
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            BuildChildrenSource(theSourceBuilder, theHashes);

            if (!theHashes.Add(HashCode))return;
            
            var source = SourceCode;
            //Console.Out.WriteLine(Name + " : " + HashCode);
            if (!string.IsNullOrWhiteSpace(source))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }
        }
    }
}