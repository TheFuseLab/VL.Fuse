namespace VL.ShaderFXtension
{
    public abstract class AbstractGpuValue
    {
        public abstract string SourceCode();

        public abstract string ID { get; }

        public abstract IShaderNode ParentNode { get; set; }
    }
}