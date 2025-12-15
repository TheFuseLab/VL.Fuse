using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Fuse.compute;
using Fuse.function;
using Stride.Graphics;
using VL.Core;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse;

public static class AbstractCreation
{
    public static AbstractShaderNode CreateAbstract(AbstractShaderNode theValue, Type theBaseType,
        object[] theArguments)
    {
        var nodeType = GetBaseType(theValue);

        var dataType = new[] { nodeType.GetGenericArguments()[0] };
        var getType = theBaseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theArguments) as AbstractShaderNode;
    }

    private static Type GetBaseType(AbstractShaderNode theNode)
    {
        var nodeType = theNode.GetType();
        while (nodeType is { BaseType: not null } && nodeType.BaseType != typeof(AbstractShaderNode))
            nodeType = nodeType.BaseType;

        return nodeType;
    }

    private static Type GetBaseType(Type type)
    {
        var nodeType = type;
        while (nodeType is { BaseType: not null } && nodeType.BaseType != typeof(AbstractShaderNode))
            nodeType = nodeType.BaseType;

        return nodeType;
    }

    public static string GetFormattedName(this Type type)
    {
        if (type.IsGenericType)
        {
            var genericArguments = type.GetGenericArguments()
                .Select(x => x.Name)
                .Aggregate((x1, x2) => $"{x1}, {x2}");
            return $"{type.Name.Substring(0, type.Name.IndexOf("`"))}"
                   + $"<{genericArguments}>";
        }

        return type.Name;
    }

    public static AbstractShaderNode AbstractGetMember<T>(NodeSubContextFactory theSubContextFactory,
        ShaderNode<T> theStruct, AbstractShaderNode theMember)
    {
        var getMemberBaseType = typeof(GetMember<,>);
        var nodeType = GetBaseType(theMember);
        var dataType = new[] { typeof(T), nodeType.GetGenericArguments()[0] };
        var getType = getMemberBaseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMember.Name, null)
            as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractGetMember<T>(NodeSubContextFactory theSubContextFactory,
        ShaderNode<T> theStruct, string theMemberName, Type theMemberGenericType)
    {
        var getMemberBaseType = typeof(GetMember<,>);
        var dataType = new[] { typeof(T), theMemberGenericType };
        var getType = getMemberBaseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMemberName, null)
            as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractSetMember<T>(NodeSubContextFactory theSubContextFactory,
        ShaderNode<T> theStruct, string theMember, AbstractShaderNode theValue)
    {
        //return CreateAbstract(theMember, typeof(AssignValueToMember<>), new object[]{theSubContextFactory.NextSubContext(), theStruct, theMember.Name, theValue});


        var setMemberBaseType = typeof(SetMember<>);
        var dataType = new[] { typeof(T) };
        var getType = setMemberBaseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMember, theValue)
            as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractCustomFunction(
        NodeContext theContext,
        string theFunction,
        string theCodeTemplate,
        AbstractShaderNode theDefault,
        IEnumerable<AbstractShaderNode> theArguments,
        IEnumerable<InputModifier> theModifiers = null)
    {
        var baseType = typeof(CustomFunction<>);
        var valueType = GetBaseType(theDefault);
        var getType = baseType.MakeGenericType(valueType);
        return Activator.CreateInstance(
            getType,
            theContext,
            theFunction,
            theCodeTemplate,
            theDefault,
            theArguments,
            null,
            null,
            false,
            theModifiers) as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractFunctionParameter(NodeSubContextFactory theSubContextFactory,
        Type theParameterType, InputModifier theModifier = InputModifier.In, int theId = 0, string PinName = "")
    {
        var baseType = typeof(FunctionParameter<>);
        var dataType = new[] { theParameterType.GetGenericArguments()[0] };
        var getType = baseType.MakeGenericType(dataType);

        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), null, theModifier, theId,
            PinName) as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractFunctionParameter(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theParameter, InputModifier theModifier = InputModifier.In, int theId = 0,
        string PinName = "")
    {
        return AbstractFunctionParameter(theSubContextFactory, GetBaseType(theParameter), theModifier, theId, PinName);
    }

    public static AbstractShaderNode AbstractComputeTextureGet(NodeSubContextFactory theSubContextFactory,
        ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
    {
        var indexType = GetBaseType(theIndex);
        var valueType = GetBaseType(theValue);

        var baseType = typeof(ComputeTextureGet<,>);
        var dataType = new[] { indexType.GetGenericArguments()[0], valueType.GetGenericArguments()[0] };
        var getType = baseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theTexture, theIndex, null) as
            AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractComputeTextureSet(NodeSubContextFactory theSubContextFactory,
        ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
    {
        var indexType = GetBaseType(theIndex);
        var valueType = GetBaseType(theValue);

        var baseType = typeof(ComputeTextureSet<,>);
        var dataType = new[] { indexType.GetGenericArguments()[0], valueType.GetGenericArguments()[0] };
        var getType = baseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theTexture, theIndex, theValue)
            as AbstractShaderNode;
    }
    
    public static AbstractShaderNode AbstractBufferGet(
        NodeSubContextFactory theSubContextFactory,
        ChangeableObjectInput<Buffer> theBuffer, 
        ShaderNode<int> theIndex, 
        AbstractShaderNode theValue)
    {
        var valueType = GetBaseType(theValue);
        var genericArg = valueType.GetGenericArguments()[0];

        var baseType = typeof(BufferGet<>);
        var getType = baseType.MakeGenericType(genericArg);
        
        // Use explicit constructor invocation to handle IBufferInput<T> interface parameter
        // Activator.CreateInstance doesn't match interface parameters correctly
        var constructor = getType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        return constructor.Invoke(new object[] { theSubContextFactory.NextSubContext(), theBuffer, theIndex, null }) as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractBufferSet(
        NodeSubContextFactory theSubContextFactory,
        ChangeableObjectInput<Buffer> theBuffer, 
        ShaderNode<int> theIndex, 
        AbstractShaderNode theValue)
    {
        var valueType = GetBaseType(theValue);
        var genericArg = valueType.GetGenericArguments()[0];

        var baseType = typeof(BufferSet<>);
        var getType = baseType.MakeGenericType(genericArg);
        
        // Use explicit constructor invocation to handle IBufferInput<T> interface parameter
        var constructor = getType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        return constructor.Invoke(new object[] { theSubContextFactory.NextSubContext(), theBuffer, theIndex, theValue }) as AbstractShaderNode;
    }
    
    public static ChangeableObjectInput<Buffer> AbstractBufferInput(
        NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue)
    {
        // Generic definitions
        var bufferInputBaseType = typeof(BufferInput<>);
        var trackerBaseType = typeof(BufferTypeTracker<>);

        // Extract T from ShaderNode<T>
        var valueType = GetBaseType(theValue);
        var genericArg = valueType.GetGenericArguments()[0];  // Get T from ShaderNode<T>
        var genericArgs = new[] { genericArg };

        // Make closed generic types
        var bufferInputType = bufferInputBaseType.MakeGenericType(genericArgs);
        var trackerType = trackerBaseType.MakeGenericType(genericArgs);

        // Get NodeContext from the sub-context factory
        var nodeContext = theSubContextFactory.NextSubContext();

        // Construct BufferTypeTracker<T> with (ShaderNode<T> theType, BufferType theBufferType)
        // Pass theValue so the tracker can use theValue.TypeName() for the buffer type declaration
        var trackerConstructor = trackerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        var trackerInstance = trackerConstructor.Invoke(new object[] { theValue, BufferType.Normal });
        
        // IMPORTANT: Call CheckDeclaration to initialize GpuType and ComputeGpuType
        // Without this, the type strings remain empty and the shader declaration is invalid
        var checkDeclarationMethod = trackerType.GetMethod("CheckDeclaration");
        checkDeclarationMethod?.Invoke(trackerInstance, new object[] { null });
        
        // Debug logging
        var gpuTypeProperty = trackerType.GetProperty("GpuType");
        var computeGpuTypeProperty = trackerType.GetProperty("ComputeGpuType");
        Console.WriteLine($"[AbstractBufferInput] GenericArg: {genericArg.Name}, GpuType: {gpuTypeProperty?.GetValue(trackerInstance)}, ComputeGpuType: {computeGpuTypeProperty?.GetValue(trackerInstance)}");

        // Construct BufferInput<T>(NodeContext, BufferTypeTracker<T>, ShaderNode<T>)
        // Pass theValue so the buffer input has access to the type information
        var bufferInputConstructor = bufferInputType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)[0];
        return bufferInputConstructor.Invoke(new object[] { nodeContext, trackerInstance, theValue }) as ChangeableObjectInput<Buffer>;
    }

    public static AbstractShaderNode AbstractShaderNodePassThrough(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue)
    {
        return CreateAbstract(theValue, typeof(PassThroughNode<>),
            new object[] { theSubContextFactory.NextSubContext(), "pass", theValue });
    }

    public static AbstractShaderNode AbstractDeclareValue(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue)
    {
        return CreateAbstract(theValue, typeof(DeclareValue<>),
            new object[] { theSubContextFactory.NextSubContext(), null });
    }

    public static AbstractShaderNode AbstractDeclareValueAssigned(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue)
    {
        return CreateAbstract(theValue, typeof(DeclareValue<>),
            new object[] { theSubContextFactory.NextSubContext(), theValue });
    }

    public static AbstractShaderNode AbstractOutput(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theComputation, AbstractShaderNode theOutput)
    {
        var result = CreateAbstract(theOutput, typeof(Output<>),
            new object[] { theSubContextFactory.NextSubContext(), theComputation, theOutput, null });
        return result;
    }

    public static AbstractShaderNode AbstractAssignNode(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theTarget, AbstractShaderNode theSource)
    {
        return CreateAbstract(theTarget, typeof(AssignValue<>),
            new object[] { theSubContextFactory.NextSubContext(), theTarget, theSource });
    }

    public static AbstractShaderNode AbstractDo2(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue, IEnumerable<AbstractShaderNode> theInputs, string theId = "Do2")
    {
        return CreateAbstract(theValue, typeof(Do2<>),
            new object[] { theSubContextFactory.NextSubContext(), theValue, theInputs, theId });
    }

    public static AbstractShaderNode AbstractDelegate(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue, string theId = "Function")
    {
        return CreateAbstract(theValue, typeof(Delegate<>),
            new object[]
                { theSubContextFactory.NextSubContext(), theValue, theValue.FunctionParameters(), theId, null });
    }

    public static AbstractShaderNode AbstractValueInput(NodeSubContextFactory theSubContextFactory,
        Type theParameterType)
    {
        var baseType = typeof(ValueInput<>);
        var dataType = new[] { theParameterType.GetGenericArguments()[0] };
        var getType = baseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext()) as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractValueInputGeneric(NodeSubContextFactory theSubContextFactory,
        Type theGenericType)
    {
        var baseType = typeof(ValueInput<>);
        var dataType = new[] { theGenericType };
        var getType = baseType.MakeGenericType(dataType);
        return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext()) as AbstractShaderNode;
    }

    public static AbstractShaderNode AbstractValueInput(NodeSubContextFactory theSubContextFactory, object Value)
    {
        if (Value != null && TypeHelpers.IsGpuType(Value.GetType()))
        {
            var baseType = typeof(ValueInput<>);
            var dataType = new[] { Value.GetType() };
            var getType = baseType.MakeGenericType(dataType);
            var abstractValueInput =
                Activator.CreateInstance(getType, theSubContextFactory.NextSubContext()) as AbstractShaderNode;
            ((AbstractSetValueInput)abstractValueInput).SetAbstractValue(Value);
            return abstractValueInput;
        }

        return null;
    }

    public static void AbstractSetValueInput(this AbstractShaderNode ValueInput, object Value)
    {
        ((AbstractSetValueInput)ValueInput).SetAbstractValue(Value);
    }

    public static AbstractShaderNode AbstractConstant(AbstractShaderNode theGpuValue, float theValue)
    {
        return AbstractConstant(theGpuValue.GetType().BaseType, theValue);
    }

    public static AbstractShaderNode AbstractConstant(Type theType, float theValue)
    {
        var dataType = new[] { theType.GetGenericArguments()[0] };
        return ConstantHelper.AbstractFromFloat(dataType[0], theValue);
    }

    public static AbstractShaderNode AbstractSemantic(NodeSubContextFactory theSubContextFactory,
        AbstractShaderNode theValue, string theName, bool define = false, string theSemantic = null)
    {
        return CreateAbstract(theValue, typeof(Semantic<>),
            new object[] { theSubContextFactory.NextSubContext(), theName, define, theSemantic });
    }
}