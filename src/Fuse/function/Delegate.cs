using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.Function;
using Stride.Rendering.Materials;
using VL.Core;

namespace Fuse.function;

public class Delegate<T> : ShaderNode<T>, IDelegate
{

    private AbstractShaderNode _delegate;

    public List<IFunctionParameter> Parameters { get; protected init; }

    public Delegate(NodeContext nodeContext, AbstractShaderNode theDelegate, List<IFunctionParameter> theParameters, string theId = "Function", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
    {
        Parameters = theParameters ?? new List<IFunctionParameter>();
            
        if (theDelegate == null) return;

        BuildFunction(theDelegate);
    }
        
    public static List<IFunctionParameter> GetUniqueParameters(List<IFunctionParameter> parameters)
    {
        var uniqueParams = new List<IFunctionParameter>();

        foreach (var parameter in parameters.Where(parameter => uniqueParams.All(p => p.ID != parameter.ID)))
        {
            uniqueParams.Add(parameter);
        }

        return uniqueParams.OrderBy(p => p.ID).ToList();
    }

    protected void BuildFunction(AbstractShaderNode theDelegate)
    {
        _delegate = theDelegate;

        theDelegate.CheckHashCodes();
            
        //Parameters = GetUniqueParameters(theDelegate.FunctionParameters());
            
        Functions = new Dictionary<string, string>();
            
        var functionValueMap = new Dictionary<string, string>
        {
            {"resultType", TypeHelpers.GetGpuType<T>()},
            {"functionName", FunctionName},
            {"arguments", BuildArguments(Parameters)},
            {"functionImplementation", theDelegate.BuildSourceCode()},
            {"result", theDelegate.ID}
        };
        string functionCode;
        if (TypeHelpers.IsVoidNode(theDelegate))
        {
            functionCode = @"${resultType} ${functionName}(${arguments}){
                ${functionImplementation}
            }";
        }
        else
        {
            functionCode = @"${resultType} ${functionName}(${arguments}){
                ${functionImplementation}
                return ${result};
            }";
        }
        Functions.Add(FunctionName, ShaderNodesUtil.Evaluate(functionCode, functionValueMap) + Environment.NewLine);
        theDelegate.FunctionMap().ForEach(kv2 => Functions.Add(kv2));
        theDelegate.PropertiesForTree().ForEach(kv =>
        {
            AddProperties(kv.Key, kv.Value );
        });
    }
        
    private static string BuildArguments(List<IFunctionParameter> inputs)
    {
        inputs.Sort((a,b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));
        var usedIDs = new HashSet<string>();
            
        var stringBuilder = new StringBuilder();
        inputs.ForEach(input =>
        {
            if (usedIDs.Contains(input.ID)) return;
            usedIDs.Add(input.ID);
            stringBuilder.Append(input.ModifierString());
            stringBuilder.Append(' ');
            stringBuilder.Append(input.TypeName());
            stringBuilder.Append(' ');
            stringBuilder.Append(ShaderNodesUtil.FixName(input.ID));
            stringBuilder.Append(", ");
        });
        if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
        return stringBuilder.ToString();
    }

    public override void OnPassContext(ShaderGeneratorContext nodeContext)
    {
        _delegate ?. CheckContext(nodeContext);
    }
        
    public sealed override IDictionary<string, string> Functions { get; protected set; }

    protected override string SourceTemplate()
    {
        return "";
    }

    public virtual string FunctionName => Name + "_" + HashCode;
}

public class Delegate1In1Out<TIn, TOut> :Delegate<TOut>
{
    public Delegate1In1Out(NodeContext nodeContext, ShaderNode<TOut> theDelegate, FunctionParameter<TIn> theParam0, string theId = "Function", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<IFunctionParameter>{theParam0}, theId, theDefault)
    {
    }
}

public class Delegate0In1Out<TOut> :Delegate<TOut>
{
    public Delegate0In1Out(NodeContext nodeContext, ShaderNode<TOut> theDelegate, string theId = "Function", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<IFunctionParameter>(), theId, theDefault)
    {
    }
}

public class Delegate2In1Out<TIn0, TIn1, TOut> :Delegate<TOut>
{
    public Delegate2In1Out(NodeContext nodeContext, ShaderNode<TOut> theDelegate, FunctionParameter<TIn0> theParam0, FunctionParameter<TIn1> theParam1, string theId = "Function", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<IFunctionParameter>{theParam0, theParam1}, theId, theDefault)
    {
    }
}

public class Delegate3In1Out<TIn0, TIn1, TIn2, TOut> :Delegate<TOut>
{
    public Delegate3In1Out(NodeContext nodeContext, ShaderNode<TOut> theDelegate, FunctionParameter<TIn0> theParam0, FunctionParameter<TIn1> theParam1, FunctionParameter<TIn2> theParam2, string theId = "Function", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<IFunctionParameter>{theParam0, theParam1, theParam2}, theId, theDefault)
    {
    }
}

public class Delegate4In1Out<TIn0, TIn1, TIn2, TIn3, TOut> :Delegate<TOut>
{
    public Delegate4In1Out(
        NodeContext nodeContext, 
        ShaderNode<TOut> theDelegate, 
        FunctionParameter<TIn0> theParam0, 
        FunctionParameter<TIn1> theParam1, 
        FunctionParameter<TIn2> theParam2, 
        FunctionParameter<TIn3> theParam3, 
        string theId = "Function", ShaderNode<TOut> theDefault = null
        ) : base(nodeContext, theDelegate, new List<IFunctionParameter>{theParam0, theParam1, theParam2}, theId, theDefault)
    {
    }
}

public class Delegate5In1Out<TIn0, TIn1, TIn2, TIn3, TIn4, TOut> :Delegate<TOut>
{
    public Delegate5In1Out(
        NodeContext nodeContext, 
        ShaderNode<TOut> theDelegate, 
        FunctionParameter<TIn0> theParam0, 
        FunctionParameter<TIn1> theParam1, 
        FunctionParameter<TIn2> theParam2, 
        FunctionParameter<TIn3> theParam3, 
        FunctionParameter<TIn4> theParam4, 
        string theId = "Function", 
        ShaderNode<TOut> theDefault = null
        ) : base(nodeContext, theDelegate, new List<IFunctionParameter>{theParam0, theParam1, theParam2}, theId, theDefault)
    {
    }
}

