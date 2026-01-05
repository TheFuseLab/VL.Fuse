# 1. Template-Based Code Generation

## Status

Accepted

## Context

VL.Fuse needs to generate SDSL shader code from visual node graphs. We needed a mechanism that:

- Allows nodes to define their own code snippets
- Supports variable substitution for unique naming
- Is easy to read and modify
- Works with the graph-based compilation model

Alternative approaches considered:
1. **AST-based generation**: Build an abstract syntax tree and serialize it
2. **String concatenation**: Direct string building in each node
3. **External templates**: Template files loaded at runtime
4. **Template strings with placeholders**: Inline templates with substitution

## Decision

We use template strings with `${placeholder}` syntax, evaluated by `ShaderNodesUtil.Evaluate()`.

Each node defines its code via `SourceTemplate()`:

```csharp
protected override string SourceTemplate()
{
    return "${resultType} ${resultName} = ${a} + ${b};";
}
```

Placeholders are replaced using a dictionary:

```csharp
protected override Dictionary<string, string> CreateTemplateMap()
{
    return new Dictionary<string, string>
    {
        { "resultType", TypeName() },
        { "resultName", ID },
        { "a", Ins[0].ID },
        { "b", Ins[1].ID }
    };
}
```

## Consequences

### Positive

- Templates are human-readable and look like the output code
- Easy to debug - you can print templates and see what's generated
- Nodes can define arbitrary code patterns
- Simple regex-based evaluation is fast
- Template method pattern allows easy customization

### Negative

- No compile-time validation of template correctness
- Placeholder typos only caught at runtime
- Complex code patterns may become hard to read as templates

### Neutral

- Developers need to understand the placeholder convention
- Template syntax is specific to VL.Fuse (not a standard like Mustache)
