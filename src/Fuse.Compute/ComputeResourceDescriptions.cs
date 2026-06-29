using System;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Stride.Graphics;

namespace Fuse.compute;

public sealed record ComputeBufferResourceDescription(
    BufferDescription Description,
    BufferViewDescription ViewDescription,
    int ElementCount,
    int ElementSizeInBytes)
{
    public int SizeInBytes => Description.SizeInBytes;
}

public sealed record ComputeTextureResourceDescription(
    TextureDescription Description,
    TextureViewDescription ViewDescription)
{
    public Int3 Size => new(Description.Width, Description.Height, Description.Depth);
}

public readonly record struct ComputeResourceLimits(
    int MaxBufferElementCount,
    int MaxTexture1DDimension,
    int MaxTexture2DDimension,
    int MaxTexture3DDimension,
    int MaxStructuredBufferStride)
{
    public static ComputeResourceLimits D3D11 { get; } = new(
        MaxBufferElementCount: 1 << 27,
        MaxTexture1DDimension: 16_384,
        MaxTexture2DDimension: 16_384,
        MaxTexture3DDimension: 2_048,
        MaxStructuredBufferStride: 2_048);
}

public static class ComputeResourceDescriptions
{
    public static ComputeBufferResourceDescription StructuredBuffer(
        long elementCount,
        long elementSizeInBytes,
        bool unorderedAccess = true,
        GraphicsResourceUsage usage = GraphicsResourceUsage.Default,
        ComputeResourceLimits? limits = null)
    {
        var actualLimits = limits ?? ComputeResourceLimits.D3D11;
        var checkedElementCount = CheckedInt(elementCount, nameof(elementCount));
        var checkedElementSize = CheckedInt(elementSizeInBytes, nameof(elementSizeInBytes));
        if (checkedElementCount > actualLimits.MaxBufferElementCount)
            throw new ArgumentOutOfRangeException(
                nameof(elementCount),
                elementCount,
                $"Structured buffer element count must not exceed {actualLimits.MaxBufferElementCount}.");
        if (checkedElementSize > actualLimits.MaxStructuredBufferStride)
            throw new ArgumentOutOfRangeException(
                nameof(elementSizeInBytes),
                elementSizeInBytes,
                $"Structured buffer stride must not exceed {actualLimits.MaxStructuredBufferStride} bytes.");

        var sizeInBytes = CheckedInt(checkedElementCount * (long)checkedElementSize, "sizeInBytes");

        var flags = BufferFlags.ShaderResource | BufferFlags.StructuredBuffer;
        if (unorderedAccess)
            flags |= BufferFlags.UnorderedAccess;

        var description = new BufferDescription(sizeInBytes, flags, usage)
        {
            StructureByteStride = checkedElementSize
        };

        var viewDescription = new BufferViewDescription
        {
            Flags = flags,
            Format = PixelFormat.None
        };

        return new ComputeBufferResourceDescription(
            description,
            viewDescription,
            checkedElementCount,
            checkedElementSize);
    }

    public static ComputeTextureResourceDescription Texture(
        Int3 size,
        PixelFormat format,
        bool unorderedAccess = true,
        GraphicsResourceUsage usage = GraphicsResourceUsage.Default,
        int mipLevels = 1,
        ComputeResourceLimits? limits = null)
    {
        if (format == PixelFormat.None)
            throw new ArgumentException("Texture resources require a concrete pixel format.", nameof(format));
        if (unorderedAccess && !IsSupportedTextureUnorderedAccessFormat(format))
            throw new ArgumentException(
                $"Texture format {format} is not supported for typed unordered access texture resources.",
                nameof(format));

        var checkedSize = EnsureOne(size);
        ValidateTextureDimensions(checkedSize, limits ?? ComputeResourceLimits.D3D11);
        var flags = TextureFlags.ShaderResource;
        if (unorderedAccess)
            flags |= TextureFlags.UnorderedAccess;

        var description = new TextureDescription
        {
            Width = checkedSize.X,
            Height = checkedSize.Y,
            Depth = checkedSize.Z,
            ArraySize = 1,
            MipLevels = global::System.Math.Max(1, mipLevels),
            MultisampleCount = MultisampleCount.None,
            Format = format,
            Dimension = GetTextureDimension(checkedSize),
            Usage = usage,
            Flags = flags,
            Options = TextureOptions.None
        };

        var viewDescription = new TextureViewDescription
        {
            Flags = flags,
            Format = PixelFormat.None,
            Type = ViewType.Full
        };

        return new ComputeTextureResourceDescription(description, viewDescription);
    }

    public static TextureDimension GetTextureDimension(Int3 size)
    {
        if (size.Z > 1)
            return TextureDimension.Texture3D;
        if (size.Y > 1)
            return TextureDimension.Texture2D;

        return TextureDimension.Texture1D;
    }

    private static void ValidateTextureDimensions(Int3 size, ComputeResourceLimits limits)
    {
        var dimension = GetTextureDimension(size);
        switch (dimension)
        {
            case TextureDimension.Texture1D:
                ThrowIfTextureDimensionExceeded(size.X, limits.MaxTexture1DDimension, "X");
                break;
            case TextureDimension.Texture2D:
                ThrowIfTextureDimensionExceeded(size.X, limits.MaxTexture2DDimension, "X");
                ThrowIfTextureDimensionExceeded(size.Y, limits.MaxTexture2DDimension, "Y");
                break;
            case TextureDimension.Texture3D:
                ThrowIfTextureDimensionExceeded(size.X, limits.MaxTexture3DDimension, "X");
                ThrowIfTextureDimensionExceeded(size.Y, limits.MaxTexture3DDimension, "Y");
                ThrowIfTextureDimensionExceeded(size.Z, limits.MaxTexture3DDimension, "Z");
                break;
        }
    }

    private static void ThrowIfTextureDimensionExceeded(int value, int limit, string dimension)
    {
        if (value <= limit)
            return;

        throw new ArgumentOutOfRangeException(
            nameof(value),
            value,
            $"Texture dimension {dimension} must not exceed {limit}.");
    }

    public static bool IsSupportedTextureUnorderedAccessFormat(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R32_Float
                or PixelFormat.R32_UInt
                or PixelFormat.R32_SInt
                or PixelFormat.R32G32_Float
                or PixelFormat.R32G32_UInt
                or PixelFormat.R32G32_SInt
                or PixelFormat.R32G32B32A32_Float
                or PixelFormat.R32G32B32A32_UInt
                or PixelFormat.R32G32B32A32_SInt
                or PixelFormat.R16_Float
                or PixelFormat.R16_UInt
                or PixelFormat.R16_SInt => true,
            _ => false
        };
    }

    private static Int3 EnsureOne(Int3 size)
    {
        return new Int3(
            global::System.Math.Max(1, size.X),
            global::System.Math.Max(1, size.Y),
            global::System.Math.Max(1, size.Z));
    }

    private static int CheckedInt(long value, string name)
    {
        if (value < 0 || value > int.MaxValue)
            throw new OverflowException($"{name} {value} does not fit into Int32.");

        return (int)value;
    }
}
