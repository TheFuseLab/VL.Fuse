using System;
using System.Collections.Generic;
using Fuse.compute;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Fuse.regions
{
    public class IfGroup: Group 
        {
            private readonly ShaderNode<bool> _inCheck;
            public IfGroup(
                ShaderNode<bool> inCheck, 
                IEnumerable<AbstractShaderNode> theInputs
                ) : base(theInputs, "IfGroup")
            {
                _inCheck = inCheck;
            }
            
            protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
            {
                Console.Out.WriteLine("Yo");
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
                if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
                {
                    theSourceBuilder.Append("        " + source + Environment.NewLine);
                }
                theSourceBuilder.AppendLine(@"
        }");
                theSourceBuilder.AppendLine();
            }
        }
        
        public class IfRegionNode : ShaderNode<GpuVoid>
        {
            
            private readonly ShaderNode<bool> _inCheck;
            private ShaderNode<GpuVoid> _inCall;
            private ShaderNode<GpuVoid> _crossLinkCall;
            private ShaderNode<GpuVoid> _ifGroup;
            private readonly List<AbstractShaderNode> _crossLinks;
            
            public IfRegionNode(
                ShaderNode<bool> inCheck, 
                IEnumerable<AbstractShaderNode> theInputs, 
                IEnumerable<AbstractShaderNode> theOutputs, 
                IEnumerable<AbstractShaderNode> theCrossLinks
                ) : base("if region")
            {
                _inCheck = inCheck;
                var outputs = theOutputs.ToList();
                _crossLinks = theCrossLinks.ToList();
                OptionalOutputs = new List<AbstractShaderNode>();
                Setup(theInputs, outputs);
            }

            private static IEnumerable<AbstractShaderNode> ResolveVoidCrossLink(AbstractShaderNode theCrossLink)
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

            private void Setup(IEnumerable<AbstractShaderNode> theInputs, IEnumerable<AbstractShaderNode> theOutputs)
            {
                var inputs = theInputs.ToList();
                var outputs = theOutputs.ToList();
                if (inputs.Count() != outputs.Count())
                {
                    SetInputs(inputs);
                    return;
                }
                
                var myCrossLinks = new List<AbstractShaderNode>();
                _crossLinks.ForEach(c =>
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
                            var myPassVoid = AbstractCreation.AbstractShaderNodePassThrough(shaderNode);
                            myPassVoid.AddInput(this);
                           // myPassVoid = this;
                            OptionalOutputs.Add(myPassVoid);
                            break;
                        default:
                            var myInput = i >= inputs.Count ? AbstractCreation.AbstractConstant(outputs[i], 0f) : inputs[i];
                            var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(myInput);
                            myInputs.Add(myDeclareValue);
                    
                            var myOutput = i >= outputs.Count ? AbstractCreation.AbstractConstant(inputs[i], 0f) : outputs[i];
                            var myAssign = AbstractCreation.AbstractAssignNode(myDeclareValue, myOutput);
                            myOutputs.Add(myAssign);
                            var myPass = AbstractCreation.AbstractShaderNodePassThrough(myDeclareValue);
                            myPass.AddInput(this);
                            // myPass = this;
                            OptionalOutputs.Add(myPass);
                            break;
                    }
                }

                _inCall = new Group(myInputs);
                _crossLinkCall = new Group(myCrossLinks);
                _ifGroup = new IfGroup(_inCheck, myOutputs);
                
                var inputList = new List<AbstractShaderNode>
                {
                    _crossLinkCall,
                    _inCheck,
                    _inCall,
                    _ifGroup
                };
                SetInputs(inputList);
            }
            
            protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
            {
                var nodes = new List<AbstractShaderNode>();
                Children.ForEach(child =>
                {
                    if (!(child is ShaderTree input))
                    {
                        return;
                    }
                    nodes.Add(input.Node);
                    input.Node.BuildSource( theSourceBuilder, theHashes);
                });
                

                var source = SourceCode;
                if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
                {
                    theSourceBuilder.Append("        " + source + Environment.NewLine);
                }
            }

            protected override string SourceTemplate()
            {
                return "";
            }
            
        }
}