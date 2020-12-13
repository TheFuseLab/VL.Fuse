namespace VL.ShaderFXtension
{
    public abstract class AbstractGPUReference: AbstractGpuValue
    {
        public string ArguemntKey { get; }

        public AbstractGPUReference(string theKey)
        {
            ArguemntKey = theKey;
        }
    }

    public class GPUReferenceOverride : AbstractGpuValue
    {
        public GPUReferenceOverride(string theID)
        {
            ID = theID;
        }
        public override string ID { get; }
    }
    
    public class GPUReference<T> : AbstractGPUReference
    {
        
        public GPUReference(ShaderNode theParent, string theKey) : base(theKey)
        {
            ParentNode = theParent;
        }

        public override string ID { get; }
    }
}