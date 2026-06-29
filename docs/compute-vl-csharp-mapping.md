# Fuse Compute VL to C# Mapping

This document treats `vl/Fuse.Compute.vl` as the behavioral source of truth for
the C# compute-system conversion.

## Current Finding

The first C# slices built a headless, testable core first:

- dispatch validation
- structured-buffer resource state
- structured-buffer read/write binding
- compute stage/system definitions
- execution planning

`D:\development\vl\VL.StandardLibs-main` is the local reference for the
Stride-facing resource layer. The most relevant source anchors are:

- `VL.Stride.Runtime/src/Graphics/BufferExtensions.cs`
  - `BufferExtensions.New(GraphicsDevice, BufferDescription,
    BufferViewDescription, IntPtr)`
  - `CombineWithStructuredBufferTypeFlag`
- `VL.Stride.Runtime/src/Graphics/GraphicsNodes.BufferBuilder.cs`
  - `BufferBuilder` rebuilds buffers from `BufferDescription`,
    `BufferViewDescription`, and pinned `IGraphicsDataProvider`
- `VL.Stride.Runtime/src/Graphics/GraphicsNodes.TextureBuilder.cs`
  - `TextureBuilder` rebuilds textures from `TextureDescription`,
    `TextureViewDescription`, and optional pinned data boxes
- `VL.Stride.Runtime/src/Rendering/Effects/ComputeEffect/VLComputeEffectShader.cs`
  - compute dispatch applies the effect, dispatches, then unsets UAVs for
    `Buffer` and `Texture` parameters whose `ViewFlags` contain
    `UnorderedAccess`
- `VL.Stride.Runtime/src/Rendering/Effects/ComputeEffect/DirectComputeEffectDispatcher.cs`
  - direct dispatch validates the D3D11 65535 groups-per-dimension limit at the
    VL node level
- `src/Fuse.IO/Ply/PlyGpuData.cs`
  - existing local Fuse.IO reference for creating Stride buffers from
    `BufferDescription`, `BufferViewDescription`, and pinned CPU memory

That is useful test infrastructure, but it drifts from the VL patch shape. The
VL patch exposes behavior mainly as operations on `ComputeStage`,
`StructuredBufferResource`, `ComputeSystem (Spectral)`, `Group (ComputeStage,Spectral)`,
and provider interfaces. The C# API should follow that shape more closely.

## VL Nodes and Required C# Shape

### DynamicIndex (native)

VL role:

- native wrapper over `Fuse.compute.DynamicIndex`
- uses `IIndexProvider`
- write/read index is later switched by compute stages

Current C#:

- `DynamicIndex`
- `IIndexProvider`
- `DispatchIdIndexProvider`
- `VertexIdIndexProvider`
- `DispatchThreadId`, `DispatchThreadIdX`, `VertexId`

Status:

- close enough
- headless fixes are useful and should stay

Correction:

- Keep public `DynamicIndex`.
- Keep dedicated compute stream nodes.
- Do not introduce another index abstraction unless VL has it.

### GlobalAttribute / IterationIndex (Global)

VL role:

- `GlobalAttribute<T>` wraps a temporary attribute by name
- `IterationIndex (Global)` is a `GlobalAttribute<int>` named
  `IterationIndex`
- `DrawStage` writes the current iteration into that global attribute before
  dispatch

Current C#:

- `GlobalAttribute<T>`
- `IterationIndexGlobal`
- `ComputeDispatchCommand.IterationIndexGlobal`
- `ComputeDispatchCommand.IterationIndexSet`
- `ComputeDispatchCommand.IterationIndexGraph`
- `ComputeDispatchCommand.IterationIndexValue`
- `ComputeDispatchExecutor`

Status:

- headless graph/value shape is now represented
- each dispatch command carries the current iteration value as a
  `ConstantValue<int>`
- the VL `Value Set` step is represented by `GlobalAttributeSet<int>`, which
  updates the target global attribute and returns its graph
- `ComputeDispatchExecutor` executes command steps in patch order:
  `IterationIndexSet -> PreRenderCommand -> TextureResourceUpdate -> Dispatch
  -> PostDispatchGraph`
- without a `RenderDrawContext`, dispatch is skipped with an explicit reason
  while all prior headless steps still run
- `PreRenderCommand` is now typed as `Stride.Rendering.IGraphicsRendererBase`
  instead of `object`, which is the visible C# renderer interface matching the
  VL `IRenderer.Draw(Context)` operation closely enough for tests
- when a `RenderDrawContext` is available, `ComputeDispatchExecutor` calls
  `PreRenderCommand.Draw(context)` before `Dispatcher.Dispatch(context)`
- before dispatch, `TextureResourceUpdate` calls
  `TextureResource.UpdateTextures(context.GraphicsDevice, ...)` for texture
  resource stages so Stride textures are created or refreshed in the draw
  context
- after a successful dispatch, `PostDispatchGraph` executes patch-named
  `TextureSwap` nodes found in the stage compute graph; if dispatch is skipped,
  the resource-side texture A/B swap is skipped too
- `ComputeStage.DrawStage(RenderDrawContext, ...)` is now the stage-level
  render-context entry point and routes through the same patch-order steps
- `ComputeSystemSpectral.Draw(RenderDrawContext, ...)` is now the system-level
  render-context entry point; `Execute(...)` is only a C# convenience alias
- `ComputeDrawResult.Execute(RenderDrawContext)` and
  `ComputeSystemSpectral.Execute(RenderDrawContext, ...)` now route the same
  command list into the executor
- `ComputeDispatchRenderer` derives from `VL.Stride.Rendering.RendererBase` and
  executes an assigned draw result from `DrawInternal(RenderDrawContext)`, so
  the C# side now has a render-graph entry point for real Stride dispatch
- constructing that renderer requires a live VL/vvvv `AppHost`; headless tests
  use `TryCreateRenderer` to make that boundary explicit
- `Attribute<T>` no longer requires a live VL `AppHost` when created with a
  null `NodeContext`

Correction:

- Next step is to decide how this renderer should be surfaced from the VL-facing
  API and how the existing `PreRenderCommand` split output should be populated.

### Buffer1DDispatchInfo

VL operations:

- `Create`
- `Split`
  - `PreRenderCommand`
  - `Dispatcher`
  - `Thread Group Size`
  - `Skip Outside Range`
- `Update`
- `GetCount`
  - `Output`
  - `Count GPU`
- `SetElementCount`
- `SetThreadGroupSize`

Current C#:

- `Buffer1DDispatchInfo`
- `IDispatchInfo.Split()` returns `DispatchGroups`, `ThreadGroupSize`,
  diagnostics, validity, `PreRenderCommand`, `Dispatcher`, and
  `SkipOutsideRange`
- `DirectDispatcher` implements the VL-shaped
  `IDispatcherProvider.GetDispatcher()` and `IDispatcher.GetDispatchInfo()`
  path for direct dispatch infos

Status:

- split shape is now closer to VL
- dispatcher-provider path is now modeled before `Split()`
- C# validates DirectX limits, which VL currently does not expose explicitly
- `Dispatcher` now returns the original VL.Stride.Runtime
  `DirectComputeEffectDispatcher` through the public
  `IComputeEffectDispatcher` interface
- `PreRenderCommand` is still a headless placeholder because
  `Stride.API.Rendering.IRenderer` is not visible as a normal C# runtime type
  in the current test assembly set
- the placeholder now uses `Stride.Rendering.IGraphicsRendererBase`, while the
  exact VL `Stride.API.Rendering.IRenderer` type remains unavailable in the
  headless C# assembly set

Correction:

- Keep validation diagnostics.
- Continue wiring real pre-render command objects into the existing split field
  via `IGraphicsRendererBase`.
- Treat diagnostics as C# safety layer, not VL replacement.

### StructuredBufferResourceDispatchInfo

VL operations:

- `Create`
- `Split`
  - `PreRenderCommand`
  - `Dispatcher`
  - `Thread Group Size`
  - `Skip Outside Range`
- `Update`
- `GetCount`
  - `Output`
  - `Count GPU`
- `SetResource`
- `SetThreadGroupSize`
- `GetElementCount`

Current C#:

- `StructuredBufferResourceDispatchInfo`
- reads `ElementCount` from `IStructuredBufferResourceInfo`

Status:

- partly aligned
- same direct-dispatcher binding as `Buffer1DDispatchInfo`

Correction:

- Align `Split()` API with VL naming and outputs.
- Keep `GetElementCount()`.

### StructuredBufferResource

VL operations:

- `Create(Node Context, Name)`
- `HandleAttribute(Attribute)`
- `Finish`
- `Prepare`
- `UpdateBuffer(Element Count, Recreate)`
- `GetBuffer`
- `BindComputeStage(Attribute Map, Post Graph Renderer) -> Read, Write`
- `FinishAttributeMap`
- `UpdateStruct`
- `Update`
- `GetAttributeMap`
- `GetVertexDeclaration`
- `BindAttribute`
- `SetElementCount`
- `GetElementCount`
- `GetStride`
- `GetStruct`
- `GetBufferInput`
- `GetSemantics`
- `BindAttributes`
- `CreateRead`
- `CreateWrite`
- `SyncAttributes`
- `GetName`
- `GetAttributeType`
- `GetSize`
- `GetDispatchInfo`
- `GetComputeResource`
- `GetStructInstance`
- `Reset`
- `GetTicket`
- `GetStructSize`
- `GetResourceSize`
- `SetDispatchGroupSize`

Current C#:

- `StructuredBufferResource`
- `StructuredBufferResourceBindings`
- `ComputeResourceDescriptions.StructuredBuffer(...)`
- `StructuredBufferResource.GetBufferDescription(...)`
- `StructuredBufferResource.UpdateBuffer(...)`
- patch-named lifecycle operations:
  `Prepare`, `HandleAttribute`, `FinishAttributeMap`, `Finish`,
  `UpdateStruct`, `BindAttributes`
- `CreateReadWriteBindings(...)`
- struct description generation
- attribute map padding
- basic compute resource and dispatch info

Status:

- core state and binding are useful
- public API is closer to VL for the attribute-map and binding path
- `BindComputeStage` is now the main resource operation; `ComputeStage` can be
  created from a structured buffer, but the binding role lives on the resource
  side as in VL
- `FinishAttributeMap` only finishes the map and bumps the resource ticket on
  changes; `UpdateStruct` remains a separate operation, matching the separate
  VL nodes
- `BindAttributes` routes to the existing read/write binding graph so the
  patch operation name now preserves the generated shader output
- binding now preserves the VL `WriteAttributes` switch: read bindings are still
  created, but per-attribute write calls, `WriteValue`, and the final buffer
  write are omitted when writing is disabled
- buffer descriptions now use the StandardLibs-compatible shape:
  `ShaderResource | StructuredBuffer | UnorderedAccess`, `PixelFormat.None`,
  `GraphicsResourceUsage.Default`, and `StructureByteStride` from the resource
  struct size
- `UpdateBuffer(...)` now follows the patch operation name and can allocate the
  Stride buffer when a `GraphicsDevice` is available; headless tests still run
  the same operation and verify the generated description plus explicit missing
  device status

Correction:

- Keep `CreateReadWriteBindings(...)` as internal/helper or make it clearly
  subordinate to `BindComputeStage`.
- Add missing lifecycle methods as no-op or deterministic placeholders only when
  they correspond to VL operations:
  - `Prepare`
  - `Finish`
  - `Reset`
  - `GetTicket`
  - `UpdateBuffer`
  - `UpdateStruct`
- Keep C# tests headless.

### TextureDispatchInfo

VL operations:

- `Create`
- `Update(Dimension, Thread Group Size)`
- `Split`
  - `PreRenderCommand`
  - `Dispatcher`
  - `Thread Group Size`
  - `Skip Outside Range`
- `GetThreadGroupInfo`
- `GetCount`
- `SetDimension`

Current C#:

- `TextureDispatchInfo`
- stores texture dimension and thread-group size
- validates dispatch groups through the existing DirectX limit checker
- `Split()` returns a direct dispatcher when valid and marks
  `SkipOutsideRange`

Status:

- first headless conversion is in place
- no GPU texture allocation is involved
- default thread-group size is represented as a `ComputeDispatchSize`, because
  the C# dispatch-info contract already uses a 3D size

Correction:

- Verify exact VL default thread-group semantics before wiring real shader
  thread-group declarations.

### TextureResource

VL operations:

- `Create`
- `HandleAttribute`
- `Finish`
- `Prepare`
- `GetTextures`
- `BindAttributes`
- `FinishAttributeMap`
- `UpdateTextures`
- `GetAttributeMap`
- `BindAttribute`
- `SetDimension`
- `GetDimension`
- `ReadCall`
- `WriteCall`
- `GetTexture`
- `SwapTextures`
- `BindComputeStage`
- `SyncAttributes`
- `GetName`
- `GetSize`
- `GetAttributeType`
- `GetDispatchInfo`
- `GetResource`
- `GetComputeResource`

Current C#:

- `TextureResource`
- `ComputeResourceDescriptions.Texture(...)`
- `TextureResource.GetTextureDescription(...)`
- `TextureResource.UpdateTextures(...)`
- patch-shaped slots:
  - `Name`
  - `Size`
  - `AttributeMap`
  - `TextureAInputs`
  - `TextureBInputs`
  - `TextureAs`
  - `TextureBs`
  - `DispatchInfo`
- patch-shaped lifecycle methods:
  `Prepare`, `HandleAttribute`, `FinishAttributeMap`, `Finish`,
  `BindAttributes`, `BindAttribute`, `SetDimension`, `GetTexture`,
  `SwapTextures`, `ReadCall`, `WriteCall`, `CreateRead`, `CreateWrite`,
  `SyncAttributes`
- `ComputeSystemSpectral.HandleAttributes` routes `AttributeType.Texture`
  attributes to a stage/main `TextureResource` instead of treating them as
  global attributes
- `ComputeStage.BuildComputeGraph` lets a texture resource bind its attributes
  without replacing the stage graph

Status:

- first resource-side conversion is in place and covered by headless tests
- texture attributes now get assigned `TextureInput`s from the resource
- double-buffered attributes create A/B texture inputs and can be swapped
- `TextureDispatchInfo` is attached to the resource and contributes the
  resource dispatch size
- texture descriptions now use the StandardLibs-compatible shape:
  `ShaderResource | UnorderedAccess`, `GraphicsResourceUsage.Default`,
  `ViewType.Full`, and dimension selected from resource size
- texture descriptions now reject `PixelFormat.None` early and have focused
  tests for both UAV/SRV and shader-resource-only flag combinations
- texture descriptions now reject typed UAV formats outside the D3D typed-UAV
  format set used by the current Fuse `TypeHelpers` mapping; for example,
  `R32G32B32_Float` is allowed for SRV-only textures but rejected for
  unordered-access textures
- device-specific support checks for optional typed UAV formats are still a
  later render-context task; the current guard prevents clearly unsupported
  formats before Stride/DirectX resource creation
- `UpdateTextures(...)` catches texture-description failures per slot and
  records `Failed:Description:...` status entries while still creating the
  patch-shaped texture inputs; direct description API calls remain strict
- `TextureResourceUpdate` execution now captures a snapshot payload
  (`TextureResourceUpdateResult`) and exposes statuses directly on
  `ComputeDispatchExecutionResult.TextureResourceStatuses`, so automated tests
  can inspect resource-slot failures without reaching back into the resource
- texture-resource update failures now block the following dispatch step and
  report the failed slots in the dispatch-step reason; because dispatch is not
  executed, post-dispatch graph actions such as `TextureSwap` are skipped too
- `ComputeDispatchExecutor.TextureResourceFailurePolicy` keeps
  `BlockAllFailures` as the default, with an explicit
  `AllowMissingGraphicsDevice` mode for headless diagnostics that should ignore
  only `Failed:GraphicsDevice=null` while still blocking description and
  resource-creation failures
- the texture-resource failure policy is now propagated through
  `ComputeDrawResult.Execute(...)`, `ComputeSystemSpectral.Draw/Execute(...)`,
  stage/group `DrawStage(...)`, and `ComputeDispatchRenderer`, so callers do
  not need to construct `ComputeDispatchExecutor` manually to choose the policy
- `UpdateTextures(...)` now follows the patch operation name, chooses texture
  pixel format from the attribute shader node via the existing Fuse type
  helpers, creates A/B texture inputs for double-buffered attributes, and can
  allocate Stride textures when a `GraphicsDevice` is available
- without a `GraphicsDevice`, `UpdateTextures(...)` still builds the same
  inputs and records explicit per-slot status strings, so the operation is
  covered by headless tests
- `ReadCall(...)` and `WriteCall(...)` now follow the separate VL operations and
  create typed `ComputeTextureGet<TIndex,T>` and
  `ComputeTextureSet<TIndex,T>` shader nodes from the texture attribute,
  texture input, and read/write index
- the typed call creation remains headless-friendly by creating null-context
  shader nodes when no VL `AppHost` is installed; with a real node context it
  still uses sub-contexts for stable graph identity
- the VL `WriteCall` overlay switches between `texture A inputs` and
  `texture B inputs` using `ITextureAttribute.DoubleBuffered`; the C# behavior
  now has a focused test for the single-buffered case where both read and write
  calls use texture A and no texture B input is created
- `CreateRead(...)` now groups the current attribute `ReadCall`s into a
  `TextureResourceRead` `Group`
- `CreateWrite(...)` now follows the patch's `Util.Writes(attribute) > 0`
  condition with `ShaderNode.WriteCounter`, groups only written attributes,
  appends `TextureSwap` for double-buffered texture attributes, and preserves
  the optional post-graph renderer at the end of the write group
- `TextureSwap` is represented as a patch-named C# shader node with an explicit
  `Execute()` method; its shader source is empty because the VL class performs
  a resource-side swap during draw rather than emitting HLSL
- `SwapTextures(...)` now updates all texture-attribute instances for the
  swapped key, not only the canonical `AttributeSet` entry, which is required
  for grouped help patches where several stages use the same texture attribute
  name
- `ComputeDispatchExecutor` now executes `TextureSwap` after a successful
  dispatch through the `PostDispatchGraph` step, matching the VL
  `TextureSwap.Draw` position after the write pass
- `ComputeDispatchExecutor` now updates a stage `TextureResource` before
  dispatch via `TextureResource.UpdateTextures(context.GraphicsDevice, ...)`;
  headless tests keep the same explicit `GraphicsDevice=null` status evidence
- `BindComputeStage(...)` now exists on `TextureResource` and routes the patch
  sequence through `BindAttributes -> CreateRead -> CreateWrite ->
  SyncAttributes`; `ComputeStage.BuildComputeGraph()` uses that operation and
  assigns the texture resource's `WriteGroup` as the stage compute graph
- `ComputeTextureStageFixture_RunsPatchOrderHeadlessAndSnapshotsResourceState`
  now covers the composed C#-only path from `TextureResource` through
  `ComputeStage.BuildComputeGraph`, `ComputeExecutionPlan`, dispatch-command
  creation, texture-resource update, dispatch, and post-dispatch `TextureSwap`
- `ComputeTextureHelpFixture_WriteToTextureRunsTwoTextureStages` is derived
  from `help/Compute System/Texture/HowTo Write To Texture.vl`: it covers one
  2D double-buffered texture stage and one 3D single-buffered texture stage in
  the same `ComputeSystemSpectral`, including different dispatch dimensions
  and the fact that only the double-buffered stage produces a post-dispatch
  `TextureSwap`. The fixture now keeps the patch's unnamed `ComputeStage` and
  `TextureResource` shape, while still proving both generated shaders compile
  and their texture declarations stay unique through
  `NoiseData_64x64x1_A/B` and `NoiseData_64x64x64_A` fallback names.
- `Average<TIndex,T>` is now ported as the production
  `Fuse.Compute.Texture/Average` operator. It follows the VL patch shape:
  neighborhood offsets are accumulated around the supplied texture index and
  divided by the sampled count. The node resolves bound `TextureAttribute`s to
  their `TextureInput`s before shader property collection, so generated texture
  declarations are present in headless shader compilation.
- `ComputeTextureHelpFixture_AverageBuildsAndCompilesPatchShapedTextureStage`
  is derived from `help/Compute System/Texture/HowTo Use Average.vl`: it covers
  one unnamed 64x64x1 `TextureResource`, double-buffered `NoiseData` and
  `SampleData` attributes, `DynamicIndex -> xy` indexing, and a generated
  `NoiseData -> Average -> SampleData` shader that compiles through Stride's
  standalone effect compiler.
- `ComputeTextureHelpFixture_GameOfLifeGroupsThreeStagesOnSharedTextureResource`
  is derived from `help/Compute System/Texture/HowTo Game of Life.vl`: it
  covers `Group (ComputeStage)` with three child stages sharing one
  double-buffered `TextureResource`, repeated `CellData` texture-attribute
  instances, and one dispatch/swap per enabled child stage
- `ComputeTextureHelpFixture_ReactionDiffusionBuildsAndCompilesTwoStageTextureSystem`
  is derived from `help/Compute System/Texture/HowTo Reaction Diffusion.vl`:
  it covers one 1024x1024 double-buffered `ReactionData` `TextureResource`,
  two grouped texture stages, the patch's disabled bang-style initialize stage
  with a Step-based rectangle seed shader, a 20-iteration enabled update stage,
  generated `RWTexture2D<float2>`/`Texture2D<float2>` shader code, production
  `Laplace2D (8 Karl Sims)` wired into the update texture attribute input, a
  test-local reaction formula around that Laplace result, and standalone
  Stride effect compilation for both stage shaders
- `Group (ComputeStage)` now propagates its resource input to child stages when
  the group resource or child-stage list changes, matching the VL help-patch
  pattern where the group owns the shared resource
- texture-stage graph binding now creates a stage-local texture attribute map
  from the current stage graph when possible, so multiple stages can write the
  same texture attribute name without each stage binding every instance from
  the shared resource map
- texture stage binding uses full `Int3` read/write indices from the
  `IIndexProvider`, unlike the structured-buffer path which projects the write
  index to X
- a C# `ITextureResourceProvider` was not introduced yet to avoid repeating the
  VL category collision seen with `IChangeGraph`; add it only if a later
  `IsntRed` run confirms the public name is safe, or use a C#-specific name
  if it collides

Checkpoint:

| VL patch area | C# status | Evidence | Remaining gap |
| --- | --- | --- | --- |
| Texture resource lifecycle (`Prepare`, `HandleAttribute`, `FinishAttributeMap`, `Finish`) | Converted with patch-shaped operation names and state slots. | `TextureResourceStateTests` covers attribute intake, map finalization, size/dimension defaults, and texture dispatch info. | Real resource lifetime under vvvv graph changes and device reset still needs renderer validation. |
| Texture A/B input creation (`UpdateTextures`, `GetTexture`, `SwapTextures`) | Converted headless and device-aware. Missing devices and description errors are explicit per-slot statuses. Texture inputs now keep default hash names unless `TextureResource` supplies readable resource/attribute/slot names. Unnamed resources use attribute/dimension/slot fallback names. | Tests cover no-device A/B creation, double buffering, single buffering, unsupported UAV formats, swap execution after dispatch, unnamed 2D/3D resources with the same attribute name, and readable A/B input names without duplicate IDs. | Actual `Texture.New(...)` behavior with a real `GraphicsDevice` is not covered by the headless lane yet. |
| Read/write shader calls (`ReadCall`, `WriteCall`) | Converted as separate operations creating typed `ComputeTextureGet<TIndex,T>` and `ComputeTextureSet<TIndex,T>`. | Tests assert generic node types, texture input IDs in generated source, readable texture declarations, unique texture declaration names, A-vs-B routing, and single-buffered write-to-A behavior. | Generated HLSL still needs comparison against representative VL/vvvv output for texture-heavy patches. |
| Texture neighborhood base (`TextureNeighborhoodNode<TIndex,T>`) | Added as a shared C# base for texture-neighborhood operators. It is not imported as a VL node; concrete patch-named nodes remain the public surface. | `TextureNeighborhoodTests` cover 1D/2D/3D axis offsets, 2D Moore diagonals, 3D diagonal count, nested radius-loop emission, and runtime offset-buffer loop emission. `ComputeReplacementClasses_ArePreparedAsExplicitProcessNodes` also asserts the base is absent from the public import list. | Runtime offset buffers are only an emitter path for future dynamic kernels; no production node uses that path yet. |
| Texture average operator (`Average`) | Converted as `Average<TIndex,T>` in `Fuse.Compute`, exposed as `Fuse.Compute.Texture/Average`, and now derives from `TextureNeighborhoodNode<TIndex,T>`. | The `HowTo Use Average` fixture compile-checks a patch-shaped texture shader and verifies readable A/B texture names, 2D dispatch guard, Average loop variables, and post-dispatch texture swaps. | It is compile-proven for the 2D float help-patch shape; 1D/3D and vector-valued variants are implemented but not yet fixture-backed. |
| Texture Laplace operator (`Laplace2D (8 Karl Sims)`) | Converted as `Laplace2DKarlSims<T>` in `Fuse.Compute`, exposed as `Fuse.Compute.Texture/Laplace2D (8 Karl Sims)`, and now derives from `TextureNeighborhoodNode<Int2,T>`. | The ReactionDiffusion fixture compile-checks the production Laplace shader inside the patch-shaped update stage. `TextureNeighborhoodTests` cover the shared fixed-offset and loop emitters used by this operator family. | Public surface is intentionally still 2D because the VL node is 2D; the internal base is dimension-aware for later operators. |
| Stage binding (`BindComputeStage`, `CreateRead`, `CreateWrite`, `SyncAttributes`) | Converted in the patch order: bind attributes, create read group, create write group, sync attributes. | Tests cover read grouping, write grouping, `Util.Writes(attribute) > 0` via `WriteCounter`, post-graph append, full `Int3` indices, and four help-patch-derived texture fixtures. ReactionDiffusion now keeps the disabled initialize stage in the group, dispatches only the enabled 20-iteration update stage by default, separately compiles the initialize shader, and has a shader-snapshot fixture for seed/update shader structure. | The ReactionDiffusion reaction formula is still test-local shader code; production graph nodes should continue to be added only when porting the corresponding VL nodes one by one. |
| Texture swap draw position (`TextureSwap.Draw`) | Represented as patch-named `TextureSwap` shader node with resource-side `Execute()`, run after successful dispatch. | Dispatch tests prove swap is skipped when dispatch is blocked and executed after a fake successful dispatch. | In vvvv/Stride this still needs validation against the renderer order used by the original patch. |
| DirectX safety around texture formats | C# is intentionally stricter than the VL patch before resource creation. | Tests reject `PixelFormat.None`, reject unsupported typed UAV formats for unordered access, and allow them for SRV-only descriptions. | Device-specific optional typed-UAV feature checks are still not implemented. DirectX debug-layer validation is still pending. |
| Dispatch gating on texture update failures | Converted as a C# execution helper around the patch sequence, defaulting to block failures. | `ComputeDispatchExecutor` tests cover default blocking, headless `AllowMissingGraphicsDevice`, status snapshots, and policy propagation through draw/execute entry points. | This is not a literal VL patch node; it is C# diagnostic and safety plumbing around the converted resource operations. |

Supported by automated headless tests:

- resource-side texture attribute intake and state synchronization
- A/B texture input creation without a `GraphicsDevice`
- typed texture read/write shader node creation
- single-buffered and double-buffered texture input routing
- unnamed 2D/3D texture resources with identical attribute names and unique
  shader texture IDs
- `CreateRead`, `CreateWrite`, `BindComputeStage`, and `TextureSwap`
  composition
- DirectX-facing preflight checks that can be decided without a device
- dispatch blocking and status reporting when texture setup fails

Not yet proven by automated tests:

- successful real Stride texture allocation on a `GraphicsDevice`
- DirectX debug-layer behavior for the produced resources and dispatches
- device-specific optional typed-UAV support
- vvvv renderer timing around texture updates, dispatch, and swap
- HLSL equivalence against a representative set of original VL compute-texture
  patches

Useful help-patch-derived fixture candidates:

- `help/Compute System/Texture/HowTo Write To Texture.vl` - already modeled as
  the two-stage 2D/3D texture write fixture
- `help/Compute System/Texture/HowTo Game of Life.vl` - already modeled as the
  grouped three-stage shared-texture fixture
- `help/Compute System/Texture/HowTo Reaction Diffusion.vl` - now modeled as a
  two-stage shared-texture fixture with a disabled initialize stage and an
  enabled 20-iteration update stage; formulas are currently test-local to avoid
  production-node clutter before the corresponding VL nodes are ported
- `help/Compute System/Reference Overview Compute System.vl` - useful as a
  broad composition smoke test for `ComputeStage`, `Group (ComputeStage)`,
  `ComputeSystem`, `StructuredBufferResource`, and `TextureResource`

### ComputeStage

VL operations:

- `Create(Index Provider, Resource)`
- `Update(Compute Graph, ShaderNode, Force Recompile, name, Profiling Name,
  Profiling Append)`
- `DrawStage(Context)`
- `GetShaderNode`
- `UpdateIndexProvider`
- `GetShaderCode`
- `SplitAttributes`
- `GetLastError`
- `SetEnabled`
- `SetDefaults`
- `SetDispatcherProvider`
- `GetEnabled`
- `GetDispatcherProvider`
- `SetPreGraphRenderer`
- `SetWriteAttributes`
- `AddChangeGraph`
- `RemoveChangeGraph`
- `HandleAttributes`
- `AddDispatchProvider`
- `GetChildren`
- `GetTicket`
- `ReadsAndWrites`
- `SetIterationCount`
- `GetComputeGraph`
- `BuildComputeGraph`
- `CallChangeGraph`
- `ProcessMainResource`
- `BindAttributes`
- `CreateWrite`
- `GetResource`
- `GetResources`
- `GetName`
- `GetComputeStage`
- `Dispose`

Current C#:

- `ComputeStage`
- `ToComputeStage` for the VL `ToComputeStage (IRenderer)` adapter
- `IComputeStage`
- `IComputeStageProvider`
- `IComputeChangeGraph` as the C# equivalent of the VL `IChangeGraph`
- stage shader generation
- stage resource/dispatch/graph properties

Status:

- still too compressed, but now exposed under the VL node name
- shared `IComputeStage` now includes the currently modeled VL stage operations
- `ComputeStageDefinition` has been removed; `ComputeStage` is the primary
  design surface
- `GetDispatcherProvider()` now returns the patch-shaped provider; draw planning
  resolves `provider.GetDispatcher().GetDispatchInfo().Split()`
- `IComputeStage` now exposes the VL `GetName` and `GetResource` operations;
  `ComputeStage`, `ComputeStageGroup`, and `ToComputeStage` implement them.

Correction:

- Continue keeping public tests and callers on `ComputeStage`.
- `DrawStage`/`Draw` now consume an execution plan before shader generation, so
  invalid dispatch and missing graph states are visible in the draw path.
- `DrawStage` and `BuildExecutionPlan` now share an internal
  `ComputeDrawStagePipeline` matching the patch order:
  `GetDispatcherProvider -> GetDispatcher -> GetDispatchInfo -> Split ->
  GenerateShaderSource`
- `DrawStage(RenderDrawContext, ...)` executes the generated command list in
  patch order through `IterationIndexSet -> PreRenderCommand -> Dispatch`
- `ToComputeStage` implements `IComputeStage` and `IComputeStageProvider` for
  the VL renderer adapter; its `DrawStage(RenderDrawContext, ...)` calls the
  assigned `IGraphicsRendererBase.Draw(context)` when enabled
- `DrawResult.DispatchCommands` now attach `IterationIndexGlobal`, matching
  the `IterationIndex (Global)` plus `Value Set` patch section
- `DrawResult` exposes the current headless dispatch evidence for a stage:
  status, dispatch groups, thread group size, iteration count, shader source,
  reason, exception, and diagnostics.
- `BuildComputeGraph` forwards `WriteAttributes` into
  `StructuredBufferResource.BindAttributes(...)`, so stages can build a
  read-only structured-buffer graph without generating attribute writes
- `BuildComputeGraph` calls the registered change graphs and bumps the stage
  ticket after the stage graph has been rebuilt
- the C# interface is named `IComputeChangeGraph` instead of `IChangeGraph` to
  avoid colliding with the existing VL `IChangeGraph` category during patch
  compilation
- `ProcessMainResource` now follows the VL `IsAssigned -> Switch` shape: a
  stage keeps its own resource when assigned and only falls back to the passed
  main resource when its resource slot is empty
- `GetResources` now merges incoming resources with the stage resource by
  compute-resource target instead of yielding duplicate targets
- `Dispose` clears stage-local graph/dispatcher/shader/listener state without
  disposing the assigned compute resource, because resource ownership stays with
  `StructuredBufferResource` or `TextureResource`.
- Fill in exact behavior behind `HandleAttributes`.
- Event/ticket/change-graph behavior should be modeled before actual GPU dispatch.
- Keep `DispatchInfo` as stage state, but route draw-time decisions through
  `IDispatcherProvider`/`IDispatcher` where the VL patch does so.

### Group (ComputeStage,Spectral)

VL role:

- groups multiple `IComputeStageProvider`s
- filters assigned stages
- forwards stage operations:
  - HandleAttributes
  - Add/RemoveChangeGraph
  - AddDispatchProvider
  - SetPreGraphRenderer
  - DrawStage
  - BuildComputeGraph
  - ProcessMainResource
  - GetResources
- ticket is max child ticket
- has enabled state

Current C#:

- `ComputeStageGroupSpectral`
- `ComputeStageGroup`
- filters `IComputeStageProvider`s to assigned child stages
- forwards the currently modeled stage operations
- forwards `AddChangeGraph` and `RemoveChangeGraph` to child stages using the
  C# `IComputeChangeGraph` equivalent of the VL interface
- `ProcessMainResource` uses the group's own resource as the effective main
  resource for children when the group resource is assigned; otherwise it falls
  back to the passed main resource
- draws all enabled child stages when used inside `ComputeSystemSpectral`
- exposes `GetName`, `GetResource`, and `Dispose` in the same operation shape
  as the VL group patch

Status:

- implemented as a separate concept
- forwards stage operations through `IComputeStage` instead of concrete type
  checks
- C# group drawing returns all child shader sources through `Draw`; the single
  `GenerateShaderSource` compatibility path returns the first child source
- execution plans flatten groups to child stage plans
- `DrawResult` flattens groups to child stage results
- `DrawStage(RenderDrawContext, ...)` now forwards directly to child
  `IComputeStage.DrawStage(...)` calls, so renderer stages created by
  `ToComputeStage` are reached
- `Dispose` forwards to child stages, clears group/provider/resource lists, and
  does not dispose shared compute resources.

Correction:

- Keep group behavior separate from `ComputeSystem`.
- Continue replacing compatibility paths with exact VL draw semantics.

### ComputeSystem (Spectral Advanced)

VL operations:

- `Create(AttributeValues)`
- `Update(External Scheduler, Compute Stages, Enabled) -> Global Attributes,
  Has Changed`
- `ChangeGraph(Node)`
- `Dispose`
- `PrepareResources`
- `HandleAttributes`
- `FinishResources`
- `RemoveEventHook`
- `AddEventHook`
- `Draw(Context)`
- `SetEnabled`
- `WriteAttributesComputeStage`
- `AddDispatcherProvider`
- `SetPreGraphRenderer`
- `BuildComputeGraph`
- `GetTicket`
- `ProcessMainResource`
- `AppendComputeStage(Node Path, Stage)`
- `GetResources`

Current C#:

- `ComputeSystemSpectral`
- `ComputeSystem`
- `ComputeSystemSpectral` implements `IComputeChangeGraph`
- `AppendedStages` dictionary keyed by node path with `IComputeStageProvider`
  values
- `AppendComputeStage(string nodePath, IComputeStageProvider stage)`
- `Update(..., IEnumerable<IComputeStageProvider> computeStages, ...)`
- `BuildComputeGraph()` owns the current headless lifecycle order:
  `PrepareResources -> HandleAttributes -> WriteAttributesComputeStage ->
  AddDispatcherProvider -> SetPreGraphRenderer -> ProcessMainResource/GetResources
  -> FinishResources`
- `Dispose()` clears child stage state, event hooks, schedulers, lifecycle
  tracking, and resource lists without disposing shared compute-resource
  objects
- `GlobalAttributeHandler`
- resource merge
- dispatch diagnostics
- shader source generation
- execution plan

Status:

- now exposed under VL-compatible public names
- node path now drives a provider dictionary, matching the VL `Appended Stages`
  slot more closely
- appending the same node path replaces the provider and rebuilds resources from
  the current stage providers
- `Update` now delegates to `BuildComputeGraph`, so the C# path has one
  patch-shaped lifecycle sequence instead of a separate simplified update path
- `PrepareResources`, `HandleAttributes`, and `FinishResources` now run through
  a C# `GlobalAttributeHandler` that mirrors the VL handler at a headless level:
  prepare the map, collect `ComputeSystemAttribute` properties from stage graphs,
  bind global attribute values by name, then finish/sync the map
- `GlobalAttributeHandler.BindAttributes(...)` now follows the VL
  `SplitAttributes` route more closely by handling only `Temporary` attributes;
  texture attributes are no longer treated as global attributes and remain
  pending for the `TextureResource` conversion
- structured-buffer attributes are now routed by `ComputeSystem` to the owning
  `StructuredBufferResource` during the same lifecycle; global/temporary
  attributes stay with `GlobalAttributeHandler`
- `ComputeStage.BuildComputeGraph` now rebuilds structured-buffer stages through
  `StructuredBufferResource.BindAttributes(...)`, so the current
  `PreGraphRenderer`, `WriteAttributes` flag, and resource attribute map are
  used during the patch-shaped build step
- lifecycle operations exist as deterministic C# methods
- scheduler/draw/event hook behavior is still simplified and headless
- `ExecutionPlan` is a C# helper, not a VL concept
- `Draw` and `GenerateShaderSources` now read from `BuildExecutionPlan`
- `Draw(RenderDrawContext, ...)` is the patch-shaped renderer operation for
  headless C# automation and future VL/vvvv render-context execution
- `Draw(RenderDrawContext, ...)` now iterates enabled stages and calls
  `IComputeStage.DrawStage(...)` directly, matching the VL renderer operation
  and allowing `ToComputeStage (IRenderer)` stages to draw
- `Dispose` now follows the VL lifecycle operation shape: it detaches event
  hooks, disposes child stage state, clears schedulers and lifecycle/resource
  collections, and leaves compute-resource ownership with the resource objects
- `ComputeSystemSpectral` now implements `IGraphicsRendererBase`, matching the
  VL patch where `ComputeSystem (Spectral Advanced)` is also an `IRenderer`
- `Update(External Scheduler, ...)` stores the external scheduler and only uses
  the internal renderer scheduler path when no external scheduler is assigned,
  mirroring the `IsAssigned -> NOT -> RendererScheduler` section of the patch
- `DrawResult` now provides the explicit headless draw result:
  `ComputeDrawResult` and `ComputeStageDrawResult`
- `DrawResult.DispatchCommands` now exposes patch-shaped command data:
  `PreRenderCommand`, `Dispatcher`, `ThreadGroupSize`, `SkipOutsideRange`,
  `DispatchGroups`, `IterationCount`, and `IterationIndex`
- `DrawResult.DispatchCommands` also carries `StageDispatcher`, the
  Fuse.Compute `IDispatcher` resolved before `IDispatchInfo.Split()`
- stage plans now also preserve the resolved dispatcher provider and dispatch
  info so tests can verify the patch-shaped draw pipeline
- `ToRenderer(...)` creates a `ComputeDispatchRenderer` for the current draw
  result, matching the patch's renderer-driven dispatch direction
- `TryCreateRenderer(...)` keeps the same API usable in headless automation by
  reporting the missing VL `AppHost` instead of throwing
- appending a stage registers the system as that stage's change-graph listener;
  replacing a stage for the same node path detaches the old listener, so stage
  graph changes flow through `ComputeSystemSpectral.ChangeGraph(...)` and then
  to external event hooks
- `ProcessMainResource` still stores and merges the system main resource, but
  child stages now preserve already assigned resources instead of being
  overwritten by the main resource

Correction:

- Continue keeping public tests and callers on `ComputeSystemSpectral` and
  `ComputeSystem`.
- Expand simplified lifecycle placeholders toward exact VL behavior:
  - `PrepareResources`
  - `HandleAttributes`
  - `BuildComputeGraph`
  - `FinishResources`
- real scheduler/dispatch consuming `ComputeDispatchCommand`
- connect the `ComputeDispatchRenderer` to the public VL node surface without
  reintroducing a separate bridge design
- Keep `ComputeExecutionPlan` and `ComputeDrawResult` as helper evidence for
  `Draw`, not as the main public VL abstraction.

### ComputeSystem

VL role:

- public process wrapper around `ComputeSystem (Spectral Advanced)`
- `Create(AttributeValues)`
- `Update(Compute Stage, External Scheduler, Enabled) -> Output, Has Changed`

Current C#:

- `ComputeSystem`

Status:

- implemented as a thin public wrapper around `ComputeSystemSpectral`

## Immediate Refactor Plan

1. Stop adding new runtime concepts until this shape is aligned.
2. Introduce VL-compatible public names:
   - `ComputeStage` done as the public stage class
   - `ComputeStageGroup`
   - `ComputeSystemSpectral` done as the spectral system class
   - `ComputeSystem` done as the public single-stage wrapper
3. Keep `ComputeExecutionPlan` behind those names as helper evidence for draw
   behavior. `ComputeStageDefinition` and `ComputeSystemDefinition` have been
   removed.
4. Add `StructuredBufferResource.BindComputeStage(...)` and route existing
   `CreateReadWriteBindings(...)` through it.
5. Expand tests to use VL operation names, not only the current definition names.
6. Only then attach real Stride GPU execution.

## Design Rule Going Forward

When converting a VL node:

1. Keep the C# public class/method names close to the VL node and operation names.
2. Implement behavior in small internal helpers only after the VL operation exists;
   tests should prefer the VL operation (`DrawStage`, `Draw`, `Split`,
   `GetDispatcher`) over helper classes.
3. Tests should call the VL-shaped API first.
4. Headless validation and DirectX diagnostics are allowed, but they must not
   replace or hide the VL operation model.
