using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{

    public static class ConstantHelper
    {
        // ReSharper disable once UnusedMember.Global
        // USED IN VL
        public static ConstantValue<T> FromFloat<T>(NodeContext nodeContext, float theValue)
        {
            if (typeof(T) == typeof(float)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(theValue, typeof(float)));
            if (typeof(T) == typeof(Vector2)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Vector2(theValue,theValue), typeof(Vector2)));
            if (typeof(T) == typeof(Vector3)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Vector3(theValue,theValue,theValue), typeof(Vector3)));
            if (typeof(T) == typeof(Vector4)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Vector4(theValue,theValue,theValue,theValue), typeof(Vector4)));
            if (typeof(T) == typeof(Color4)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Color4(theValue,theValue,theValue,theValue), typeof(Color4)));

            var intValue = (int) theValue;
            if (typeof(T) == typeof(int)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(intValue, typeof(int)));
            if (typeof(T) == typeof(Int2)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Int2(intValue,intValue), typeof(Int2)));
            if (typeof(T) == typeof(Int3)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Int3(intValue,intValue,intValue), typeof(Int3)));
            if (typeof(T) == typeof(Int4)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(new Int4(intValue,intValue,intValue,intValue), typeof(Int4)));

            var boolValue = theValue > 0;
            if (typeof(T) == typeof(bool)) return new ConstantValue<T>(nodeContext,(T)Convert.ChangeType(boolValue, typeof(bool)));
            
            return null;
        }

        public static AbstractShaderNode AbstractFromFloat(NodeContext nodeContext, Type theType, float theValue)
        {
            if (theType == typeof(float)) return new ConstantValue<float>(nodeContext, theValue);
            if (theType == typeof(Vector2)) return new ConstantValue<Vector2>(nodeContext,new Vector2(theValue,theValue));
            if (theType == typeof(Vector3)) return new ConstantValue<Vector3>(nodeContext,new Vector3(theValue,theValue,theValue));
            if (theType == typeof(Vector4)) return new ConstantValue<Vector4>(nodeContext,new Vector4(theValue,theValue,theValue,theValue));

            var intValue = (int) theValue;
            if (theType == typeof(int)) return new ConstantValue<int>(nodeContext,intValue);
            if (theType == typeof(Int2)) return new ConstantValue<Int2>(nodeContext,new Int2(intValue,intValue));
            if (theType == typeof(Int3)) return new ConstantValue<Int3>(nodeContext,new Int3(intValue,intValue,intValue));
            if (theType == typeof(Int4)) return new ConstantValue<Int4>(nodeContext,new Int4(intValue,intValue,intValue,intValue));
            
            return null;
        }
    }
    
    public class ConstantValue<T>: ShaderNode<T>
    {
        private T _value;
        public ConstantValue(NodeContext nodeContext, T theValue) : base(nodeContext, "constant", null, false)
        {
            _value = theValue;
        }


        public T Value
        {
            get => _value;
            set
            {
                if (value == null) return;
                if (value.Equals(_value)) return;
                _value = value;
                CallChangeEvent();
            }
        }

        public override string ID => TypeHelpers.GetDefaultForType(Value);
        public override string TypeName()
        {
            return TypeHelpers.GetGpuType<T>();
        }

        protected override string SourceTemplate()
        {
            return "";//TypeHelpers.GetDefaultForType(Value);
        }
    }

    
}