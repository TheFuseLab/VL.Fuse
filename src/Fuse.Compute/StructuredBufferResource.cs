using System;
using System.Collections.Generic;
using System.Linq;
using Fuse;
using Fuse.ComputeSystem;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core.Import;
using VL.Model;
using VL.Stride.Graphics;
using StrideBuffer = Stride.Graphics.Buffer;

namespace Fuse.compute;

[ProcessNode(Name = "StructuredBufferResource", Category = "Fuse.Compute", FragmentSelection = FragmentSelection.Explicit)]
public sealed partial class StructuredBufferResource : IStructuredBufferResourceInfo, IComputeResourceProvider
{
    public const long DefaultElementCount = 1_024;
    public const long DefaultThreadGroupSize = 256;

    private readonly ComputeDispatchLimits _limits;

    [Fragment(Order = 0)]
    public StructuredBufferResource(
        string name = null,
        long elementCount = DefaultElementCount,
        long threadGroupSize = DefaultThreadGroupSize,
        long structSize = 0,
        ComputeDispatchLimits? limits = null)
    {
        _limits = limits ?? ComputeDispatchLimits.D3D11;
        Name = name;
        ElementCount = elementCount;
        ThreadGroupSize = threadGroupSize;
        StructSize = structSize;
        DispatchInfo = CreateDispatchInfo();
    }

    public string Name { get; private set; }

    public AttributeType AttributeType => AttributeType.StructuredBuffer;

    public long ElementCount { get; private set; }

    public long ThreadGroupSize { get; private set; }

    public long StructSize { get; private set; }

    public AttributeMap AttributeMap { get; private set; }

    public StructuredBufferStructDescription StructDescription { get; private set; }

    public GpuStruct StructInstance { get; private set; }

    public IDispatchInfo DispatchInfo { get; private set; }

    public bool ChangedAttributes { get; private set; }

    public int Ticket { get; private set; }

    public StrideBuffer Buffer { get; private set; }

    public ComputeBufferResourceDescription LastBufferDescription { get; private set; }

    public StructuredBufferResourceBindings LastBindings { get; internal set; }

    public string LastBufferStatus { get; private set; } = "Uninitialized";

    [Fragment(Order = 100)]
    public StructuredBufferResource Output => this;

    public static StructuredBufferResource Create(
        string name = null,
        long elementCount = DefaultElementCount,
        long threadGroupSize = DefaultThreadGroupSize,
        long structSize = 0,
        ComputeDispatchLimits? limits = null)
    {
        return new StructuredBufferResource(name, elementCount, threadGroupSize, structSize, limits);
    }

    [Fragment(Order = 10)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource Update()
    {
        DispatchInfo = CreateDispatchInfo();
        return this;
    }

    [Fragment(Order = 20)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource Prepare()
    {
        EnsureAttributeMap().Prepare();
        ChangedAttributes = false;
        return this;
    }

    [Fragment(Order = 21)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource HandleAttribute(IAttribute attribute)
    {
        EnsureAttributeMap().HandleAttribute(attribute);
        return this;
    }

    [Fragment(Order = 22)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource FinishAttributeMap(
        bool applyPadding = false,
        bool syncAttributes = true)
    {
        var changed = EnsureAttributeMap().Finish(applyPadding, syncAttributes);
        ChangedAttributes = changed;
        if (changed)
            Ticket++;

        return this;
    }

    [Fragment(Order = 25)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource Finish(
        bool applyPadding = false,
        bool syncAttributes = true)
    {
        return FinishAttributeMap(applyPadding, syncAttributes);
    }

    [Fragment(Order = 30)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource UpdateStruct(
        bool usePaddedStructSize = false,
        Func<IAttribute, string> getGpuType = null,
        Func<IAttribute, int> getSizeInBytes = null,
        Func<IAttribute, bool> isArray = null,
        Func<IAttribute, int> getArrayCount = null)
    {
        if (AttributeMap == null)
            return this;

        StructDescription = StructuredBufferStructDescription.FromAttributeMap(
            Name,
            AttributeMap,
            getGpuType,
            getSizeInBytes,
            isArray,
            getArrayCount);
        StructInstance = new GpuStruct(StructDescription.Name);
        if (usePaddedStructSize)
            UpdateStructSizeFromAttributeMap(true);
        else
            StructSize = StructDescription?.Stride ?? AttributeMap.GetStructSize();

        return this;
    }

    [Fragment(Order = 40)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource SetName(string name)
    {
        Name = name;
        return this;
    }

    [Fragment(Order = 50)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource SetElementCount(long elementCount)
    {
        ElementCount = elementCount;
        return Update();
    }

    [Fragment(Order = 60)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource SetDispatchGroupSize(long threadGroupSize)
    {
        ThreadGroupSize = threadGroupSize;
        return Update();
    }

    [Fragment(Order = 70)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource SetStructSize(long structSize)
    {
        StructSize = structSize;
        return this;
    }

    [Fragment(Order = 80)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource SetAttributeMap(
        AttributeMap attributeMap,
        bool usePaddedStructSize = false,
        Func<IAttribute, string> getGpuType = null,
        Func<IAttribute, int> getSizeInBytes = null,
        Func<IAttribute, bool> isArray = null,
        Func<IAttribute, int> getArrayCount = null)
    {
        AttributeMap = attributeMap;
        return UpdateStruct(
            usePaddedStructSize,
            getGpuType,
            getSizeInBytes,
            isArray,
            getArrayCount);
    }

    public AttributeMap GetAttributeMap()
    {
        return AttributeMap;
    }

    public StructuredBufferResource UpdateStructSizeFromAttributeMap(bool usePaddedStructSize = false)
    {
        if (AttributeMap == null)
            return this;

        StructSize = usePaddedStructSize
            ? AttributeMap.GetPaddedStructSize()
            : AttributeMap.GetStructSize();

        return this;
    }

    public StructuredBufferResource SetStructDescription(StructuredBufferStructDescription structDescription)
    {
        StructDescription = structDescription;
        StructSize = structDescription?.Stride ?? 0;
        return this;
    }

    public StructuredBufferStructDescription GetStructDescription()
    {
        return StructDescription;
    }

    public StructuredBufferStructDescription GetStruct()
    {
        return StructDescription;
    }

    public GpuStruct GetStructInstance()
    {
        return StructInstance ??= new GpuStruct(StructuredBufferStructDescription.SanitizeStructName(Name));
    }

    public long GetStride()
    {
        return StructSize;
    }

    public BufferInput<GpuStruct> GetBufferInput()
    {
        return LastBindings?.BufferInput;
    }

    public IReadOnlyList<string> GetSemantics()
    {
        return StructDescription?.Members
                   .Where(member => member.Name != PaddingAttribute.DefaultName)
                   .Select(member => member.Name.ToUpperInvariant())
                   .ToArray()
               ?? Array.Empty<string>();
    }

    public IReadOnlyList<string> GetVertexDeclaration()
    {
        return StructDescription?.BuildMemberDescriptions() ?? Array.Empty<string>();
    }

    public string GetName()
    {
        return Name;
    }

    public AttributeType GetAttributeType()
    {
        return AttributeType;
    }

    public int GetTicket()
    {
        return Ticket;
    }

    public long GetElementCount()
    {
        return ElementCount;
    }

    public Int3 GetSize()
    {
        return new Int3(CheckedInt(ElementCount), 1, 1);
    }

    public long GetStructSize()
    {
        return StructSize;
    }

    public long GetResourceSize()
    {
        try
        {
            return checked(ElementCount * StructSize);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    public IDispatchInfo GetDispatchInfo()
    {
        return DispatchInfo;
    }

    public ComputeResource GetComputeResource()
    {
        return new ComputeResource(AttributeType.StructuredBuffer, Name, GetSize());
    }

    public ComputeBufferResourceDescription GetBufferDescription(bool unorderedAccess = true)
    {
        return ComputeResourceDescriptions.StructuredBuffer(
            ElementCount,
            StructSize,
            unorderedAccess);
    }

    [Fragment(Order = 120)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource UpdateBuffer(
        GraphicsDevice graphicsDevice = null,
        long? elementCount = null,
        bool recreate = false,
        bool unorderedAccess = true)
    {
        if (elementCount.HasValue)
            SetElementCount(elementCount.Value);

        try
        {
            LastBufferDescription = GetBufferDescription(unorderedAccess);
        }
        catch (Exception ex)
        {
            LastBufferDescription = null;
            LastBufferStatus =
                $"Failed:Description:{ex.GetType().Name}:{ex.Message};ElementCount={ElementCount};Stride={StructSize};UnorderedAccess={unorderedAccess}";
            return this;
        }

        if (LastBufferDescription.SizeInBytes <= 0 || LastBufferDescription.ElementSizeInBytes <= 0)
        {
            LastBufferStatus = "Skipped:SizeInBytes<=0";
            return this;
        }

        if (graphicsDevice == null)
        {
            LastBufferStatus = "Failed:GraphicsDevice=null";
            return this;
        }

        if (Buffer != null && !recreate && Matches(Buffer, LastBufferDescription))
        {
            LastBufferStatus = "AlreadyCreated";
            return this;
        }

        try
        {
            Buffer?.Dispose();
            Buffer = BufferExtensions.New(
                graphicsDevice,
                LastBufferDescription.Description,
                LastBufferDescription.ViewDescription,
                IntPtr.Zero);
            LastBufferStatus = Buffer != null
                ? $"Created:Size={LastBufferDescription.SizeInBytes},Stride={LastBufferDescription.ElementSizeInBytes},Flags={LastBufferDescription.Description.BufferFlags}"
                : "Failed:BufferExtensions.New returned null";
        }
        catch (Exception ex)
        {
            Buffer = null;
            LastBufferStatus =
                $"Exception:{ex.GetType().Name}:{ex.Message};Size={LastBufferDescription.SizeInBytes};Stride={LastBufferDescription.ElementSizeInBytes};DescFlags={LastBufferDescription.Description.BufferFlags};ViewFlags={LastBufferDescription.ViewDescription.Flags};ViewFormat={LastBufferDescription.ViewDescription.Format}";
        }

        return this;
    }

    public StrideBuffer GetBuffer()
    {
        return Buffer;
    }

    public StructuredBufferResource DisposeBuffer()
    {
        Buffer?.Dispose();
        Buffer = null;
        LastBufferStatus = "Disposed";
        return this;
    }

    [Fragment(Order = 122)]
    [return: Pin(Visibility = PinVisibility.Hidden)]
    public StructuredBufferResource Reset(bool condition = true)
    {
        if (!condition)
            return this;

        DisposeBuffer();
        LastBufferDescription = null;
        LastBindings = null;
        LastBufferStatus = "Reset";
        Ticket++;
        return this;
    }

    private Buffer1DDispatchInfo CreateDispatchInfo()
    {
        return Buffer1DDispatchInfo.Create(ElementCount, ThreadGroupSize, _limits);
    }

    private AttributeMap EnsureAttributeMap()
    {
        return AttributeMap ??= new AttributeMap(AttributeType.StructuredBuffer);
    }

    private static int CheckedInt(long value)
    {
        if (value < int.MinValue || value > int.MaxValue)
            throw new OverflowException($"Structured buffer size dimension {value} does not fit into Int32.");

        return (int)value;
    }

    private static bool Matches(StrideBuffer buffer, ComputeBufferResourceDescription description)
    {
        return buffer.Description.SizeInBytes == description.Description.SizeInBytes
               && buffer.Description.StructureByteStride == description.Description.StructureByteStride
               && buffer.Description.BufferFlags == description.Description.BufferFlags
               && buffer.ViewFlags == description.ViewDescription.Flags
               && buffer.ViewFormat == description.ViewDescription.Format;
    }
}
