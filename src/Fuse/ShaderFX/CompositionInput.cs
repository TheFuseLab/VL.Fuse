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

        public CompositionInput(NodeContext nodeContext, IComputeValue<T> value)
            : base(nodeContext, "compositionInput")
        {
            _value = value;

            SetProperty(Compositions, this);
        }

        public string Declaration => ShaderNodesUtil.Evaluate("compose ${compositionType} ${compositionId};",
            new Dictionary<string, string>()
            {
                { "compositionType", TypeHelpers.GetCompositionType<T>() },
                { "compositionId", ID }
            });

        public IComputeNode ComputeNode => _value;

        public string CompositionName => ID;

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${in}.Compute();",
                new Dictionary<string, string>
                {
                    {"in", ID},
                });
        }
    }
}