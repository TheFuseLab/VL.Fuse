namespace Fuse
{

    public interface IGpuStruct
    {
        string TypeName();
    }

    public class GpuStruct : IGpuStruct
    {
        public string TypeName()
        {
            return "GpuStruct";
        }
    }
}