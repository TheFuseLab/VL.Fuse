using System.Collections.Generic;
using Stride.Rendering.Materials;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public interface IComposition
    {
        string Declaration { get; }
        string CompositionName { get; }
        IComputeNode ComputeNode { get; }
    }

    public class CompositionInput<T> : ShaderNode<T>, IComposition
        where T : unmanaged
    {
        IComputeValue<T> _value;

        public CompositionInput(NodeContext nodeContext, SetVar<T> value /* Should just be IComputeValue<T> */)
            : base(nodeContext, "compositionInput")
        {
            _value = value?.Value ?? new VL.Stride.Shaders.ShaderFX.ComputeNode<T>();

            SetProperty(Compositions, this);
        }

        public string Declaration => ShaderNodesUtil.Evaluate("compose ${compositionType} ${compositionId};",
            new Dictionary<string, string>()
            {
                { "compositionType", TypeHelpers.GetCompositionType<T>() },
                { "compositionId", CompositionName }
            });

        public IComputeNode ComputeNode => _value;

        public string CompositionName => $"composition{ID}";

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${compositionId}.Compute();",
                new Dictionary<string, string>
                {
                    {"compositionId", CompositionName},
                });
        }
    }
}