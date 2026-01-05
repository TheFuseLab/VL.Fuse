# VL.Fuse Architecture

This document describes the architecture of VL.Fuse, a visual GPU programming library for vvvv gamma.

## Overview

VL.Fuse enables visual shader programming by compiling node graphs into SDSL (Stride Shading Language) code at runtime. The system bridges three worlds:

```
VL Patches (Visual) → C# Engine (Logic) → SDSL Shaders (GPU)
```

## Core Concepts

### 1. Shader Node Graph

Shaders are built as directed acyclic graphs (DAGs) of `ShaderNode<T>` instances. Each node:
- Has typed inputs and outputs
- Generates a snippet of SDSL code
- Tracks dependencies via the Property system

```
[Input Nodes] → [Operation Nodes] → [Output/Result Nodes]
     ↓                  ↓                    ↓
  Parameters         Processing           Final Value
```

### 2. Code Generation Pipeline

```
┌─────────────────┐
│  ShaderNode<T>  │  ← Visual node in VL
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ SourceTemplate()│  ← Returns SDSL template with ${placeholders}
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Evaluate()    │  ← Replaces ${key} with actual values
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  BuildSource()  │  ← Traverses graph, collects all code
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   Final SDSL    │  ← Complete shader ready for Stride
└─────────────────┘
```

### 3. Template Substitution System

Nodes generate code using templates with `${placeholder}` syntax:

```csharp
// In a node's SourceTemplate() method:
"${resultType} ${resultName} = ${a} + ${b};"

// After evaluation by ShaderNodesUtil.Evaluate():
"float3 Add_12345 = position_111 + offset_222;"
```

Common placeholders:
- `${resultType}` - Output type (float, float3, etc.)
- `${resultName}` - Unique variable name (ID)
- `${arguments}` - Comma-separated input IDs
- `${implementation}` - For ResultNode, the expression

## Key Classes

### AbstractShaderNode (`src/Fuse/ShaderNode.cs`)

Base class for all shader nodes. Key responsibilities:
- Manages input connections (`Ins` list)
- Generates source code via `SourceTemplate()`
- Tracks properties for dependencies
- Supports visitor pattern traversal

```csharp
public abstract class AbstractShaderNode
{
    public List<AbstractShaderNode> Ins;           // Input connections
    public Dictionary<string, IList> Property;     // Dependency tracking
    public abstract string SourceTemplate();       // Code generation
    public void PreOrderVisit(IShaderNodeVisitor); // Graph traversal
}
```

### ShaderNode<T> (`src/Fuse/ShaderNode.cs`)

Generic typed wrapper providing type safety:

```csharp
public class ShaderNode<T> : AbstractShaderNode
{
    public ShaderNode<T> Default;     // Fallback when input is null
    public override string ID;        // Unique: Name_HashCode
    public override string TypeName(); // GPU type from T
}
```

### ResultNode<T> (`src/Fuse/ShaderNode.cs`)

For nodes that produce a result via an expression:

```csharp
protected override string SourceTemplate()
{
    return "${resultType} ${resultName} = ${implementation};";
}

protected abstract string ImplementationTemplate();
```

### ShaderNodesUtil (`src/Fuse/ShaderNodesUtil.cs`)

Utility class for:
- Template evaluation: `Evaluate(template, dictionary)`
- Shader registration with Stride
- ID generation and hashing
- Code formatting

## Property System

Nodes track dependencies using string-keyed property bags:

| Property ID    | Purpose                              | Type              |
|----------------|--------------------------------------|-------------------|
| `Mixins`       | Required shader mixins               | `string`          |
| `Inputs`       | GPU input parameters                 | `IGpuInput`       |
| `Compositions` | Shader compositions                  | `string`          |
| `Declarations` | Field declarations                   | `FieldDeclaration`|
| `Structs`      | Custom struct definitions            | `string`          |
| `Streams`      | Shader stream definitions            | `string`          |

Properties are collected via visitor traversal and merged into the final shader.

## Visitor Pattern

Graph analysis uses the visitor pattern:

```csharp
public interface IShaderNodeVisitor
{
    void Visit(AbstractShaderNode node, int recursionLevel);
}
```

Built-in visitors:
- `ChildrenOfTypeVisitor<T>` - Find all nodes of a type
- `PropertyOfTypeVisitor<T>` - Collect properties by type
- `FunctionMapVisitor` - Gather function declarations
- `CheckIdsVisitor` - Ensure unique node names
- `CheckContextVisitor` - Validate shader context

## Shader Output Targets

### Compute Shaders (`ToComputeFx<T>`)
- General-purpose GPU computation
- Registered via `RegisterComputeShader()`

### Draw Shaders (`ToDrawFX`)
- Rendering and visual effects
- Registered via `RegisterDrawShader()`

### ShaderFX (`ToShaderFX<T>`)
- Material and effect integration
- Works with Stride's material system

## SDSL Shader Organization

Shaders in `vl/shaders/`:

```
FuseCommon*.sdsl      - Utility functions (Buffer, Draw, SDF, etc.)
FuseCore*.sdsl        - Core operations (Transform, Color, Texture)
*_ShaderFX.sdsl       - ShaderFX implementations
*_ComputeFX.sdsl      - Compute shader implementations
*_TextureFX.sdsl      - Texture processing effects
```

Inheritance pattern:
```hlsl
shader FuseCommonBuffer : FuseCommonTypes
{
    // Functions that depend on types defined in FuseCommonTypes
}
```

## Type Mapping

| C# Type    | GPU Type  | Dimension |
|------------|-----------|-----------|
| `float`    | `float`   | 1         |
| `Vector2`  | `float2`  | 2         |
| `Vector3`  | `float3`  | 3         |
| `Vector4`  | `float4`  | 4         |
| `int`      | `int`     | 1         |
| `uint`     | `uint`    | 1         |
| `bool`     | `bool`    | 1         |
| `Matrix`   | `float4x4`| 16        |
| `GpuStruct`| custom    | varies    |

## NodeContext

Every node receives a `NodeContext` as its first constructor parameter:
- Provides unique identification via `Path`
- Enables hash code generation for unique naming
- Supports sub-context creation for nested operations

```csharp
public AbstractShaderNode(NodeContext nodeContext, string theId)
{
    NodeContext = nodeContext;
    HashCode = ShaderNodesUtil.GetHashCode(nodeContext);
}
```

## Runtime Model

VL.Fuse follows vvvv's "always runtime" philosophy:
- No separate compilation step
- Changes are immediately reflected
- Shaders compile on-demand via Stride
- Fast iteration during development

## Directory Structure

```
VL.Fuse/
├── src/Fuse/           # C# shader generation engine
│   ├── ShaderNode.cs   # Core node classes
│   ├── ShaderNodesUtil.cs
│   ├── compute/        # Compute shader support
│   ├── function/       # Function abstractions
│   └── ShaderFX/       # ShaderFX integration
├── vl/
│   ├── shaders/        # SDSL shader files
│   └── *.vl            # VL patch definitions
├── help/               # Documentation and examples
└── R&D/                # Research and experiments
```
