namespace VL.ShaderFXtension
{
    public class GpuValue<T> : AbstractGpuValue
    {
        protected string name;

        public GpuValue(string theName)
        {
            name = theName;
        }
        public override string SourceCode()
        {
            return "";
        }

        public void ConstrainType()
        {
            
        }

        public override string ID => name + "_" + GetHashCode();


        public override IShaderNode ParentNode { get; set; }
    }
}