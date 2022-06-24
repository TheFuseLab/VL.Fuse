namespace Fuse.ComputeSystem
{
    public abstract class AbstractAttribute
    {
        public IMember Member { get; protected set; }

        public abstract void SetInput(AbstractShaderNode theNode);
    }
    
    public class Attribute<T> : AbstractAttribute
    {
        
        public PassThroughNode<T> Node { get; private set; }

        public Attribute(string theGroup, string theName, bool theIsBuffered, bool theIsDoubleBuffered)
        {
            Member = new Member<T>(theName, theIsBuffered, theIsDoubleBuffered);
            Node = new PassThroughNode<T>();
            Node.AddResource(theGroup, this);
        }

        public override void SetInput(AbstractShaderNode theNode)
        {
            Node.SetInput(theNode);
        }
    }
}