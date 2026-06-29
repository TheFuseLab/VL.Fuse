using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.ComputeSystem;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;
using VL.Core.Import;
using VL.Model;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute;

[ProcessNode(Name = "TextureResource", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed partial class TextureResource : IComputeResourceProvider
{
    private readonly Dictionary<string, TextureInput> _textureAInputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextureInput> _textureBInputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture> _textureAs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture> _textureBs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _textureStatuses = new(StringComparer.Ordinal);

    [Fragment(Order = 0)]
    public TextureResource(
        string name = null,
        Int3? size = null,
        ComputeDispatchSize? threadGroupSize = null,
        ComputeDispatchLimits? limits = null)
    {
        Name = name;
        Size = EnsureOne(size ?? new Int3(1, 1, 1));
        AttributeMap = new AttributeMap(AttributeType.Texture);
        DispatchInfo = TextureDispatchInfo.Create(Size, threadGroupSize, limits);
    }

    public string Name { get; private set; }

    public AttributeType AttributeType => AttributeType.Texture;

    public Int3 Size { get; private set; }

    public AttributeMap AttributeMap { get; private set; }

    public TextureDispatchInfo DispatchInfo { get; private set; }

    public bool ChangedAttributes { get; private set; }

    public int Ticket { get; private set; }

    public IReadOnlyDictionary<string, TextureInput> TextureAInputs => _textureAInputs;

    public IReadOnlyDictionary<string, TextureInput> TextureBInputs => _textureBInputs;

    public IReadOnlyDictionary<string, Texture> TextureAs => _textureAs;

    public IReadOnlyDictionary<string, Texture> TextureBs => _textureBs;

    public IReadOnlyDictionary<string, string> TextureStatuses => _textureStatuses;

    public string LastTextureStatus { get; private set; } = "Uninitialized";

    public Group ReadGroup { get; private set; }

    public Group WriteGroup { get; private set; }

    [Fragment(Order = 100)]
    public TextureResource Output => this;

    public static TextureResource Create(
        string name = null,
        Int3? size = null,
        ComputeDispatchSize? threadGroupSize = null,
        ComputeDispatchLimits? limits = null)
    {
        return new TextureResource(name, size, threadGroupSize, limits);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource SetName(string name)
    {
        Name = name;
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource SetDimension(Int3 dimension)
    {
        Size = EnsureOne(dimension);
        DispatchInfo.SetDimension(Size);
        return this;
    }

    public TextureResource SetSize(Int3 size)
    {
        return SetDimension(size);
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource SetThreadGroupSize(ComputeDispatchSize threadGroupSize)
    {
        DispatchInfo.SetThreadGroupSize(threadGroupSize);
        return this;
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource Prepare()
    {
        AttributeMap.Prepare();
        ChangedAttributes = false;
        return this;
    }

    [Fragment(Order = 41)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource HandleAttribute(IAttribute attribute)
    {
        if (attribute?.AttributeType != AttributeType.Texture)
            return this;

        AttributeMap.HandleAttribute(attribute);
        return this;
    }

    [Fragment(Order = 42)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource FinishAttributeMap(bool syncAttributes = true)
    {
        ChangedAttributes = AttributeMap.Finish(syncAttributes: syncAttributes);
        if (ChangedAttributes)
            Ticket++;

        RemoveUnusedTextures();
        return this;
    }

    [Fragment(Order = 50)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource Finish(bool syncAttributes = true)
    {
        return FinishAttributeMap(syncAttributes);
    }

    [Fragment(Order = 60)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource BindAttributes(
        NodeContext nodeContext = null,
        AbstractShaderNode readIndex = null,
        AbstractShaderNode writeIndex = null,
        AttributeMap attributeMap = null)
    {
        var subContextFactory = new NodeSubContextFactory(nodeContext);
        foreach (var attribute in (attributeMap ?? AttributeMap)?.AttributeSet.Values ?? Array.Empty<IAttribute>())
            BindAttribute(attribute, nodeContext, readIndex, writeIndex, subContextFactory);

        return this;
    }

    [Fragment(Order = 70)]
    public Group CreateRead(
        NodeContext nodeContext = null,
        AttributeMap attributeMap = null)
    {
        var readCalls = new List<AbstractShaderNode>();
        foreach (var attribute in (attributeMap ?? AttributeMap)?.AttributeSet.Values ?? Array.Empty<IAttribute>())
        {
            if (attribute.ReadCall != null)
                readCalls.Add(attribute.ReadCall);
        }

        ReadGroup = new Group(nodeContext, readCalls, "TextureResourceRead");
        return ReadGroup;
    }

    [Fragment(Order = 80)]
    public Group CreateWrite(
        NodeContext nodeContext = null,
        AttributeMap attributeMap = null,
        AbstractShaderNode postGraphRenderer = null)
    {
        var writeCalls = new List<AbstractShaderNode>();
        var map = attributeMap ?? AttributeMap;

        foreach (var attribute in map?.GetAttributeInstances() ?? Enumerable.Empty<IAttribute>())
        {
            if (!Writes(attribute))
                continue;

            if (attribute.WriteCall != null)
                writeCalls.Add(attribute.WriteCall);
            if (attribute is ITextureAttribute { DoubleBuffered: true })
                writeCalls.Add(new TextureSwap(nodeContext, this, attribute));
        }

        if (postGraphRenderer != null)
            writeCalls.Add(postGraphRenderer);

        WriteGroup = new Group(nodeContext, writeCalls, "TextureResourceWrite");
        return WriteGroup;
    }

    public TextureResource BindComputeStage(
        NodeContext nodeContext,
        AttributeMap attributeMap = null,
        AbstractShaderNode postGraphRenderer = null,
        bool writeAttributes = true)
    {
        return BindComputeStage(
            nodeContext,
            CreateTextureIndex(nodeContext, Size),
            CreateTextureIndex(nodeContext, Size),
            attributeMap,
            postGraphRenderer,
            writeAttributes);
    }

    [Fragment(Order = 90)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource BindComputeStage(
        NodeContext nodeContext,
        AttributeMap attributeMap,
        AbstractShaderNode postGraphRenderer,
        bool writeAttributes,
        out Group read,
        out Group write)
    {
        BindComputeStage(
            nodeContext,
            attributeMap,
            postGraphRenderer,
            writeAttributes);
        read = ReadGroup;
        write = WriteGroup;
        return this;
    }

    public TextureResource BindComputeStage(
        NodeContext nodeContext,
        AbstractShaderNode readIndex,
        AbstractShaderNode writeIndex,
        AttributeMap attributeMap = null,
        AbstractShaderNode postGraphRenderer = null,
        bool writeAttributes = true)
    {
        var map = attributeMap ?? AttributeMap;
        BindAttributes(nodeContext, readIndex, writeIndex, map);
        CreateRead(nodeContext, map);

        if (writeAttributes)
        {
            CreateWrite(nodeContext, map, postGraphRenderer);
        }
        else
        {
            var writeCalls = new List<AbstractShaderNode>(ReadGroup?.Ins ?? []);
            if (postGraphRenderer != null)
                writeCalls.Add(postGraphRenderer);
            WriteGroup = new Group(nodeContext, writeCalls, "TextureResourceWrite");
        }

        SyncAttributes(map);
        return this;
    }

    public TextureResource BindAttribute(
        IAttribute attribute,
        NodeContext nodeContext = null,
        AbstractShaderNode readIndex = null,
        AbstractShaderNode writeIndex = null)
    {
        return BindAttribute(
            attribute,
            nodeContext,
            readIndex,
            writeIndex,
            new NodeSubContextFactory(nodeContext));
    }

    public TextureResource BindAttribute(
        IAttribute attribute,
        NodeContext nodeContext,
        AbstractShaderNode readIndex,
        AbstractShaderNode writeIndex,
        NodeSubContextFactory subContextFactory)
    {
        if (attribute is not ITextureAttribute textureAttribute)
            return this;

        textureAttribute.Index = readIndex ?? textureAttribute.Index;
        attribute.ReadCall = ReadCall(
            attribute,
            subContextFactory,
            textureAttribute.Index ?? readIndex ?? new DispatchThreadId(nodeContext),
            nodeContext);

        attribute.WriteCall = WriteCall(
            attribute,
            subContextFactory,
            writeIndex ?? textureAttribute.Index ?? readIndex ?? new DispatchThreadId(nodeContext),
            nodeContext);

        return this;
    }

    public TextureInput AddInput(
        NodeContext nodeContext,
        string key,
        Texture texture = null,
        bool useRw = false)
    {
        return AddInput(nodeContext, key, texture, useRw, _textureAInputs);
    }

    public IReadOnlyDictionary<string, Texture> GetTextures()
    {
        return TextureAs;
    }

    public TextureInput GetTexture(string key)
    {
        if (key == null)
            return null;

        return _textureAInputs.GetValueOrDefault(key);
    }

    [Fragment(Order = 96)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource UpdateTextures(
        GraphicsDevice graphicsDevice = null,
        IEnumerable<string> attributesToRemove = null,
        bool recreate = false,
        NodeContext nodeContext = null,
        bool unorderedAccess = true)
    {
        RemoveTextureKeys(attributesToRemove);

        foreach (var pair in AttributeMap.AttributeSet)
        {
            if (pair.Value is not ITextureAttribute textureAttribute)
                continue;

            var format = GetPixelFormat(pair.Value);
            ComputeTextureResourceDescription description;
            try
            {
                description = GetTextureDescription(format, unorderedAccess);
            }
            catch (Exception ex)
            {
                RecordTextureDescriptionFailure(pair.Key, "A", ex, format, unorderedAccess);
                var failedReadInput = AddInput(nodeContext, pair.Key, _textureAs.GetValueOrDefault(pair.Key), false);
                textureAttribute.TextureInput = failedReadInput;

                if (textureAttribute.DoubleBuffered)
                {
                    RecordTextureDescriptionFailure(pair.Key, "B", ex, format, unorderedAccess);
                    AddInput(nodeContext, pair.Key, _textureBs.GetValueOrDefault(pair.Key), true, _textureBInputs);
                }

                continue;
            }

            var textureA = CreateTexture(graphicsDevice, description, pair.Key, "A", _textureAs.GetValueOrDefault(pair.Key), recreate);
            var readInput = AddInput(nodeContext, pair.Key, textureA, false);
            textureAttribute.TextureInput = readInput;

            if (!textureAttribute.DoubleBuffered)
                continue;

            var textureB = CreateTexture(graphicsDevice, description, pair.Key, "B", _textureBs.GetValueOrDefault(pair.Key), recreate);
            AddInput(nodeContext, pair.Key, textureB, true, _textureBInputs);
        }

        LastTextureStatus = _textureStatuses.Count == 0
            ? "NoTextures"
            : string.Join(";", _textureStatuses.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"));

        return this;
    }

    public AbstractShaderNode ReadCall(
        IAttribute input,
        NodeSubContextFactory subContextFactory,
        AbstractShaderNode readIndex,
        NodeContext nodeContext = null)
    {
        if (input is not ITextureAttribute textureAttribute)
            return input?.ReadCall;

        var texture = AddInput(nodeContext, input.Name, _textureAs.GetValueOrDefault(input.Name), false);
        textureAttribute.TextureInput = texture;
        textureAttribute.Index = readIndex ?? textureAttribute.Index;

        var call = CreateTextureReadCall(
            subContextFactory ?? new NodeSubContextFactory(nodeContext),
            texture,
            textureAttribute.Index,
            input.ShaderNode,
            nodeContext);

        input.ReadCall = call;
        return call;
    }

    public ShaderNode<GpuVoid> WriteCall(
        IAttribute input,
        NodeSubContextFactory subContextFactory,
        AbstractShaderNode writeIndex,
        NodeContext nodeContext = null)
    {
        if (input is not ITextureAttribute textureAttribute)
            return input?.WriteCall;

        var texture = textureAttribute.DoubleBuffered
            ? AddInput(nodeContext, input.Name, _textureBs.GetValueOrDefault(input.Name), true, _textureBInputs)
            : AddInput(nodeContext, input.Name, _textureAs.GetValueOrDefault(input.Name), true, _textureAInputs);
        var value = input.InputAbstract ?? input.ReadCall ?? input.ShaderNode;
        var call = CreateTextureWriteCall(
            subContextFactory ?? new NodeSubContextFactory(nodeContext),
            texture,
            writeIndex ?? textureAttribute.Index,
            value,
            nodeContext);

        input.InputAbstract = value;
        input.WriteCall = call;
        return call;
    }

    [Fragment(Order = 98)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource SwapTextures(string key)
    {
        if (key == null)
            return this;

        Swap(_textureAInputs, _textureBInputs, key);
        Swap(_textureAs, _textureBs, key);

        foreach (var textureAttribute in AttributeMap
                     .GetAttributeInstances()
                     .Where(attribute => attribute.Name == key)
                     .OfType<ITextureAttribute>())
        {
            textureAttribute.TextureInput = GetTexture(key);
        }

        return this;
    }

    [Fragment(Order = 99)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource SyncAttributes(AttributeMap attributeMap = null)
    {
        (attributeMap ?? AttributeMap)?.SyncAttributes();
        return this;
    }

    public AttributeMap GetAttributeMap()
    {
        return AttributeMap;
    }

    public string GetName()
    {
        return Name;
    }

    public AttributeType GetAttributeType()
    {
        return AttributeType;
    }

    public Int3 GetSize()
    {
        return Size;
    }

    public Int3 GetDimension()
    {
        return Size;
    }

    public TextureDispatchInfo GetDispatchInfo()
    {
        return DispatchInfo;
    }

    public int GetTicket()
    {
        return Ticket;
    }

    public ComputeResource GetResource()
    {
        return GetComputeResource();
    }

    public ComputeResource GetComputeResource()
    {
        return new ComputeResource(AttributeType.Texture, Name, Size);
    }

    public IReadOnlyDictionary<string, string> GetTextureStatuses()
    {
        return TextureStatuses;
    }

    public string GetLastTextureStatus()
    {
        return LastTextureStatus;
    }

    [Fragment(Order = 97)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public TextureResource Reset(bool condition = true)
    {
        if (!condition)
            return this;

        foreach (var texture in _textureAs.Values)
            texture?.Dispose();
        foreach (var texture in _textureBs.Values)
            texture?.Dispose();

        _textureAInputs.Clear();
        _textureBInputs.Clear();
        _textureAs.Clear();
        _textureBs.Clear();
        _textureStatuses.Clear();
        ReadGroup = null;
        WriteGroup = null;
        LastTextureStatus = "Reset";
        Ticket++;
        return this;
    }

    public ComputeTextureResourceDescription GetTextureDescription(PixelFormat? format = null, bool unorderedAccess = true)
    {
        return ComputeResourceDescriptions.Texture(
            Size,
            format ?? PixelFormat.R32G32B32A32_Float,
            unorderedAccess);
    }

    private TextureInput AddInput(
        NodeContext nodeContext,
        string key,
        Texture texture,
        bool useRw,
        Dictionary<string, TextureInput> inputs)
    {
        if (key == null)
            return null;

        if (!inputs.TryGetValue(key, out var input))
        {
            input = new TextureInput(nodeContext, new TextureTypeTracker(useRw), BuildTextureInputName(key, inputs));
            inputs.Add(key, input);
        }
        else
        {
            input.SetTextureName(BuildTextureInputName(key, inputs));
        }

        var expectedFormat = AttributeMap.AttributeSet.TryGetValue(key, out var attribute)
            ? GetPixelFormat(attribute)
            : PixelFormat.R32G32B32A32_Float;
        input.SetTexture(
            useRw,
            texture,
            expectedFormat,
            ComputeResourceDescriptions.GetTextureDimension(Size));
        if (ReferenceEquals(inputs, _textureAInputs))
            _textureAs[key] = texture;
        else
            _textureBs[key] = texture;

        return input;
    }

    private string BuildTextureInputName(
        string key,
        Dictionary<string, TextureInput> inputs)
    {
        var slot = ReferenceEquals(inputs, _textureBInputs) ? "B" : "A";
        var resourceName = string.IsNullOrWhiteSpace(Name) ? null : Name;
        var attributeName = string.IsNullOrWhiteSpace(key) ? null : key;
        var baseName = resourceName == null
            ? $"{attributeName ?? "Texture"}_{BuildDimensionName(Size)}"
            : string.Equals(resourceName, attributeName, StringComparison.Ordinal)
                ? resourceName
                : attributeName == null
                    ? resourceName
                    : $"{resourceName}_{attributeName}";

        return $"{baseName}_{slot}";
    }

    private static string BuildDimensionName(Int3 size)
    {
        return $"{size.X}x{size.Y}x{size.Z}";
    }

    private void RemoveUnusedTextures()
    {
        var usedKeys = AttributeMap.AttributeSet.Keys.ToHashSet(StringComparer.Ordinal);
        RemoveUnused(_textureAInputs, usedKeys);
        RemoveUnused(_textureBInputs, usedKeys);
        RemoveUnused(_textureAs, usedKeys);
        RemoveUnused(_textureBs, usedKeys);
        RemoveUnused(_textureStatuses, usedKeys);
    }

    private static void RemoveUnused<T>(Dictionary<string, T> dictionary, HashSet<string> usedKeys)
    {
        foreach (var key in dictionary.Keys.Where(key => !IsUsedTextureKey(key, usedKeys)).ToArray())
            dictionary.Remove(key);
    }

    private void RemoveTextureKeys(IEnumerable<string> keys)
    {
        if (keys == null)
            return;

        foreach (var key in keys.Where(key => key != null))
        {
            if (_textureAs.Remove(key, out var textureA))
                textureA?.Dispose();
            if (_textureBs.Remove(key, out var textureB))
                textureB?.Dispose();
            _textureAInputs.Remove(key);
            _textureBInputs.Remove(key);
            _textureStatuses.Remove(TextureStatusKey(key, "A"));
            _textureStatuses.Remove(TextureStatusKey(key, "B"));
        }
    }

    private Texture CreateTexture(
        GraphicsDevice graphicsDevice,
        ComputeTextureResourceDescription description,
        string key,
        string slot,
        Texture existing,
        bool recreate)
    {
        var statusKey = TextureStatusKey(key, slot);
        if (description.Description.Width <= 0 || description.Description.Height <= 0 || description.Description.Depth <= 0)
        {
            _textureStatuses[statusKey] = "Skipped:Size<=0";
            return existing;
        }

        if (graphicsDevice == null)
        {
            _textureStatuses[statusKey] = $"Failed:GraphicsDevice=null;Size={FormatSize(description.Size)};Format={description.Description.Format};Flags={description.Description.Flags}";
            return existing;
        }

        if (existing != null && !recreate && Matches(existing, description))
        {
            _textureStatuses[statusKey] = "AlreadyCreated";
            return existing;
        }

        try
        {
            existing?.Dispose();
            var texture = Texture.New(
                graphicsDevice,
                description.Description,
                description.ViewDescription,
                Array.Empty<DataBox>());
            _textureStatuses[statusKey] = texture != null
                ? $"Created:Size={FormatSize(description.Size)};Format={description.Description.Format};Flags={description.Description.Flags}"
                : "Failed:Texture.New returned null";
            return texture;
        }
        catch (Exception ex)
        {
            _textureStatuses[statusKey] =
                $"Exception:{ex.GetType().Name}:{ex.Message};Size={FormatSize(description.Size)};Format={description.Description.Format};DescFlags={description.Description.Flags};ViewFlags={description.ViewDescription.Flags}";
            return null;
        }
    }

    private void RecordTextureDescriptionFailure(
        string key,
        string slot,
        Exception exception,
        PixelFormat format,
        bool unorderedAccess)
    {
        _textureStatuses[TextureStatusKey(key, slot)] =
            $"Failed:Description:{exception.GetType().Name}:{exception.Message};Format={format};UnorderedAccess={unorderedAccess}";
    }

    private static void Swap<T>(Dictionary<string, T> a, Dictionary<string, T> b, string key)
    {
        var aHasValue = a.Remove(key, out var aValue);
        var bHasValue = b.Remove(key, out var bValue);

        if (bHasValue)
            a[key] = bValue;
        if (aHasValue)
            b[key] = aValue;
    }

    private static PixelFormat GetPixelFormat(IAttribute attribute)
    {
        try
        {
            return attribute?.ShaderNode == null
                ? PixelFormat.R32G32B32A32_Float
                : TypeHelpers.GetPixelFormat(attribute.ShaderNode);
        }
        catch (NotImplementedException)
        {
            return PixelFormat.R32G32B32A32_Float;
        }
    }

    private static bool Writes(IAttribute attribute)
    {
        return attribute?.ShaderNode?.WriteCounter > 0;
    }

    private static AbstractShaderNode CreateTextureIndex(NodeContext nodeContext, Int3 size)
    {
        var dispatchThreadId = new DispatchThreadId(nodeContext);
        return TypeHelpers.GetDimensionFromInt3(size) switch
        {
            1 => new DispatchThreadIdX(nodeContext),
            2 => new GetMember<Int3, Int2>(nodeContext, dispatchThreadId, "xy"),
            _ => dispatchThreadId
        };
    }

    private static AbstractShaderNode CreateTextureReadCall(
        NodeSubContextFactory subContextFactory,
        TextureInput texture,
        AbstractShaderNode index,
        AbstractShaderNode value,
        NodeContext nodeContext)
    {
        if (texture == null || index == null || value == null)
            return texture;

        var getType = typeof(ComputeTextureGet<,>).MakeGenericType(
            GetShaderNodeValueType(index),
            GetShaderNodeValueType(value));
        return Activator.CreateInstance(
            getType,
            NextSubContextOrNull(subContextFactory, nodeContext),
            texture,
            index,
            null) as AbstractShaderNode;
    }

    private static ShaderNode<GpuVoid> CreateTextureWriteCall(
        NodeSubContextFactory subContextFactory,
        TextureInput texture,
        AbstractShaderNode index,
        AbstractShaderNode value,
        NodeContext nodeContext)
    {
        if (texture == null || index == null || value == null)
            return new EmptyVoid(nodeContext);

        var setType = typeof(ComputeTextureSet<,>).MakeGenericType(
            GetShaderNodeValueType(index),
            GetShaderNodeValueType(value));
        return Activator.CreateInstance(
            setType,
            NextSubContextOrNull(subContextFactory, nodeContext),
            texture,
            index,
            value) as ShaderNode<GpuVoid> ?? new EmptyVoid(nodeContext);
    }

    private static NodeContext NextSubContextOrNull(NodeSubContextFactory subContextFactory, NodeContext nodeContext)
    {
        return nodeContext == null ? null : subContextFactory.NextSubContext();
    }

    private static Type GetShaderNodeValueType(AbstractShaderNode node)
    {
        for (var type = node?.GetType(); type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ShaderNode<>))
                return type.GetGenericArguments()[0];
        }

        throw new InvalidOperationException($"Could not resolve ShaderNode value type for {node?.GetType().FullName ?? "<null>"}.");
    }

    private static bool Matches(Texture texture, ComputeTextureResourceDescription description)
    {
        return texture.Width == description.Description.Width
               && texture.Height == description.Description.Height
               && texture.Depth == description.Description.Depth
               && texture.ArraySize == description.Description.ArraySize
               && texture.Format == description.Description.Format
               && texture.Dimension == description.Description.Dimension
               && texture.ViewFlags == description.ViewDescription.Flags;
    }

    private static string FormatSize(Int3 size)
    {
        return $"{size.X}x{size.Y}x{size.Z}";
    }

    private static string TextureStatusKey(string key, string slot)
    {
        return $"{key}:{slot}";
    }

    private static bool IsUsedTextureKey(string key, HashSet<string> usedKeys)
    {
        var separator = key.IndexOf(':');
        var attributeKey = separator < 0 ? key : key[..separator];
        return usedKeys.Contains(attributeKey);
    }

    private static Int3 EnsureOne(Int3 size)
    {
        return new Int3(
            global::System.Math.Max(1, size.X),
            global::System.Math.Max(1, size.Y),
            global::System.Math.Max(1, size.Z));
    }
}

public sealed class TextureSwap : ShaderNode<GpuVoid>, IComputeVoid
{
    private readonly TextureResource _textureGroup;
    private readonly IAttribute _attribute;

    public TextureSwap(NodeContext nodeContext, TextureResource textureGroup, IAttribute attribute)
        : base(nodeContext, "TextureSwap")
    {
        _textureGroup = textureGroup;
        _attribute = attribute;
        SetInputs(Array.Empty<AbstractShaderNode>());
    }

    public TextureResource TextureGroup => _textureGroup;

    public IAttribute Attribute => _attribute;

    public TextureSwap Execute()
    {
        _textureGroup?.SwapTextures(_attribute?.Name);
        return this;
    }

    protected override string SourceTemplate()
    {
        return "";
    }
}
