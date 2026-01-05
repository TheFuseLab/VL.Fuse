# 2. Generic Shader Nodes

## Status

Accepted

## Context

Shader operations often work across multiple data types:
- `float + float`
- `float3 + float3`
- `float4 + float4`

We needed a way to:
- Avoid duplicating node classes for each type
- Maintain type safety in the visual environment
- Map C# types to GPU types correctly

Alternative approaches considered:
1. **Separate classes per type**: `AddFloat`, `AddFloat3`, `AddFloat4`, etc.
2. **Dynamic typing**: Nodes accept any type, resolve at runtime
3. **Generic types**: `Add<T>` works with any supported type
4. **Type unions**: Single class with multiple type parameters

## Decision

We use C# generics with a `ShaderNode<T>` base class where `T : struct`.

```csharp
public class ShaderNode<T> : AbstractShaderNode
{
    public override string TypeName()
    {
        return TypeHelpers.GetGpuType<T>();
    }
}

public class Add<T> : ResultNode<T> where T : struct
{
    public Add(NodeContext nodeContext, ShaderNode<T> a, ShaderNode<T> b)
        : base(nodeContext, "Add")
    {
        SetInputs(new AbstractShaderNode[] { a, b });
    }
}
```

Type mapping is handled by `TypeHelpers.GetGpuType<T>()`:
- `float` → `"float"`
- `Vector3` → `"float3"`
- `Matrix` → `"float4x4"`

## Consequences

### Positive

- Single implementation works for all numeric types
- Type errors caught at compile time in VL
- Clean, DRY code
- Easy to add new operations
- VL's monadic type filter integrates well

### Negative

- Some operations don't generalize well (e.g., cross product is only for float3)
- Generic constraints can't express "numeric types only"
- Reflection-based type creation is needed in some cases

### Neutral

- VL must instantiate specific generic types
- Type parameter appears in node names in VL
