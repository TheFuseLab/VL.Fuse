# Fuse Compute ProcessNode Surface

This note records the current rule for the C# replacement node surface.

## Rule

ProcessNode fragments are for:

- constructors and the visible instance `Output`
- patch lifecycle/state-changing operations
- graph-building operations
- real multi-output patch operations

Getter-style operations stay normal instance methods. They are reachable from
the visible instance `Output` and should not be duplicated as process fragments.

## Current Surface

| Type | Fragment surface | Instance-only examples |
| --- | --- | --- |
| `ComputeGraph`, `ComputeGraph1D`, `ComputeGraph2D`, `ComputeGraph3D` | constructor, `Update`, `Output` | `GetComputeGraph`, `GetDispatcher`, `GetChangedTick`, `SplitDispatchInfo` |
| `ComputeStage` | constructor, stage setters, `Update`, `Output` | shader/debug/result getters, draw helpers |
| `Group (ComputeStage,Spectral)` | constructor, stage lifecycle forwarding, `Output` | child/resource/ticket getters |
| `Group (ComputeStage)` | constructor, group setters/update, `Output` | inherited getter surface |
| `ComputeSystem (Spectral Advanced)` | constructor, lifecycle/build/update/append operations, `GetResources`, `Output` | diagnostics, stage/resource/global-attribute getters |
| `ComputeSystem` | constructor, single-stage update and lifecycle forwarding, `Output` | inherited getter surface |
| `Buffer1DDispatchInfo` | constructor, update/setters, `Split`, `Output` | `GetCount` |
| `StructuredBufferResourceDispatchInfo` | constructor, update/setters, `Split`, `Output` | `GetElementCount`, `GetCount` |
| `TextureDispatchInfo` | constructor, update/setters, `Split`, `Output` | `GetThreadGroupInfo`, `GetCount`, `GetCountGpu` |
| `StructuredBufferResource` | constructor, resource lifecycle, binding, update-buffer, reset, `Output` | buffer/struct/resource getters |
| `TextureResource` | constructor, resource lifecycle, binding, texture update/swap/reset, `Output` | texture/status/resource getters |
| `ToComputeStage` | constructor, `Update`, `SetEnabled`, `Output` | stage interface getters |

`GetResources` on `ComputeSystem (Spectral Advanced)` stays a fragment even
though its name starts with `Get`, because the VL patch uses it as part of the
compute-system resource collection lifecycle and the C# method records that
lifecycle step and rebuilds resource state.
