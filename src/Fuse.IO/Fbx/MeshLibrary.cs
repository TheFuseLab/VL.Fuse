using System.Runtime.InteropServices;
namespace Fuse.IO.Fbx;

/// <summary>GPU metadata, 32 bytes. Indices are global; aliases remain asset-local.</summary>
[StructLayout(LayoutKind.Sequential, Pack=4)]
public struct MeshLibraryAsset
{
    public int VertexOffset, VertexCount, TriangleOffset, TriangleCount;
    public int AliasOffset, Valid;
    public float SurfaceArea;
    public int Reserved;
}

/// <summary>Owned CPU snapshot. Upload arrays with ImmutableBuffer or MeshLibraryBuffers.
/// Treat published arrays as immutable. Skeleton/clip banks are not combined.</summary>
public sealed class MeshLibrary
{
    public GpuVertex[] Vertices { get; internal set; } = [];
    public int[] Indices { get; internal set; } = [];
    public float[] AliasProbabilities { get; internal set; } = [];
    public int[] AliasIndices { get; internal set; } = [];
    public MeshLibraryAsset[] Assets { get; internal set; } = [];
    public BoundingBox[] Bounds { get; internal set; } = [];
    public MeshCustomChannel[] CustomData { get; internal set; } = [];
    public string[] Diagnostics { get; internal set; } = [];
    public MeshView GetMeshView(int assetId) => new(this,assetId);
    public MeshCustomChannel GetCustomChannel(string name) => CustomData.First(c=>c.Name==name);
}

/// <summary>Non-owning selection into a library, without copying geometry.</summary>
public sealed class MeshView
{
    public MeshLibrary Library { get; }
    public int AssetId { get; }
    public MeshLibraryAsset Asset => Library.Assets[AssetId];
    public bool Valid => Asset.Valid!=0;
    public MeshView(MeshLibrary library,int assetId) {
        ArgumentNullException.ThrowIfNull(library);
        if((uint)assetId>=(uint)library.Assets.Length)throw new ArgumentOutOfRangeException(nameof(assetId));
        Library=library;AssetId=assetId;
    }
}

/// <summary>Additive multi-model builder. Reference changes rebuild; pulse Reload after in-place edits.</summary>
[ProcessNode]
public sealed class MeshLibraryBuilder
{
    private sealed record Prepared(object Vertices, object Indices, object Custom, string Reason, BoundingBox Bounds, float Area, TriangleAliasTable? Table);
    private readonly Dictionary<MeshGpu, Prepared> _prepared = new(ReferenceEqualityComparer.Instance);
    private ModelGpu?[] _models=[];
    private MeshGpu?[] _meshes=[];
    private object?[] _arrays=[];
    private bool _reload;
    private MeshLibrary? _output;
    public void Update(IEnumerable<ModelGpu?> models,out MeshLibrary library,out string status,bool reload=false) {
        var input=models?.ToArray()??[];
        var meshes=input.Select(m=>m?.Mesh).ToArray();
        var arrays=meshes.SelectMany(m=>new object?[]{m?.Vertices,m?.Indices,m?.CustomData}).ToArray();
        bool edge=reload&&!_reload;_reload=reload;
        if(_output is null||edge||!Same(input,_models)||!Same(meshes,_meshes)||!Same(arrays,_arrays)) {
            if(edge) _prepared.Clear();
            var retained = new HashSet<MeshGpu>(meshes.OfType<MeshGpu>(), ReferenceEqualityComparer.Instance);
            foreach(var key in _prepared.Keys.Where(k=>!retained.Contains(k)).ToArray()) _prepared.Remove(key);
            _output=Build(input,_prepared);_models=input;_meshes=meshes;_arrays=arrays;
        }
        library=_output;status=string.Join("; ",library.Diagnostics.Where(s=>s.Length>0));
        if(status.Length==0)status=$"{library.Assets.Length} assets; {library.Vertices.Length} vertices; {library.Indices.Length/3} triangles";
    }
    private static bool Same<T>(T[] a,T[] b) where T:class? => a.Length==b.Length&&a.Where((t,i)=>!ReferenceEquals(t,b[i])).Any()==false;
    public static MeshLibrary Build(IEnumerable<ModelGpu?> models) => Build(models, new(ReferenceEqualityComparer.Instance));
    private static MeshLibrary Build(IEnumerable<ModelGpu?> models, Dictionary<MeshGpu, Prepared> cache) {
        var input=models.ToArray();var lib=new MeshLibrary{Assets=new MeshLibraryAsset[input.Length],Bounds=new BoundingBox[input.Length],Diagnostics=new string[input.Length]};
        var valid=new MeshGpu?[input.Length];var tables=new TriangleAliasTable?[input.Length];int nv=0,nt=0;
        for(int id=0;id<input.Length;id++) {
            lib.Diagnostics[id]="";var m=input[id]?.Mesh;
            if(m is null){lib.Diagnostics[id]=$"Asset {id}: missing model (slot retained)";continue;}
            if(!cache.TryGetValue(m,out var prepared)||!ReferenceEquals(prepared.Vertices,m.Vertices)||!ReferenceEquals(prepared.Indices,m.Indices)||!ReferenceEquals(prepared.Custom,m.CustomData)) {
                var reason=Validate(m);float area=0;var min=new Vector3(float.MaxValue);var max=new Vector3(float.MinValue);TriangleAliasTable? table=null;
                if(reason.Length==0) {
                    foreach(var v in m.Vertices){min=Vector3.Min(min,v.Position);max=Vector3.Max(max,v.Position);}
                    var areas=new float[m.Indices.Length/3];
                    for(int t=0;t<areas.Length;t++) {
                        int i=t*3;
                        areas[t]=Vector3.Cross(m.Vertices[m.Indices[i+1]].Position-m.Vertices[m.Indices[i]].Position,m.Vertices[m.Indices[i+2]].Position-m.Vertices[m.Indices[i]].Position).Length()*.5f;
                        area+=areas[t];
                    }
                    if(!float.IsFinite(area)||area<=0)reason="no finite positive surface area";
                    else table=GpuPacking.BuildTriangleAliasTable(areas);
                }
                prepared=new(m.Vertices,m.Indices,m.CustomData,reason,new(min,max),area,table);cache[m]=prepared;
            }
            if(prepared.Reason.Length>0){lib.Diagnostics[id]=$"Asset {id}: {prepared.Reason}";continue;}
            valid[id]=m;tables[id]=prepared.Table;
            lib.Assets[id]=new(){VertexOffset=nv,VertexCount=m.Vertices.Length,TriangleOffset=nt,TriangleCount=m.Indices.Length/3,AliasOffset=nt,Valid=1,SurfaceArea=prepared.Area};
            lib.Bounds[id]=prepared.Bounds;nv=checked(nv+m.Vertices.Length);nt=checked(nt+m.Indices.Length/3);
        }
        lib.Vertices=new GpuVertex[nv];lib.Indices=new int[checked(nt*3)];lib.AliasIndices=new int[nt];lib.AliasProbabilities=new float[nt];
        for(int id=0;id<input.Length;id++) {
            var m=valid[id];if(m is null)continue;var a=lib.Assets[id];
            Array.Copy(m.Vertices,0,lib.Vertices,a.VertexOffset,a.VertexCount);
            for(int i=0;i<m.Indices.Length;i++)lib.Indices[3*a.TriangleOffset+i]=checked(m.Indices[i]+a.VertexOffset);
            Array.Copy(tables[id]!.Prob,0,lib.AliasProbabilities,a.AliasOffset,a.TriangleCount);
            Array.Copy(tables[id]!.Alias,0,lib.AliasIndices,a.AliasOffset,a.TriangleCount);
        }
        var semantics=valid.Where(m=>m is not null).SelectMany(m=>m!.CustomData).GroupBy(c=>c.Name,StringComparer.Ordinal).OrderBy(g=>g.Key,StringComparer.Ordinal);
        var channels=new List<MeshCustomChannel>();
        foreach(var semantic in semantics) {
            var first=semantic.First();var values=new Vector4[nv];var present=new bool[nv];
            for(int id=0;id<input.Length;id++) {
                var c=valid[id]?.CustomData.FirstOrDefault(c=>c.Name==semantic.Key);if(c is null)continue;
                Array.Copy(c.Values,0,values,lib.Assets[id].VertexOffset,c.Values.Length);
                Array.Copy(c.Present,0,present,lib.Assets[id].VertexOffset,c.Present.Length);
            }
            channels.Add(new(){Name=first.Name,Kind=first.Kind,Index=first.Index,ComponentCount=semantic.Max(c=>c.ComponentCount),Values=values,Present=present});
        }
        lib.CustomData=channels.ToArray();return lib;
    }
    private static string Validate(MeshGpu? m) {
        if(m is null)return "missing model (slot retained)";
        if(m.Vertices is null||m.Indices is null||m.Vertices.Length==0||m.Indices.Length==0)return "empty mesh";
        if(m.Indices.Length%3!=0)return "index count is not divisible by three";
        if(m.Indices.Any(i=>i<0||i>=m.Vertices.Length))return "index outside vertex range";
        if(m.Vertices.Any(v=>!float.IsFinite(v.Position.X)||!float.IsFinite(v.Position.Y)||!float.IsFinite(v.Position.Z)))return "non-finite position";
        if(m.BoneCount>1)return "skinned mesh requires per-asset skeleton banks; static surface library only";
        if(m.CustomData is null||m.CustomData.Any(c=>c.Values.Length!=m.Vertices.Length||c.Present.Length!=m.Vertices.Length))return "custom channel length mismatch";
        if(m.CustomData.Select(c=>c.Name).Distinct().Count()!=m.CustomData.Length)return "duplicate custom channel name";
        return "";
    }
}

/// <summary>Single-input convenience node, using exactly the same library builder.</summary>
[ProcessNode]
public sealed class SingleMeshLibrary
{
    private readonly MeshLibraryBuilder _builder=new();
    private MeshLibrary? _last;private MeshView? _view;
    public void Update(ModelGpu? model,out MeshLibrary library,out MeshView mesh,out string status,bool reload=false) {
        _builder.Update(new[]{model},out library,out status,reload);
        if(!ReferenceEquals(library,_last)){_last=library;_view=library.GetMeshView(0);}
        mesh=_view!;
    }
}

/// <summary>CPU reference for the GPU algorithm. Random values must be independent in [0,1).
/// Returned indices address shared vertices/custom channels, not the original mesh.</summary>
public static class MeshLibrarySampling
{
    public static void SampleSurface(MeshView mesh,Vector4 random,out Vector3 position,out Vector3 normal,out Vector2 uv,out Int3 indices,out Vector3 barycentric,out bool valid) {
        position=normal=default;uv=default;indices=default;barycentric=default;valid=false;
        var a=mesh.Asset;if(a.Valid==0)return;
        foreach(float x in new[]{random.X,random.Y,random.Z,random.W})if(!float.IsFinite(x)||x<0||x>=1)throw new ArgumentOutOfRangeException(nameof(random));
        var l=mesh.Library;int bucket=Math.Min((int)(random.X*a.TriangleCount),a.TriangleCount-1);
        int t=random.Y<l.AliasProbabilities[a.AliasOffset+bucket]?bucket:l.AliasIndices[a.AliasOffset+bucket];
        int offset=3*(a.TriangleOffset+t);indices=new(l.Indices[offset],l.Indices[offset+1],l.Indices[offset+2]);
        float root=MathF.Sqrt(random.Z);barycentric=new(1-root,root*(1-random.W),root*random.W);
        var v0=l.Vertices[indices.X];var v1=l.Vertices[indices.Y];var v2=l.Vertices[indices.Z];
        position=v0.Position*barycentric.X+v1.Position*barycentric.Y+v2.Position*barycentric.Z;
        normal=v0.Normal*barycentric.X+v1.Normal*barycentric.Y+v2.Normal*barycentric.Z;
        if(normal.LengthSquared()>1e-20f)normal=Vector3.Normalize(normal);
        uv=v0.Tex*barycentric.X+v1.Tex*barycentric.Y+v2.Tex*barycentric.Z;valid=true;
    }
    public static Vector4 SampleCustomChannel(MeshCustomChannel channel,Int3 indices,Vector3 barycentric,out bool present) {
        present=channel.Present[indices.X]&&channel.Present[indices.Y]&&channel.Present[indices.Z];
        return channel.Values[indices.X]*barycentric.X+channel.Values[indices.Y]*barycentric.Y+channel.Values[indices.Z]*barycentric.Z;
    }
}

