using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;

namespace Fuse.regions
{
    
        
        public class ForGroup: ShaderNode<GpuVoid> 
        {
            private readonly GpuValue<int> _inStart;
            private readonly GpuValue<int> _inEnd;
            public ForGroup(GpuValue<int> inStart, GpuValue<int> inEnd, IEnumerable<AbstractGpuValue> theInputs) : base("IfGroup")
            { 
            
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
        for(int index = ${start}; index < ${end};index++){";
                theSourceBuilder.Append(ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>(){{"start", _inStart.ID}, {"end", _inEnd.ID}}));
                theSourceBuilder.AppendLine();
                BuildChildrenSource(theSourceBuilder, theHashes);

                var source = SourceCode;
                if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
                {
                    theSourceBuilder.Append("        " + source + Environment.NewLine);
                }
                theSourceBuilder.AppendLine(@"
        }");
            }
        }
        
        public class ForRegionNode : ShaderNode<GpuVoid>
        {
            
            private GpuValue<GpuVoid> _inCall;
            private GpuValue<GpuVoid> _forGroup;
            private readonly List<AbstractGpuValue> _outputs;
            
            private readonly GpuValue<int> _inStart;
            private readonly GpuValue<int> _inEnd;
            public ForRegionNode(GpuValue<int> inStart, GpuValue<int> inEnd, IEnumerable<AbstractGpuValue> theInputs, IEnumerable<AbstractGpuValue> theOutputs, string outputName = "result") : base("if region", null, outputName)
            {
                _inStart = inStart;
                _inEnd = inEnd;
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

                _inCall = new IfRegion.GroupValues(myInputs).Output;
                _forGroup = new ForGroup(_inStart, _inEnd, myOutputs).Output;
                Setup(new List<AbstractGpuValue>(){_inCall,_forGroup});
            }

            protected override string SourceTemplate()
            {
                return "";//ShaderNodesUtil.Evaluate(shaderCode,templateMap);
            }
            
            
        }
}