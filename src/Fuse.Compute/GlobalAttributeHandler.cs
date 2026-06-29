using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fuse.ComputeSystem;

namespace Fuse.compute;

public sealed class GlobalAttributeHandler
{
    public const string ComputeSystemAttributeProperty = "ComputeSystemAttribute";

    private readonly Dictionary<string, AbstractShaderNode> _values = new(StringComparer.Ordinal);

    public GlobalAttributeHandler(
        object attributeValues = null,
        AttributeMap attributeMap = null)
    {
        AttributeValues = attributeValues;
        AttributeMap = attributeMap ?? new AttributeMap(AttributeType.Temporary);
    }

    public object AttributeValues { get; private set; }

    public AttributeMap AttributeMap { get; }

    public IReadOnlyDictionary<string, AbstractShaderNode> Values => _values;

    public bool ChangedAttributes => AttributeMap.ChangedAttributes;

    public static GlobalAttributeHandler Create(
        object attributeValues = null,
        AttributeMap attributeMap = null)
    {
        return new GlobalAttributeHandler(attributeValues, attributeMap);
    }

    public GlobalAttributeHandler Update(object attributeValues = null)
    {
        AttributeValues = attributeValues;
        GetValues(attributeValues);
        return this;
    }

    public GlobalAttributeHandler Prepare()
    {
        AttributeMap.Prepare();
        return this;
    }

    public GlobalAttributeHandler HandleAttribute(IAttribute attribute)
    {
        if (attribute != null)
            AttributeMap.HandleAttribute(attribute);

        return this;
    }

    public GlobalAttributeHandler BindAttributes(
        IEnumerable<IComputeStage> stages,
        object attributeValues = null)
    {
        if (attributeValues != null)
            AttributeValues = attributeValues;

        GetValues(AttributeValues);

        foreach (var attribute in CollectAttributes(stages).Where(attribute => attribute.AttributeType == AttributeType.Temporary))
            HandleAttribute(attribute);

        foreach (var attribute in AttributeMap.GetAttributeInstances())
        {
            if (_values.TryGetValue(attribute.Name, out var value))
                attribute.InputAbstract = value;
        }

        return this;
    }

    public bool FinishAttributeMap(bool applyPadding = false, bool syncAttributes = true)
    {
        return AttributeMap.Finish(applyPadding, syncAttributes);
    }

    public bool Finish(bool applyPadding = false, bool syncAttributes = true)
    {
        return FinishAttributeMap(applyPadding, syncAttributes);
    }

    public IReadOnlyDictionary<string, AbstractShaderNode> GetValues(object attributeValues = null)
    {
        if (attributeValues != null)
            AttributeValues = attributeValues;

        _values.Clear();
        AddValues(AttributeValues);
        return Values;
    }

    public IReadOnlyList<IAttribute> GetAttributeInstances()
    {
        return AttributeMap.GetAttributeInstances();
    }

    public IReadOnlyDictionary<string, IAttribute> GetAttributeSet()
    {
        return AttributeMap.AttributeSet;
    }

    public static IEnumerable<IAttribute> CollectAttributes(IEnumerable<IComputeStage> stages)
    {
        if (stages == null)
            yield break;

        foreach (var stage in stages)
        {
            foreach (var attribute in CollectAttributes(stage?.ComputeGraph))
                yield return attribute;
        }
    }

    public static IEnumerable<IAttribute> CollectAttributes(AbstractShaderNode graph)
    {
        if (graph == null)
            yield break;

        foreach (var attribute in graph.PropertyForTree<IAttribute>(ComputeSystemAttributeProperty))
            yield return attribute;
    }

    private void AddValues(object values)
    {
        switch (values)
        {
            case null:
                return;
            case IGlobalAttribute globalAttribute:
                AddGlobalAttribute(globalAttribute);
                return;
            case IReadOnlyDictionary<string, AbstractShaderNode> abstractDictionary:
                foreach (var (key, value) in abstractDictionary)
                    AddValue(key, value);
                return;
            case IEnumerable<IGlobalAttribute> globalAttributes:
                foreach (var globalAttribute in globalAttributes)
                    AddGlobalAttribute(globalAttribute);
                return;
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is string key)
                        AddValueObject(key, entry.Value);
                }
                return;
            case IEnumerable enumerable when values is not string:
                foreach (var item in enumerable)
                    AddValues(item);
                return;
        }
    }

    private void AddGlobalAttribute(IGlobalAttribute globalAttribute)
    {
        AddValue(globalAttribute.GetName(), globalAttribute.GetValue());
    }

    private void AddValueObject(string key, object value)
    {
        switch (value)
        {
            case AbstractShaderNode abstractShaderNode:
                AddValue(key, abstractShaderNode);
                break;
            case IGlobalAttribute globalAttribute:
                AddGlobalAttribute(globalAttribute);
                break;
        }
    }

    private void AddValue(string key, AbstractShaderNode value)
    {
        if (!string.IsNullOrWhiteSpace(key) && value != null)
            _values[key] = value;
    }
}
