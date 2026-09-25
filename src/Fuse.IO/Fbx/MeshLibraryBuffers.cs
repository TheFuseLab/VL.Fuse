using Stride.Graphics;
using GpuBuffer=Stride.Graphics.Buffer;
namespace Fuse.IO.Fbx;

/// <summary>Uploads a library snapshot once per device/library change. This node owns its buffers.
/// Execute on the graphics thread, like ImmutableBuffer. Empty libraries use one dummy element.</summary>
[ProcessNode]
public sealed class MeshLibraryBuffers : IDisposable
{
    private MeshLibrary? _library;private GraphicsDevice? _device;
    private GpuBuffer? _vertices,_indices,_assets,_prob,_alias,_positions,_normals,_uvs,_ranges;
    private readonly List<GpuBuffer> _custom=[];
    private readonly List<GpuBuffer> _present=[];
    public void Update(MeshLibrary? library,GraphicsDevice? device,out GpuBuffer? vertices,out GpuBuffer? indices,
        out GpuBuffer? assets,out GpuBuffer? aliasProbabilities,out GpuBuffer? aliasIndices,
        out GpuBuffer? positions,out GpuBuffer? normals,out GpuBuffer? uvs,out GpuBuffer? ranges,
        out Spread<GpuBuffer> customValues,out Spread<GpuBuffer> customPresent,out Spread<string> customNames) {
        if(!ReferenceEquals(library,_library)||!ReferenceEquals(device,_device)) {
            Dispose();
            if(library is not null&&device is not null) {
                try {
                    _positions=Upload(device,library.Vertices.Select(v=>v.Position).ToArray());
                    _normals=Upload(device,library.Vertices.Select(v=>v.Normal).ToArray());
                    _uvs=Upload(device,library.Vertices.Select(v=>v.Tex).ToArray());
                    _ranges=Upload(device,library.Assets.Select(a=>new Int4(a.VertexOffset,a.VertexCount,a.TriangleOffset,a.TriangleCount)).ToArray());
                    _vertices=Upload(device,library.Vertices);_indices=Upload(device,library.Indices);
                    _assets=Upload(device,library.Assets);_prob=Upload(device,library.AliasProbabilities);_alias=Upload(device,library.AliasIndices);
                    foreach(var channel in library.CustomData){_custom.Add(Upload(device,channel.Values));_present.Add(Upload(device,channel.Present.Select(p=>p?1:0).ToArray()));}
                    _library=library;_device=device;
                } catch {Dispose();throw;}
            }
        }
        positions=_positions;normals=_normals;uvs=_uvs;ranges=_ranges;vertices=_vertices;indices=_indices;assets=_assets;aliasProbabilities=_prob;aliasIndices=_alias;
        customValues=_custom.ToSpread();customPresent=_present.ToSpread();customNames=(_library?.CustomData.Select(c=>c.Name)??Enumerable.Empty<string>()).ToSpread();
    }
    private static GpuBuffer Upload<T>(GraphicsDevice device,T[] data) where T:unmanaged =>
        GpuBuffer.New(device,data.Length==0?new T[1]:data,BufferFlags.ShaderResource|BufferFlags.StructuredBuffer,GraphicsResourceUsage.Immutable);
    public void Dispose() {
        _positions?.Dispose();_normals?.Dispose();_uvs?.Dispose();_ranges?.Dispose();_vertices?.Dispose();_indices?.Dispose();_assets?.Dispose();_prob?.Dispose();_alias?.Dispose();
        foreach(var b in _custom)b.Dispose();foreach(var b in _present)b.Dispose();_custom.Clear();_present.Clear();
        _positions=_normals=_uvs=_ranges=_vertices=_indices=_assets=_prob=_alias=null;_library=null;_device=null;
    }
}
