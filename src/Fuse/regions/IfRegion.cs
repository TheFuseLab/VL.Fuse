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

                for (var i = 0; i < inputs.Count(); i++)
                {
                    var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(inputs[i]);
                    myInputs.Add(myDeclareValue);
                    var myAssign = AbstractCreation.AbstractAssignNode(myDeclareValue, outputs[i]);
                    myOutputs.Add(myAssign);
                    var myPass = AbstractCreation.AbstractGpuValuePassThrough(myDeclareValue);
                    myPass.ParentNode = this;
                    OptionalOutputs.Add(myPass);
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