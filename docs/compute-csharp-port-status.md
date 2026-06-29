# Fuse Compute C# Port Status

This is the short working status for the C# conversion of the VL compute
system. The detailed node-by-node notes remain in
`docs/compute-vl-csharp-mapping.md`.

## Baseline

Source of truth:

- `vl/Fuse.Compute.vl`
- help patches under `help/Compute System`
- Stride/VL runtime behavior from `D:\development\vl\VL.StandardLibs-main`

Current C# goal:

- keep the public operation names close to the VL patch
- convert the patch behavior incrementally into C#
- keep production `Fuse.dll` compatible while the C# compute replacement is
  isolated in `src/Fuse.Compute`
- keep C#-only tests runnable without vvvv for most graph/resource logic
- add DirectX preflight checks where the original patch otherwise fails only in
  the runtime/debug layer

## Project Boundary

The C# compute replacement now lives in its own project:

- `src/Fuse.Compute/Fuse.Compute.csproj`
- output assembly: `lib/net8.0/Fuse.Compute.dll`
- current explicit VL imports:
  `ComputeGraph`, `ComputeGraph1D`, `ComputeGraph2D`, `ComputeGraph3D`,
  `ComputeStage`, `ComputeStageGroupSpectral`, `ComputeStageGroup`,
  `ComputeSystemSpectral`, `ComputeSystem`,
  `Buffer1DDispatchInfo`, `StructuredBufferResourceDispatchInfo`,
  `TextureDispatchInfo`, `StructuredBufferResource`, `TextureResource`, and
  `ToComputeStage`, plus the texture operators `Average` and
  `Laplace2D (8 Karl Sims)`

This is not a wrapper/facade project. The moved files are the real replacement
classes for the compute system:

- `ComputeStage`, `ComputeStageGroup`
- `ComputeSystem`, `ToComputeStage`
- `ComputeGraph`, `ComputeGraph1D`, `ComputeGraph2D`, `ComputeGraph3D`
- `StructuredBufferResource`, `TextureResource`
- dispatch, execution-plan, renderer-scheduler, and resource-description
  support

The existing production `Fuse.dll` keeps the legacy/core compute types that the
current VL patches already reference, for example `StructuredBufferAttribute<T>`,
`TextureAttribute<T>`, and `DynamicIndex`. `DynamicIndex` was tested in
`Fuse.Compute`, but `vl/Fuse.Compute.vl` still resolves it through
`Fuse.dll/Fuse.compute.DynamicIndex`, so it remains in the core assembly until
those VL references are migrated.

`AttributeMap`, `IAttributeLayout`, and `PaddingAttribute` live in
`Fuse.Compute`, because the C# replacement resources and their tests are now
their only C# consumers.

The migration path is to move further real replacement behavior into
`Fuse.Compute` while only touching `Fuse.dll` where old production classes need
small compatibility/support changes.

## Current Verified Surface

The current headless lane covers:

- `ComputeGraph`, `ComputeGraph1D`, `ComputeGraph2D`, `ComputeGraph3D`
- `ComputeStage`
- `ComputeStageGroup`
- `ComputeSystemSpectral` and `ComputeSystem`
- `ToComputeStage`
- `Buffer1DDispatchInfo`, `StructuredBufferResourceDispatchInfo`,
  `TextureDispatchInfo`
- `StructuredBufferResource`
- `TextureResource`
- generated shader compile checks through Stride's standalone effect compiler
- DirectX-facing preflight checks for dispatch counts, texture dimensions,
  texture UAV formats, structured-buffer stride, buffer element count, and
  buffer size overflow
- patch-shaped `StructuredBufferResource` getters for struct, stride, buffer
  input, semantics, vertex declaration, resource name/type, struct instance,
  and reset/ticket behavior
- patch-shaped `TextureResource` getters for dimension, resource name/type,
  compute resource, texture statuses, last texture status, and reset/ticket
  behavior
- patch-shaped `IComputeStage` operations for name/resource access plus
  deterministic stage/group dispose behavior that clears stage state without
  taking ownership of compute resources
- patch-shaped `ComputeSystem (Spectral Advanced).Dispose` behavior that clears
  stages, hooks, scheduler references, lifecycle state, and resource lists
  without taking ownership of compute resources
- optional `TextureInput` names that keep the default hash-based IDs when unset
  and generate readable resource/attribute/slot names when supplied by
  `TextureResource`; unnamed texture resources use attribute/dimension/slot
  fallback names so parallel resources with the same attribute do not collide
- shared texture-neighborhood infrastructure:
  `TextureNeighborhoodNode<TIndex,T>` centralizes texture binding, index
  dimensionality, nested radius-loop emission, unrolled fixed-offset emission,
  and runtime offset-buffer loop emission. Public VL nodes stay concrete and
  patch-named; `Average` and `Laplace2D (8 Karl Sims)` derive from this base.
- help-patch-derived texture compute fixtures for WriteToTexture, Average,
  GameOfLife, and ReactionDiffusion formula-backed shader generation.
  WriteToTexture now uses the patch's unnamed stage/resource shape for a 2D
  double-buffered and a 3D single-buffered `NoiseData` resource, and
  compile-checks both generated shaders. Average now ports the production
  `Average<TIndex,T>` texture operator used by
  `help/Compute System/Texture/HowTo Use Average.vl` and compile-checks a
  patch-shaped `NoiseData -> Average -> SampleData` shader. ReactionDiffusion
  now uses the production `Laplace2D (8 Karl Sims)` operator in its enabled
  20-iteration update stage while keeping the remaining reaction formula
  test-local.
- compute-system port inventory in `docs/compute-system-port-inventory.md`,
  generated from `vl/Fuse.Compute.vl` and `help/Compute System/**/*.vl`
- explicit ProcessNode annotations for the concrete patch-facing C# classes:
  `ComputeGraph`, `ComputeGraph1D`, `ComputeGraph2D`, `ComputeGraph3D`,
  `ComputeStage`, `Group (ComputeStage,Spectral)`, `Group (ComputeStage)`,
  `ComputeSystem (Spectral Advanced)`, `ComputeSystem`,
  `Buffer1DDispatchInfo`, `StructuredBufferResourceDispatchInfo`,
  `TextureDispatchInfo`, `StructuredBufferResource`, `TextureResource`,
  `ToComputeStage`, `Average`, and `Laplace2D (8 Karl Sims)`
- patch-shaped resource binding ProcessNode fragments now expose `Read` and
  `Write` through explicit `out` pins on `BindComputeStage`, instead of only
  returning the internal C# bindings object
- patch-shaped `Update` ProcessNode fragments now expose the expected outputs:
  `ComputeSystem (Spectral Advanced)` returns `Global Attributes` and
  `Has Changed`, while `ComputeSystem` returns `Has Changed`
- patch-shaped dispatch-info `Split` ProcessNode fragments now expose
  `PreRenderCommand`, `Dispatcher`, `Thread Group Size`, and
  `Skip Outside Range` as explicit out pins for buffer, structured-buffer, and
  texture dispatch info
- patch-shaped resource ProcessNodes keep a visible instance `Output`; getters
  remain callable on that instance instead of being duplicated as process
  fragments. The explicit resource fragments focus on lifecycle/state changes:
  `HandleAttribute`, `FinishAttributeMap`, `UpdateBuffer`, `Reset`,
  `BindComputeStage`, `BindAttributes`, `SyncAttributes`, and texture
  `SwapTextures`.
- current ProcessNode surface rule is captured in
  `docs/compute-process-node-surface.md`

Latest verified commands:

```powershell
dotnet build PatchTests\PatchTests.csproj
dotnet test PatchTests\PatchTests.csproj --no-build --filter "TestCategory=FuseComputeCore" --logger "console;verbosity=minimal"
```

Latest result:

- build passed
- `TextureResourceStateTests`: 22 passed
- `ComputeStageTests|ComputeSystemTests`: 84 passed
- `ComputeSystemTests`: 56 passed
- texture help-patch fixtures: 4 passed
- `TextureNeighborhoodTests`: 5 passed
- `FuseComputeCore`: 240 passed, 1 skipped

Inventory:

- `docs/compute-system-port-inventory.md`
- `artifacts/compute-system-vl-node-inventory.csv`
- `artifacts/compute-system-vl-node-summary.csv`
- `artifacts/compute-system-csharp-coverage-rough.csv`

## Patch-Close Areas

These areas are intentionally close to the VL patch shape:

- resource lifecycle:
  `Prepare -> HandleAttribute -> FinishAttributeMap/Finish -> UpdateStruct`
- stage binding:
  `BindComputeStage -> BindAttributes -> CreateRead/CreateWrite -> SyncAttributes`
- draw-command order:
  `IterationIndexSet -> PreRenderCommand -> TextureResourceUpdate -> Dispatch -> PostDispatchGraph`
- stage and system naming:
  `ComputeStage`, `Group (ComputeStage)`, `ComputeSystem (Spectral)`,
  `ComputeSystem`, `ToComputeStage`
- resource slots:
  structured-buffer attribute map and struct description;
  texture A/B inputs, A/B textures, texture swap
- dispatcher split shape:
  dispatch groups, thread-group size, diagnostics, dispatcher, skip-outside-range

## Intentional C# Safety Layer

These parts are not literal VL nodes. They are C# guard rails around the port:

- `ComputeDispatchValidator`
- `ComputeResourceLimits`
- resource-description validation for DirectX limits
- `ComputeExecutionPlan` / `ComputeDrawResult`
- `ComputeDispatchExecutor`
- texture-resource failure policy
- standalone shader compiler test utility

Reason:

- they make DirectX/debug-layer failures deterministic in C# tests
- they prevent invalid dispatch/resource states from reaching Stride/D3D11
- they keep automation useful without requiring vvvv for every test

## Known Deviations And Risks

1. `ComputeStageDefinition` and `ComputeSystemDefinition` have been removed.
   The intended public surface is `ComputeStage` and
   `ComputeSystemSpectral`/`ComputeSystem`.

2. `ComputeExecutionPlan` is a helper, not a VL patch concept. Tests should use
   it as evidence, but the public behavior should still be expressed through
   `Draw`, `DrawStage`, `BuildComputeGraph`, and resource operations.

3. Renderer integration is not fully proven in vvvv. Headless tests exercise
   command order and fake dispatchers, but real `RenderDrawContext`,
   `GraphicsDevice`, and scheduler behavior still need a vvvv/runtime lane.

4. Real Stride resource allocation is only partially covered. Headless tests
   verify descriptions and failure statuses; successful `Texture.New` and
   `BufferExtensions.New` with a real `GraphicsDevice` are not yet covered.

5. HLSL equivalence is compile-checked, not yet compared against generated
   output from representative original VL patches.

6. Some C# names are deliberately non-identical where VL category names could
   collide, for example `IComputeChangeGraph` instead of `IChangeGraph`.

## Next Recommended Slice

The next slice should be a cleanup/alignment slice, not another feature slice:

1. Audit public methods against the VL operation list and add missing no-op or
   deterministic operations only when the VL patch exposes them.
2. Add a small shader-snapshot fixture from one real help patch, preferably:
   `help/Compute System/Texture/HowTo Reaction Diffusion.vl`.
3. After that, add a real vvvv/runtime test lane for resource allocation and
   debug-layer-facing dispatch behavior.

## Immediate Review Checklist

Before committing the current port slice:

- verify all untracked compute files belong to this slice
- keep generated `lib/net8.0/*.dll` changes only if this repo convention expects
  built binaries to be committed
- review `PatchTests/PatchTests.cs` separately because it now shares the common
  shader compiler utility
- confirm that all new public classes are intended package surface
- confirm that DirectX guard rails are acceptable as stricter C# behavior around
  the VL patch
