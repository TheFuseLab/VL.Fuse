using Stride.Rendering.Materials;

namespace Fuse.function;

public interface IFunctionParameter : IComputeNode
{
    string ID { get; }

    string PinName { get; }

    InputModifier Modifier { get; }

    int ArgumentNumber { get; }
    uint HashCode { get; set; }
    string TypeName();

    string ModifierString();
}