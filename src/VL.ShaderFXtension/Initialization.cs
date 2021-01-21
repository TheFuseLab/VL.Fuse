using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Stride.Rendering;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Stride;
using VL.Stride.Rendering;

[assembly: AssemblyInitializer(typeof(VL.ShaderFXtension.Initialization))]

namespace VL.ShaderFXtension
{
    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory services)
        {
            services.RegisterNodeFactory("VL.Fuse.FunctionNodes",init : factory =>
            {
                var nodes = GetNodeDescriptions(factory).ToImmutableArray();
                return NodeBuilding.NewFactoryImpl(nodes);
            });
        }

        static IEnumerable<IVLNodeDescription> GetNodeDescriptions(IVLNodeDescriptionFactory factory,
            string path = default, string shadersPath = default)
        {

            return new List<IVLNodeDescription>()
            {
                factory.NewNodeDescription(
                    name: "FUSE TEST NODE",
                    category: "FUSE.TESTNODE.TESTNODE",
                    fragmented: true,
                    init: buildContext =>
                    {

                        var _inputs = new List<IVLPinDescription>(){buildContext.Pin("Input", typeof(string))};
                        var _outputs = new List<IVLPinDescription>() {buildContext.Pin("Output", typeof(string))};


                        return buildContext.Implementation(
                            inputs: _inputs,
                            outputs: _outputs,
                            newNode: nodeBuildContext =>
                            {

                                var inputs = new List<IVLPin>();
                                var myBilder = new StringBuilder();
                                foreach (var _input in _inputs)
                                {
                                    nodeBuildContext.Input<string>(val => myBilder.Append(val));
                                }

                                var output = nodeBuildContext.Output<string>(() => "FUSE");
                                return nodeBuildContext.Node(
                                    inputs: inputs,
                                    outputs: new[] {output},
                                    update: default
                                    );
                            }
                        );
                    }
                )

            };
        }
    }
}