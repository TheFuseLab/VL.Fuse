# Contributing to VL.Fuse

Thank you for your interest in contributing to VL.Fuse! This guide will help you get started.

## Getting Started

### Prerequisites

- [vvvv gamma](https://visualprogramming.net/) (latest version)
- Visual Studio 2022 or Rider
- .NET 8.0 SDK
- Git

### Setting Up Development Environment

1. Clone the repository:
   ```bash
   git clone https://github.com/TheFuseLab/VL.Fuse.git
   cd VL.Fuse
   ```

2. Build the C# project:
   ```bash
   dotnet build src/Fuse/Fuse.csproj
   ```

3. Open vvvv gamma and reference the local VL.Fuse package

## How to Contribute

### Adding a New Shader Node

1. **Create the C# class** in `src/Fuse/`:

   ```csharp
   namespace Fuse;

   public class MyNewOperation<T> : ResultNode<T> where T : struct
   {
       public MyNewOperation(NodeContext nodeContext, ShaderNode<T> input)
           : base(nodeContext, "MyNewOperation")
       {
           SetInputs(new AbstractShaderNode[] { input });
       }

       protected override string ImplementationTemplate()
       {
           return "myShaderFunction(${arguments})";
       }
   }
   ```

2. **Add SDSL function** (if needed) in `vl/shaders/`:

   ```hlsl
   // In appropriate FuseCommon*.sdsl or new file
   float myShaderFunction(float input)
   {
       return input * 2.0;
   }
   ```

3. **Create VL node** by exposing the C# class in the appropriate `.vl` file

4. **Add help patch** in `help/[Category]/`:
   - Create a reference patch showing basic usage
   - Add a HowTo patch for common use cases

### Adding a New SDSL Function

1. Identify the appropriate shader file in `vl/shaders/`
2. Add the function with documentation:

   ```hlsl
   // @purpose: Brief description of what this does
   // @param paramName: What this parameter represents
   // @returns: What the function returns
   float3 myNewFunction(float3 position, float scale)
   {
       return position * scale;
   }
   ```

3. Create a C# node to expose it (see above)

### Modifying Existing Code

1. Read the existing code first
2. Understand the dependencies and callers
3. Make minimal, focused changes
4. Test with existing help patches
5. Update documentation if behavior changes

## Code Style

### Commit Messages

Use conventional commits for clear history:

```
feat(compute): add parallel reduction support
fix(sdf): correct smooth union calculation
docs(help): add SDF boolean operations tutorial
refactor(core): simplify visitor traversal
test(noise): add Perlin noise validation patch
chore(build): update Stride dependency
```

Format: `type(scope): description`

Types:
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation only
- `refactor` - Code change that neither fixes a bug nor adds a feature
- `test` - Adding or modifying tests
- `chore` - Build process or auxiliary tool changes

### Code Conventions

See [PATTERNS.md](PATTERNS.md) for detailed patterns. Key points:

- `NodeContext` is always the first constructor parameter
- Use `SetInputs()` to set node inputs
- Register dependencies via `AddProperty()`
- Follow existing naming conventions

### Documentation

- Add XML documentation to public classes and methods
- Create help patches for new features
- Update ARCHITECTURE.md for architectural changes
- Add ADR for significant design decisions

## Testing

### Manual Testing

1. Open vvvv gamma
2. Load relevant help patches
3. Test with various input combinations
4. Verify GPU output is correct

### Validation Checklist

- [ ] Node compiles without errors
- [ ] Generated SDSL is valid
- [ ] Shader compiles in Stride
- [ ] Visual output matches expected behavior
- [ ] Performance is acceptable
- [ ] Help patch works correctly

## Pull Request Process

1. **Create a branch** from `1.0`:
   ```bash
   git checkout -b feature/my-new-feature
   ```

2. **Make your changes** following the code style guidelines

3. **Test thoroughly** using help patches

4. **Commit** with conventional commit messages

5. **Push** and create a Pull Request

6. **Fill out the PR template** completely

7. **Address review feedback** promptly

### PR Requirements

- Clear description of changes
- Affected domains identified
- Breaking changes noted
- Help patches added/updated for new features
- All existing patches still work

## Questions?

- Open an issue for bugs or feature requests
- Join the [Element chat](https://app.element.io/#/room/#VL.Fuse:matrix.org) for discussions
- Check existing issues before creating new ones

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
