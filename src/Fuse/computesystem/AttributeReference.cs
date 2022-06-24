namespace Fuse.ComputeSystem
{
    public abstract class AbstractAttributeReference
    {
        public IMember Member { get; protected set; }

        public abstract void SetInput(AbstractShaderNode theNode);
    }
    
    public class AttributeReference<T> : AbstractAttributeReference
    {
        
        public PassThroughNode<T> ShaderNodeReference { get; private set; }

        public AttributeReference(
            IMember theMember,
            PassThroughNode<T> theReferenceNode)
        {
            Member = theMember;
            ShaderNodeReference = theReferenceNode;
        }

        public override void SetInput(AbstractShaderNode theNode)
        {
            ShaderNodeReference.SetInput(theNode);
        }
    }
}