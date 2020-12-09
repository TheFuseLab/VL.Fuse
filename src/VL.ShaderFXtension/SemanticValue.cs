namespace VL.ShaderFXtension
{
    public class SemanticValue <T>: GpuValue<T>
    {
        public SemanticValue(string theName) : base(theName)
        {
        }
        
        public override string ID => "streams." + name;
    }
}