namespace Fuse.ComputeSystem
{
    public class Member<T> : IMember
    {
        public string Name { get; }
        
        public bool IsBuffered { get; }
        public bool IsDoubleBuffered { get; }
        

        public Member(string theName, bool theIsBuffered, bool theIsDoubleBuffered = false)
        {
            Name = theName;
            IsBuffered = theIsBuffered;
            IsDoubleBuffered = theIsDoubleBuffered;
            ShaderNode = TypedShaderNode = new ShaderNode<T>(theName);
        }
        
        public AbstractShaderNode ShaderNode { get; }

        public ShaderNode<T> TypedShaderNode { get; }



    }
}