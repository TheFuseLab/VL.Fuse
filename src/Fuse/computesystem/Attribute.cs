namespace Fuse.ComputeSystem
{
    public interface IAttribute
    {
        public string Name { get; }
        
        public bool IsBuffered { get; }
        public bool IsDoubleBuffered { get; }
        
        public AbstractShaderNode ShaderNode { get; }
        
        

        public void SetInput(AbstractShaderNode theNode);
    }
    
    public class Attribute<T> : PassThroughNode<T>, IAttribute
    {
        
        public bool IsBuffered { get; }
        public bool IsDoubleBuffered { get; }
        
        public AbstractShaderNode ShaderNode { get; }

        public Attribute(string theGroup, string theName, bool theIsBuffered, bool theIsDoubleBuffered) : base(theName)
        {
            IsBuffered = theIsBuffered;
            IsDoubleBuffered = theIsDoubleBuffered;
            ShaderNode = new ShaderNode<T>(theName);
            
            AddResource(theGroup, this);
        }

        public void SetAbstractInput(AbstractShaderNode theNode)
        {
            SetInput(theNode);
        }
    }
}