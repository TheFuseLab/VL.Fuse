using System;
using Fuse.compute;
using NUnit.Framework;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class ComputeResourceDescriptionTests
{
    [Test]
    public void StructuredBuffer_CreatesComputeCompatibleBufferAndViewDescriptions()
    {
        var description = ComputeResourceDescriptions.StructuredBuffer(
            elementCount: 1024,
            elementSizeInBytes: 16);

        Assert.That(description.Description.SizeInBytes, Is.EqualTo(16_384));
        Assert.That(description.Description.StructureByteStride, Is.EqualTo(16));
        Assert.That(HasFlag(description.Description.BufferFlags, BufferFlags.ShaderResource), Is.True);
        Assert.That(HasFlag(description.Description.BufferFlags, BufferFlags.StructuredBuffer), Is.True);
        Assert.That(HasFlag(description.Description.BufferFlags, BufferFlags.UnorderedAccess), Is.True);
        Assert.That(description.Description.Usage, Is.EqualTo(GraphicsResourceUsage.Default));
        Assert.That(description.ViewDescription.Format, Is.EqualTo(PixelFormat.None));
        Assert.That(HasFlag(description.ViewDescription.Flags, BufferFlags.StructuredBuffer), Is.True);
        Assert.That(HasFlag(description.ViewDescription.Flags, BufferFlags.UnorderedAccess), Is.True);
    }

    [Test]
    public void StructuredBufferResource_ExposesComputeCompatibleBufferDescription()
    {
        var resource = StructuredBufferResource
            .Create("Particles", elementCount: 256)
            .SetStructSize(32);

        var description = resource.GetBufferDescription();

        Assert.That(description.ElementCount, Is.EqualTo(256));
        Assert.That(description.ElementSizeInBytes, Is.EqualTo(32));
        Assert.That(description.SizeInBytes, Is.EqualTo(8192));
        Assert.That(description.Description.StructureByteStride, Is.EqualTo(32));
        Assert.That(HasFlag(description.Description.BufferFlags, BufferFlags.UnorderedAccess), Is.True);
    }

    [Test]
    public void Texture_CreatesComputeCompatible3DTextureAndViewDescriptions()
    {
        var description = ComputeResourceDescriptions.Texture(
            new Int3(32, 16, 8),
            PixelFormat.R32G32B32A32_Float);

        Assert.That(description.Description.Dimension, Is.EqualTo(TextureDimension.Texture3D));
        Assert.That(description.Description.Width, Is.EqualTo(32));
        Assert.That(description.Description.Height, Is.EqualTo(16));
        Assert.That(description.Description.Depth, Is.EqualTo(8));
        Assert.That(description.Description.Format, Is.EqualTo(PixelFormat.R32G32B32A32_Float));
        Assert.That(HasFlag(description.Description.Flags, TextureFlags.ShaderResource), Is.True);
        Assert.That(HasFlag(description.Description.Flags, TextureFlags.UnorderedAccess), Is.True);
        Assert.That(description.Description.Usage, Is.EqualTo(GraphicsResourceUsage.Default));
        Assert.That(description.ViewDescription.Format, Is.EqualTo(PixelFormat.None));
        Assert.That(description.ViewDescription.Type, Is.EqualTo(ViewType.Full));
        Assert.That(HasFlag(description.ViewDescription.Flags, TextureFlags.UnorderedAccess), Is.True);
    }

    [Test]
    public void Texture_CanCreateShaderResourceOnlyDescription()
    {
        var description = ComputeResourceDescriptions.Texture(
            new Int3(16, 8, 1),
            PixelFormat.R32_Float,
            unorderedAccess: false);

        Assert.That(HasFlag(description.Description.Flags, TextureFlags.ShaderResource), Is.True);
        Assert.That(HasFlag(description.Description.Flags, TextureFlags.UnorderedAccess), Is.False);
        Assert.That(HasFlag(description.ViewDescription.Flags, TextureFlags.ShaderResource), Is.True);
        Assert.That(HasFlag(description.ViewDescription.Flags, TextureFlags.UnorderedAccess), Is.False);
    }

    [Test]
    public void Texture_RejectsMissingPixelFormat()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ComputeResourceDescriptions.Texture(new Int3(16, 8, 1), PixelFormat.None));

        Assert.That(exception.ParamName, Is.EqualTo("format"));
        Assert.That(exception.Message, Does.Contain("concrete pixel format"));
    }

    [Test]
    public void Texture_RejectsUnsupportedTypedUnorderedAccessFormat()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ComputeResourceDescriptions.Texture(new Int3(16, 8, 1), PixelFormat.R32G32B32_Float));

        Assert.That(exception.ParamName, Is.EqualTo("format"));
        Assert.That(exception.Message, Does.Contain("not supported for typed unordered access"));
    }

    [Test]
    public void StructuredBuffer_RejectsStrideAboveD3D11StructuredBufferLimit()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ComputeResourceDescriptions.StructuredBuffer(
                elementCount: 16,
                elementSizeInBytes: ComputeResourceLimits.D3D11.MaxStructuredBufferStride + 1));

        Assert.That(exception.ParamName, Is.EqualTo("elementSizeInBytes"));
        Assert.That(exception.Message, Does.Contain("Structured buffer stride"));
        Assert.That(exception.Message, Does.Contain("2048"));
    }

    [Test]
    public void StructuredBuffer_RejectsElementCountAboveD3D11BufferResourceLimit()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ComputeResourceDescriptions.StructuredBuffer(
                elementCount: ComputeResourceLimits.D3D11.MaxBufferElementCount + 1L,
                elementSizeInBytes: 16));

        Assert.That(exception.ParamName, Is.EqualTo("elementCount"));
        Assert.That(exception.Message, Does.Contain("Structured buffer element count"));
        Assert.That(exception.Message, Does.Contain("134217728"));
    }

    [Test]
    public void StructuredBuffer_RejectsSizeInBytesAboveInt32()
    {
        var exception = Assert.Throws<OverflowException>(() =>
            ComputeResourceDescriptions.StructuredBuffer(
                elementCount: int.MaxValue / 16L + 1L,
                elementSizeInBytes: 16));

        Assert.That(exception.Message, Does.Contain("sizeInBytes"));
    }

    [TestCase(16_385, 1, 1, "X")]
    [TestCase(16_385, 16, 1, "X")]
    [TestCase(16, 16_385, 1, "Y")]
    [TestCase(2_049, 16, 2, "X")]
    [TestCase(16, 2_049, 2, "Y")]
    [TestCase(16, 16, 2_049, "Z")]
    public void Texture_RejectsDimensionsAboveD3D11Limits(
        int x,
        int y,
        int z,
        string dimension)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ComputeResourceDescriptions.Texture(new Int3(x, y, z), PixelFormat.R32_Float));

        Assert.That(exception.Message, Does.Contain($"Texture dimension {dimension}"));
    }

    [Test]
    public void Texture_AllowsUnsupportedTypedUnorderedAccessFormatForShaderResourceOnly()
    {
        var description = ComputeResourceDescriptions.Texture(
            new Int3(16, 8, 1),
            PixelFormat.R32G32B32_Float,
            unorderedAccess: false);

        Assert.That(description.Description.Format, Is.EqualTo(PixelFormat.R32G32B32_Float));
        Assert.That(HasFlag(description.Description.Flags, TextureFlags.ShaderResource), Is.True);
        Assert.That(HasFlag(description.Description.Flags, TextureFlags.UnorderedAccess), Is.False);
    }

    [TestCase(PixelFormat.R32_Float, true)]
    [TestCase(PixelFormat.R32G32_Float, true)]
    [TestCase(PixelFormat.R32G32B32_Float, false)]
    [TestCase(PixelFormat.R32G32B32A32_Float, true)]
    [TestCase(PixelFormat.R1_UNorm, false)]
    public void IsSupportedTextureUnorderedAccessFormat_ReportsTypedUavCompatibility(
        PixelFormat format,
        bool expected)
    {
        Assert.That(
            ComputeResourceDescriptions.IsSupportedTextureUnorderedAccessFormat(format),
            Is.EqualTo(expected));
    }

    [Test]
    public void Texture_DimensionFallsBackTo1D2D3DBySize()
    {
        Assert.That(
            ComputeResourceDescriptions.Texture(new Int3(64, 1, 1), PixelFormat.R32_Float).Description.Dimension,
            Is.EqualTo(TextureDimension.Texture1D));
        Assert.That(
            ComputeResourceDescriptions.Texture(new Int3(64, 16, 1), PixelFormat.R32_Float).Description.Dimension,
            Is.EqualTo(TextureDimension.Texture2D));
        Assert.That(
            ComputeResourceDescriptions.Texture(new Int3(64, 16, 4), PixelFormat.R32_Float).Description.Dimension,
            Is.EqualTo(TextureDimension.Texture3D));
    }

    [Test]
    public void TextureResource_ExposesComputeCompatibleTextureDescription()
    {
        var resource = TextureResource.Create("Volume", new Int3(8, 4, 2));

        var description = resource.GetTextureDescription(PixelFormat.R32_Float);

        Assert.That(description.Size, Is.EqualTo(new Int3(8, 4, 2)));
        Assert.That(description.Description.Format, Is.EqualTo(PixelFormat.R32_Float));
        Assert.That(description.Description.Dimension, Is.EqualTo(TextureDimension.Texture3D));
        Assert.That(HasFlag(description.Description.Flags, TextureFlags.UnorderedAccess), Is.True);
    }

    [Test]
    public void TextureResource_RejectsMissingPixelFormat()
    {
        var resource = TextureResource.Create("Volume", new Int3(8, 4, 2));

        var exception = Assert.Throws<ArgumentException>(() =>
            resource.GetTextureDescription(PixelFormat.None));

        Assert.That(exception.ParamName, Is.EqualTo("format"));
    }

    private static bool HasFlag(BufferFlags value, BufferFlags flag)
    {
        return (value & flag) == flag;
    }

    private static bool HasFlag(TextureFlags value, TextureFlags flag)
    {
        return (value & flag) == flag;
    }
}
