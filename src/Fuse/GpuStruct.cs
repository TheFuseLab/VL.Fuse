namespace Fuse
{

    public interface IGpuStruct
    {
        string TypeName();
    }

    public class GpuStruct : IGpuStruct
    {
        private readonly string _typeName;
        public GpuStruct(string theTypeName = "GpuStruct")
        {
            _typeName = theTypeName;
        }
        
        public string TypeName()
        {
            return _typeName;
        }
    }

    
}