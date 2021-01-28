namespace Fuse
{
    public abstract class AbstractGpuValue
    {

        public abstract string ID { get; }

        public AbstractShaderNode ParentNode { get; set; }
    }
    
    public class GpuValue<T> : AbstractGpuValue
    {
        protected string name;

        public GpuValue(string theName)
        {
            name = theName;
        }

        public void ConstrainType()
        {
            
        }

        public override string ID => name + "_" + GetHashCode();

    }
}