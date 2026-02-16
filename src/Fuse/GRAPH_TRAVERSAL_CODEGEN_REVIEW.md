# Graph Traversal + Codegen Review Notes

Date: 2026-02-14
Scope: `src/Fuse` graph traversal and shader source build pipeline

## Findings

### 1) `PrepareGraph` hook is not wired into the main compile path
- Relevant code:
  - `ShaderNode.cs:640` (`CallPrepareGraph()`)
  - `ShaderFX/AbstractToShaderFX.cs:111` (compile entry)
  - `ShaderFX/AbstractToShaderFX.cs:258` (`CompileProperties`)
  - `ShaderFX/AbstractToShaderFX.cs:277` (`BuildSourceCode`)
- Observation:
  - `CallPrepareGraph()` exists and recursively traverses inputs, but is not called from the shader generation pipeline.
- Risk:
  - If any node relies on `IPrepareGraph` side effects, behavior depends on manual invocation by callers.

### 2) `PropertiesTypedVisitor` can skip valid properties on the same node
- Relevant code:
  - `ShaderNode.cs:109` (`PropertiesTypedVisitor<TProperty>`)
  - `ShaderNode.cs:118` (`if (values.IsEmpty()) return;`)
  - `ShaderNode.cs:869` (`PropertiesForTree<TProperty>()` uses this visitor)
- Observation:
  - `return` exits `Visit` entirely when one property key has no values of `TProperty`.
  - This prevents scanning remaining property keys on that node.
- Risk:
  - Missing properties in aggregation APIs depending on `PropertiesTypedVisitor`.

### 3) Potential nondeterministic shader text ordering
- Relevant code:
  - `ShaderFX/AbstractToShaderFX.cs:42`, `:47`, `:52`, `:59` (property buckets use `HashSet<string>`)
  - `ShaderFX/AbstractToShaderFX.cs:192` onward (`BuildTemplateMap()` iterates sets)
  - `ShaderFX/AbstractToShaderFX.cs:144` (`ShaderName` derived from shader text hash)
- Observation:
  - Set iteration order may not be stable, which can change shader text ordering.
  - Shader name is hash-derived from text, so unstable ordering can produce unstable IDs/cache keys.
- Risk:
  - Cache churn or hard-to-reproduce differences across environments/runs.

### 4) Use-count comment and implementation drift
- Relevant code:
  - `ShaderNode.cs:256` comment says use-count is post-order
  - `ShaderNode.cs:820-821` implementation uses pre-order passes
- Observation:
  - Current logic works for simple edge-counting, but comment is inaccurate.
- Risk:
  - Maintenance confusion, not an immediate functional issue.

## Production Risk Assessment

- Overall: **Medium** if all changes are applied at once.
- Lowest-risk fix: **Finding #2** (`return` -> `continue`).
- Medium-risk: **Finding #3** (deterministic ordering) due to potential shader hash/ID changes.
- Medium-high risk: **Finding #1** (auto-calling `CallPrepareGraph`) due to possible behavior changes from side effects/timing.

## Suggested Rollout Order

1. Apply Finding #2 only, ship first.
2. Add deterministic ordering (Finding #3), preferably behind a feature flag.
3. Wire `CallPrepareGraph` (Finding #1) behind a feature flag and compare outputs in staging.

## Additional Opportunities (Speed + Simplification)

### A) Remove dead/unused code and no-op state
- Candidates:
  - `ShaderNode.cs:190` (`BuildSourceVisitor` is effectively empty and appears unused)
  - `ShaderNode.cs:691` (`GenerateSource(IEnumerable<AbstractShaderNode> theIns)` does not use `theIns`)
  - `ShaderNode.cs:814` (`_contexts.Add(context)` in `CompileProperties` currently has no effect)
- Risk:
  - **Low** when validated with build/tests.

### B) Trim avoidable allocations in hot paths
- Candidates:
  - `ShaderNode.cs:649` (`Ins.SequenceEqual(theIns)` can be expensive in large graphs; add cheap fast-path first)
  - Repeated wrapper `ToList()` calls around visitor results:
    - `ShaderNode.cs:830`
    - `ShaderNode.cs:842`
    - `ShaderNode.cs:849`
    - `ShaderNode.cs:856`
- Risk:
  - **Low to Medium** depending on exact refactor.

### C) Remove minor debug/perf leftovers
- Candidates:
  - `ShaderNode.cs:73` allocates `Stopwatch` in `PropertyOfTypeAndIdVisitor` but never uses elapsed time
  - `ShaderFX/AbstractToShaderFX.cs:71` and `:74` duplicate `_stages = theStages;`
- Risk:
  - **Low**.

### D) Simplify dictionary insertion patterns
- Candidates:
  - Replace `ContainsKey` + `Add` with `TryAdd` where semantics match:
    - `ShaderFX/AbstractToShaderFX.cs:242`
    - `ShaderNode.cs:98`
    - `ShaderNode.cs:120`
- Risk:
  - **Low**.

### E) Deterministic emission for reproducibility
- Candidate:
  - Sort collection outputs in `BuildTemplateMap()` before final shader text/hash:
    - `ShaderFX/AbstractToShaderFX.cs:192` onward
- Benefit:
  - Stable shader text and hash naming, easier cache/debug behavior.
- Risk:
  - **Medium** due to shader hash/name changes.

## Lowest-Risk First Batch

1. Remove dead/unused code and duplicate assignment (A + C).
2. Apply safe `TryAdd` simplifications (D).
3. Keep deterministic ordering (E) and `PrepareGraph` wiring for staged rollout.

## Mixin Factory Review Notes

Scope: `MixinNodeFactory/*` (parser -> metadata -> node factory -> runtime node instance)
Date: 2026-02-14

### MF-1) Inline defaults with commas are parsed incorrectly
- Relevant code:
  - `MixinNodeFactory/MixinFunctionParser.cs:29` (`ParameterPattern` uses `(?<default>[^,)]+)`)
  - `MixinNodeFactory/MixinFunctionParser.cs:184` (`SplitParameters` tracks only angle brackets)
- Observation:
  - Default expressions containing commas (for example vector constructors) can be truncated/split incorrectly.
- Impact:
  - Wrong parsed defaults, especially for expressions like `float3(1,2,3)`.
- Risk:
  - **Medium** correctness risk for affected signatures.

### MF-2) Array parameter fallback can become scalar when input is unconnected
- Relevant code:
  - `MixinNodeFactory/MixinShaderNodes.cs:541` (`GetPinType` maps arrays to `ShaderNode<GpuArray<T>>`)
  - `MixinNodeFactory/MixinShaderNodes.cs:583` (`CreateDefaultForParameter` uses scalar `ConstantHelper.AbstractFromObject`)
  - `MixinNodeFactory/MixinShaderNodes.cs:410` (fallback to `paramDefaults` for null inputs)
- Observation:
  - For array parameters, the default fallback path may not produce an array-typed node.
- Impact:
  - Potential type mismatch/runtime failures when array pins are left unconnected.
- Risk:
  - **Medium-High** correctness risk for array-based mixins.

### MF-3) Unknown return types silently fallback to float
- Relevant code:
  - `MixinNodeFactory/MixinNodeFactory.cs:188` (`CreateMixinFunctionDynamic` default branch)
- Observation:
  - Unrecognized return types are coerced to `MixinFunction<float>` instead of failing explicitly.
- Impact:
  - Silent mis-typing and confusing downstream behavior.
- Risk:
  - **Medium** diagnosability/correctness risk.

### MF-4) Reflection-based access for optional outputs
- Relevant code:
  - `MixinNodeFactory/MixinShaderNodes.cs:449` (`GetProperty("OptionalOutputs")`)
- Observation:
  - Uses reflection per output access path.
- Impact:
  - Added overhead and brittleness (renames/signature changes).
- Risk:
  - **Low-Medium** performance/maintainability risk (not primary correctness issue).

### MF-5) Test coverage gaps for edge cases
- Relevant code:
  - `..\..\PatchTests\MixinNodeFactoryTests.cs`
- Observation:
  - Existing tests cover many basics (in/out/inout, resources) but not array pin fallback behavior and comma-containing inline defaults.
- Impact:
  - Regressions in these areas may slip through.
- Risk:
  - **Medium**.

## Mixin Factory Suggested Rollout

1. Add tests first for:
   - Inline defaults with comma expressions
   - Array parameter with unconnected input fallback
2. Change unknown return-type handling from silent float fallback to explicit failure/logging.
3. Fix parser splitting/default capture logic for parentheses-aware defaults.
4. Fix array fallback node creation to emit array-typed defaults.
5. Optionally optimize/remove reflection-based optional output access after correctness fixes.

## Delegate Extension Strategy (Mixin Factory + Functional Composition)

Date: 2026-02-14
Goal: Enable higher-order shader composition (e.g., `Raymarch(SdfFn)`, `Fbm(NoiseFn)`) via factory-generated nodes.

### Current Capability Assessment

- `CustomFunction<T>` already supports delegate injection:
  - Accepts `IDictionary<string, IDelegate>` in ctor (`Functions.cs:168`)
  - Injects delegate function names into template map (`Functions.cs:211`, `Functions.cs:228`)
  - Merges delegate functions/properties (`Functions.cs:229`, `Functions.cs:230`)
  - Propagates context to delegates (`Functions.cs:234`)
- Current mixin factory path does **not** use this:
  - Dynamic mixin nodes are created as `MixinFunction<T>` (`MixinNodeFactory/MixinNodeFactory.cs:103`)
  - Node factory inputs are currently `AbstractShaderNode`-based (`MixinNodeFactory/MixinShaderNodes.cs:374`)

### Recommended Extension Path

1. Add delegate metadata directives to mixin comments.
   - Example concept: `@delegate noise float(float3 p)` / `@delegate sdf float(float3 p)`
   - Parse in `MixinCommentParser` and store in `MixinMetadata`.

2. Extend function model with delegate parameter descriptors.
   - Add delegate parameter collection to `MixinFunctionInfo`.
   - Keep existing value/resource parameters unchanged.

3. Expose delegate pins in VL node factory.
   - In `CreateNodeImplementation`, add inputs for `IDelegateProvider` for delegate params.
   - Retain existing `AbstractShaderNode` inputs for regular params.

4. Route delegate-enabled nodes through `CustomFunction<T>`.
   - For functions with delegate params: generate call template and instantiate `CustomFunction<T>`.
   - Build delegate dictionary as `{ paramName -> provider.GetDelegate() }`.
   - For functions without delegates: keep existing `MixinFunction<T>` path.

5. Roll out by concrete signatures first.
   - Start with `NoiseFn` + `Fbm(NoiseFn, ...)`.
   - Then `SdfFn` + `Raymarch(SdfFn, ...)`.

### Design Note

- This is compile-time composition (delegate-expanded shader code), not runtime function pointers.
- That model is appropriate for shader generation and matches existing architecture.
