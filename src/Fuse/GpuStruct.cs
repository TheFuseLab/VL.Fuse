namespace Fuse
{

    public interface IGpuStruct
    {
        string TypeName();
    }

    public class GpuStruct : IGpuStruct
    {
        private readonly string _typeName;
        public GpuStruct()
        {
            _typeName = "GpuStruct";
        }
        
        public string TypeName()
        {
            return _typeName;
        }
    }
}