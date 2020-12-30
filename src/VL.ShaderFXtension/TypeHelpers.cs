using System;
using System.Collections;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Stride.Shaders.ShaderFX;
using VL.Stride.Shaders.ShaderFX.Functions;
using Buffer = System.Buffer;

namespace VL.ShaderFXtension
{
    
        public static class TypeHelpers
        {
            

            public static void ConstrainTypes<T>(T input, T input2, T input3, T input4)
            {
            }
            
            public static void ConstrainTypesByEnumerable<T>(T input, IEnumerable<T> input2)
            {
            }
            
            public static void ConstrainTypesByDictionary<T>(T input, IDictionary<string,T> input2)
            {
            }

            public static void ConstrainTypesByFunction<T1, T2>(Funk1In1Out<T1, T2> funk, SetVar<T1> inputT1, SetVar<T2> inputT2)
            {
                
            }
            
            public static void ConstrainTypes<T>(GPUReference<T> inputT1, GpuValue<T> inputT2)
            {
                
            }
            
            public static (string inputType, string outputType) FunkTypes<T1, T2>(Funk1In1Out<T1, T2> funk)
            {
                return (
                    ShaderFXUtils.GetNameForType<T1>(),
                    ShaderFXUtils.GetNameForType<T2>()
                );
            }
            
            public static void FunkTypes<T1, T2>(Funk1In1Out<T1, T2> funk, out string inputType, out string outputType, out string signature)
            {

                inputType = ShaderFXUtils.GetNameForType<T1>();
                outputType = ShaderFXUtils.GetNameForType<T2>();
                signature = inputType + "To" + outputType;
            }
            
            static Dictionary<Type, string> TypeDefaults = new Dictionary<Type, string>();

            public static string GetDefaultForType<T>(T theValue)
            {
                if (typeof(T) == typeof(float)) return theValue.ToString();
                if (typeof(T) == typeof(Vector2))
                {
                    var vec2 = (Vector2) Convert.ChangeType(theValue, typeof(Vector2));
                    return $"float2({vec2.X},{vec2.Y})";
                }
                if (typeof(T) == typeof(Vector3))
                {
                    var vec3 = (Vector3) Convert.ChangeType(theValue, typeof(Vector3));
                    return $"float3({vec3.X},{vec3.Y},{vec3.Z})";
                }
                if (typeof(T) == typeof(Vector4))
                {
                    var vec4 = (Vector4) Convert.ChangeType(theValue, typeof(Vector4));
                    return $"float4({vec4.X},{vec4.Y},{vec4.Z},{vec4.W})";
                }
                return GetDefaultForType(typeof(T));        
            }
            
            

            public static string GetDefaultForType(Type t)
            {
                if (TypeDefaults.TryGetValue(t, out var result))
                    return result;

                throw new NotImplementedException("No name defined for type: " + t.Name);
            }
            
            static Dictionary<Type, string> KnownTypes = new Dictionary<Type, string>();

            static TypeHelpers()
            {
                KnownTypes.Add(typeof(float), "Float");
                KnownTypes.Add(typeof(Vector2), "Float2");
                KnownTypes.Add(typeof(Vector3), "Float3");
                KnownTypes.Add(typeof(Vector4), "Float4");
                KnownTypes.Add(typeof(Matrix), "Matrix");
                KnownTypes.Add(typeof(int), "Int");
                KnownTypes.Add(typeof(Int2), "Int2");
                KnownTypes.Add(typeof(Int3), "Int3");
                KnownTypes.Add(typeof(Int4), "Int4");
                KnownTypes.Add(typeof(uint), "UInt");
                KnownTypes.Add(typeof(bool), "Bool");
                
                TypeDefaults.Add(typeof(float), "0.0");
                TypeDefaults.Add(typeof(Vector2), "float2(0.0, 0.0)");
                TypeDefaults.Add(typeof(Vector3), "float3(0.0, 0.0, 0.0)");
                TypeDefaults.Add(typeof(Vector4), "float4(0.0, 0.0, 0.0, 0.0)");
                TypeDefaults.Add(typeof(Matrix), "Matrix");
                TypeDefaults.Add(typeof(int), "Int");
                TypeDefaults.Add(typeof(Int2), "Int2");
                TypeDefaults.Add(typeof(Int3), "Int3");
                TypeDefaults.Add(typeof(Int4), "Int4");
                TypeDefaults.Add(typeof(uint), "UInt");
                TypeDefaults.Add(typeof(bool), "Bool");
            }

            public static string GetNameForType<T>()
            {
                return GetNameForType(typeof(T));        
            }

            public static string GetNameForType(Type t)
            {
                if (KnownTypes.TryGetValue(t, out var result))
                    return result;

                throw new NotImplementedException("No name defined for type: " + t.Name);
            }

            public static string GetType(AbstractGPUReference theReference)
            {
                switch (theReference)
                {
                    case GPUReference<float> _:
                        return "Float";
                    case GPUReference<Vector2> _:
                        return "Float2";
                    case GPUReference<Vector3> _:
                        return "Float3";
                    case GPUReference<Vector4> _:
                        return "Float4";
                }
                throw new NotImplementedException("No name defined for type: " + theReference.GetType().FullName);
            }
            
            public static string GetType(AbstractGpuValue theReference)
            {
                switch (theReference)
                {
                    case GpuValue<float> _:
                        return "Float";
                    case GpuValue<Vector2> _:
                        return "Float2";
                    case GpuValue<Vector3> _:
                        return "Float3";
                    case GpuValue<Vector4> _:
                        return "Float4";
                }
                throw new NotImplementedException("No name defined for type: " + theReference.GetType().FullName);
            }
            
            public static string GetType<T1>(T1 var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
   
            public static string VarType<T1>(SetVar<T1> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            public static string VarType<T1>(IEnumerable<SetVar<T1>> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            
            public static string VarType<T1>(IDictionary<string,SetVar<T1>> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            
            public static string VarType<T1>(GetVar<T1> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            public static string VarType<T1>(IEnumerable<GetVar<T1>> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            
            public static string VarType<T1>(IDictionary<string,GetVar<T1>> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
        }
}