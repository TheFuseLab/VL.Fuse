namespace VL.ShaderFXtension
{
    public class GPUValue<T> : AbstractGPUValue
    {
        private string name;

        public GPUValue(string theName)
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