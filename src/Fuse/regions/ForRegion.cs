using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;

namespace Fuse.regions
{
    
        
        public class ForGroup: ShaderNode<GpuVoid> 
        {
            private readonly ShaderNode<int> _index;
            private readonly ShaderNode<int> _inEnd;
            public ForGroup(
                ShaderNode<int> index, 
                ShaderNode<int> inEnd, 
                IEnumerable<AbstractShaderNode> theInputs
                ) : base("ForGroup")
            {
                _index = index;
                _inEnd = inEnd;
                var abstractShaderNodes = new List<AbstractShaderNode>();
                theInputs.ForEach(input =>
                {
                    if (input == null) return;
                    abstractShaderNodes.Add(input);
                });
            
                SetInputs(abstractShaderNodes);
            }

            protected override string SourceTemplate()
            {
                return "";
            }
            
            protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<int> theHashes)
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
                            {"indexStart", "indexStart_" + new Random().Next()}
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
                theSourceBuilder.AppendLine();
            }
        }
        
        public class ForRegionNode : ShaderNode<GpuVoid>
        {
            
            private readonly ShaderNode<int> _inEnd;
            private ShaderNode<GpuVoid> _inCall;
            private ShaderNode<GpuVoid> _crossLinkCall;
            private ShaderNode<GpuVoid> _forGroup;
            private readonly List<AbstractShaderNode> _crossLinks;
            
            public ForRegionNode(
                ShaderNode<int> inEnd, 
                IEnumerable<AbstractShaderNode> theInputs, 
                IEnumerable<AbstractShaderNode> theOutputs, 
                IEnumerable<AbstractShaderNode> theCrossLinks, 
                string outputName = "result"
                ) : base("for region", null)
            {
                _inEnd = inEnd;
                var outputs = theOutputs.ToList();
                _crossLinks = theCrossLinks.ToList();
                OptionalOutputs = new List<AbstractShaderNode>();
                Setup(theInputs, outputs);
            }
            
            private List<AbstractShaderNode> ResolveVoidCrossLink(AbstractShaderNode theCrossLink)
            {
                var myResult = new List<AbstractShaderNode>();

                theCrossLink.Children.ForEach(child =>
                {
                    if (!(child is AbstractShaderNode input)) return;
                    switch (input)
                    {
                        case ShaderNode<GpuVoid> gpuValue:
                            myResult.AddRange(ResolveVoidCrossLink(gpuValue));
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
                        case ShaderNode<GpuVoid> gpuValue:
                            myCrossLinks.AddRange(ResolveVoidCrossLink(gpuValue));
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
                        case ShaderNode<GpuVoid> gpuValue:
                            var myInputVoid = inputs[i];
                            myInputs.Add(myInputVoid);
                            myOutputs.Add(gpuValue);
                            var myPassVoid = AbstractCreation.AbstractShaderNodePassThrough(gpuValue);
                            myPassVoid = this;
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
                            myPass = this;
                            OptionalOutputs.Add(myPass);
                            break;
                    }
                }

                _inCall = new Group(myInputs);
                _crossLinkCall = new Group(myCrossLinks);
                
                var index = inputs.Count > 0 ? inputs[0] as ShaderNode<int> : new ConstantValue<int>(0);
                _forGroup = new ForGroup(index, _inEnd, myOutputs);
                
                var inputList = new List<AbstractShaderNode>
                {
                    _crossLinkCall,
                    _inEnd,
                    _inCall,
                    _forGroup
                };
                
                SetInputs(inputList);
            }

            protected override string SourceTemplate()
            {
                return "";
            }
            
            
        }
}