using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fuse.ComputeSystem;

namespace Fuse.compute;

public sealed record StructuredBufferStructMember
{
    public StructuredBufferStructMember(
        string name,
        string gpuType,
        int sizeInBytes,
        bool isArray = false,
        int arrayCount = 1)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Structured buffer member name must not be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(gpuType))
            throw new ArgumentException("Structured buffer member GPU type must not be empty.", nameof(gpuType));
        if (sizeInBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeInBytes), sizeInBytes, "Structured buffer member size must be greater than zero.");
        if (arrayCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(arrayCount), arrayCount, "Structured buffer member array count must be greater than zero.");

        Name = name;
        GpuType = gpuType;
        SizeInBytes = sizeInBytes;
        IsArray = isArray;
        ArrayCount = arrayCount;
    }

    public string Name { get; }

    public string GpuType { get; }

    public int SizeInBytes { get; }

    public bool IsArray { get; }

    public int ArrayCount { get; }

    public string Declaration => IsArray
        ? $"{GpuType} {Name}[{ArrayCount}];"
        : $"{GpuType} {Name};";

    public int TotalSizeInBytes => IsArray
        ? checked(SizeInBytes * ArrayCount)
        : SizeInBytes;
}

public sealed class StructuredBufferStructDescription
{
    public StructuredBufferStructDescription(string name, IReadOnlyList<StructuredBufferStructMember> members)
    {
        Name = SanitizeStructName(name);
        Members = members;
        Stride = members.Sum(member => member.TotalSizeInBytes);
    }

    public string Name { get; }

    public IReadOnlyList<StructuredBufferStructMember> Members { get; }

    public int Stride { get; }

    public string BuildStructSource()
    {
        var builder = new StringBuilder();
        builder.Append("    struct ");
        builder.Append(Name);
        builder.AppendLine("{");

        foreach (var member in Members)
        {
            builder.Append("        ");
            builder.AppendLine(member.Declaration);
        }

        builder.Append("    };");
        return builder.ToString();
    }

    public IReadOnlyList<string> BuildMemberDescriptions()
    {
        return Members.Select(member => $"{Name}.{member.Declaration}").ToArray();
    }

    public static StructuredBufferStructDescription FromAttributeMap(
        string name,
        AttributeMap attributeMap,
        Func<IAttribute, string> getGpuType = null,
        Func<IAttribute, int> getSizeInBytes = null,
        Func<IAttribute, bool> isArray = null,
        Func<IAttribute, int> getArrayCount = null)
    {
        if (attributeMap == null)
            return new StructuredBufferStructDescription(name, Array.Empty<StructuredBufferStructMember>());

        var gpuTypeResolver = getGpuType ?? GetGpuType;
        var sizeResolver = getSizeInBytes ?? GetSizeInBytes;
        var arrayResolver = isArray ?? IsArray;
        var arrayCountResolver = getArrayCount ?? GetArrayCount;

        var members = attributeMap.AttributeSet.Values
            .Select(attribute => new StructuredBufferStructMember(
                attribute.Name,
                gpuTypeResolver(attribute),
                sizeResolver(attribute),
                arrayResolver(attribute),
                arrayCountResolver(attribute)))
            .ToArray();

        return new StructuredBufferStructDescription(name, members);
    }

    public static string SanitizeStructName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == "struct")
            return "GpuStruct";

        return name;
    }

    private static string GetGpuType(IAttribute attribute)
    {
        if (attribute is IAttributeLayout layout)
            return layout.GpuType;

        return TypeHelpers.GetGpuType(attribute.ShaderNode);
    }

    private static int GetSizeInBytes(IAttribute attribute)
    {
        if (attribute is IAttributeLayout layout)
            return layout.SizeInBytes;

        return TypeHelpers.GetSizeInBytes(attribute.ShaderNode);
    }

    private static bool IsArray(IAttribute attribute)
    {
        if (attribute is IAttributeLayout layout)
            return layout.IsArray;

        return attribute?.ShaderNode != null && TypeHelpers.IsGpuArray(attribute.ShaderNode);
    }

    private static int GetArrayCount(IAttribute attribute)
    {
        if (attribute is IAttributeLayout layout)
            return System.Math.Max(1, layout.ArrayCount);

        return attribute is IStructureBufferAttribute structuredBufferAttribute
            ? System.Math.Max(1, structuredBufferAttribute.ArrayCount)
            : 1;
    }
}
