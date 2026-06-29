using System;
using System.Collections.Generic;
using Fuse.ComputeSystem;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using VL.Stride.Shaders.ShaderFX;
using StrideBuffer = Stride.Graphics.Buffer;

namespace Fuse.compute;

public sealed class StructuredBufferResourceBinding
{
    public StructuredBufferResourceBinding(
        IAttribute attribute,
        AbstractShaderNode readCall,
        AbstractShaderNode input,
        ShaderNode<GpuVoid> writeCall)
    {
        Attribute = attribute;
        ReadCall = readCall;
        Input = input;
        WriteCall = writeCall;
    }

    public IAttribute Attribute { get; }

    public AbstractShaderNode ReadCall { get; }

    public AbstractShaderNode Input { get; }

    public ShaderNode<GpuVoid> WriteCall { get; }
}

public sealed class StructuredBufferResourceBindings
{
    private readonly List<StructuredBufferResourceBinding> _items = new();

    public IReadOnlyList<StructuredBufferResourceBinding> Items => _items;

    public Group ReadGroup { get; private set; }

    public Group WriteGroup { get; private set; }

    public BufferInput<GpuStruct> BufferInput { get; internal set; }

    public BufferGet<GpuStruct> BufferRead { get; internal set; }

    public DeclareValue<GpuStruct> WriteValue { get; internal set; }

    public BufferSet<GpuStruct> BufferWrite { get; internal set; }

    public AbstractShaderNode PostGraphRenderer { get; private set; }

    public bool WriteAttributes { get; internal set; } = true;

    internal void Add(StructuredBufferResourceBinding binding)
    {
        _items.Add(binding);
    }

    internal void SetGroups(NodeContext nodeContext, AbstractShaderNode postGraphRenderer = null)
    {
        var readCalls = new List<AbstractShaderNode>();
        var writeCalls = new List<AbstractShaderNode>();
        PostGraphRenderer = postGraphRenderer;

        foreach (var item in _items)
        {
            readCalls.Add(item.ReadCall);
            if (WriteAttributes)
                writeCalls.Add(item.WriteCall);
            else
                writeCalls.Add(item.ReadCall);
        }

        if (BufferWrite != null)
            writeCalls.Add(BufferWrite);
        if (PostGraphRenderer != null)
            writeCalls.Add(PostGraphRenderer);

        ReadGroup = new Group(nodeContext, readCalls, "StructuredBufferResourceRead");
        WriteGroup = new Group(nodeContext, writeCalls, "StructuredBufferResourceWrite");
    }
}

public partial class StructuredBufferResource
{
    [Fragment(Order = 85)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource BindAttributes(
        NodeContext nodeContext,
        ShaderNode<int> index,
        AttributeMap attributeMap,
        AbstractShaderNode postGraphRenderer,
        bool writeAttributes,
        out Group read,
        out Group write)
    {
        var bindings = BindAttributes(
            nodeContext,
            index,
            attributeMap,
            postGraphRenderer,
            writeAttributes);
        read = bindings.ReadGroup;
        write = bindings.WriteGroup;
        return this;
    }

    public StructuredBufferResourceBindings BindAttributes(
        NodeContext nodeContext,
        ShaderNode<int> index = null,
        AttributeMap attributeMap = null,
        AbstractShaderNode postGraphRenderer = null,
        bool writeAttributes = true)
    {
        return BindComputeStage(
            nodeContext,
            index ?? new DispatchThreadIdX(nodeContext),
            attributeMap,
            postGraphRenderer,
            writeAttributes);
    }

    public StructuredBufferResourceBindings BindComputeStage(
        NodeContext nodeContext,
        AttributeMap attributeMap = null,
        AbstractShaderNode postGraphRenderer = null,
        bool writeAttributes = true)
    {
        return BindComputeStage(
            nodeContext,
            new DispatchThreadIdX(nodeContext),
            attributeMap,
            postGraphRenderer,
            writeAttributes);
    }

    [Fragment(Order = 90)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource BindComputeStage(
        NodeContext nodeContext,
        AttributeMap attributeMap,
        AbstractShaderNode postGraphRenderer,
        bool writeAttributes,
        out Group read,
        out Group write)
    {
        var bindings = BindComputeStage(
            nodeContext,
            attributeMap,
            postGraphRenderer,
            writeAttributes);
        read = bindings.ReadGroup;
        write = bindings.WriteGroup;
        return this;
    }

    public StructuredBufferResourceBindings BindComputeStage(
        NodeContext nodeContext,
        ShaderNode<int> index,
        AttributeMap attributeMap = null,
        AbstractShaderNode postGraphRenderer = null,
        bool writeAttributes = true)
    {
        var bindings = CreateReadWriteBindings(nodeContext, index, attributeMap, writeAttributes);
        if (postGraphRenderer != null)
            bindings.SetGroups(nodeContext, postGraphRenderer);

        return bindings;
    }

    public StructuredBufferResourceBindings CreateReadWriteBindings(
        NodeContext nodeContext,
        ShaderNode<int> index,
        AttributeMap attributeMap = null,
        bool writeAttributes = true)
    {
        var map = attributeMap ?? AttributeMap;
        var bindings = new StructuredBufferResourceBindings
        {
            WriteAttributes = writeAttributes
        };

        if (map == null)
        {
            bindings.SetGroups(nodeContext);
            LastBindings = bindings;
            return bindings;
        }

        var bufferType = CreateBufferStructType(nodeContext);
        var typeTracker = new BufferTypeTracker<GpuStruct>(bufferType);
        var bufferInput = new BufferInput<GpuStruct>(nodeContext, typeTracker, bufferType);
        var bufferRead = new BufferGet<GpuStruct>(nodeContext, bufferInput, index, bufferType);
        var writeValue = writeAttributes
            ? new DeclareValue<GpuStruct>(nodeContext, bufferRead)
            : null;
        var bufferWrite = writeAttributes
            ? new BufferSet<GpuStruct>(nodeContext, bufferInput, index, writeValue)
            : null;

        bindings.BufferInput = bufferInput;
        bindings.BufferRead = bufferRead;
        bindings.WriteValue = writeValue;
        bindings.BufferWrite = bufferWrite;

        foreach (var attribute in map.AttributeSet.Values)
        {
            if (attribute is PaddingAttribute)
                continue;

            var binding = CreateAttributeBinding(nodeContext, attribute, bufferRead, writeValue, writeAttributes);
            if (binding == null)
                continue;

            attribute.ReadCall = binding.ReadCall;
            attribute.InputAbstract = binding.Input;
            attribute.WriteCall = binding.WriteCall;
            bindings.Add(binding);
        }

        map.SyncAttributes();
        bindings.SetGroups(nodeContext);
        LastBindings = bindings;
        return bindings;
    }

    public StructuredBufferResourceBinding CreateRead<T>(
        NodeContext nodeContext,
        IAttribute attribute,
        ShaderNode<GpuStruct> structInput)
    {
        if (attribute == null)
            throw new ArgumentNullException(nameof(attribute));

        var readCall = new GetMember<GpuStruct, T>(
            nodeContext,
            structInput,
            attribute.Name,
            attribute.ShaderNode as ShaderNode<T>);

        attribute.ReadCall = readCall;
        return new StructuredBufferResourceBinding(attribute, readCall, attribute.InputAbstract, attribute.WriteCall);
    }

    public StructuredBufferResourceBinding CreateWrite(
        NodeContext nodeContext,
        IAttribute attribute,
        ShaderNode<GpuStruct> structOutput)
    {
        if (attribute == null)
            throw new ArgumentNullException(nameof(attribute));

        var input = attribute.InputAbstract ?? attribute.ReadCall ?? attribute.ShaderNode;
        var writeCall = new SetMember<GpuStruct>(nodeContext, structOutput, attribute.Name, input);

        attribute.InputAbstract = input;
        attribute.WriteCall = writeCall;

        return new StructuredBufferResourceBinding(attribute, attribute.ReadCall, input, writeCall);
    }

    [Fragment(Order = 95)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource SyncAttributes(AttributeMap attributeMap = null)
    {
        (attributeMap ?? AttributeMap)?.SyncAttributes();
        return this;
    }

    private ShaderNode<GpuStruct> CreateBufferStructType(NodeContext nodeContext)
    {
        var typeNode = new ShaderNode<GpuStruct>(nodeContext, Name ?? "StructuredBufferResource", theCreateDefault: false);
        typeNode.TypeOverride = StructDescription?.Name ?? StructuredBufferStructDescription.SanitizeStructName(Name);
        if (StructDescription != null)
            typeNode.SetProperty(ShaderConstants.PropertyKeys.Structs, StructDescription.BuildStructSource());

        return typeNode;
    }

    private StructuredBufferResourceBinding CreateAttributeBinding(
        NodeContext nodeContext,
        IAttribute attribute,
        ShaderNode<GpuStruct> bufferRead,
        ShaderNode<GpuStruct> writeValue,
        bool writeAttributes)
    {
        var shaderNodeType = attribute.ShaderNode?.GetType();
        if (shaderNodeType == null || !shaderNodeType.IsGenericType)
            return null;

        var valueType = shaderNodeType.GetGenericArguments()[0];
        var createRead = GetType()
            .GetMethod(nameof(CreateRead), new[] { typeof(NodeContext), typeof(IAttribute), typeof(ShaderNode<GpuStruct>) })
            ?.MakeGenericMethod(valueType);

        var readBinding = (StructuredBufferResourceBinding)createRead?.Invoke(this, new object[] { nodeContext, attribute, bufferRead });
        if (!writeAttributes)
        {
            attribute.InputAbstract = readBinding?.ReadCall;
            attribute.WriteCall = null;
            return new StructuredBufferResourceBinding(attribute, readBinding?.ReadCall, attribute.InputAbstract, null);
        }

        var writeBinding = CreateWrite(nodeContext, attribute, writeValue);

        return new StructuredBufferResourceBinding(attribute, readBinding?.ReadCall, writeBinding.Input, writeBinding.WriteCall);
    }

    public void SetBufferOnStructuredAttributes(StrideBuffer buffer, AttributeMap attributeMap = null)
    {
        var map = attributeMap ?? AttributeMap;
        if (map == null)
            return;

        foreach (var attribute in map.AttributeSet.Values)
        {
            if (attribute is IStructureBufferAttribute structuredBufferAttribute)
                structuredBufferAttribute.Buffer = buffer;
        }
    }
}
