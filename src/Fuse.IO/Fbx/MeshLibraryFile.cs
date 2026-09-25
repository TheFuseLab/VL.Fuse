using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace Fuse.IO.Fbx;

/// <summary>Optional static FBX library loader with an exact-content disk cache. Input order defines asset IDs.
/// Pulse Reload after editing source files. Cache misses use the established FBX loader.</summary>
[ProcessNode]
public sealed class MeshLibraryFile
{
    private string[] _paths=[]; private string _folder=""; private float _scale; private bool _reload;
    private FbxImportBackend _backend;
    private MeshLibrary? _library; private string _status="";
    public void Update(IEnumerable<string> paths, out MeshLibrary library, out string status,
        string cacheDirectory="", float sceneScale=0.01f, bool reload=false, FbxImportBackend backend=FbxImportBackend.Ufbx)
    {
        var input=paths.Select(Path.GetFullPath).ToArray();
        var folder=string.IsNullOrWhiteSpace(cacheDirectory)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Fuse","MeshLibraryCache"):Path.GetFullPath(cacheDirectory);
        bool edge=reload&&!_reload;_reload=reload;
        if(_library is null||edge||!input.SequenceEqual(_paths)||folder!=_folder||sceneScale!=_scale||backend!=_backend)
        {
            // Publish state only after successful loading. Never silently cache the loader's fallback quad.
            var next=Load(input,folder,sceneScale,out var message,backend);
            _library=next;_paths=input;_folder=folder;_scale=sceneScale;_backend=backend;_status=message;
        }
        library=_library;status=_status;
    }
    public static MeshLibrary Load(string[] paths,string folder,float scale,out string status,FbxImportBackend backend=FbxImportBackend.Ufbx)
    {
        if(!float.IsFinite(scale)||scale<=0)throw new ArgumentOutOfRangeException(nameof(scale));
        using var key=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        key.AppendData(Encoding.UTF8.GetBytes("FuseMeshLibrary-v1-"+typeof(MeshLibraryFile).Module.ModuleVersionId));
        key.AppendData(BitConverter.GetBytes(scale));key.AppendData(BitConverter.GetBytes((int)backend));
        foreach(var path in paths){using var file=File.OpenRead(path);key.AppendData(SHA256.HashData(file));}
        var cache=Path.Combine(folder,Convert.ToHexString(key.GetHashAndReset())+".fml");
        if(File.Exists(cache))try {var result=Read(cache);status="Disk cache hit; "+paths.Length+" assets";return result;}
            catch(Exception ex) when(ex is IOException or InvalidDataException or ArgumentException or OverflowException){/* Rebuild damaged or incompatible cache. */}
        var models=paths.Select(p=>FBXLoader.LoadFbxWithBackend(p,scale,30f,backend)).ToArray();
        // A fallback quad is indistinguishable from a genuine quad in this legacy API: reject both in this optional loader.
        if(models.Any(m=>m.Mesh.Vertices.Length<=4))throw new InvalidDataException("FBX failed or is a quad; use the legacy loader for quad assets. Cache not written.");
        var library=MeshLibraryBuilder.Build(models);
        if(library.Assets.Any(a=>a.Valid==0))throw new InvalidDataException(string.Join("; ",library.Diagnostics));
        try {Directory.CreateDirectory(folder);Write(cache,library);status="Disk cache created; "+paths.Length+" assets";}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){status="Loaded; cache not saved: "+ex.Message;}
        return library;
    }
    internal static void Write(string path,MeshLibrary l)
    {
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var w=new BinaryWriter(File.Create(temp))) {
                w.Write("FML1");Put(w,l.Vertices);Put(w,l.Indices);Put(w,l.AliasProbabilities);Put(w,l.AliasIndices);Put(w,l.Assets);Put(w,l.Bounds);
                w.Write(l.Diagnostics.Length);foreach(var d in l.Diagnostics)w.Write(d);
                w.Write(l.CustomData.Length);foreach(var c in l.CustomData){w.Write(c.Name);w.Write(c.Kind);w.Write(c.Index);w.Write(c.ComponentCount);Put(w,c.Values);Put(w,c.Present);}
            }
            byte[] checksum;using(var input=File.OpenRead(temp))checksum=SHA256.HashData(input);
            using(var append=new FileStream(temp,FileMode.Append))append.Write(checksum);
            File.Move(temp,path,true);
        } finally {if(File.Exists(temp))File.Delete(temp);}
    }
    internal static MeshLibrary Read(string path)
    {
        using var r=new BinaryReader(File.OpenRead(path));
        long payload=r.BaseStream.Length-32;if(payload<0)throw new InvalidDataException("Truncated cache");
        using(var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256)) {
            var block=new byte[1024*1024];long remaining=payload;
            while(remaining>0){int n=r.BaseStream.Read(block,0,(int)Math.Min(remaining,block.Length));if(n==0)throw new EndOfStreamException();hash.AppendData(block,0,n);remaining-=n;}
            var expected=r.ReadBytes(32);if(!hash.GetHashAndReset().SequenceEqual(expected))throw new InvalidDataException("Cache checksum");
        }
        r.BaseStream.Position=0;
        if(r.ReadString()!="FML1")throw new InvalidDataException("Cache version");
        var l=new MeshLibrary{Vertices=Get<GpuVertex>(r),Indices=Get<int>(r),AliasProbabilities=Get<float>(r),AliasIndices=Get<int>(r),Assets=Get<MeshLibraryAsset>(r),Bounds=Get<BoundingBox>(r)};
        int count=r.ReadInt32();if(count!=l.Assets.Length)throw new InvalidDataException("Diagnostics count");l.Diagnostics=new string[count];for(int i=0;i<count;i++)l.Diagnostics[i]=r.ReadString();
        count=r.ReadInt32();if(count<0||count>64)throw new InvalidDataException("Channel count");l.CustomData=new MeshCustomChannel[count];
        for(int i=0;i<count;i++)l.CustomData[i]=new(){Name=r.ReadString(),Kind=r.ReadString(),Index=r.ReadInt32(),ComponentCount=r.ReadInt32(),Values=Get<Vector4>(r),Present=Get<bool>(r)};
        if(r.BaseStream.Position!=payload||l.Bounds.Length!=l.Assets.Length||l.Indices.Length%3!=0||l.AliasIndices.Length!=l.Indices.Length/3||l.AliasProbabilities.Length!=l.AliasIndices.Length||l.CustomData.Any(c=>c.Values.Length!=l.Vertices.Length||c.Present.Length!=l.Vertices.Length))throw new InvalidDataException("Cache layout");
        return l;
    }
    private static void Put<T>(BinaryWriter w,T[] data) where T:unmanaged {w.Write(data.Length);w.Write(MemoryMarshal.AsBytes(data.AsSpan()));}
    private static T[] Get<T>(BinaryReader r) where T:unmanaged {
        int count=r.ReadInt32();long bytes=(long)count*Marshal.SizeOf<T>();
        // bool is one byte in the managed array, unlike Marshal.SizeOf<bool>().
        if(typeof(T)==typeof(bool))bytes=count;
        if(count<0||bytes>r.BaseStream.Length-r.BaseStream.Position)throw new InvalidDataException("Cache array length");
        var data=new T[count];r.BaseStream.ReadExactly(MemoryMarshal.AsBytes(data.AsSpan()));return data;
    }
}
