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

    public AttributeType AttributeType { get;  }
  //  public string Group { get;  }
    public string Resource { get;  }

    public Int3 Size { get; }
/*
    public bool SameTarget(ComputeResource theOverride)
    {
        return AttributeType == theOverride.AttributeType && Group == theOverride.Group;
    }*/

    public override bool Equals(object obj)
    {
        if (obj is ComputeResource otherOverride)
        {
            return /*SameTarget(otherOverride) &&*/ Resource == otherOverride.Resource;
        }
        return false;
    }

    public override int GetHashCode()
    {
        var hash = 17; 
        hash = hash * 23 + AttributeType.GetHashCode();
      //  hash = hash * 23 + (Group?.GetHashCode() ?? 0);
        hash = hash * 23 + (Resource?.GetHashCode() ?? 0);
        return hash;
    }
    
    public static IEnumerable<ComputeResource> MergeResources(IEnumerable<ComputeResource> baseSequence, IEnumerable<ComputeResource> sequence2)
    {
        var baseDict = baseSequence.ToDictionary(g => new { g.AttributeType, g.Resource }, g => g);

        foreach (var item in sequence2)
        {
            if (item == null) continue;
            var key = new { item.AttributeType, item.Resource };

            if (baseDict.ContainsKey(key))
            {
                if (!baseDict[key].Equals(item)) // If the item in sequence2 is different than the one in baseSequence
                {
                    baseDict[key] = item; // Replace the item in the baseSequence with the one from sequence2
                }
            }
            else
            {
                baseDict[key] = item; // If it's not in baseSequence, add it to the dictionary
            }
        }

        return baseDict.Values; // Return the modified sequence
    }
}