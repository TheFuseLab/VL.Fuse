# Compute System Port Inventory

Date: 2026-06-26

This inventory is generated from:

- `vl/Fuse.Compute.vl`
- `help/Compute System/**/*.vl`
- current C# sources under `src/Fuse`
- current tests under `PatchTests`

The raw extraction artifacts are:

- `artifacts/compute-system-vl-node-inventory.csv`
- `artifacts/compute-system-vl-node-summary.csv`
- `artifacts/compute-system-csharp-coverage-rough.csv`

The C# coverage column is a rough first pass based on C# class names and test
text. It is useful for triage, not as proof that a VL node is fully ported.

## Current Answer

No, the Compute System is not fully ported.

The current C# port covers the central execution/resource path well enough for
headless tests:

- `ComputeSystem`
- `ComputeStage`
- `Group (ComputeStage)`
- `StructuredBufferResource`
- `TextureResource`
- `StructuredBufferAttribute`
- `TextureAttribute`
- dispatch info and dispatch validation
- compute graph wrappers for 1D/2D/3D
- attribute maps and basic read/write binding
- double-buffered texture A/B routing and swap execution
- shader generation, shader dumps, and standalone Stride shader compile tests

The current port does not yet cover most specialized compute-system nodes from
`Fuse.Compute.vl` and the help patches. Those should be ported only when a
help-patch fixture needs them, and preferably in the same shape as the VL patch.
Texture-neighborhood operators now share `TextureNeighborhoodNode<TIndex,T>`
internally for texture binding, index dimensionality, fixed-offset emission,
nested radius loops, and future runtime offset-buffer loops. That base is not a
public VL node; public nodes remain patch-named.

## High-Use Nodes Already Represented

These are represented by production C# classes and test coverage:

| VL node | Help patch count | Current C# status |
| --- | ---: | --- |
| `ComputeSystem` | 35 | `ComputeSystemSpectral` / `ComputeSystem`, execution-plan tests |
| `StructuredBufferResource` | 30 | `StructuredBufferResource`, binding and state tests |
| `ComputeStage` | 23 | `ComputeStage`, shader generation and execution-plan tests |
| `Group (ComputeStage)` | 20 | `ComputeStageGroupSpectral`, grouped-stage tests |
| `TextureResource` | 10 | `TextureResource`, texture lifecycle/state tests |
| `TextureAttribute` | 9 | `TextureAttribute<T>`, texture help-patch fixtures |
| `Average` | 1 | `Average<TIndex,T>`, `HowTo Use Average` shader compile fixture |
| `Laplace2D (8 Karl Sims)` | 2 | `Laplace2DKarlSims<T>`, ReactionDiffusion shader compile fixture |
| `DispatchThreadIdX` / `DispatchThreadId` | 7 / 1 | compute index nodes and graph tests |
| `StructuredBufferAttribute` | 6 | structured-buffer attribute/resource tests |
| `GetTexture` | 5 | represented in texture-resource state and ReactionDiffusion fixture |
| `ToComputeStage (IRenderer)` | 2 | `ToComputeStage` tests |

## Important Open Areas

These appear in help patches but are not yet confidently ported as production
C# nodes. Keep them out of `src` until a concrete help-patch slice needs them.

| Area | Representative VL nodes | Help patch signal | Suggested next action |
| --- | --- | ---: | --- |
| Compute draw/rendering | `BufferToRenderEntity`, `BufferToRenderEntityGroup`, `BufferToEntity`, `TextureRenderer`, `InstancedRenderer` | high | Do not port blindly. First map one rendering help patch and verify what belongs to ComputeSystem vs Draw. |
| Compute random | `Random`, `SphereRandom (Compute)`, `DiskRandom (Compute)` | high | Port only when a selected help patch needs the exact random node shape. |
| Texture compute operators | `SampleTexture3DBox`, `Advect2D`, `Divergence2D`, `Gradient2D`, `Jacobi2D`, `Neighborhood2D (8 Moore)`, `Neighbor2D`, `IdToUV` | medium | Continue this track one VL node at a time from selected help patches. `Average` and `Laplace2D (8 Karl Sims)` are now represented by production C# plus help-patch fixtures. |
| Neighborhood / graph traversal | `GetNeighborInfo`, `PathToTargetClosest`, `PathToTargetRandom`, `NetworkTrails`, `TrajectoryNeighborTo`, `PreviousTargetID`, `FurthestNeighborFrom` | medium | Defer until a Neighborhood help patch is chosen. Likely a separate slice. |
| Selection / indices | `Select (Indices)`, `IndicesDispatcher`, `Indices`, `Reset (Indices)`, `IndexBuffer (FrameReset)`, `IndicesSelectStage` | medium | Candidate after core buffer fixtures. Needs append/index-buffer semantics. |
| Buffer utilities | `AppendItem (Buffer)`, `SetItem (Buffer)`, `GetSlice (Buffer)`, `CopyResource (Buffer)`, `AppendBuffer (FrameReset)` | medium | Good bounded next slice if we choose a buffer help patch. |
| Interlocked operations | `InterlockedAddFloat (Buffer)`, `InterlockedMaxFloat (Buffer)`, `InterlockedAdd (Buffer)`, `InterlockedMax (Buffer)` | low in help patches, present in VL | Existing `Interlocked.cs` exists, but coverage should be checked against `HowTo Use Interlocked Operations.vl`. |
| Utility interpolation | `IDW (Buffer)`, `IDW (Texture)` | low | Defer until `Reference IDW.vl` becomes the selected fixture. |

## Help Patch Priorities

Recommended order if the goal is a 1:1 ComputeSystem conversion with minimal
clutter:

1. `help/Compute System/Texture/HowTo Write To Texture.vl`
   - Structural coverage is now patch-shaped for two unnamed texture resources:
     one 2D double-buffered `NoiseData` stage and one 3D single-buffered
     `NoiseData` stage.
   - Both generated shaders compile, dispatch dimensions are checked, and
     readable fallback texture names avoid duplicate shader IDs.

2. `help/Compute System/Texture/HowTo Game of Life.vl`
   - Already has grouped-stage structural coverage.
   - Good next texture-operator fixture, but keep formulas test-local until the
     exact VL nodes are ported.

3. `help/Compute System/Texture/HowTo Use Average.vl`
   - Now represented by `ComputeTextureHelpFixture_AverageBuildsAndCompilesPatchShapedTextureStage`.
   - Ports production `Average<TIndex,T>` and compile-checks the generated
     `NoiseData -> Average -> SampleData` texture shader.

4. `help/Compute System/Buffer/HowTo Use GetSlice.vl`
   - Good bounded buffer utility slice.

5. `help/Compute System/Buffer/HowTo Use Interlocked Operations.vl`
   - Useful to validate existing interlocked C# code against a real help patch.

6. `help/Compute System/Selection/HowTo Select Indices.vl`
   - Starts the indices/selection track.

7. `help/Compute System/Neighborhood/HowTo Follow Networks.vl`
   - Larger slice. Do only after indices/buffer basics are stable.

8. `help/Compute System/Rendering/Reference Use TextureRenderer.vl`
   - Rendering-heavy. Important, but it crosses the ComputeSystem/Draw boundary.

## Current ReactionDiffusion Status

`help/Compute System/Texture/HowTo Reaction Diffusion.vl` is represented as a
patch-shaped fixture:

- one 1024x1024x1 double-buffered `TextureResource`
- two grouped stages
- disabled `Initialize` stage, matching the bang-style initializer in the patch
- enabled `Update` stage with iteration count 20
- test-local Step rectangle seed shader for the initializer
- production `Laplace2D (8 Karl Sims)` in the update shader
- test-local reaction formula around the Laplace result
- both shaders compile through the standalone Stride shader compiler
- generated shader dumps show readable `ReactionData_A` / `ReactionData_B`
  texture input names

`Laplace2D (8 Karl Sims)` is now a production C# operator. The remaining
ReactionDiffusion formula code should stay test-local until the next concrete
VL node in that formula is selected for porting.

## Next Slice

The next practical slice should continue the same rule as the WriteToTexture
and Average slices: choose one help patch, compare it node-by-node, port only
the production node needed by that patch, and keep formula-only glue test-local.

Good candidates:

- `help/Compute System/Buffer/HowTo Use GetSlice.vl`
- `help/Compute System/Buffer/HowTo Use Interlocked Operations.vl`
- the next texture-operator patch that exercises one of
  `SampleTexture3DBox`, `Advect2D`, `Divergence2D`, `Gradient2D`, `Jacobi2D`,
  `Neighbor2D`, or `IdToUV`
