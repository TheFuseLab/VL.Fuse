namespace Fuse.ComputeSystem
{
    public interface IMember
    {
        public string Name { get; }

        public AbstractShaderNode ShaderNode { get; }

        public bool IsBuffered { get; }

        public bool IsDoubleBuffered { get; }
    }
}