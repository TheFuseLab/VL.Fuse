using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using VL.AppServices.CompilerServices.CustomRegion;
using VL.Core;
using VL.Core.Import;
using VL.Core.PublicAPI;

namespace Fuse.regions;

public interface IForRegionInlay
{
    void Update(ShaderNode<int> index);
}

[ProcessNode(FragmentSelection = FragmentSelection.Explicit)]
public class ForRegion2 : FuseRegionBase<IForRegionInlay>, IRegion<IForRegionInlay>
{
    private static readonly ConstantValue<int> s_fallbackStart = new(0);

    private ShaderNode<int> _inEnd;
    private ShaderNode<int> _inStart;
    private readonly ShaderNode<int> _indexNode;
    private bool _theLoop;
    private bool _theUnroll;
    private int _theUnrollLoops;
    private ForRegion _forRegion;
    private readonly NodeContext _nodeContext;
    private bool _updateShaderNode;
    private IForRegionInlay _forRegionInlay;
    private readonly Dictionary<InputDescription, AbstractShaderNode> innerInputs = new Dictionary<InputDescription, AbstractShaderNode>();

    [Fragment]
    public ForRegion2(NodeContext nodeContext)
    {
        _nodeContext = nodeContext;
        _indexNode = new IndexNode(nodeContext, default);
    }

    [Fragment]
    public void Update(
        [Pin(Visibility = VL.Model.PinVisibility.Optional)] ShaderNode<int> inStart,
        ShaderNode<int> inEnd,
        [Pin(Visibility = VL.Model.PinVisibility.Optional)] bool theLoop,
        [Pin(Visibility = VL.Model.PinVisibility.Optional)] bool theUnroll,
        [Pin(Visibility = VL.Model.PinVisibility.Optional)] int theUnrollLoops)
    {
        if (_forRegionInlay is null)
        {
            _forRegionInlay = _patchInlayFactory?.Invoke();
        }

        if (_forRegionInlay is null)
            return;

        _forRegionInlay.Update(_indexNode);

        if (_updateShaderNode || inStart != _inStart || inEnd != _inEnd || theLoop != _theLoop || theUnroll != _theUnroll || theUnrollLoops != _theUnrollLoops)
        {
            _updateShaderNode = false;
            _inStart = inStart;
            _inEnd = inEnd;
            _theLoop = theLoop;
            _theUnroll = theUnroll;
            _theUnrollLoops = theUnrollLoops;

            var subFac = new NodeSubContextFactory(_nodeContext);
            // Somehow we need to pass this along as 
            var inputs = _inputValues.Where(e => !e.Key.IsLink).Select(e => innerInputs[e.Key]);
            var inputDescriptions = _inputValues.Where(e => !e.Key.IsLink).Select((e, i) => new BorderControlPointDescription(e.Key.Name, e.Key.InnerType, i, e.Key.IsSplicer));
            var crossLinks = _inputValues.Where(e => e.Key.IsLink).Select(e => e.Value).OfType<AbstractShaderNode>();
            var outputs = _outputValues.Select(e => e.Value).OfType<AbstractShaderNode>();
            _forRegion = new ForRegion(_nodeContext, inStart ?? s_fallbackStart, inEnd ?? s_fallbackStart, theLoop, theUnroll, theUnrollLoops, inputs, outputs, crossLinks, inputDescriptions);
            var i = 0;
            foreach (var (decription, value) in _outputValues)
            {
                _outputValues[decription] = _forRegion.OptionalOutputs[i++];
            }
        }
    }

    public override void AcknowledgeInput(in InputDescription description, object outerValue)
    {
        if (_inputValues.TryGetValue(description, out var existingValue) && existingValue == outerValue)
        {
            return; // No change, do nothing
        }

        if (description.IsLink)
        {
            innerInputs[description] = (AbstractShaderNode)outerValue;
        }
        else
        {
            var context = _nodeContext.CreateSubContext("x", description.Id);
            var declareValue = AbstractCreation.CreateAbstract((AbstractShaderNode)outerValue, typeof(DeclareValue<>), [context, outerValue]);
            innerInputs[description] = declareValue;
        }

        _updateShaderNode = true;

        base.AcknowledgeInput(description, outerValue);
    }

    public override void RetrieveInput(in InputDescription description, IForRegionInlay patchInlay, out object innerValue)
    {
        if (innerInputs.TryGetValue(description, out var existingValue))
        {
            innerValue = existingValue;
            return;
        }
        base.RetrieveInput(description, patchInlay, out innerValue);
    }

    public override void AcknowledgeOutput(in OutputDescription description, IForRegionInlay patchInlay, object innerValue)
    {
        if (_outputValues.TryGetValue(description, out var existingValue) && existingValue == innerValue)
        {
            return; // No change, do nothing
        }

        _updateShaderNode = true;

        base.AcknowledgeOutput(description, patchInlay, innerValue);
    }

    public override void SetPatchInlayFactory([CustomRegion(SupportedBorderControlPointsSpecified_CompileTime = SupportedBorderControlPoints.Accumulator, TypeConstraint = "ShaderNode")] Func<IForRegionInlay> patchInlayFactory)
    {
        base.SetPatchInlayFactory(patchInlayFactory);
    }
}