namespace VL.ShaderFXtension
{
    public abstract class AbstractGPUValue
    {
        public abstract string SourceCode();

        public abstract string ID { get; }

        public abstract IShaderNode ParentNode { get; set; }
    }
}