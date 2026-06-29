using System.Collections.Generic;
using System.Linq;
using Fuse.ComputeSystem;
using Stride.Core.Mathematics;

namespace Fuse.compute;

public class ComputeResource
{
    public ComputeResource(AttributeType attributeType, /*string group, */string resource, Int3 size)
    {
        AttributeType = attributeType;
        Resource = resource;
        Size = size;
    }

    public AttributeType AttributeType { get; }

    //  public string Group { get;  }
    public string Resource { get; }

    public Int3 Size { get; }
/*
    public bool SameTarget(ComputeResource theOverride)
    {
        return AttributeType == theOverride.AttributeType && Group == theOverride.Group;
    }*/

    public override bool Equals(object obj)
    {
        if (obj is ComputeResource otherOverride)
            return GetMergeKey(this).Equals(GetMergeKey(otherOverride));
        return false;
    }

    public override int GetHashCode()
    {
        return GetMergeKey(this).GetHashCode();
    }

    public static IEnumerable<ComputeResource> MergeResources(IEnumerable<ComputeResource> baseSequence,
        IEnumerable<ComputeResource> sequence2)
    {
        var baseDict = (baseSequence ?? Enumerable.Empty<ComputeResource>())
            .Where(g => g != null)
            .ToDictionary(GetMergeKey, g => g);

        foreach (var item in sequence2 ?? Enumerable.Empty<ComputeResource>())
        {
            if (item == null) continue;
            var key = GetMergeKey(item);

            baseDict[key] = item;
        }

        return baseDict.Values; // Return the modified sequence
    }

    private static (AttributeType AttributeType, string Resource, Int3 Size) GetMergeKey(ComputeResource resource)
    {
        var resourceName = string.IsNullOrWhiteSpace(resource.Resource)
            ? null
            : resource.Resource;
        var size = resourceName == null
            ? resource.Size
            : default;

        return (resource.AttributeType, resourceName, size);
    }
}
