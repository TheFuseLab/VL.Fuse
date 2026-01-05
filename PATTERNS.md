# VL.Fuse Code Patterns

This document describes the established code patterns used throughout VL.Fuse. Follow these patterns when contributing to maintain consistency.

## C# Patterns

### 1. NodeContext First Parameter

All shader nodes receive `NodeContext` as the first constructor parameter:

```csharp
// Correct
public MyShaderNode(NodeContext nodeContext, ShaderNode<float> input)
    : base(nodeContext, "MyShader")
{
    SetInputs(new[] { input });
}

// Incorrect - missing NodeContext
public MyShaderNode(ShaderNode<float> input) // Don't do this
```

### 2. Template Method Pattern

Nodes generate code via overridable template methods:

```csharp
public class MyOperation<T> : ResultNode<T>
{
    // Return the template with ${placeholders}
    protected override string ImplementationTemplate()
    {
        return "${a} * ${b}";
    }

    // Provide values for placeholders
    protected override Dictionary<string, string> CreateTemplateMap()
    {
        var map = base.CreateTemplateMap();
        map["a"] = Ins[0].ID;
        map["b"] = Ins[1].ID;
        return map;
    }
}
```

### 3. Visitor Pattern for Graph Traversal

Use visitors to analyze or transform the node graph:

```csharp
// Define a visitor
public class MyVisitor : IShaderNodeVisitor
{
    public List<string> Results = new();

    public void Visit(AbstractShaderNode node, int recursionLevel)
    {
        if (node is MyNodeType myNode)
        {
            Results.Add(myNode.SomeProperty);
        }
    }
}

// Use the visitor
var visitor = new MyVisitor();
rootNode.PreOrderVisit(visitor);
var results = visitor.Results;
```

### 4. Property-Based Dependency Tracking

Register dependencies using the Property system:

```csharp
public class MyBufferNode : ShaderNode<float>
{
    public MyBufferNode(NodeContext nodeContext, Buffer buffer)
        : base(nodeContext, "MyBuffer")
    {
        // Register as an input dependency
        AddProperty(Inputs, new BufferInput(buffer));

        // Register required mixin
        AddProperty(Mixins, "FuseCommonBuffer");
    }
}
```

Property IDs (defined in AbstractShaderNode):
- `Mixins` - Shader mixins to include
- `Inputs` - GPU parameters
- `Compositions` - Shader compositions
- `Declarations` - Field declarations
- `Structs` - Struct definitions
- `Streams` - Stream definitions

### 5. VL Integration Markers

Mark methods accessed via VL reflection:

```csharp
// ReSharper disable once UnusedMember.Global
// accessed from vl
public static ToShaderFX<T> RegisterShaderFX<T>(ShaderNode<T> theGpuValue)
{
    return new ToShaderFX<T>(theGpuValue);
}

// USED BY VL - do not remove
public string TypeOverride { get; set; }
```

### 6. Generic Node Creation

Use generics for type-safe shader operations:

```csharp
public class Multiply<T> : ResultNode<T> where T : struct
{
    public Multiply(NodeContext nodeContext, ShaderNode<T> a, ShaderNode<T> b)
        : base(nodeContext, "Multiply")
    {
        SetInputs(new AbstractShaderNode[] { a, b });
    }

    protected override string ImplementationTemplate()
    {
        return "${a} * ${b}";
    }
}
```

### 7. Null Input Handling

Configure how null inputs are treated:

```csharp
public class MyNode : ShaderNode<float>
{
    public MyNode(NodeContext nodeContext)
        : base(nodeContext, "MyNode")
    {
        // Options:
        NullInputMode = HandleNullInputMode.SetOff;           // Skip node if null (default)
        NullInputMode = HandleNullInputMode.ReplaceWithDefault; // Use Default value
        NullInputMode = HandleNullInputMode.Remove;           // Remove from Ins list
    }
}
```

## Naming Conventions

### C# Code

| Element            | Convention        | Example                    |
|--------------------|-------------------|----------------------------|
| Classes            | PascalCase        | `ShaderNode`, `BufferInput`|
| Public methods     | PascalCase        | `BuildSource()`, `TypeName()`|
| Public properties  | PascalCase        | `HashCode`, `SourceCode`   |
| Private fields     | _camelCase        | `_hasNullInput`, `_contexts`|
| Parameters         | camelCase         | `nodeContext`, `theInput`  |
| Constants          | PascalCase        | `Mixins`, `Declarations`   |
| Generic types      | Single letter     | `T`, `TNode`, `TProperty`  |

### Node IDs

Node IDs follow the pattern: `Name_HashCode`

```csharp
public override string ID => Name + "_" + HashCode;
// Result: "Multiply_123456789"
```

### SDSL Shaders

| Element            | Convention              | Example                  |
|--------------------|-------------------------|--------------------------|
| Shader name        | PascalCase              | `FuseCommonBuffer`       |
| Functions          | camelCase               | `sbLoad()`, `sdRoundBox()`|
| Parameters         | camelCase               | `sBuffer`, `defaultValue`|
| File suffix        | Purpose indicator       | `_ShaderFX`, `_ComputeFX`|

## SDSL Patterns

### 1. Shader Inheritance

```hlsl
shader MyShader : FuseCommonTypes, FuseCommonBuffer
{
    // Inherits from base shaders
}
```

### 2. Overloaded Functions

Use type suffixes when Stride can't differentiate:

```hlsl
// Can't overload on StructuredBuffer<T> type alone
uint sbSize(StructuredBuffer<float> sBuffer) { ... }
uint sbSize2(StructuredBuffer<float2> sBuffer) { ... }
uint sbSize3(StructuredBuffer<float3> sBuffer) { ... }
```

### 3. Safe Buffer Access

Always check buffer size before access:

```hlsl
float sbLoad(uint index, StructuredBuffer<float> sBuffer, float defaultValue = 0.)
{
    float value = defaultValue;
    uint count = sbSize(sBuffer);
    if (count > 0) value = sBuffer[index % count];
    return value;
}
```

### 4. Section Comments

Organize shader code with clear section headers:

```hlsl
////////////////////////////////////////////////////////////////
//
//             Section Name
//
////////////////////////////////////////////////////////////////
```

## File Organization

### New Node Class

```csharp
using System.Collections.Generic;
using VL.Core;

namespace Fuse;

/// <summary>
/// Brief description of what this node does.
/// </summary>
public class MyNewNode<T> : ResultNode<T> where T : struct
{
    public MyNewNode(NodeContext nodeContext, ShaderNode<T> input)
        : base(nodeContext, "MyNewNode")
    {
        SetInputs(new AbstractShaderNode[] { input });
    }

    protected override string ImplementationTemplate()
    {
        return "myFunction(${arguments})";
    }

    protected override Dictionary<string, string> CreateTemplateMap()
    {
        var map = base.CreateTemplateMap();
        // Add custom mappings
        return map;
    }
}
```

### New Shader File

```hlsl
// File: vl/shaders/MyFeature.sdsl

shader MyFeature : FuseCommonTypes
{
    ////////////////////////////////////////////////////////////////
    //
    //             Feature Description
    //
    ////////////////////////////////////////////////////////////////

    // @purpose: Brief description
    // @param paramName: Description of parameter
    // @returns: Description of return value
    float myFunction(float param)
    {
        return param * 2.0;
    }
};
```

## Anti-Patterns to Avoid

### Don't Skip NodeContext
```csharp
// Bad - breaks node identification
public MyNode() : base(null, "MyNode") { }
```

### Don't Hardcode IDs
```csharp
// Bad - will cause conflicts
return "myVar";

// Good - use unique ID
return ID; // "MyNode_123456"
```

### Don't Modify Ins Directly
```csharp
// Bad
Ins.Add(someNode);

// Good
SetInputs(new[] { someNode });
// or
AddInput(someNode);
```

### Don't Ignore Null Checks
```csharp
// Bad - crashes on null
protected override string ImplementationTemplate()
{
    return $"{Ins[0].ID} + {Ins[1].ID}"; // Crashes if Ins[1] is null
}

// Good - let null handling work
// Configure NullInputMode and let the base class handle it
```
