using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fuse.regions;
using VL.Lang.Platforms;
using VL.Lang.Symbols;
using VL.Lang.Symbols.Mutable;
using VL.Lib.Primitive;
using VL.Model;

[assembly: PluginFactory(typeof(PluginFactory))]

namespace Fuse.regions
{
    sealed class PluginFactory : IPluginFactory
    {
        public IEnumerable<INodePlugin> GetPlugins(IServiceProvider serviceProvider, IPlatformTypes platformTypes)
        {
            yield return new GpuFunctionRegion(serviceProvider, platformTypes);
        }
    }
    
    public partial class GpuFunctionRegion : NodePluginBase, IRegionPlugin
    {
        const string ForceInputName = "Force";
        const string HasChangedOutputName = "Has Changed";
        IPatch FPatchDescription;

        public GpuFunctionRegion(IServiceProvider serviceProvider, IPlatformTypes platformTypes)
        {
            PlatformTypes = platformTypes;

            Inputs = new[] 
            { 
                new PinDescription(ElementKind.InputPin, ForceInputName, PlatformTypes.Boolean)
            };
            Outputs = new[] 
            { 
                new PinDescription(ElementKind.OutputPin, HasChangedOutputName, PlatformTypes.Boolean) 
            };
    }

        public override NameAndVersion Name => new NameAndVersion("GpuFunction");
        public override string Category => "Fuse";
        public bool ExecutesOnlyOnce => true;
        public bool IsAtomic => true;
        public bool IsSynchronous => true;
        public bool SupportsPins => false;
        public IPlatformTypes PlatformTypes { get; }
        public override ElementKind Kind => ChoiceBasedNodeReference.CacheRegionRef.Kind;
        public IEnumerable<ElementKind> ApplicationKinds => new[] { ElementKind.ProcessStatefulRegion };
        public override IEnumerable<IPinDescription> Inputs { get; }
        public override IEnumerable<IPinDescription> Outputs { get; }

        public IPatch GetPatchDescription(INode node)
        {
            return FPatchDescription ?? InterlockedHelper.Init(ref FPatchDescription, Compute());

            IPatch Compute()
            {
                var dummyIfRegion = new IfRegion(null, PlatformTypes);
                return dummyIfRegion.GetPatchDescription(node);
            }
        }

        public ControlPointKind SupportedBorderControlPoints => ControlPointKind.Border;

        public override string Summary => "Whenever an input value changes the region executes and caches the output values.";

        // See comment in manager
        //public override string Remarks => "Since 2020.2.3 the region always executes in the first frame.";

        public override ImmutableArray<string> Tags => ImmutableArray.Create("changed");

        public bool IsCached(IBorderControlPointSymbol cp) => cp.Alignment == Alignment.Bottom ? true : false;

        public override IEnumerable<ISlotSymbol> GetSlots(INodeSymbol node)
        {
            var platformTypes = node.DocSymbols.PlatformTypes;
            var inputsType = platformTypes.GetValueTupleType(node.DocSymbols, node.InputControlPoints.Where(cp => !cp.OuterType.IsUnused).Select(cp => cp.OuterType));
            var stateType = node.PrivateHelperType;
            var outputsType = platformTypes.GetValueTupleType(node.DocSymbols, node.OutputControlPoints.Where(cp => !cp.OuterType.IsUnused).Select(cp => cp.OuterType));
            var managerType = platformTypes.GetTypeSymbol(typeof(Manager<,,>)).CreateInstance(node.DocSymbols, inputsType, stateType, outputsType);
            yield return new SlotSymbol(node.GetContainingPatch(), $"__gpuFunction_{node.PersistentId}", managerType) { IsManaged = true, TracingId = node.TracingId };
        }

        public IEnumerable<TypeConstraint> GetConstraints(INodeSymbol node)
        {
            foreach (var cp in node.ControlPoints)
                yield return new TypeConstraint(cp.InnerType, cp.OuterType, TypeConstraintRelation.Equal, cp, ConstraintReason.BorderCP);
        }

        public void FilterPins(INodeSymbol node, ref ImmutableArray<IPinDefinitionSymbol> inputs, ref ImmutableArray<IPinDefinitionSymbol> outputs) { }

        public bool NeedsExpansion(INodeSymbol node) => false;
    }
}