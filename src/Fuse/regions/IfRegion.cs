using System;
using System.Collections.Generic;
using System.Text;
using VL.Core;
using VL.Core.PublicAPI;

namespace Fuse.regions
{
    public class IfGroup : Group
    {
        private readonly ShaderNode<bool> _inCheck;

        public IfGroup(
            NodeContext nodeContext,
            ShaderNode<bool> inCheck,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, theInputs,"IfGroup")
        {
            Name = "IfGroup";
            _inCheck = inCheck;
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<AbstractShaderNode> theHashes, string thePrepend)
        {
            if (!theHashes.Add(this)) return;
            
            if (ShaderNodesUtil.DebugShaderGeneration) Console.WriteLine(thePrepend + ID);

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
            BuildChildrenSource(myChildSourceBuilder, theHashes, thePrepend);
            var myChildSource = myChildSourceBuilder.ToString();
            myChildSource = ShaderNodesUtil.IndentCode(myChildSource);
            theSourceBuilder.Append(myChildSource);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source.Trim()) && theHashes.Add(this))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }

            theSourceBuilder.Append(@"        }
");
        }
    }

    public class IfRegion : AbstractRegion
    {

        public IfRegion(
            NodeContext nodeContext,
            ShaderNode<bool> inCheck,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks,
            IEnumerable<BorderControlPointDescription> theDescriptions) : base(nodeContext, "ifRegion")
        {
            SetupRegion(
                (subContextFactory, myOutputs) => new IfGroup(subContextFactory.NextSubContext(), inCheck, myOutputs),
                (theInputList) => { theInputList.Add(inCheck);},
                theInputs,
                theOutputs,
                theCrossLinks,
                theDescriptions);
        }
    }
}