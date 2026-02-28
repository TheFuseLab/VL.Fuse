using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VL.Core;

namespace Fuse.compute;

public class DynamicStruct<T> : ShaderNode<T>
{
    private readonly string _sourceTemplate = "";
    private readonly string _structName;

    private static string SanitizeStructName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "struct")
            return "GpuStruct";
        return name;
    }

    public DynamicStruct(NodeContext nodeContext, IEnumerable<AbstractShaderNode> theInputs, string theName, T instance, bool addStructDefinition = true)
        : base(nodeContext, "GPUAttributeStruct")
    {
        _structName = SanitizeStructName(theName);

        const string shaderCode =
            @"    struct ${structName}{
${structMembers}
    };";
        
        var myStride = 0;
        var call = new StringBuilder();
        theInputs.ForEach(input =>
        {
            if (TypeHelpers.IsGpuArray(input))
            {
                var arrayCount = input is IStructureBufferAttribute structBuffer ? structBuffer.ArrayCount : 1;
                call.Append("        " + TypeHelpers.GetGpuType(input) + " " + input.Name + "[" + arrayCount + "];" +
                            Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(input);
            }
            else
            {
                call.Append("        " + TypeHelpers.GetGpuType(input) + " " + input.Name + ";" + Environment.NewLine);
                myStride += TypeHelpers.GetSizeInBytes(input);
            }
        });
        var structString = ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "structName", _structName },
            { "structMembers", call.ToString() }
        });

        if(addStructDefinition)SetProperty(Structs, structString);
        
        Stride = myStride;

        TypeOverride = _structName;

        SetInputs(theInputs);
    }

    public DynamicStruct(NodeContext nodeContext, Dictionary<string, AbstractShaderNode> theInputs, T instance,
        bool AddTypesToName = false, bool addStructDefinition = true) : base(nodeContext, "GPUAttributeStruct")
    {
        var _name = SanitizeStructName(TypeHelpers.GetGpuType<T>());
        Name = ShaderNodesUtil.FirstLetterToLower(_name);

        if (AddTypesToName)
            _name = theInputs.Aggregate(_name, (current, input) => current + TypeHelpers.GetGpuType(input.Value));

        _structName = _name;

        const string shaderCode =
            """
                struct ${structName}{
            ${structMembers}
                };
            """;

        var myStride = 0;
        var call = new StringBuilder();
        foreach (var kv in theInputs)
        {
            call.Append("        " + TypeHelpers.GetGpuType(kv.Value) + " " + kv.Key + ";" + Environment.NewLine);
            myStride += TypeHelpers.GetSizeInBytes(kv.Value);
        }

        var structString = ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
        {
            { "structName", _structName },
            { "structMembers", call.ToString() }
        });

        var template = new StringBuilder();
        template.Append("${resultType} ${resultName};" + Environment.NewLine);
        foreach (var kv in theInputs)
        {
            template.Append("        ${resultName}." + kv.Key + " = " + kv.Value.ID + ";" + Environment.NewLine);
            myStride += TypeHelpers.GetSizeInBytes(kv.Value);
        }

        _sourceTemplate = ShaderNodesUtil.Evaluate(template.ToString(),
            new Dictionary<string, string>
            {
                { "resultType", _structName }
            });

        if(addStructDefinition)SetProperty(Structs, structString);
        Stride = myStride;

        TypeOverride = _structName;

        SetInputs(theInputs.Values);
    }

    public List<string> Description
    {
        get
        {
            var result = new List<string>();
            Ins.ForEach(input => result.Add(_structName + "." + TypeHelpers.GetDescription(input)));
            return result;
        }
    }

    public int Stride { get; private set; }

    protected override string SourceTemplate()
    {
        return _sourceTemplate;
    }
}
