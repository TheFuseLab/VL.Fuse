# 3. Property-Based Dependency Tracking

## Status

Accepted

## Context

Shader compilation requires gathering various dependencies from the node graph:
- Input parameters (uniforms, textures, buffers)
- Required shader mixins
- Custom struct definitions
- Stream declarations

We needed a way to:
- Let any node declare its dependencies
- Collect all dependencies from the entire graph
- Support different dependency types
- Avoid tight coupling between node types

Alternative approaches considered:
1. **Interface-based**: `IRequiresMixin`, `IDeclaresStruct`, etc.
2. **Inheritance-based**: Different base classes for different dependency patterns
3. **Property bags**: Generic key-value storage with typed retrieval
4. **Event-based**: Nodes emit dependency events during compilation

## Decision

We use a property bag system with string keys and `IList` values.

```csharp
public Dictionary<string, IList> Property { get; } = new();

// Standard property IDs
protected const string Mixins = "Mixins";
protected const string Inputs = "Inputs";
protected const string Declarations = "Declarations";
protected const string Structs = "Structs";
protected const string Streams = "Streams";

// Adding properties
public void AddProperty(string propertyId, object property)
{
    if (!Property.ContainsKey(propertyId))
        Property[propertyId] = new ArrayList();
    Property[propertyId].Add(property);
}
```

Properties are collected via visitor traversal:

```csharp
public List<TPropertyType> PropertyForTree<TPropertyType>(string propertyId)
{
    var visitor = new PropertyOfTypeAndIdVisitor<TPropertyType>(propertyId);
    PreOrderVisit(visitor);
    return visitor.Result.ToList();
}
```

## Consequences

### Positive

- Any node can declare any dependency type
- New dependency types can be added without changing base classes
- Collection is automatic via graph traversal
- Properties can store arbitrary data
- Decoupled from node inheritance hierarchy

### Negative

- String keys are not type-safe (typos possible)
- `IList` requires casting for typed access
- Property system is essentially dynamic typing
- No compile-time validation of property contents

### Neutral

- Standard property IDs defined as constants for consistency
- Visitors needed to collect properties from graph
- Properties are per-node, not shared
