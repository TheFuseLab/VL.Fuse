namespace Fuse.ComputeSystem
{
    public interface IResource
    {
        public void CreateResources();

        public void AddListener(IResourceListener theListener);

        public void AddAttribute(IMember theMember);
    }
}