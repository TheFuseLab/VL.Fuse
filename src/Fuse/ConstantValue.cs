using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse;

public static class ConstantHelper
{
    // ReSharper disable once UnusedMember.Global
    // USED IN VL
    public static ConstantValue<T> FromFloat<T>(float theValue)
    {
        if (typeof(T) == typeof(float)) return new ConstantValue<T>((T)Convert.ChangeType(theValue, typeof(float)));
        if (typeof(T) == typeof(Vector2))
            return new ConstantValue<T>((T)Convert.ChangeType(new Vector2(theValue, theValue), typeof(Vector2)));
        if (typeof(T) == typeof(Vector3))
            return new ConstantValue<T>((T)Convert.ChangeType(new Vector3(theValue, theValue, theValue),
                typeof(Vector3)));
        if (typeof(T) == typeof(Vector4))
            return new ConstantValue<T>((T)Convert.ChangeType(new Vector4(theValue, theValue, theValue, theValue),
                typeof(Vector4)));
        if (typeof(T) == typeof(Color4))
            return new ConstantValue<T>((T)Convert.ChangeType(new Color4(theValue, theValue, theValue, theValue),
                typeof(Color4)));

        var intValue = (int)theValue;
        if (typeof(T) == typeof(int)) return new ConstantValue<T>((T)Convert.ChangeType(intValue, typeof(int)));
        if (typeof(T) == typeof(Int2))
            return new ConstantValue<T>((T)Convert.ChangeType(new Int2(intValue, intValue), typeof(Int2)));
        if (typeof(T) == typeof(Int3))
            return new ConstantValue<T>((T)Convert.ChangeType(new Int3(intValue, intValue, intValue), typeof(Int3)));
        if (typeof(T) == typeof(Int4))
            return new ConstantValue<T>((T)Convert.ChangeType(new Int4(intValue, intValue, intValue, intValue),
                typeof(Int4)));

        var boolValue = theValue > 0;
        if (typeof(T) == typeof(bool)) return new ConstantValue<T>((T)Convert.ChangeType(boolValue, typeof(bool)));

        return null;
    }

    public static AbstractShaderNode AbstractFromFloat(Type theType, float theValue)
    {
        if (theType == typeof(float)) return new ConstantValue<float>(theValue);
        if (theType == typeof(Vector2)) return new ConstantValue<Vector2>(new Vector2(theValue, theValue));
        if (theType == typeof(Vector3)) return new ConstantValue<Vector3>(new Vector3(theValue, theValue, theValue));
        if (theType == typeof(Vector4))
            return new ConstantValue<Vector4>(new Vector4(theValue, theValue, theValue, theValue));

        var intValue = (int)theValue;
        if (theType == typeof(int)) return new ConstantValue<int>(intValue);
        if (theType == typeof(Int2)) return new ConstantValue<Int2>(new Int2(intValue, intValue));
        if (theType == typeof(Int3)) return new ConstantValue<Int3>(new Int3(intValue, intValue, intValue));
        if (theType == typeof(Int4)) return new ConstantValue<Int4>(new Int4(intValue, intValue, intValue, intValue));

        return null;
    }

    /// <summary>
    /// Creates a ConstantValue from an object value. Falls back to zero if value is null.
    /// </summary>
    public static AbstractShaderNode AbstractFromObject(Type theType, object value)
    {
        // If no value provided, use zero default
        if (value == null)
            return AbstractFromFloat(theType, 0f);

        // Try to use the value directly if it's the right type
        if (theType == typeof(float) && value is float f) return new ConstantValue<float>(f);
        if (theType == typeof(Vector2) && value is Vector2 v2) return new ConstantValue<Vector2>(v2);
        if (theType == typeof(Vector3) && value is Vector3 v3) return new ConstantValue<Vector3>(v3);
        if (theType == typeof(Vector4) && value is Vector4 v4) return new ConstantValue<Vector4>(v4);
        if (theType == typeof(int) && value is int i) return new ConstantValue<int>(i);
        if (theType == typeof(Int2) && value is Int2 i2) return new ConstantValue<Int2>(i2);
        if (theType == typeof(Int3) && value is Int3 i3) return new ConstantValue<Int3>(i3);
        if (theType == typeof(Int4) && value is Int4 i4) return new ConstantValue<Int4>(i4);
        if (theType == typeof(bool) && value is bool b) return new ConstantValue<bool>(b);

        // Try to convert numeric values
        if (value is IConvertible convertible)
        {
            try
            {
                var floatVal = Convert.ToSingle(convertible);
                return AbstractFromFloat(theType, floatVal);
            }
            catch
            {
                // Fall through to default
            }
        }

        // Fallback to zero
        return AbstractFromFloat(theType, 0f);
    }
}

public interface IConstantValue
{
    public object GetValueAsObject();
}

public class ConstantValue<T> : ShaderNode<T>, IConstantValue
{
    public ConstantValue(T theValue) : base(NodeContext.CurrentRoot, "constant", null, false)
    {
        Value = theValue;
        HasFixedName = true;
    }

    public T Value { get; }

    public override string ID => TypeHelpers.GetDefaultForType(Value);

    public object GetValueAsObject()
    {
        return Value;
    }

    public override string TypeName()
    {
        return TypeHelpers.GetGpuType<T>();
    }

    protected override string SourceTemplate()
    {
        return ""; //TypeHelpers.GetDefaultForType(Value);
    }
}

public class ConstantKeyword : ShaderNode<float>
{
    private readonly string _keyword;

    protected ConstantKeyword(NodeContext nodeContext, string theKeyword) : base(nodeContext, "constant keyword")
    {
        _keyword = theKeyword;
        SetInputs(new List<AbstractShaderNode> { null });
        HasFixedName = true;
    }

    protected override Dictionary<string, string> CreateTemplateMap()
    {
        return new Dictionary<string, string>();
    }

    protected override string GenerateDefaultSource()
    {
        return _keyword;
    }

    protected override string SourceTemplate()
    {
        return _keyword;
    }
}