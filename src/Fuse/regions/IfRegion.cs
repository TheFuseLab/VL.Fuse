using System;
using System.Collections.Generic;
using Fuse.compute;
using System.Linq;
using System.Text;
using VL.Core;

namespace Fuse.regions
{
    public class IfGroup : Group
    {
        private readonly ShaderNode<bool> _inCheck;

        public IfGroup(
            NodeContext nodeContext,
            ShaderNode<bool> inCheck,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, "IfGroup")
        {
            Name = "IfGroup";
            _inCheck = inCheck;

            SetInputs(theInputs);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            if (!theHashes.Add(HashCode)) return;

            const string shaderCode = @"
        if(${check}){";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string> {{"check", _inCheck.ID}}
                )
            );
            theSourceBuilder.AppendLine();
            var myChildSourceBuilder = new StringBuilder();
            BuildChildrenSource(myChildSourceBuilder, theHashes);
            var myChildSource = myChildSourceBuilder.ToString();
            myChildSource = ShaderNodesUtil.IndentCode(myChildSource);
            theSourceBuilder.Append(myChildSource);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source.Trim()) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }

            theSourceBuilder.Append(@"
        }");
            //   theSourceBuilder.AppendLine();
        }
    }

    public class IfRegion : AbstractRegion
    {
        private ShaderNode<bool> _inCheck;
        private ShaderNode<GpuVoid> _ifGroup;

        public IfRegion(NodeContext nodeContext) : base(nodeContext, "ifRegion")
        { }

        public void Setup(
            ShaderNode<bool> inCheck,
            List<AbstractShaderNode> theInputs,
            List<AbstractShaderNode> theOutputs,
            List<AbstractShaderNode> theCrossLinks)
        {
            SetupRegion(ref theInputs, ref theOutputs, ref theCrossLinks);
            _inCheck = inCheck;

            _ifGroup = new IfGroup(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), _inCheck, theOutputs);

            var inputList = new List<AbstractShaderNode>
            {
                _crossLinkCall,
                _inCheck,
                _inCall,
                _ifGroup
            };
            SetInputs(inputList);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            var nodes = new List<AbstractShaderNode>();
            Children.ForEach(child =>
            {
                if (!(child is ShaderTree input))
                {
                    return;
                }

                nodes.Add(input.Node);
                input.Node.BuildSource(theSourceBuilder, theHashes);
            });


            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }
        }
    }
}