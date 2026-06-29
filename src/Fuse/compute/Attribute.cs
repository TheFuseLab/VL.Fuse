using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.ComputeSystem;

public interface IAttribute
{
    public string Name { get; }

    public AttributeType AttributeType { get; set; }

    public AbstractShaderNode ShaderNode { get; }

    public AbstractShaderNode InputAbstract { get; set; }

    public ShaderNode<GpuVoid> WriteCall { get; set; }

    public AbstractShaderNode ReadCall { get; set; }

    public Int3 Resolution { get; }

    public bool IsOverridden { get; }

    public void Sync(IAttribute theAttribute);
}

public enum AttributeType
{
    Temporary,
    StructuredBuffer,
    Texture
}

public abstract class Attribute<T> : PassThroughNode<T>, IAttribute
{
    public Attribute(NodeContext nodeContext, string theName, AttributeType theType, AbstractShaderNode theValue = null)
        : base(nodeContext, theValue, theName)
    {
        AttributeType = theType;
        WriteCall = new EmptyVoid(NextSubContextOrNull(nodeContext));
        ShaderNode = new ShaderNode<T>(NextSubContextOrNull(nodeContext), theName);
        IsOverridden = false;
        HasFixedName = true;
    }

    public AttributeType AttributeType { get; set; }
    public AbstractShaderNode ShaderNode { get; }

    public AbstractShaderNode InputAbstract
    {
        get => Input;
        set => SetInput(value);
    }

    public virtual void Sync(IAttribute theAttribute)
    {
        if (theAttribute == null || theAttribute == this) return;

        InputAbstract = theAttribute.InputAbstract;
        ReadCall = theAttribute.ReadCall;
        WriteCall = theAttribute.WriteCall;
    }

    public ShaderNode<GpuVoid> WriteCall { get; set; }

    public AbstractShaderNode ReadCall { get; set; }

    public abstract Int3 Resolution { get; }

    public bool IsOverridden { get; protected set; }

    public void SetInput(AbstractShaderNode theNode)
    {
        Input = theNode as ShaderNode<T>;
        SetInputs(new List<AbstractShaderNode> { Input });
    }

    private static NodeContext NextSubContextOrNull(NodeContext nodeContext)
    {
        return nodeContext == null
            ? null
            : new NodeSubContextFactory(nodeContext).NextSubContext();
    }
}

public class TemporaryAttribute<T> : Attribute<T>
{
    public TemporaryAttribute(NodeContext nodeContext, string theName) : base(nodeContext, theName,
        AttributeType.Temporary)
    {
        AddProperty("ComputeSystemAttribute", this);
    }

    public override Int3 Resolution => new(1);
}

public interface IGlobalAttribute
{
    AbstractShaderNode GetValue();

    string GetName();
}

public class GlobalAttribute<T> : IGlobalAttribute where T : struct
{
    public GlobalAttribute(
        NodeContext nodeContext,
        string name,
        ShaderNode<T> value = null)
    {
        Name = name;
        Attribute = new TemporaryAttribute<T>(nodeContext, name);
        Update(value ?? Fuse.ConstantHelper.FromFloat<T>(0));
    }

    public string Name { get; }

    public TemporaryAttribute<T> Attribute { get; }

    public ShaderNode<T> Value { get; private set; }

    public ShaderNode<T> GetGraph()
    {
        return Attribute;
    }

    public GlobalAttribute<T> Update(ShaderNode<T> value)
    {
        Value = value;
        Attribute.SetInput(value);
        return this;
    }

    AbstractShaderNode IGlobalAttribute.GetValue()
    {
        return Value;
    }

    public ShaderNode<T> GetValue()
    {
        return Value;
    }

    public string GetName()
    {
        return Name;
    }
}

public sealed class GlobalAttributeSet<T> where T : struct
{
    public GlobalAttributeSet(
        GlobalAttribute<T> target,
        ShaderNode<T> source)
    {
        Target = target ?? throw new System.ArgumentNullException(nameof(target));
        Source = source ?? Fuse.ConstantHelper.FromFloat<T>(0);
        Apply();
    }

    public GlobalAttribute<T> Target { get; }

    public ShaderNode<T> Source { get; }

    public ShaderNode<T> Graph { get; private set; }

    public string Name => Target.GetName();

    public ShaderNode<T> Apply()
    {
        Graph = Target.Update(Source).GetGraph();
        return Graph;
    }
}

public sealed class IterationIndexGlobal : GlobalAttribute<int>
{
    public const string AttributeName = "IterationIndex";

    public IterationIndexGlobal(
        NodeContext nodeContext = null,
        ShaderNode<int> value = null)
        : base(nodeContext, AttributeName, value)
    {
    }

    public static IterationIndexGlobal Create(
        NodeContext nodeContext = null,
        int iterationIndex = 0)
    {
        return new IterationIndexGlobal(nodeContext, new ConstantValue<int>(iterationIndex));
    }
}
