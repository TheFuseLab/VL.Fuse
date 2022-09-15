using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using VL.Core;

namespace Fuse.regions
{
    public class ForGroup : ShaderNode<GpuVoid>
    {
        private readonly ShaderNode<int> _index;
        private readonly ShaderNode<int> _inEnd;


        public ForGroup(
            NodeContext nodeContext,
            ShaderNode<int> index,
            ShaderNode<int> inEnd,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, "ForGroup")
        {
            
            _index = index;
            _inEnd = inEnd;

            SetInputs(theInputs);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            if (!theHashes.Add(HashCode)) return;

            const string shaderCode = @"
        for(int ${indexStart} = 0; ${index} < ${end};${index}++){";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string>
                    {
                        {"end", _inEnd.ID},
                        {"index", _index.ID},
                        {"indexStart", "indexStart_" + ID}
                    }
                )
            );
            theSourceBuilder.AppendLine();
            var myChildSourceBuilder = new StringBuilder();
            BuildChildrenSource(myChildSourceBuilder, theHashes);
            var myChildSource = myChildSourceBuilder.ToString();
            myChildSource = ShaderNodesUtil.IndentCode(myChildSource);
            theSourceBuilder.Append(myChildSource);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }

            theSourceBuilder.AppendLine(@"
        }");
        }


        protected override string SourceTemplate()
        {
            return "";
        }
    }

    public class ForRegionNode : ShaderNode<GpuVoid>
    {
        private ShaderNode<int> _inEnd;
        private Group _inCall;
        private Group _crossLinkCall;
        private ShaderNode<GpuVoid> _forGroup;
        
        private int _subContextIds;

        public ForRegionNode(NodeContext nodeContext) : base(nodeContext, "for region")
        {
            _subContextIds = 0;
            OptionalOutputs = new List<AbstractShaderNode>();
        }

        public void Setup(
            ShaderNode<int> inEnd,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks)
        {
            _subContextIds = 0;
            _inEnd = inEnd;

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
                switch (c)
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        myCrossLinks.AddRange(ResolveVoidCrossLink(shaderNode));
                        //myCrossLinks.Add(AbstractCreation.AbstractShaderNodePassThrough(c));
                        break;
                    default:
                        //myCrossLinks.Add(AbstractCreation.AbstractDeclareValueAssigned(c));
                        //myCrossLinks.Add(AbstractCreation.AbstractShaderNodePassThrough(c));
                        myCrossLinks.Add(c);
                        break;
                }
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
                        var myInput = i >= inputs.Count ? AbstractCreation.AbstractConstant(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), outputs[i], 0f) : inputs[i];
                        var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), myInput);
                        myInputs.Add(myDeclareValue);

                        var myOutput = i >= outputs.Count
                            ? AbstractCreation.AbstractConstant(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), inputs[i], 0f)
                            : outputs[i];
                        var myAssign = AbstractCreation.AbstractAssignNode(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), myDeclareValue, myOutput);
                        myOutputs.Add(myDeclareValue);
                        
                        break;
                }
            }

            _inCall = new Group(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++));
            _inCall.SetInput(myInputs);
            
            _crossLinkCall = new Group(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++));
            _crossLinkCall.SetInput(myCrossLinks);
            
            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = myInputs[i];
                        var outputVoid = AbstractCreation.AbstractOutput(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), this, myInputVoid);
                        OptionalOutputs.Add(outputVoid);

                        break;
                    default:
                        var myInput = myInputs[i];
                        var output = AbstractCreation.AbstractOutput(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), this, myInput);
                        OptionalOutputs.Add(output);

                        break;
                }
            }

            var index = inputs.Count > 0 ? inputs[0] as ShaderNode<int> : new ConstantValue<int>(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), 0);
            _forGroup = new ForGroup(ShaderNodesUtil.GetContext(NodeContext, _subContextIds++), index, _inEnd, myOutputs);

            var inputList = new List<AbstractShaderNode>
            {
                _crossLinkCall,
                _inEnd,
                _inCall,
                _forGroup
            };

            SetInputs(inputList);
        }

        private List<AbstractShaderNode> ResolveVoidCrossLink(AbstractShaderNode theCrossLink)
        {
            var myResult = new List<AbstractShaderNode>();

            theCrossLink.Children.ForEach(child =>
            {
                if (!(child is AbstractShaderNode input)) return;
                switch (input)
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        myResult.AddRange(ResolveVoidCrossLink(shaderNode));
                        break;
                    default:
                        myResult.Add(input);
                        break;
                }
            });
            return myResult;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
}