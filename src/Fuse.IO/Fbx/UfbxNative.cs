using System.Runtime.InteropServices;
namespace Fuse.IO.Fbx;

/// <summary>Select Assimp explicitly for legacy import, or prefer ufbx with compatibility fallback.</summary>
public enum FbxImportBackend { Ufbx, Assimp }

/// <summary>Windows x64 native static-mesh adapter. Unsupported features are delegated to Assimp.</summary>
internal static class UfbxNative
{
    [StructLayout(LayoutKind.Sequential)]
    private struct View {public IntPtr Vertices,Indices;public int VertexCount,IndexCount,Colors;}
    [DllImport("Fuse.Ufbx.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern unsafe int fuse_ufbx_load(byte* data,nuint size,float scale,out IntPtr handle,out View view);
    [DllImport("Fuse.Ufbx.dll",CallingConvention=CallingConvention.Cdecl)]
    private static extern void fuse_ufbx_free(IntPtr handle);
    internal static unsafe bool TryLoad(string path,float scale,float fps,out ModelGpu model)
    {
        model=null!;if(!OperatingSystem.IsWindows()||RuntimeInformation.ProcessArchitecture!=Architecture.X64)return false;
        if(!float.IsFinite(scale)||scale<=0)return false;
        var data=File.ReadAllBytes(path);IntPtr handle=IntPtr.Zero;
        try {
            View view;int code;fixed(byte* bytes=data)code=fuse_ufbx_load(bytes,(nuint)data.Length,scale,out handle,out view);
            if(code!=0)return false;
            var vertices=new GpuVertex[view.VertexCount];var indices=new int[view.IndexCount];Marshal.Copy(view.Indices,indices,0,indices.Length);
            var channels=new MeshCustomChannel[view.Colors];
            for(int c=0;c<channels.Length;c++){var present=new bool[vertices.Length];Array.Fill(present,true);channels[c]=new(){Name="COLOR"+c,Kind="COLOR",Index=c,ComponentCount=4,Values=new Vector4[vertices.Length],Present=present};}
            float* p=(float*)view.Vertices;
            for(int i=0;i<vertices.Length;i++,p+=16){
                vertices[i]=new(){Position=new(p[0],p[1],p[2]),Normal=new(p[3],p[4],p[5]),Tex=new(p[6],p[7]),Bone=new Int4(0),Weights=new(1,0,0,0)};
                for(int c=0;c<channels.Length;c++){int o=8+c*4;channels[c].Values[i]=new(p[o],p[o+1],p[o+2],p[o+3]);}
            }
            model=new(){Mesh=new(){Vertices=vertices,Indices=indices,CustomData=channels,BoneCount=1},BoneNames=["Root"],Skeleton=new(){Parent=[-1],InverseBind=[Matrix.Identity],BindLocal=[Matrix.Identity]},Anim=new(){ClipNames=["StaticPose"],LocalDeltaDq=[new(){Qr=new(0,0,0,1),Qd=Vector4.Zero}],Clips=[new(){BoneCount=1,FrameCount=1,TicksPerSecond=fps,StepTick=1}]}};
            return true;
        }catch(DllNotFoundException){return false;}catch(BadImageFormatException){return false;}catch(EntryPointNotFoundException){return false;}
        finally{if(handle!=IntPtr.Zero)fuse_ufbx_free(handle);}
    }
}
