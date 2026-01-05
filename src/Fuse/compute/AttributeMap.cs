using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using Fuse.ComputeSystem;

namespace Fuse.compute;

public class AttributeMap
{
    private Dictionary<string, IAttribute> _attributeSet = new();
    private Dictionary<string, List<IAttribute>> _attributeInstances = new();
    private HashSet<string> _attributesToRemove = [];
    private bool _changedAttributes = false;
    
    public AttributeMap(AttributeType theAttributeType)
    {
        AttributeType = theAttributeType;
    }
    
    public AttributeType AttributeType { get; }
    
    /// <summary>
    /// --- Patch: Prepare ---
    /// Clears Instances dictionary,
    /// clears _attributesToRemove and fills it with all keys from _attributeSet.
    /// Resets _changedAttributes.
    /// Call once per "evaluation/frame" before you feed attributes.
    /// </summary>
    public void Prepare()
    {
        _changedAttributes = false;

        _attributeInstances.Clear();
        _attributesToRemove.Clear();
        foreach (var k in _attributeSet.Keys)
            _attributesToRemove.Add(k);
    }

    /// <summary>
    /// --- Patch: HandleAttribute ---
    /// If attribute is NOT overridden, run HandleCheckedAttribute.
    /// </summary>
    public void HandleAttribute(IAttribute attribute)
    {
        if (attribute.IsOverridden)
            return;

        HandleCheckedAttribute(attribute);
    }

    /// <summary>
    /// --- Patch: HandleCheckedAttribute (Internal) ---
    /// - remove attribute.Name from _attributesToRemove (it's used)
    /// - if not in _attributeSet: add and mark Changed
    /// - ensure a list exists in _attributeInstances for this name (create if missing)
    /// - add this instance to the list
    /// </summary>
    private void HandleCheckedAttribute(IAttribute attribute)
    {
        var name = attribute.Name;

        // Remove(MutableHashSet)
        _attributesToRemove.Remove(name);

        // ContainsKey/NOT/If/Add(MutableDictionary)
        if (_attributeSet.TryAdd(name, attribute))
        {
            _changedAttributes = true;
        }

        // _attributeInstances.ContainsKey / NOT / If -> Create MutableList + Add MutableDictionary
        if (!_attributeInstances.TryGetValue(name, out var list))
        {
            list = new List<IAttribute>(capacity: 4);
            _attributeInstances.Add(name, list);
            // In the patch this "might" be considered a change; keep conservative:
            _changedAttributes = true;
        }

        // GetItem(MutableDictionary) -> Add(MutableList)
        list.Add(attribute);
    }

    /// <summary>
    /// --- Patch: SyncAttributes ---
    /// For each entry in _attributeSet, lookup instances and call SyncFrom(canonical) if supported.
    /// This matches the "SyncAttribute" node behavior conceptually.
    /// </summary>
    public void SyncAttributes()
    {
        foreach (var (name, canonical) in _attributeSet)
        {
            if (!_attributeInstances.TryGetValue(name, out var instances))
                continue;

            for (int i = 0; i < instances.Count; i++)
            {
                instances[i].Sync(canonical);
            }
        }
    }

    /// <summary>
    /// --- Patch: GetAttributeInstances ---
    /// Flattens the dictionary-of-lists into a single list (AddRange in loop).
    /// </summary>
    public List<IAttribute> GetAttributeInstances()
    {
        var result = new List<IAttribute>();
        foreach (var kv in _attributeInstances)
            result.AddRange(kv.Value);
        return result;
    }

    /// <summary>
    /// --- Patch: ApplyPadding ---
    /// Computes total struct size in bytes (sum of CatSizeInBytes) and adds one of Padding1/2/3
    /// if needed to align to 16 bytes (HLSL cbuffer packing style).
    ///
    /// Returns true if it modified _attributeSet.
    /// </summary>
    public bool ApplyPadding()
    {
        /*
        int totalBytes = 0;
        foreach (var a in _attributeSet.Values)
            totalBytes += TypeHelpers.GetSizeInBytes(a.ShaderNode);

        int padFloats = GetPaddingFloatCount(totalBytes); // 0..3
        if (padFloats == 0)
            return false;

        IAttribute? padAttr = padFloats switch
        {
            1 => new StructuredBufferAttribute<float>(),
            2 => Padding2,
            3 => Padding3,
            _ => null
        };

        if (padAttr is null)
            return false; // no padding attribute configured

        // Patch uses Name(Attribute) into Add(MutableDictionary)
        // We'll add/replace by its own Name.
        var key = padAttr.Name;

        bool changed = !_attributeSet.TryGetValue(key, out var existing) || !ReferenceEquals(existing, padAttr);
        _attributeSet[key] = padAttr;

        if (changed)
            _changedAttributes = true;

        return changed;*/
        return false;
    }

    /// <summary>
    /// --- Patch: RemoveUnusedAttributes (Internal) ---
    /// Removes all keys still in _attributesToRemove from _attributeSet.
    /// Returns true if anything was removed.
    /// </summary>
    public bool RemoveUnusedAttributes()
    {
        if (_attributesToRemove.Count == 0)
            return false;

        var removedAny = false;

        // copy to avoid modifying set while iterating elsewhere
        var toRemove = new List<string>(_attributesToRemove);
        for (int i = 0; i < toRemove.Count; i++)
        {
            if (_attributeSet.Remove(toRemove[i]))
                removedAny = true;
        }

        return removedAny;
    }

    /// <summary>
    /// --- Patch: Finish ---
    /// removedUnused OR _changedAttributes
    /// </summary>
    public bool Finish(bool applyPadding = false, bool syncAttributes = false)
    {
        
        if (applyPadding)
            ApplyPadding();

        if (syncAttributes)
            SyncAttributes();

        var removedUnused = RemoveUnusedAttributes();
        _changedAttributes = _changedAttributes || removedUnused;

        return _changedAttributes;
    }

    /// <summary>
    /// --- Patch: GetPadding ---
    /// Equivalent of:
    /// rem = size % 16
    /// padBytes = rem == 0 ? 0 : (16 - rem)
    /// padFloats = padBytes / 4   // 0..3
    /// </summary>
    public static int GetPaddingFloatCount(int structSizeBytes)
    {
        int rem = structSizeBytes % 16;
        if (rem == 0) return 0;
        int padBytes = 16 - rem;
        return padBytes / 4;
    }

}