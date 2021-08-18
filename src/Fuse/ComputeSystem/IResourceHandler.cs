using Stride.Core.Mathematics;

namespace Fuse.ComputeSystem
{
    public interface IResourceHandler : ITicketHolder
    {
        public IResource CreateResource(string theName);

        public void GetThreadGroupInfo(ref Int3 theThreadGroupCount, ref Int3 theThreadGroupSize );

        public IResourceBinder CreateBinder(IResource theResource, GpuValue<Int3> theIndex);

        public int GetElementCount();
    }
}