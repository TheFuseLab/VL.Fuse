using System;
using Stride.Core.Mathematics;

namespace VL.ShaderFXtension
{

    
    public class ConstantValue<T>: GpuValue<T>
    {
        private T _myValue;
        public ConstantValue(T theValue) : base("constant")
        {
            _myValue = theValue;
        }
        
        public ConstantValue(float theValue): base("constant")
        {
           
                if (typeof(T) == typeof(float)) _myValue = (T)Convert.ChangeType(theValue, typeof(float));
                if (typeof(T) == typeof(Vector2))_myValue = (T)Convert.ChangeType(new Vector2(theValue,theValue), typeof(Vector2));
                if (typeof(T) == typeof(Vector3))_myValue = (T)Convert.ChangeType(new Vector3(theValue,theValue,theValue), typeof(Vector3));
                if (typeof(T) == typeof(Vector4))_myValue = (T)Convert.ChangeType(new Vector4(theValue,theValue,theValue,theValue), typeof(Vector4));
        }
        
        public override string ID => TypeHelpers.GetDefaultForType<T>(_myValue);
    }
}