namespace Fuse.ComputeSystem
{
    public class Attribute<T>
    {

        public ShaderNode<T> Node { get; }

        public Attribute(string theGroup, string theName, bool theIsBuffered, bool theIsDoubleBuffered)
        {

            var member = new Member<T>(theName, theIsBuffered, theIsDoubleBuffered);
            var referenceNode = new PassThroughNode<T>();
            var attributeReference = new AttributeReference<T>(member, referenceNode);
            
            referenceNode.AddResource(theGroup, attributeReference);

            Node = referenceNode;
        }
    }
}