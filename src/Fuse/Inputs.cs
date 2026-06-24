using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse;

public interface IGpuInput : IComputeNode
{
    void SetParameterValue(ParameterCollection theCollection);

    public void OnUpdateName();
}

public class DefaultInput<T> : ShaderNode<T>, IGpuInput
{
    public DefaultInput(NodeContext nodeContext, string theName) : base(nodeContext, theName)
    {
        SetInputs(new List<AbstractShaderNode>());
    }

    public override string ID => Name;

    public void SetParameterValue(ParameterCollection theCollection)
    {
    }

    public void OnUpdateName()
    {
    }

    public bool CheckDeclaration()
    {
        return false;
    }

    protected override string SourceTemplate()
    {
        return "";
    }
}

public class FieldDeclaration
{
    private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";

    private string _computeShaderDeclaration;
    private string _declaration;

    public FieldDeclaration(bool isResource, string computeShaderDeclaration, string declaration, string theID)
    {
        IsResource = isResource;
        Set(computeShaderDeclaration, declaration, theID);
    }

    public FieldDeclaration(string declaration)
    {
        _computeShaderDeclaration = declaration;
        _declaration = declaration;
    }

    public string TypeName { get; private set; }

    public string ComputeTypeName { get; private set; }

    public string InputName { get; private set; }

    public bool IsResource { get; }

    private string BuildDeclaration(string theTypeName, string theID)
    {
        return ShaderNodesUtil.Evaluate(
            DeclarationTemplate,
            new Dictionary<string, string>
            {
                { "inputName", theID },
                { "inputType", theTypeName }
            });
    }

    public void Set(string computeShaderTypeName, string typeName, string theID)
    {
        InputName = theID;
        ComputeTypeName = computeShaderTypeName;
        TypeName = typeName;
        _computeShaderDeclaration = BuildDeclaration(computeShaderTypeName, theID);
        _declaration = BuildDeclaration(typeName, theID);
    }

    public string GetDeclaration(bool theIsComputeShader)
    {
        return theIsComputeShader && _computeShaderDeclaration != null ? _computeShaderDeclaration : _declaration;
    }

    public override string ToString()
    {
        return _declaration;
    }
}

public abstract class AbstractInput<T, TParameterKeyType, TParameterUpdaterType> : ShaderNode<T>, IGpuInput
    where TParameterKeyType : ParameterKey<T> where TParameterUpdaterType : ParameterUpdater<T, TParameterKeyType>
{
    private readonly FieldDeclaration _fieldDeclaration;

    protected readonly TParameterUpdaterType Updater; // = new ValueParameterUpdater<T>();

    protected T _value;

    protected AbstractInput(NodeContext nodeContext, string theName, bool theIsResource) : base(nodeContext, theName)
    {
        Updater = CreateUpdater();
        SetInputs(new List<AbstractShaderNode>());
        AddProperty(Inputs, this);

        _fieldDeclaration = new FieldDeclaration(theIsResource, "", "", ID);
        AddProperty(Declarations, _fieldDeclaration);
    }

    protected TParameterKeyType ParameterKey { get; set; }

    public virtual T Value
    {
        get => Updater.Value;
        set
        {
            _value = value;
            Updater.Value = value;
        }
    }

    public abstract void SetParameterValue(ParameterCollection theCollection);
    public abstract void OnUpdateName();

    protected abstract TParameterUpdaterType CreateUpdater();


    protected override string SourceTemplate()
    {
        return "";
    }

    protected override string GenerateDefaultSource()
    {
        return "";
    }


    // Called by IMonadicValue<T>
    protected override T GetValue()
    {
        return Value;
    }

    // Called by IMonadicValue<T>
    protected override ShaderNode<T> SetValue(T value)
    {
        Value = value;
        return this;
    }


    protected void SetFieldDeclaration(string theTypeName, string theComputeTypeName)
    {
        _fieldDeclaration.Set(theComputeTypeName, theTypeName, ID);
    }

    protected void SetFieldDeclaration(string theTypeName)
    {
        SetFieldDeclaration(theTypeName, theTypeName);
    }
}

public abstract class ObjectInput<T> : AbstractInput<T, ObjectParameterKey<T>, ObjectParameterUpdater<T>>
    where T : class
{
    protected ObjectInput(NodeContext nodeContext, string theName) : base(nodeContext, theName, true)
    {
        ParameterKey = new ObjectParameterKey<T>(ID);
    }

    protected override ObjectParameterUpdater<T> CreateUpdater()
    {
        return new ObjectParameterUpdater<T>();
    }

    public override void OnPassContext(ShaderGeneratorContext nodeContext)
    {
        Updater.Track(nodeContext, ParameterKey);
        Updater.Value = _value;
    }

    public override void SetParameterValue(ParameterCollection theCollection)
    {
        theCollection.Set(ParameterKey, _value);
    }
}

public abstract class GpuTypeTracker<T>
{
    protected GpuTypeTracker()
    {
        ComputeGpuType = "";
        GpuType = "";
    }

    public string ComputeGpuType { get; private set; }

    public string GpuType { get; private set; }

    protected abstract string DefineGpuType(T value);
    protected abstract string DefineComputeGpuType(T value);

    public bool CheckDeclaration(T value)
    {
        var gpuType = DefineGpuType(value);
        var computeGpuType = DefineComputeGpuType(value);

        if (computeGpuType.Equals(ComputeGpuType) &&
            gpuType.Equals(GpuType)) return false;

        ComputeGpuType = computeGpuType;
        GpuType = gpuType;
        return true;
    }
}

public abstract class ChangeableObjectInput<T> : ObjectInput<T> where T : class
{
    protected ChangeableObjectInput(NodeContext nodeContext, GpuTypeTracker<T> theTypeTracker, string theName) : base(
        nodeContext, theName)
    {
        TypeTracker = theTypeTracker;
        SetFieldDeclaration(TypeTracker.GpuType, TypeTracker.ComputeGpuType);
    }

    public GpuTypeTracker<T> TypeTracker { get; }

    public override void OnUpdateName()
    {
        ParameterKey = new ObjectParameterKey<T>(ID);
        SetFieldDeclaration(TypeTracker.GpuType, TypeTracker.ComputeGpuType);
    }
}

public class TextureTypeTracker : GpuTypeTracker<Texture>
{
    private bool _useRw;

    public TextureTypeTracker(bool theUseRw)
    {
        _useRw = theUseRw;
    }

    public void SetUseRw(bool theUseRw)
    {
        _useRw = theUseRw;
    }

    protected override string DefineGpuType(Texture value)
    {
        if (value == null) return "Texture2D";

        return
            value.Dimension switch
            {
                TextureDimension.Texture1D => "Texture1D",
                TextureDimension.Texture2D => value.ArraySize == 1 ? "Texture2D" : "Texture2DArray",
                TextureDimension.Texture3D => "Texture3D",
                TextureDimension.TextureCube => "TextureCube",
                _ => "Texture2D"
            };
    }

    protected override string DefineComputeGpuType(Texture value)
    {
        return value == null ? "Texture2D<float4>" : TypeHelpers.TextureTypeName(value, _useRw);
    }
}

public class TextureInput : ChangeableObjectInput<Texture>, ITextureInput, ITextureInputProvider
{
    public TextureInput(NodeContext nodeContext, TextureTypeTracker theTypeTracker) : base(nodeContext, theTypeTracker,
        "TextureInput")
    {
        TextureTypeTracker = theTypeTracker;
    }

    public TextureTypeTracker TextureTypeTracker { get; }

    public string TextureID()
    {
        return ID;
    }

    public Int3 TextureSize()
    {
        return Value == null ? new Int3(1, 1, 1) : new Int3(Value.Width, Value.Height, Value.Depth);
    }

    public ITextureInput GetTextureInput()
    {
        return this;
    }

    public void SetTexture(bool theUseRW, Texture theTexture)
    {
        TextureTypeTracker.SetUseRw(theUseRW);
        TextureTypeTracker.CheckDeclaration(theTexture);
        OnUpdateName();
        Value = theTexture;
    }
}

public class SamplerInput : ObjectInput<SamplerState>, IGpuInput
{
    public SamplerInput(NodeContext nodeContext) : base(nodeContext, "SamplerInput")
    {
        SetFieldDeclaration("SamplerState", "SamplerState");
    }

    public override void OnUpdateName()
    {
        ParameterKey = new ObjectParameterKey<SamplerState>(ID);
        SetFieldDeclaration("SamplerState", "SamplerState");
    }
}

public class BufferTypeTracker<T> : GpuTypeTracker<Buffer>
{
    private readonly ShaderNode<T> _type;

    public BufferTypeTracker(ShaderNode<T> theType, BufferType theBufferType = BufferType.Auto)
    {
        _type = theType;
        BufferType = theBufferType;
    }

    public BufferType BufferType { get; set; }

    protected override string DefineGpuType(Buffer value)
    {
        return TypeHelpers.BufferTypeName(value, _type == null ? TypeHelpers.GetGpuType<T>() : _type.TypeName(),
            BufferType);
    }

    protected override string DefineComputeGpuType(Buffer value)
    {
        return DefineGpuType(value);
    }
}

public enum BufferType
{
    Append,
    Consume,
    Normal,
    RW,
    Auto
}

public class BufferInput<T> : ChangeableObjectInput<Buffer>, IBufferInput<T>
{
    private readonly BufferTypeTracker<T> _typeTracker;

    public BufferInput(NodeContext nodeContext, BufferTypeTracker<T> theTypeTracker, ShaderNode<T> theType) : base(
        nodeContext, theTypeTracker, "DynamicBufferInput")
    {
        _typeTracker = theTypeTracker;
        Value = null;
        Type = theType;

        SetInputs(new List<AbstractShaderNode> { Type });
    }


    public ShaderNode<T> Type { get; }

    public BufferType BufferType
    {
        get => _typeTracker.BufferType;
        set
        {
            _typeTracker.BufferType = value;
            // Recalculate the type strings with the new BufferType
            _typeTracker.CheckDeclaration(Value);
            SetFieldDeclaration(_typeTracker.ComputeGpuType, _typeTracker.GpuType);
        }
    }

    public override string TypeName()
    {
        return Type == null ? TypeHelpers.GetGpuType<T>() : Type.TypeName();
    }
}

public class ValueInput<T> : AbstractInput<T, ValueParameterKey<T>, ValueParameterUpdater<T>>, AbstractSetValueInput
    where T : struct
{
    /// <summary>
    /// Creates a value input with a default name.
    /// </summary>
    public ValueInput(NodeContext nodeContext) : this(nodeContext, null)
    {
    }

    /// <summary>
    /// Creates a value input with an optional semantic name for better shader code readability.
    /// </summary>
    /// <param name="nodeContext">The VL node context.</param>
    /// <param name="semanticName">Optional human-readable name (e.g., "boxSize", "intensity").
    /// When set, generated shader code will use this name instead of "input_123456".</param>
    public ValueInput(NodeContext nodeContext, string semanticName) : base(nodeContext, "input", false)
    {
        if (!string.IsNullOrEmpty(semanticName))
            SemanticName = ShaderNodesUtil.FixName(semanticName);
        ParameterKey = new ValueParameterKey<T>(ID);
        SetFieldDeclaration(TypeHelpers.GetGpuType<T>());
    }

    public void SetAbstractValue(object Value)
    {
        SetValue((T)Value);
    }

    protected override ValueParameterUpdater<T> CreateUpdater()
    {
        return new ValueParameterUpdater<T>();
    }

    public override void OnPassContext(ShaderGeneratorContext nodeContext)
    {
        try
        {
            Updater.Track(nodeContext, ParameterKey);
            Updater.Value = _value;
        }
        catch (Exception)
        {
            // Console.WriteLine(e);
        }
    }

    public override void SetParameterValue(ParameterCollection theCollection)
    {
        theCollection.Set(ParameterKey, Value);
    }

    public override void OnUpdateName()
    {
        ParameterKey = new ValueParameterKey<T>(ID);
        SetFieldDeclaration(TypeHelpers.GetGpuType<T>());
    }

    // Called by IMonadicValue
    protected override bool HasValue()
    {
        return true;
    }
}

public interface AbstractSetValueInput
{
    void SetAbstractValue(object Value);
}

/*
public class CompositionInput<T> : AbstractInput<IComputeNode,PermutationParameterKey<T>, PermutationParameterUpdater<T>> where T : IComputeNode
{

    public CompositionInput(SetVar<T> value): base("input", false, value)
    {
    }

    protected override PermutationParameterUpdater<IComputeNode> CreateUpdater()
    {
        return new PermutationParameterUpdater<IComputeNode>();
    }

    protected override void OnPassContext(ShaderGeneratorContext nodeContext)
    {
        Updater.Track(nodeContext, ParameterKey);
    }

    protected override void OnGenerateCode(ShaderGeneratorContext nodeContext)
    {
        ParameterKey = new PermutationParameterKey<ShaderSource>(ID);
        SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
    }


}*/
