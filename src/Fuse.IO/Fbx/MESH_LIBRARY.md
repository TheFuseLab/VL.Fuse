# Mesh library — additive FBX nodes

Implemented in the active source checkout D:/development/vl/repo/VL.Fuse.
Existing Mesh (FBX), FBXLoader, RandomTriangleData, RandomVertex and their VL definitions were not changed by this addition.

## Nodes and wiring

The C# nodes are imported from Fuse.IO.dll under Fuse.IO.Fbx:

- MeshLibraryBuilder: ordered ModelGpu collection -> MeshLibrary, Status. Null/invalid entries retain their Asset ID slot with Valid=0.
- SingleMeshLibrary: one ModelGpu -> the same library type plus MeshView.
- MeshLibrary.GetMeshView: library + Asset ID -> non-owning CPU view.
- MeshLibraryBuffers: library + GraphicsDevice -> shared GPU buffers. This node owns and disposes these buffers. Run on the graphics thread and keep it alive while consumers use them.
- MeshLibrarySampling.SampleSurface: CPU reference sampler, not a per-particle GPU node.

New GPU mixin functions in vl/shaders/FuseMeshLibrarySampling.sdsl are exported under Fuse.IO.FBX.Library:

- LibrarySurfaceSample
- LibraryCustomSample

Reload Fuse/shader discovery to expose these. If auto-discovery is unavailable in the running build, use MixinFunction with mixin FuseMeshLibrarySampling and the function name. The new functions require a live Fuse graph/shader compilation check before production use.

Connect MeshLibraryBuffers Positions, Normals, UVs, Indices, Alias Probabilities, Alias Indices and Ranges to LibrarySurfaceSample. Asset Count = Library.Assets.Length (including invalid slots). Asset ID selects the variant, not a placed instance or an animation segment. Supply four independent random values in [0,1), derived from a stable instance/particle seed. Respect Valid=false for absent assets.

The function returns local position, normal, UV, three global vertex indices, barycentric weights and validity. Use indices and weights with LibraryCustomSample for COLOR0/COLOR1 or additional UV streams; select matching Custom Values and Custom Present buffers by Custom Names. Missing channels return zero with Present=false. Do not infer categorical IDs by interpolating values across a triangle unless that triangle is known to have a constant ID.

Apply UV flip and atlas mapping as before; animate in local space; apply the instance matrix exactly once. Confirm loader Scene Scale against the scaffold coordinate contract. No automatic scale correction is applied here.

## Storage contract

GpuVertex remains the existing 64-byte struct. MeshLibraryAsset is 32 bytes: four ints VertexOffset/VertexCount/TriangleOffset/TriangleCount, then int AliasOffset, int Valid, float SurfaceArea, int Reserved. Bounds are CPU BoundingBox values in loader coordinates.

Ranges is an int4 buffer: vertex offset, vertex count, triangle offset, triangle count. AliasOffset equals TriangleOffset in this builder. Index data is globally rebased; alias entries stay local. Address triangles at 3*(TriangleOffset+localTriangle). Never add VertexOffset again to returned vertex indices.

Both interleaved vertices and separate position/normal/UV buffers are uploaded for compatibility with the existing vertex layout and generic Fuse shader inputs. This duplicates these attributes in GPU memory; it can be reduced later once the preferred consumer is fixed. One library should be shared across instances.

Uniform scaling leaves alias probabilities unchanged. SurfaceArea is local; world density budgeting uses area*scale^2. The library samples rest surfaces; nonuniform scaling and deformation do not recompute area weights.

## Scope and lifecycle

Static surface sampling only. BoneCount > 1 is explicitly rejected; skeletons/animation banks are not merged and copied bone IDs are not globally rebased. The legacy skinned-model path remains untouched.

Builder caches by ordered model, mesh and top-level array references. Replacing these triggers rebuilding; pulse Reload after in-place edits (including custom-channel array contents). Reload is a rising edge. Treat library output arrays as immutable. GPU buffers rebuild on library/device identity changes and are disposed on replacement or node disposal. Empty libraries allocate a dummy element; counts/Valid must gate sampling.

No culling, instance allocation, atlas generation or particle state is hidden in these nodes. They supply the shared geometry to the existing patch.

## Validation

Checks/Fuse.IO.MeshLibraryCheck verifies stable missing slots, index rebasing, local aliases, 1:4 area distribution, custom-channel alignment, caching/reload, single/multi sampling equivalence, invalid/degenerate rejection and struct strides. A production Type05B child_c FBX was imported and packed successfully (1,006,049 vertices / 1,938,330 triangles). A standalone Stride GPU-device check also passed buffer upload, reuse and clearing. Rendered Fuse sampler parity and shader compilation in the live patch remain unverified.

Pre-update deployed Fuse.IO.dll/xml copies are in Checks/MeshLibraryBackup. New tests build in Checks/MeshLibraryBuild before the assembly is published to lib/net8.0.

## Installation status

Installed successfully after vvvv was closed. lib/net8.0/Fuse.IO.dll matches the verified build SHA256 DD7C46A4C56AC36F7A6AC9477989F139AA8BF49D3869AF2A40733EE00828A1AA. Matching XML installed. Reopen vvvv and reload Fuse shader discovery. Live shader compilation and rendered sampling remain to be checked.

## Optional fast startup: MeshLibraryFile

Use MeshLibraryFile in place of the individual FBXLoader nodes plus MeshLibraryBuilder when building a static library from files. Connect its Library output to the existing MeshLibraryBuffers. Existing nodes remain available unchanged in interface.

Inputs: Paths (ordered full FBX paths; this order determines AssetId), Cache Directory (blank defaults to LocalApplicationData/Fuse/MeshLibraryCache), Scene Scale (0.01), Reload (pulse after file edits). Outputs: Library and Status. Input changes trigger loading; unchanged inputs reuse the same library object. Changes to files on disk require Reload. Reload validates content and reuses an exact matching disk cache; it is not a force-reimport switch.

The first load imports and prepares all models and writes a binary library. Subsequent starts read the ready arrays, including COLOR0/COLOR1, alias tables and bounds. Cache identity includes ordered source content hashes, scale and assembly version. A checksum detects damaged cache data and triggers rebuilding. The cache is local derived data and can be deleted when not in use; it does not replace FBX exports. Changing input order, contents, scale or library build creates a new cache; old cache files are not automatically evicted. This loader supports static meshes only and rejects quads because the legacy loader also returns a quad on import failure. Invalid assets throw rather than caching a fallback. Loading remains synchronous.

2026-09-22 Type05B nine-asset local test: original builder 1.098s, updated builder 0.971s; upload 0.367s vs 0.348s. FBX loading remains the dominant cost; no reliable import speedup claimed. Managed allocations decrease roughly 22%. First cache creation 57.147s; immediate cache hit 1.595s (warm OS cache, excludes GPU upload). All geometry/index/alias SHA256 hashes match original results. Small regression tests also cover custom streams, cache corruption, array replacement and Reload after in-place changes.

MeshLibraryBuilder now caches per-mesh validation/bounds/alias preparation by mesh and backing-array identity, retaining only inputs still in use. Replacing a model still assembles a new combined library and uploads it; this does not eliminate those copies. Pulse Reload after in-place mutations. Unchanged inputs were already fast; this change mainly benefits partially changing libraries.

## ufbx default (2026-09-22)

FBXLoader.LoadFbx now prefers the Windows x64 ufbx adapter. Its existing signature remains unchanged. LoadFbxAssimp retains the original implementation; LoadFbxWithBackend exposes FbxImportBackend.Ufbx (default) and Assimp. MeshLibraryFile also exposes Backend, includes it in cache identity, and defaults to Ufbx. Multi-file skeletal animation loading remains on the original Assimp route.

The native adapter accepts static triangle meshes, normals, at most one UV set and up to two color sets with consistent presence across mesh instances. Animated/skinned/blendshape scenes, polygons, mirrored or singular transforms, extra channels and missing normals fall back to Assimp. Missing/incompatible native DLL also falls back. Static colors are signed float4 data; no gamma or alpha conversion is performed. Vertices/indices may be reordered or differ slightly in count; rebuild derived buffers when switching importers.

Native source: Native/bridge.cpp, pinned ufbx source/header and license under Native/ufbx (commit fcc5d6ba444cfd3eb80677dba5e37e493941abe5). Native/build.cmd produces Fuse.Ufbx.dll with MSVC x64; csproj copies it beside Fuse.IO.dll. Install both with Checks/Fuse.IO.MeshLibraryCheck/Install.ps1, which preflights file locks. Keep the bundled license when distributing the native library.

Integration validation: all nine Type05B assets passed 20,000 sampled triangle comparisons each, exact UV and COLOR0/1 matches, position error <= 6e-8, normal error < 9e-6. This is not exhaustive topology validation or a live vvvv visual test. Standard mesh library/GPU/cache regression checks pass. Native-to-C# load ~0.9–1.0 seconds per asset. Full nine-asset cache creation 12.458s, immediate cache hit 1.414s in the local warm-filesystem test. Legacy and ufbx buffers are not byte-identical due to indexing/merge differences; the ufbx cache roundtrip is byte-identical.

## Shared triangle tables and per-instance ranges

New node: TriangleAliasTableForLibrary. New function: BuildTriangleAliasTableForLibrary. The original BuildTriangleAliasTableForMesh(MeshGpu) is unchanged.

Inputs: Library and Instance Asset Ids (same ordering as your instance transforms). Outputs: Table, Triangle Count (total library entries), Prob, Alias, Asset Ranges and Instance Ranges. Prob/Alias reference existing library arrays without recomputation or duplication. Empty instance IDs means zero instances, not one default instance per asset.

Each range is an Int4 (16 bytes): X alias offset, Y local triangle count, Z global triangle offset, W valid flag. Invalid/missing assets or IDs yield zero ranges and retain their slots. Repeated instances of an asset reference identical ranges. Upload Instance Ranges through ImmutableBuffer; either upload Prob/Alias similarly or use the existing equivalent MeshLibraryBuffers outputs.

Shader sampling for an instance (u and coin are independent random values in [0,1)):
```
range = InstanceRanges[instanceId];
// Stop for range.w == 0 or range.y == 0 BEFORE reading the alias arrays.
bucket = min(floor(u * range.y), range.y - 1);
localTriangle = coin < Prob[range.x + bucket] ? bucket : Alias[range.x + bucket];
globalTriangle = range.z + localTriangle;
i0 = Indices[3 * globalTriangle]; // next entries are i1, i2
```
Vertex indices are already global: do not add VertexOffset again. Aliases remain LOCAL to each asset. The arrays contain independent per-asset probability distributions, not one globally normalized distribution. Use local geometry/UV/custom data, animate locally, then apply the instance transform. Per-instance particle allocation/visibility/transforms are supplied separately. Uniform scaling preserves within-mesh probabilities; nonuniform scaling does not generally preserve them.

Output arrays are immutable snapshots by convention. The process node caches on library identity and instance-ID values. Instance-list changes only allocate new range metadata; they do not rebuild or reupload the geometry tables unless the consumer does so unnecessarily.

## Simplified asset-only table node

Use **TriangleAliasTableForAssets** for the normal library sampling path. Its only input is Library; outputs are Triangle Count (total entries), Prob, Alias and Asset Ranges. No instance input or output is needed. The function BuildTriangleAliasTableForLibrary also has a library-only overload.

Select AssetRanges[assetId] in the shader. Range layout remains (alias offset, triangle count, global triangle offset, valid). Use the sampling formula above with this asset range; instance transforms are a separate later stage. Shared arrays are reused and ranges are cached until the library changes. A null library clears all outputs.

TriangleAliasTableForLibrary's original instance-mapping node/overload remains available unchanged for existing patches. BuildTriangleAliasTableForMesh(MeshGpu) also remains unchanged.

## AssetAliasTable: choose an asset by scaled surface area

This separate node takes Library and Scales. Supply one uniform scale per library asset, or an empty scale list for all ones. It outputs Prob, Alias, Weights (double, diagnostic), Count and Valid. Sampling the resulting alias table returns an AssetId, which selects AssetRanges in TriangleAliasTableForAssets. It does not sample triangles itself or require instances.

Weight[i] = Library.Assets[i].SurfaceArea * Scales[i]^2. Invalid asset slots get zero weight without renumbering IDs. Negative/nonfinite scales and mismatched list lengths are rejected. Empty/all-zero weights return empty alias buffers, Count=0, Valid=false: skip sampling in that case. Uploaded empty buffers may require the existing dummy-buffer convention. The node caches unchanged inputs; treat library data as immutable.

Upload Prob and Alias, choose bucket=min(floor(random1*Count),Count-1), then AssetId=random2<Prob[bucket]?bucket:Alias[bucket]. Use independent random values for subsequent triangle/point sampling. This selects assets, not placed instances; if one asset has differently scaled placements, a separate instance distribution is needed.

Generic weighted sampling refactor is deferred: see WEIGHTED_SAMPLING_FOLLOWUP.md.

## InstanceAliasTable: visible scaffold selection

Inputs: Library, Asset Ids (full scaffold list), Scales (actual uniform source-mesh scale per instance; empty = all 1), Visible Indices (indices into that full list). Do not pass target heights as scales; use the scale actually applied to loaded mesh positions. Do not prefilter Asset Ids or Scales if Visible Indices still addresses the full list.

Outputs: Prob, Alias, Instance Indices, Weights, Count, Valid. Each eligible instance is weighted by its asset SurfaceArea * scale^2. Sample a slot using Prob/Alias, then read InstanceIndices[slot] to recover the original scaffold index. Use that index for the scaffold AssetId, transform and stable InstanceId. Upload Prob, Alias and Instance Indices with ImmutableBuffer.

Invalid/missing assets and zero-area/zero-scale instances are omitted. Mapping retains original scaffold indices in visible-list order. Empty visibility means none visible. No eligible instances gives Count=0/Valid=false; skip sampling before any reads. Duplicate/out-of-range visible indices and negative/nonfinite/mismatched scales throw. Unchanged inputs reuse outputs. Changing transforms without changing uniform scales does not require rebuilding this table.

This node redistributes a fixed particle budget among the visible instances. It is intentionally view-dependent: overlapping projectors are not guaranteed the same density or particle identity. For matching overlaps, allocate stable particles per instance globally and cull their rendering separately. Nonuniform scaling requires a different surface-area treatment.
