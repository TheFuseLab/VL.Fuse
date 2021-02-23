using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Fuse.regions;
using VL.Lang.Platforms;
using VL.Lang.Symbols;
using VL.Model;

[assembly: PluginFactory(typeof(PluginFactory))]

namespace Fuse.regions
{
    sealed class PluginFactory : IPluginFactory
    {
        public IEnumerable<INodePlugin> GetPlugins(IServiceProvider serviceProvider, IPlatformTypes platformTypes)
        {
            yield return new Fuse.regions.ShaderFunctionRegion();
        }
    }

    public partial class ShaderFunctionRegion : NodePluginBase, IRegionPlugin
    {
        public override NameAndVersion Name => new NameAndVersion("ShaderFunction");

        public override string Category => "Fuse";

        public override ElementKind Kind => ElementKind.ApplicationStatefulRegion;

        public override IEnumerable<IPinDescription> Inputs => Enumerable.Empty<IPinDescription>();

        public override IEnumerable<IPinDescription> Outputs => Enumerable.Empty<IPinDescription>();

        public override string Summary => null;

        public IEnumerable<ElementKind> ApplicationKinds => new[] { Kind };

        public bool IsSynchronous => true;

        public bool ExecutesOnlyOnce => true;

        public bool IsAtomic => true;

        public ControlPointKind SupportedBorderControlPoints => ControlPointKind.Accumulator;

        public void FilterPins(INodeSymbol node, ref ImmutableArray<IPinDefinitionSymbol> inputs, ref ImmutableArray<IPinDefinitionSymbol> outputs)
        {
        }

        public IEnumerable<TypeConstraint> GetConstraints(INodeSymbol node)
        {
            // Loops do something like this here:
            //var scope = node.DocSymbols;
            //var platformTypes = scope.PlatformTypes;
            //var collectionType = platformTypes.IReadOnlyList;
            //foreach (var cp in node.ControlPoints)
            //{
            //    yield return new TypeConstraint(cp.OuterType, collectionType.CreateInstance(scope, cp.InnerType), TypeConstraintRelation.Subtype, cp, ConstraintReason.BorderCP);
            //}

            var scope = node.DocSymbols;
            var gpuValueType = scope.PlatformTypes.GetTypeSymbol(typeof(GpuValue<>));
            foreach (var cp in node.ControlPoints)
            {
                yield return new TypeConstraint(cp.OuterType, gpuValueType.CreateInstance(scope, cp.OuterType), TypeConstraintRelation.Equal, cp, ConstraintReason.BorderCP);
            }

            yield break;
        }

        public override IEnumerable<ISlotSymbol> GetSlots(INodeSymbol node)
        {
            // Allows the region to setup fields in the containing type. Usually the region comes with its own manager class which wraps the system generated private helper type.
            // Cache region does something like this:
            //var platformTypes = node.DocSymbols.PlatformTypes;
            //var inputsType = platformTypes.GetValueTupleType(node.DocSymbols, node.InputControlPoints.Where(cp => !cp.OuterType.IsUnused).Select(cp => cp.OuterType));
            //var stateType = node.PrivateHelperType;
            //var outputsType = platformTypes.GetValueTupleType(node.DocSymbols, node.OutputControlPoints.Where(cp => !cp.OuterType.IsUnused).Select(cp => cp.OuterType));
            //var managerType = platformTypes.GetTypeSymbol(typeof(Manager<,,>)).CreateInstance(node.DocSymbols, inputsType, stateType, outputsType);
            //yield return new Lang.Symbols.Mutable.SlotSymbol(node.GetContainingPatch(), $"__cache_{node.PersistentId}", managerType) { IsManaged = true, TracingId = node.TracingId };

            return base.GetSlots(node);
        }

        public IPatch GetPatchDescription(INode node)
        {
            return _fPatchDescription ?? InterlockedHelper.Init(ref _fPatchDescription, Compute());

            IPatch Compute()
            {
                var master = new VL.MutableModel.Patch(ElementKind.MasterPatchFlag, null);
                master.Children.Add(new VL.MutableModel.Patch(ElementKind.SubPatchFlag, master, Names.Create)
                {
                    IsOptional = true
                });
                master.Children.Add(new VL.MutableModel.Patch(ElementKind.SubPatchFlag, master, Names.UpdateOperation)
                {
                    IsOptional = false,
                    IsDefault = true
                });
                return master;
            }
        }

        private IPatch _fPatchDescription;

        public bool IsCached(IBorderControlPointSymbol cp)
        {
            return false;
        }
    }
}
