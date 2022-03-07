using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;

namespace Fuse.regions
{
    
        
        public class ForGroup: ShaderNode<GpuVoid> 
        {
            private readonly GpuValue<int> _index;
            private readonly GpuValue<int> _inStart;
            private readonly GpuValue<int> _inEnd;
            public ForGroup(
                GpuValue<int> index, 
                GpuValue<int> inStart, 
                GpuValue<int> inEnd, 
                IEnumerable<AbstractGpuValue> theInputs
                ) : base("ForGroup")
            {
                _index = index;
                _inStart = inStart;
                _inEnd = inEnd;
                var abstractGpuValues = new List<AbstractGpuValue>();
                theInputs.ForEach(input =>
                {
                    if (input == null) return;
                    abstractGpuValues.Add(input);
                });
            
                Setup(abstractGpuValues);
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
                            {"start", _inStart.ID}, 
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
            
            private readonly GpuValue<int> _inStart;
            private readonly GpuValue<int> _inEnd;
            private GpuValue<GpuVoid> _inCall;
            private GpuValue<GpuVoid> _crossLinkCall;
            private GpuValue<GpuVoid> _forGroup;
            private readonly List<AbstractGpuValue> _crossLinks;
            
            public ForRegionNode(
                GpuValue<int> inStart, 
                GpuValue<int> inEnd, 
                IEnumerable<AbstractGpuValue> theInputs, 
                IEnumerable<AbstractGpuValue> theOutputs, 
                IEnumerable<AbstractGpuValue> theCrossLinks, 
                string outputName = "result"
                ) : base("for region", null, outputName)
            {
                _inStart = inStart;
                _inEnd = inEnd;
                var outputs = theOutputs.ToList();
                _crossLinks = theCrossLinks.ToList();
                OptionalOutputs = new List<AbstractGpuValue>();
                Setup(theInputs, outputs);
            }
            
            private List<AbstractGpuValue> ResolveVoidCrossLink(AbstractGpuValue theCrossLink)
            {
                var myResult = new List<AbstractGpuValue>();

                theCrossLink.ParentNode.Children.ForEach(child =>
                {
                    if (!(child is AbstractShaderNode input)) return;
                    switch (input.AbstractOutput())
                    {
                        case GpuValue<GpuVoid> gpuValue:
                            myResult.AddRange(ResolveVoidCrossLink(gpuValue));
                            break;
                        default:
                            myResult.Add(input.AbstractOutput());
                            break;
                    }
                });
                return myResult;
            }

            private void Setup(IEnumerable<AbstractGpuValue> theInputs, IEnumerable<AbstractGpuValue> theOutputs)
            {
                var inputs = theInputs.ToList();
                var outputs = theOutputs.ToList();
                if (inputs.Count() != outputs.Count())
                {
                    Setup(inputs);
                    return;
                }
                
                var myCrossLinks = new List<AbstractGpuValue>();
                _crossLinks.ForEach(c =>
                {
                    if (c == null) return;
                    switch (c)
                    {
                        case GpuValue<GpuVoid> gpuValue:
                            myCrossLinks.AddRange(ResolveVoidCrossLink(gpuValue));
                            //myCrossLinks.Add(AbstractCreation.AbstractGpuValuePassThrough(c));
                            break;
                        default:
                            myCrossLinks.Add(AbstractCreation.AbstractDeclareValueAssigned(c));
                            //myCrossLinks.Add(AbstractCreation.AbstractGpuValuePassThrough(c));
                            myCrossLinks.Add(c);
                            break;
                    }
                });
                
                var myInputs = new List<AbstractGpuValue>();
                var myOutputs = new List<AbstractGpuValue>();

                for (var i = 0; i < outputs.Count; i++)
                {
                    switch (outputs[i])
                    {
                        case GpuValue<GpuVoid> gpuValue:
                            var myInputVoid = inputs[i];
                            myInputs.Add(myInputVoid);
                            myOutputs.Add(gpuValue);
                            var myPassVoid = AbstractCreation.AbstractGpuValuePassThrough(gpuValue);
                            myPassVoid.ParentNode = this;
                            OptionalOutputs.Add(myPassVoid);
                            break;
                        default:
                            var myInput = i >= inputs.Count ? AbstractCreation.AbstractConstant(outputs[i], 0f) : inputs[i];
                            var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(myInput);
                            myInputs.Add(myDeclareValue);
                    
                            var myOutput = i >= outputs.Count ? AbstractCreation.AbstractConstant(inputs[i], 0f) : outputs[i];
                            var myAssign = AbstractCreation.AbstractAssignNode(myDeclareValue, myOutput);
                            myOutputs.Add(myAssign);
                            var myPass = AbstractCreation.AbstractGpuValuePassThrough(myDeclareValue);
                            myPass.ParentNode = this;
                            OptionalOutputs.Add(myPass);
                            break;
                    }
                }

                _inCall = new GroupValues(myInputs).Output;
                _crossLinkCall = new GroupValues(myCrossLinks).Output;
                var index = inputs.Count > 0 ? inputs[0] as GpuValue<int> : new ConstantValue<int>(0);
                _forGroup = new ForGroup(index, _inStart, _inEnd, myOutputs).Output;
                Setup(new List<AbstractGpuValue>(){_crossLinkCall, _inStart,_inEnd,_inCall,_forGroup});
            }

            protected override string SourceTemplate()
            {
                return "";//ShaderNodesUtil.Evaluate(shaderCode,templateMap);
            }
            
            
        }
}