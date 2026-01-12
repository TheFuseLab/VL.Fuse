# Planned Feature: Delegate Support for MixinNodeFactory

**Status**: Planned
**Created**: 2026-01-08

## Overview

Add support for passing delegates (callable shader functions) as parameters to mixin functions. The system will auto-detect delegate signatures from SDSL code analysis, using the base `Delegate<T>` class configured dynamically.

## How Fuse Delegates Work

The Fuse delegate system:
- `IDelegate` interface with `Name`, `FunctionName`, `Functions` dictionary
- `Delegate<T>` base class - configurable with any `List<IFunctionParameter>`
- `FunctionName` - unique generated name for the delegate's function
- `Functions` - dictionary containing actual HLSL function code
- When used, delegate's `Functions` are merged into parent shader

Key insight: `CustomFunction<T>` already handles delegates via:
1. Template substitution: `${delegateName}` → `delegate.FunctionName`
2. Function merging: `delegate.Functions` → parent `Functions`
3. Property propagation from delegate tree

### Key Files in Existing Delegate System

- `src/Fuse/function/IDelegate.cs` - Interface definition
- `src/Fuse/function/Delegate.cs` - Base `Delegate<T>` and typed variants
- `src/Fuse/function/Invoke.cs` - Delegate invocation
- `src/Fuse/function/FunctionParameter.cs` - Function parameters
- `src/Fuse/Functions.cs` - `CustomFunction<T>` with delegate handling

## Proposed SDSL Syntax

Use annotation to declare delegate parameters with auto-detected signature:

```hlsl
// @export
// @delegate func
float applyFunc(float x)
{
    return ${func}(x);  // Called with float, returns float → Delegate<float>
}

// @export
// @delegate transform
float3 transformPoint(float3 p)
{
    return ${transform}(p);  // Called with float3, returns float3 → Delegate<Vector3>
}

// @export
// @delegate sdf
float rayMarch(float3 origin, float3 dir)
{
    float3 p = origin;
    for(int i = 0; i < 64; i++) {
        float d = ${sdf}(p);  // SDF delegate: float3 → float
        p += dir * d;
    }
    return length(p - origin);
}
```

## Signature Auto-Detection

Parse the SDSL function body to find delegate call sites:
1. Find `${delegateName}(args)` patterns
2. Analyze argument types from context (parameter types, literals, expressions)
3. Determine return type from usage context (assignment, return, expression)
4. Build `List<IFunctionParameter>` dynamically

Example analysis for `${func}(x)` where `x` is `float`:
- Input: 1 parameter of type `float`
- Output: determined from context (e.g., `return ${func}(x)` in `float` function → `float`)
- Result: `Delegate<float>` with one `FunctionParameter<float>`

## Implementation Plan

### 1. MixinParameterInfo.cs - Add delegate properties

```csharp
public bool IsDelegate { get; set; }
public string DelegateName { get; set; }  // Name used in ${name} substitution
public Type DelegateReturnType { get; set; }  // Return type of delegate
public List<Type> DelegateInputTypes { get; set; }  // Input parameter types
```

### 2. MixinCommentParser.cs - Parse @delegate annotation

Add parsing for `// @delegate paramName` annotations in the comment block before a function.

### 3. MixinFunctionParser.cs - Analyze delegate usage

New method to scan function body for delegate calls:
```csharp
private DelegateSignature AnalyzeDelegateUsage(string functionBody, string delegateName,
    List<MixinParameterInfo> parameters, Type returnType)
{
    // Find ${delegateName}(args) patterns
    // Resolve argument types from parameter list and context
    // Determine return type from usage
    return new DelegateSignature { InputTypes, ReturnType };
}
```

### 4. SdslTypeMapper.cs - No changes needed

Delegate types are constructed dynamically, not mapped from SDSL type names.

### 5. MixinShaderNodes.cs - Handle delegate pins

```csharp
private static Type GetPinType(MixinParameterInfo param)
{
    if (param.IsDelegate)
    {
        // Use base Delegate<T> where T is return type
        return typeof(Delegate<>).MakeGenericType(param.DelegateReturnType);
    }
    // ... existing code
}
```

### 6. Create MixinFunctionWithDelegates<T>

New class extending the function pattern to handle delegates:

```csharp
public class MixinFunctionWithDelegates<T> : AbstractFunction<T>
{
    private readonly IDictionary<string, IDelegate> _delegates;
    private readonly string _codeTemplate;

    public MixinFunctionWithDelegates(
        NodeContext nodeContext,
        string theFunction,
        string theCodeTemplate,
        ShaderNode<T> theDefault,
        string theMixin,
        IEnumerable<AbstractShaderNode> theArguments,
        IDictionary<string, IDelegate> theDelegates,
        bool theIsGroupable = false,
        IEnumerable<InputModifier> theModifiers = null)
        : base(nodeContext, theFunction, theArguments, theModifiers, theDefault, theIsGroupable)
    {
        _delegates = theDelegates;
        _codeTemplate = theCodeTemplate;

        // Add base mixin for inheritance
        AddProperty(Mixins, theMixin);

        // Build function with delegate substitution
        BuildFunction();
    }

    private void BuildFunction()
    {
        Functions = new Dictionary<string, string>();

        var functionValueMap = new Dictionary<string, string>();

        // Substitute delegate names
        foreach (var del in _delegates.Where(d => d.Value != null))
        {
            functionValueMap[del.Key] = del.Value.FunctionName;
            del.Value.Functions.ForEach(kv => Functions[kv.Key] = kv.Value);
            del.Value.PropertiesForTree().ForEach(kv => AddProperties(kv.Key, kv.Value));
        }

        // Process template
        var processedCode = ShaderNodesUtil.Evaluate(_codeTemplate, functionValueMap);
        Functions.Add(FunctionName, processedCode);
    }
}
```

### 7. MixinNodeFactory.cs - Add delegate-aware creation

```csharp
public static AbstractShaderNode CreateMixinFunctionWithDelegates(
    NodeContext nodeContext,
    MixinFunctionInfo functionInfo,
    IEnumerable<AbstractShaderNode> arguments,
    IDictionary<string, IDelegate> delegates)
{
    // Create MixinFunctionWithDelegates with appropriate return type
}
```

### 8. MixinShaderNodes.cs - CreateNodeInstance changes

In the node instance creation:
```csharp
// Separate delegates from regular arguments
var delegateInputs = new Dictionary<string, IDelegate>();
var regularArguments = new List<AbstractShaderNode>();

for (int i = 0; i < inputParamIndices.Count; i++)
{
    var param = func.Parameters[inputParamIndices[i]];
    var value = inputValues[i];

    if (param.IsDelegate)
    {
        delegateInputs[param.DelegateName] = value as IDelegate;
    }
    else
    {
        regularArguments.Add(value as AbstractShaderNode);
    }
}

// Create node with delegates
if (delegateInputs.Count > 0)
{
    resultNode = MixinNodeFactory.CreateMixinFunctionWithDelegates(
        nodeContext, func, regularArguments, delegateInputs);
}
else
{
    resultNode = MixinNodeFactory.CreateMixinFunctionDynamic(
        nodeContext, func, regularArguments);
}
```

## Files to Modify

1. `src/Fuse/MixinNodeFactory/MixinParameterInfo.cs` - Add delegate properties
2. `src/Fuse/MixinNodeFactory/MixinCommentParser.cs` - Parse @delegate annotation
3. `src/Fuse/MixinNodeFactory/MixinFunctionParser.cs` - Analyze delegate call sites in function body
4. `src/Fuse/MixinNodeFactory/MixinShaderNodes.cs` - Handle delegate pin type and node creation
5. `src/Fuse/MixinNodeFactory/MixinNodeFactory.cs` - Add delegate-aware factory method
6. `src/Fuse/Functions.cs` - Add `MixinFunctionWithDelegates<T>` class (or new file)

## Verification Steps

1. Create test SDSL with delegate parameter:
   ```hlsl
   // @export
   // @delegate func
   float applyTwice(float x)
   {
       return ${func}(${func}(x));
   }
   ```

2. Verify node appears in VL with `Delegate<float>` pin

3. Connect a Fuse `Delegate1In1Out<float, float>` node (e.g., from a lambda)

4. Verify shader compiles and executes correctly

## Important Technical Note: Template vs Mixin

**Problem**: Standard `MixinFunction` adds a mixin name that Stride compiles externally. The `${delegateName}` placeholders in the SDSL file won't be substituted because Stride compiles the file as-is.

**Solution**: For functions with delegates, we must treat the SDSL code as a **template** (like `CustomFunction`), not a compiled mixin:

1. Read the SDSL function body at runtime
2. Substitute `${delegateName}` → `delegate.FunctionName`
3. Add processed code to `Functions` dictionary (generates inline HLSL)
4. Merge delegate's function definitions
5. Still reference base mixin for inheritance if needed

This means functions with `@delegate` will use `CustomFunction`-style code generation rather than pure mixin references.

## Challenges & Considerations

1. **Signature inference complexity**: May need heuristics for complex expressions
2. **Multiple delegate calls**: Same delegate called with different apparent types
3. **Nested delegates**: Delegates that themselves take delegates
4. **Error reporting**: Clear errors when signature can't be inferred
5. **Pin type in VL**: Using base `Delegate<T>` means any compatible delegate works
6. **Code generation mode**: Delegate functions use template substitution, not mixin compilation

## References

- Existing delegate implementation: `src/Fuse/function/Delegate.cs`
- CustomFunction delegate handling: `src/Fuse/Functions.cs` (lines 155-240)
- FunctionParameter: `src/Fuse/function/FunctionParameter.cs`
- Invoke pattern: `src/Fuse/function/Invoke.cs`
