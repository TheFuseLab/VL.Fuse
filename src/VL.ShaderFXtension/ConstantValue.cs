namespace VL.ShaderFXtension
{

    public abstract class AbstractConstantValue : AbstractGpuValue
    {
        
    }
    public class ConstantValue<T>: AbstractConstantValue
    {
        private T _myValue;
        public ConstantValue(T theValue) : base()
        {
            _myValue = theValue;
        }
        
        public override string ID => TypeHelpers.GetDefaultForType<T>(_myValue);
    }
}