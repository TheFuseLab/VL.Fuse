# 4. Visitor Pattern for Graph Traversal

## Status

Accepted

## Context

The shader node graph needs to be traversed for various purposes:
- Collecting all properties/dependencies
- Building the final source code
- Validating node IDs for uniqueness
- Debugging and visualization
- Finding nodes of specific types

We needed a way to:
- Support multiple traversal operations without modifying node classes
- Handle both pre-order and post-order traversal
- Track visited nodes to avoid infinite loops (graphs may have shared nodes)
- Allow different traversal algorithms for different purposes

Alternative approaches considered:
1. **Methods on nodes**: Each node has `CollectDependencies()`, `Validate()`, etc.
2. **Iterator pattern**: Expose graph as enumerable
3. **Visitor pattern**: External visitors operate on nodes
4. **Callback-based**: Pass lambdas for traversal operations

## Decision

We implement the Visitor pattern with `IShaderNodeVisitor` interface.

```csharp
public interface IShaderNodeVisitor
{
    void Visit(AbstractShaderNode node, int recursionLevel);
}
```

Nodes support pre-order and post-order traversal:

```csharp
public void PreOrderVisit(IShaderNodeVisitor visitor, HashSet<AbstractShaderNode> visitedNodes, int level = 0)
{
    if (!visitedNodes.Add(this)) return;  // Already visited

    visitor.Visit(this, level);
    foreach (var node in Ins)
        node?.PreOrderVisit(visitor, visitedNodes, level + 1);
}

public void PostOrderVisit(IShaderNodeVisitor visitor, HashSet<AbstractShaderNode> visitedNodes, int level = 0)
{
    if (!visitedNodes.Add(this)) return;

    foreach (var node in Ins)
        node?.PostOrderVisit(visitor, visitedNodes);
    visitor.Visit(this, level);
}
```

Standard visitors are provided:
- `ChildrenOfTypeVisitor<T>` - Find nodes by type
- `PropertyOfTypeVisitor<T>` - Collect properties
- `CheckIdsVisitor` - Ensure unique naming
- `FunctionMapVisitor` - Gather function declarations
- `PrintShaderNodeVisitor` - Debug output

## Consequences

### Positive

- New traversal operations don't require modifying nodes
- Clean separation of concerns
- Recursion level available for indentation/debugging
- Visited set prevents infinite loops
- Both traversal orders supported

### Negative

- More classes to maintain (one per operation)
- Visitor must handle all node types it cares about
- State management in visitors can be complex

### Neutral

- Standard OOP pattern, well understood
- Visitors can accumulate results in instance fields
- HashSet allocation for each traversal
