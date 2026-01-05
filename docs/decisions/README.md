# Architecture Decision Records

This directory contains Architecture Decision Records (ADRs) for VL.Fuse. ADRs document significant architectural decisions and their rationale.

## What is an ADR?

An ADR captures a single architectural decision along with its context and consequences. They help:

- Understand why decisions were made
- Onboard new contributors
- Avoid re-discussing settled decisions
- Track the evolution of the architecture

## Index

| ID | Title | Status |
|----|-------|--------|
| [0001](0001-template-based-code-generation.md) | Template-Based Code Generation | Accepted |
| [0002](0002-generic-shader-nodes.md) | Generic Shader Nodes | Accepted |
| [0003](0003-property-based-dependency-tracking.md) | Property-Based Dependency Tracking | Accepted |
| [0004](0004-visitor-pattern-for-graph-traversal.md) | Visitor Pattern for Graph Traversal | Accepted |

## Creating a New ADR

1. Copy the template below
2. Number it sequentially (e.g., `0005-my-decision.md`)
3. Fill in all sections
4. Add to the index above
5. Submit with your PR

### Template

```markdown
# [ID]. [Title]

## Status

[Proposed | Accepted | Deprecated | Superseded by ADR-XXXX]

## Context

[What is the issue we're addressing?]

## Decision

[What did we decide?]

## Consequences

### Positive
- [Good outcomes]

### Negative
- [Trade-offs]

### Neutral
- [Other effects]
```
