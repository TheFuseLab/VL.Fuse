using Fuse.compute;

namespace Fuse.ComputeSystem
{
    public interface IResourceBinder
    {
        public GpuValue<GpuVoid> WriteAttributes();

        public AbstractGpuValue GetAttribute(string theAttribute);
    }
}