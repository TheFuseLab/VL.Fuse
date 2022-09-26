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

    public class IfRegion : ShaderNode<GpuVoid>
    {
        private ShaderNode<bool> _inCheck;
        private ShaderNode<GpuVoid> _ifGroup;
        
        protected Group InCall;
        protected Group CrossLinkCall;

        protected int SubContextIds;

        public IfRegion(NodeContext nodeContext) : base(nodeContext, "ifRegion")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
        }

        public void Setup(
            ShaderNode<bool> inCheck,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks)
        {
            SubContextIds = 0;
            OptionalOutputs.Clear();

            var inputs = theInputs.ToList();
            var outputs = theOutputs.ToList();
            var crossLinks = theCrossLinks.ToList();
            
            if (inputs.Count != outputs.Count)
            {
                SetInputs(inputs);
                return;
            }

            var myCrossLinks = new List<AbstractShaderNode>();
            crossLinks.ForEach(c =>
            {
                if (c == null) return;
                myCrossLinks.Add(c);
            });

            var myInputs = new List<AbstractShaderNode>();
            var myOutputs = new List<AbstractShaderNode>();

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = inputs[i];
                        myInputs.Add(myInputVoid);
                        myOutputs.Add(shaderNode);

                        break;
                    default:
                        var myInput = i >= inputs.Count ? AbstractCreation.AbstractConstant(NodeContext, SubContextIds++, outputs[i], 0f) : inputs[i];
                        var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(NodeContext, SubContextIds++, myInput);
                        myInputs.Add(myDeclareValue);

                        var myOutput = i >= outputs.Count
                            ? AbstractCreation.AbstractConstant(NodeContext, SubContextIds++, inputs[i], 0f)
                            : outputs[i];
                        var myAssign = AbstractCreation.AbstractAssignNode(NodeContext, SubContextIds++, myDeclareValue, myOutput);
                        myOutputs.Add(myAssign);

                        break;
                }
            }

            InCall = new Group(ShaderNodesUtil.GetContext(NodeContext, SubContextIds++));
            InCall.SetInput(myInputs);

            CrossLinkCall = new Group(ShaderNodesUtil.GetContext(NodeContext, SubContextIds++));
            CrossLinkCall.SetInput(myCrossLinks);

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = myInputs[i];
                        var outputVoid = AbstractCreation.AbstractOutput(NodeContext, SubContextIds++, this, myInputVoid);
                        OptionalOutputs.Add(outputVoid);

                        break;
                    default:
                        var myInput = myInputs[i];
                        var output = AbstractCreation.AbstractOutput(NodeContext, SubContextIds++, this, myInput);
                        OptionalOutputs.Add(output);

                        break;
                }
            }
            
            _inCheck = inCheck;

            _ifGroup = new IfGroup(ShaderNodesUtil.GetContext(NodeContext, SubContextIds++), _inCheck, myOutputs);

            var inputList = new List<AbstractShaderNode>
            {
                CrossLinkCall,
                _inCheck,
                InCall,
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