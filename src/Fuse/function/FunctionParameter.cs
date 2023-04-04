using System.Collections.Generic;
using VL.Core;

namespace Fuse.function;

public class FunctionParameter<T> : ShaderNode<T> , IFunctionParameter
{

    public FunctionParameter(NodeContext nodeContext, ShaderNode<T> theType, InputModifier theInputModifier = InputModifier.In,  int theId = 0): base(nodeContext, "arg_" + theId)
    {
        Ins = new List<AbstractShaderNode>();
        Modifier = theInputModifier;
        ArgumentNumber = theId;
    }
        
    public override string ID => Name;

    public string ModifierString()
    {
        return Modifier switch
        {
            InputModifier.In => "in",
            InputModifier.InOut => "inout",
            InputModifier.Out => "out",
            _ => ""
        };
    }

    public int ArgumentNumber { get; }
        
    public InputModifier Modifier { get; }

    public override string TypeName()
    {
        return TypeHelpers.GetGpuType<T>();
    }

    protected override string SourceTemplate()
    {
        return "";
    }
}