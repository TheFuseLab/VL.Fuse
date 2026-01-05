# AI Context for VL.Fuse

This document provides essential context for AI assistants (Claude, GPT, Copilot, etc.) working with the VL.Fuse codebase.

## Project Overview

VL.Fuse is a visual GPU programming library for [vvvv gamma](https://visualprogramming.net/). It compiles node graphs into SDSL (Stride Shading Language) shaders at runtime.

**Tech Stack:**
- C# 13 / .NET 8.0 - Core engine
- SDSL - GPU shaders (Stride's HLSL-based language)
- VL - Visual programming patches
- Stride 3D Engine - Rendering backend

## Key Concepts

### ShaderNode<T> Hierarchy

```
AbstractShaderNode        (base class, manages graph connections)
    └── ShaderNode<T>     (generic typed wrapper)
        └── ResultNode<T> (nodes that produce a value)
        └── ComputeNode<T>(compute shader nodes)
```

### Code Generation Flow

1. `ShaderNode.SourceTemplate()` returns SDSL template with `${placeholders}`
2. `CreateTemplateMap()` provides values for placeholders
3. `ShaderNodesUtil.Evaluate(template, map)` performs substitution
4. `BuildSource()` traverses graph and concatenates all code

### Template Syntax

```csharp
// Template with placeholders
"${resultType} ${resultName} = ${a} + ${b};"

// After evaluation
"float3 Add_12345 = position_111 + offset_222;"
```

Common placeholders:
- `${resultType}` - GPU type (float, float3, etc.)
- `${resultName}` - Unique variable name (ID)
- `${arguments}` - Comma-separated input IDs
- `${implementation}` - Expression for ResultNode

### Property System

Nodes declare dependencies via string-keyed properties:
- `Mixins` - Required shader includes
- `Inputs` - GPU parameters (uniforms, buffers)
- `Declarations` - Field declarations
- `Structs` - Custom struct definitions
- `Streams` - Shader stream definitions

## Critical Conventions

### 1. NodeContext First

Always pass `NodeContext` as the first constructor parameter:

```csharp
// Correct
public MyNode(NodeContext nodeContext, ShaderNode<float> input)
    : base(nodeContext, "MyNode") { }

// Wrong - will break node identification
public MyNode(ShaderNode<float> input)
```

### 2. Use SetInputs()

```csharp
// Correct
SetInputs(new AbstractShaderNode[] { input1, input2 });

// Wrong - bypasses null handling
Ins.Add(input1);
```

### 3. Unique IDs

Node IDs are `Name_HashCode`. Never hardcode IDs:

```csharp
// Correct
return ID;  // "MyNode_12345678"

// Wrong - causes collisions
return "myVar";
```

### 4. VL Integration Markers

Methods called via reflection from VL should be marked:

```csharp
// ReSharper disable once UnusedMember.Global
// accessed from vl
public static void MyMethod() { }

// USED BY VL - do not remove
public string TypeOverride { get; set; }
```

## Directory Structure

```
src/Fuse/              # C# shader generation engine
├── ShaderNode.cs      # Core classes: AbstractShaderNode, ShaderNode<T>
├── ShaderNodesUtil.cs # Evaluate(), BuildArguments(), utilities
├── compute/           # Compute shader support
├── function/          # Function abstractions
└── ShaderFX/          # ShaderFX integration

vl/
├── shaders/           # SDSL shader files (FuseCommon*.sdsl, etc.)
└── *.vl              # Visual patches

help/                 # Documentation patches by category
docs/decisions/       # Architecture Decision Records
```

## Type Mapping

| C# Type | GPU Type | Notes |
|---------|----------|-------|
| `float` | `float` | |
| `Vector2` | `float2` | |
| `Vector3` | `float3` | |
| `Vector4` | `float4` | |
| `int` | `int` | |
| `uint` | `uint` | |
| `bool` | `bool` | |
| `Matrix` | `float4x4` | |
| `GpuStruct` | custom | Uses TypeOverride |

## Common Tasks

### Adding a New Node

```csharp
namespace Fuse;

public class MyOperation<T> : ResultNode<T> where T : struct
{
    public MyOperation(NodeContext nodeContext, ShaderNode<T> a, ShaderNode<T> b)
        : base(nodeContext, "MyOperation")
    {
        SetInputs(new AbstractShaderNode[] { a, b });
    }

    protected override string ImplementationTemplate()
    {
        return "myFunc(${arguments})";
    }
}
```

### Adding SDSL Function

```hlsl
// In vl/shaders/FuseCommon*.sdsl
float myFunc(float a, float b)
{
    return a * b;
}
```

### Registering Dependencies

```csharp
// In constructor
AddProperty(Mixins, "FuseCommonBuffer");
AddProperty(Inputs, new BufferInput(buffer));
```

## What NOT to Do

1. Don't skip `NodeContext` parameter
2. Don't hardcode variable names (use `ID`)
3. Don't modify `Ins` list directly
4. Don't ignore null input handling
5. Don't add breaking changes without updating help patches
6. Don't remove methods marked `// USED BY VL`

## Useful Entry Points

- `ShaderNode.cs:245` - `AbstractShaderNode` class definition
- `ShaderNode.cs:609` - `ShaderNode<T>` generic wrapper
- `ShaderNodesUtil.cs:125` - `Evaluate()` template function
- `ShaderNodesUtil.cs:254` - `RegisterComputeShader()`
- `ShaderNodesUtil.cs:267` - `RegisterDrawShader()`

## Related Documentation

- [ARCHITECTURE.md](ARCHITECTURE.md) - Full system architecture
- [PATTERNS.md](PATTERNS.md) - Code patterns and conventions
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guidelines
- [docs/decisions/](docs/decisions/) - Architecture Decision Records
