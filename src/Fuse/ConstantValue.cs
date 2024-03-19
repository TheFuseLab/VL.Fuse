﻿using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{

    public static class ConstantHelper
    {
        // ReSharper disable once UnusedMember.Global
        // USED IN VL
        public static ConstantValue<T> FromFloat<T>(float theValue)
        {
            if (typeof(T) == typeof(float)) return new ConstantValue<T>((T)Convert.ChangeType(theValue, typeof(float)));
            if (typeof(T) == typeof(Vector2)) return new ConstantValue<T>((T)Convert.ChangeType(new Vector2(theValue,theValue), typeof(Vector2)));
            if (typeof(T) == typeof(Vector3)) return new ConstantValue<T>((T)Convert.ChangeType(new Vector3(theValue,theValue,theValue), typeof(Vector3)));
            if (typeof(T) == typeof(Vector4)) return new ConstantValue<T>((T)Convert.ChangeType(new Vector4(theValue,theValue,theValue,theValue), typeof(Vector4)));
            if (typeof(T) == typeof(Color4)) return new ConstantValue<T>((T)Convert.ChangeType(new Color4(theValue,theValue,theValue,theValue), typeof(Color4)));

            var intValue = (int) theValue;
            if (typeof(T) == typeof(int)) return new ConstantValue<T>((T)Convert.ChangeType(intValue, typeof(int)));
            if (typeof(T) == typeof(Int2)) return new ConstantValue<T>((T)Convert.ChangeType(new Int2(intValue,intValue), typeof(Int2)));
            if (typeof(T) == typeof(Int3)) return new ConstantValue<T>((T)Convert.ChangeType(new Int3(intValue,intValue,intValue), typeof(Int3)));
            if (typeof(T) == typeof(Int4)) return new ConstantValue<T>((T)Convert.ChangeType(new Int4(intValue,intValue,intValue,intValue), typeof(Int4)));

            var boolValue = theValue > 0;
            if (typeof(T) == typeof(bool)) return new ConstantValue<T>((T)Convert.ChangeType(boolValue, typeof(bool)));
            
            return null;
        }

        public static AbstractShaderNode AbstractFromFloat(Type theType, float theValue)
        {
            if (theType == typeof(float)) return new ConstantValue<float>( theValue);
            if (theType == typeof(Vector2)) return new ConstantValue<Vector2>(new Vector2(theValue,theValue));
            if (theType == typeof(Vector3)) return new ConstantValue<Vector3>(new Vector3(theValue,theValue,theValue));
            if (theType == typeof(Vector4)) return new ConstantValue<Vector4>(new Vector4(theValue,theValue,theValue,theValue));

            var intValue = (int) theValue;
            if (theType == typeof(int)) return new ConstantValue<int>(intValue);
            if (theType == typeof(Int2)) return new ConstantValue<Int2>(new Int2(intValue,intValue));
            if (theType == typeof(Int3)) return new ConstantValue<Int3>(new Int3(intValue,intValue,intValue));
            if (theType == typeof(Int4)) return new ConstantValue<Int4>(new Int4(intValue,intValue,intValue,intValue));
            
            return null;
        }
    }
    
    public class ConstantValue<T>: ShaderNode<T>
    {
        public ConstantValue(T theValue) : base(NodeContext.CurrentRoot, "constant", null, false)
        {
            Value = theValue;
            HasFixedName = true;
        }
        
        public T Value { get; }

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

    public class ConstantKeyword : ShaderNode<float>
    {

        private readonly string _keyword;

        protected ConstantKeyword(NodeContext nodeContext, string theKeyword) : base( nodeContext, "constant keyword")
        {
            _keyword = theKeyword;
            SetInputs(new List<AbstractShaderNode>{null});
            HasFixedName = true;
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return _keyword;
        }

        protected override string SourceTemplate()
        {
            return _keyword;
        }
    }
    
}