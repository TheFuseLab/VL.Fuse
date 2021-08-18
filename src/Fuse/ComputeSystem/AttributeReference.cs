namespace Fuse.ComputeSystem
{
    public class AttributeReference
    {
        public AttributeReference(IMember theMember, IReferenceNode theReferenceNode)
        {
            Member = theMember;
            Reference = theReferenceNode;
        }

        public IMember Member { get; }

        public IReferenceNode Reference { get; }

        public AbstractGpuValue AttributeGraph { get; set; }
    }
}