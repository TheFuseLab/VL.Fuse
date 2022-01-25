using System;
using System.Collections.Generic;
using Fuse.compute;
using System.Linq;
using System.Text;

namespace Fuse.regions
{
    public class IfRegion
    {
        public class GroupValues: ShaderNode<GpuVoid> 
        {
            public GroupValues(IEnumerable<AbstractGpuValue> theInputs) : base("CallStack")
            { 
            
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
        }
        
        public class IfGroup: ShaderNode<GpuVoid> 
        {
            private readonly GpuValue<bool> _inCheck;
            public IfGroup(GpuValue<bool> inCheck, IEnumerable<AbstractGpuValue> theInputs) : base("IfGroup")
            { 
            
                _inCheck = inCheck;
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
        if(${check}){";
                theSourceBuilder.Append(ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>(){{"check", _inCheck.ID}}));
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
            
            private readonly GpuValue<bool> _inCheck;
            private GpuValue<GpuVoid> _inCall;
            private GpuValue<GpuVoid> _ifGroup;
            private readonly List<AbstractGpuValue> _outputs;
            
            public IfRegionNode(GpuValue<bool> inCheck, IEnumerable<AbstractGpuValue> theInputs, IEnumerable<AbstractGpuValue> theOutputs, string outputName = "result") : base("if region", null, outputName)
            {
                _inCheck = inCheck;
                _outputs = theOutputs.ToList();
                OptionalOutputs = new List<AbstractGpuValue>();
                Setup(theInputs, _outputs);
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
            
                var myInputs = new List<AbstractGpuValue>();
                var myOutputs = new List<AbstractGpuValue>();

                //var maxCount = Math.Max(inputs.Count, outputs.Count);
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
                _ifGroup = new IfGroup(_inCheck, myOutputs).Output;
                Setup(new List<AbstractGpuValue>(){_inCheck, _inCall,_ifGroup});
            }

            protected override string SourceTemplate()
            {
                return "";//ShaderNodesUtil.Evaluate(shaderCode,templateMap);
            }
            
            
        }
    }
}