using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.compute;
using VL.Core;
using VL.Core.PublicAPI;

namespace Fuse.regions;

public abstract class AbstractRegion : ShaderNode<GpuVoid>
{
    public AbstractRegion(NodeContext nodeContext, string theName) : base(nodeContext, theName)
    {
        OptionalOutputs = new List<AbstractShaderNode>();
    }

    public delegate Group CreateRegionGroup(NodeSubContextFactory subContextFactory, List<AbstractShaderNode> outputs);

    public delegate void AddRegionNodes(List<AbstractShaderNode> theNodes);

    public void SetupRegion(
        CreateRegionGroup theCreateRegion,
        AddRegionNodes theAddRegionNodes,
        IEnumerable<AbstractShaderNode> theInputs,
        IEnumerable<AbstractShaderNode> theOutputs,
        IEnumerable<AbstractShaderNode> theCrossLinks,
        IEnumerable<BorderControlPointDescription> theDescriptions)
    {
        var subContextFactory = new NodeSubContextFactory(NodeContext);
        OptionalOutputs.Clear();

        var inputs = theInputs.ToList();
        var outputs = theOutputs.ToList();
        var crossLinks = theCrossLinks.ToList();
        var descriptions = theDescriptions.ToList();

        var myCrossLinks = new List<AbstractShaderNode>();
        crossLinks.ForEach(c =>
        {
            if (c is null or IInjectToRegion) return;
            if (TypeHelpers.IsDelegate(c)) return;
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
                    var myInput = inputs[i] == null
                        ? AbstractCreation.AbstractConstant(descriptions[i].TypeInfo, 0f)
                        : inputs[i];
                    //var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(subContextFactory, myInput);
                    myInputs.Add(myInput);

                    var myOutput = i >= outputs.Count
                        ? AbstractCreation.AbstractConstant(descriptions[i].TypeInfo, 0f)
                        : outputs[i];
                    myOutputs.Add(myOutput);
                    break;
            }
        }

        for (var i = 0; i < outputs.Count; i++)
        {
            switch (outputs[i])
            {
                case ShaderNode<GpuVoid>:
                    break;
                default:
                    var myAssign = AbstractCreation.AbstractAssignNode(subContextFactory, myInputs[i], myOutputs[i]);
                    myOutputs.Add(myAssign);
                    break;
            }
        }

        var inCall = new Group(subContextFactory.NextSubContext(),myInputs);
        var crossLinkCall = new Group(subContextFactory.NextSubContext(), myCrossLinks);

        for (var i = 0; i < outputs.Count; i++)
        {
            switch (outputs[i])
            {
                case ShaderNode<GpuVoid> shaderNode:
                    var myInputVoid = myInputs[i] ?? new EmptyVoid(subContextFactory.NextSubContext());
                    var outputVoid = AbstractCreation.AbstractOutput(subContextFactory, this, myInputVoid);
                    OptionalOutputs.Add(outputVoid);

                    break;
                default:
                    var myInput = myInputs[i] ?? AbstractCreation.AbstractConstant(subContextFactory, outputs[i], 0);
                    var output = AbstractCreation.AbstractOutput(subContextFactory, this, myInput);
                    OptionalOutputs.Add(output);

                    break;
            }
        }

        var inputList = new List<AbstractShaderNode> { crossLinkCall };
        theAddRegionNodes(inputList);
        inputList.Add(inCall);
        inputList.Add(theCreateRegion(subContextFactory, myOutputs));
        SetInputs(inputList);
    }
}