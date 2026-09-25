# Generic FBX custom channels

Both existing entry points, `FBXLoader.LoadFbx` and `LoadFbxWithExtraAnimations`, return the unchanged `ModelGpu`/`MeshGpu` hierarchy. `GpuVertex` remains 64 bytes. The provisional dedicated motion loader/struct has been removed.

`ModelGpu.Mesh.GetCustomData()` returns available `MeshCustomChannel` descriptors. `GetCustomData("TEXCOORD1")` returns a named descriptor and throws KeyNotFoundException if absent. Generated semantic names are stable channel indices, not original Blender layer names.

Each descriptor exposes:

- `Name`: TEXCOORD1.. or COLOR0..
- `Kind` and `Index`: channel family and source channel index.
- `ComponentCount`: maximum source component count across contributing meshes.
- `Values` / `GetValues()`: Vector4 array with one entry per imported mesh vertex, **16-byte GPU stride** for every channel regardless of ComponentCount. UV XYZ are copied with W=0; colors copy RGBA. No clamping, normalization, axis conversion, UV flip or color conversion is performed on these values by Fuse.IO.
- `Present`: per-vertex bool flag. A channel absent from a source mesh is zero-filled with Present=false. This distinguishes missing data from actual zero values. Do not upload this CPU bool array as an assumed HLSL bool layout.

UV0 stays in GpuVertex.Tex. Additional UV sets and all vertex color sets are captured after Assimp postprocessing in the exact mesh-instance and vertex order used for geometry. Reordering, seam splits, repeated instances and concatenation therefore remain aligned. Arbitrary Blender attributes not exposed by Assimp are outside this contract. Existing loader error/fallback behavior remains unchanged.

## Manual patch use

Existing FBXLoader → ModelGpu.Mesh → GetCustomData("TEXCOORD1") → GetValues → structured Vector4 GPU buffer.

Repeat for the other desired channels. The library API is implemented; the `Mesh (FBX)` VL wrapper and manual Make/Split/interpolation nodes have not been edited. Use the same imported vertex index for Vertices and each custom channel.

## Mature C motion-v2 interpretation (outside the loader)

| Generic channel | Manual interpretation |
| --- | --- |
| TEXCOORD1.XY | MotionBlend.XY |
| TEXCOORD2.XY | MotionBlend.ZW |
| TEXCOORD3.X | one-based MotionId; convert to int after validating |
| TEXCOORD3.Y | format sentinel1709; validate in application |

The loader does not inspect that sentinel, recognize IDs or normalize weights. The existing FBX-v2 and small lookup EXRs remain compatible. A hand-built `MotionVertex` can combine float4 MotionBlend and int MotionId **after reading** these channel buffers; the raw channel buffers themselves are each float4/16bytes, not the former packed20byte motion stream.

Lookup UV: `((section+0.5)/4, (MotionId-1+0.5)/partCount)`. Point/LOD0, no mips/premultiplication, linear data. Pivots RGB are FBX-object-space Y-up, axes RGB must be normalized. Sections3→0, angle=totalRadians*MotionBlend[section]; section shares are already baked. Apply instance transformation afterward. Mature C sceneScale0.01 matches the supplied lookup coordinates.

For economical sampling, interpolate rest position and four blend values, retain a common ID, then deform once (approximate surface attachment). For exact attachment, deform the triangle corners then interpolate their resulting positions. Neither shader path is implemented here.

## Verification / deployment

`Checks/Fuse.IO.MotionCheck` checks synthetic sparse UV/color channels, unnormalized/out-of-range raw values, missing channel masks, repeated mesh instances, both existing loaders, v1/v2 compatibility, Mature-C triangle IDs and alignment. The output binary is compared to every Blender source vertex/weight by the Fungi reference checker.

Build with `dotnet build Checks/Fuse.IO.MotionCheck/Fuse.IO.MotionCheck.csproj -p:OutputPath=D:/development/vl/repo/VL.Fuse/artifacts/motion-check/`. Checker arguments are Mature-C-v2 FBX, output binary, Mature-C-v1 FBX. The installed library is still locked by running vvvv; close vvvv and build `src/Fuse.IO/Fuse.IO.csproj` normally to activate. The isolated build does not update the running application.
