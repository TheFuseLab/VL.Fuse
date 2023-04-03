using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.PublicAPI;

namespace Fuse.regions
{
    /*
    public class DelegateGroup : Group
    {
        public DelegateGroup(
            NodeContext nodeContext,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, theInputs, "DelegateGroup")
        {
            Name = "DelegateGroup";
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder,
            HashSet<AbstractShaderNode> theHashes, string thePrepend)
        {
            if (!theHashes.Add(this)) return;

            if (ShaderNodesUtil.DebugShaderGeneration) Console.WriteLine(thePrepend + ID);

            const string shaderCode = @"
        if(${check}){";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string> { { "check", _inCheck.ID } }
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
    }*/

    public class DelegateRegion : Delegate<GpuVoid>
    {
        public DelegateRegion(
            NodeContext nodeContext,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks,
            IEnumerable<BorderControlPointDescription> theInputDescriptions,
            IEnumerable<BorderControlPointDescription> theOutputDescriptions) : base(nodeContext, null, null, "delegateRegion")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
            Parameters = GetUniqueParameters(FunctionParameters());
            BuildFunction(SetupRegion(
                theInputs,
                theOutputs,
                theCrossLinks,
                theInputDescriptions,
                theOutputDescriptions));
        }

        private AbstractShaderNode SetupRegion(
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks,
            IEnumerable<BorderControlPointDescription> theInputDescriptions,
            IEnumerable<BorderControlPointDescription> theOutputDescriptions)
        {
            var subContextFactory = new NodeSubContextFactory(NodeContext);
            OptionalOutputs.Clear();

            var inputs = theInputs.ToList();
            var outputs = theOutputs.ToList();
            var crossLinks = theCrossLinks.ToList();
            var inputDescriptions = theInputDescriptions.ToList();
            var outputDescriptions = theOutputDescriptions.ToList();

            var myCrossLinks = new List<AbstractShaderNode>();
            crossLinks.ForEach(c =>
            {
                if (c is null or IInjectToRegion) return;
                if (TypeHelpers.IsDelegate(c)) return;
                myCrossLinks.Add(c);
            });

            var myInputs = new List<AbstractShaderNode>();
            var myOutputs = new List<AbstractShaderNode>();

            foreach (var t in inputs)
            {
                switch (t)
                {
                    case ShaderNode<GpuVoid>:
                        myInputs.Add(t);

                        break;
                    default:
                        myInputs.Add(t);
                        
                        break;
                }
            }

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        myOutputs.Add(shaderNode);

                        break;
                    default:
/*
                        var myOutput = i >= outputs.Count
                            ? AbstractCreation.AbstractConstant(inputDescriptions[i].TypeInfo, 0f)
                            : outputs[i];
                            */
                        var myFunctionParameter = AbstractCreation.AbstractFunctionParameter(subContextFactory, outputDescriptions[i].TypeInfo, InputModifier.Out, i + inputs.Count);
                        var myAssign = AbstractCreation.AbstractAssignNode(subContextFactory, myFunctionParameter, outputs[i]);
                        myOutputs.Add(myAssign);
                        break;
                }
            }

            var inCall = new Group(subContextFactory.NextSubContext(), myInputs);
            var crossLinkCall = new Group(subContextFactory.NextSubContext(), myCrossLinks);

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid>:
                        var myInputVoid = myInputs[i] ?? new EmptyVoid(subContextFactory.NextSubContext());
                        var outputVoid = AbstractCreation.AbstractOutput(subContextFactory, this, myInputVoid);
                        OptionalOutputs.Add(outputVoid);

                        break;
                    default:
                        var myInput = myInputs[i] ??
                                      AbstractCreation.AbstractConstant(subContextFactory, outputs[i], 0);
                        var output = AbstractCreation.AbstractOutput(subContextFactory, this, myInput);
                        OptionalOutputs.Add(output);

                        break;
                }
            }

            var inputList = new List<AbstractShaderNode> { crossLinkCall };
       //     theAddRegionNodes(inputList);
       inputList.AddRange(myInputs);
            inputList.Add(inCall);
            inputList.AddRange(myOutputs);
            
            //SetInputs(new Group(subContextFactory.NextSubContext(), myOutputs), inputList);
            return new Group(subContextFactory.NextSubContext(), inputList);
            
            
        }
    }
}