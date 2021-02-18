using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Stride.Shaders.ShaderFX;
using VL.Stride.Shaders.ShaderFX.Functions;

namespace Fuse
{
    
        public static class TypeHelpers
        {
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainTypes<T>(T input, T input2, T input3, T input4)
            {
                
            }
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainTypesByEnumerable<T>(T input, IEnumerable<T> input2)
            {
            }
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainTypesByDictionary<T>(T input, IDictionary<string,T> input2)
            {
            }

            private static readonly Dictionary<Type, string> TypeDefaults = new Dictionary<Type, string>();

            public static string GetDefaultForType<T>(T theValue)
            {
                if (typeof(T) == typeof(float)) return theValue.ToString();
                if (typeof(T) == typeof(bool)) return theValue.ToString().ToLower();
                if (typeof(T) == typeof(int)) return theValue.ToString();
                
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
                
                if (typeof(T) == typeof(SamplerState))
                {
                    return "Sampler";
                }
                if (typeof(T) == typeof(Texture))
                {
                    return "Texture0";
                }
                
                return GetDefaultForType(typeof(T));        
            }
            
            public static string GetDefaultForType(Type t)
            {
                if (TypeDefaults.TryGetValue(t, out var result))
                    return result;

                throw new NotImplementedException("No name defined for type: " + t.Name);
            }

            private static readonly Dictionary<Type, string> KnownTypes = new Dictionary<Type, string>();

            static TypeHelpers()
            {
                KnownTypes.Add(typeof(float), "Float");
                KnownTypes.Add(typeof(Vector2), "Float2");
                KnownTypes.Add(typeof(Vector3), "Float3");
                KnownTypes.Add(typeof(Vector4), "Float4");
                KnownTypes.Add(typeof(Color4), "Float4");
                KnownTypes.Add(typeof(Matrix), "Float4x4");
                KnownTypes.Add(typeof(int), "Int");
                KnownTypes.Add(typeof(Int2), "Int2");
                KnownTypes.Add(typeof(Int3), "Int3");
                KnownTypes.Add(typeof(Int4), "Int4");
                KnownTypes.Add(typeof(uint), "UInt");
                KnownTypes.Add(typeof(bool), "Bool");
                KnownTypes.Add(typeof(Texture), "Texture");
                KnownTypes.Add(typeof(SamplerState), "Sampler");
                
                TypeDefaults.Add(typeof(float), "0.0");
                TypeDefaults.Add(typeof(Vector2), "float2(0.0, 0.0)");
                TypeDefaults.Add(typeof(Vector3), "float3(0.0, 0.0, 0.0)");
                TypeDefaults.Add(typeof(Vector4), "float4(0.0, 0.0, 0.0, 0.0)");
                TypeDefaults.Add(typeof(Color4), "float4(0.0, 0.0, 0.0, 0.0)");
                TypeDefaults.Add(typeof(Matrix), "float4x4()");
                TypeDefaults.Add(typeof(int), "0");
                TypeDefaults.Add(typeof(Int2), "int2(0, 0)");
                TypeDefaults.Add(typeof(Int3), "int3(0, 0)");
                TypeDefaults.Add(typeof(Int4), "int4(0, 0)");
                TypeDefaults.Add(typeof(uint), "0");
                TypeDefaults.Add(typeof(bool), "true");
            }

            public static string GetNameForType<T>()
            {
                return GetNameForType(typeof(T));        
            }

            public static string GetNameForType(Type t)
            {
                if (KnownTypes.TryGetValue(t, out var result))
                    return result;

                throw new NotImplementedException("No name defined for type: " + t.Name + " : " + t.FullName);
            }

            public static string GetTypeByValue(AbstractGpuValue theValue)
            {
                switch (theValue)
                {
                    case GpuValue<float> _:
                        return "Float";
                    case GpuValue<Vector2> _:
                        return "Float2";
                    case GpuValue<Vector3> _:
                        return "Float3";
                    case GpuValue<Vector4> _:
                        return "Float4";
                    case GpuValue<bool> _:
                        return "Bool";
                    case GpuValue<int> _:
                        return "Int";
                    case GpuValue<Int2> _:
                        return "Int2";
                    case GpuValue<Int3> _:
                        return "Int3";
                    case GpuValue<Int4> _:
                        return "Int4";
                }
                throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName);
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global
            public static string GetTypeByGeneric<T>(GpuValue<T> theValue)
            {
                return GetNameForType<T>();
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global
            public static int GetDimension<T>(GpuValue<T> theValue)
            {
                return GetDimension(typeof(T));
            }
            
            public static int GetDimension(Type theType)
            {
                if (theType == typeof(float)) return 1;
                if (theType == typeof(Vector2)) return 2;
                if (theType == typeof(Vector3)) return 3;
                if (theType == typeof(Vector4)) return 4;
                if (theType == typeof(Color4)) return 4;
                if (theType == typeof(int)) return 1;
                if (theType == typeof(Int2)) return 2;
                if (theType == typeof(Int3)) return 3;
                if (theType == typeof(Int4)) return 4;

                return 0;
            }

            public static GpuValue<TOut> AdaptiveSignature<TIn,TOut>(string thePrepend, GpuValue<TIn> theValue, out string theSignature)
            {
                theSignature = thePrepend + GetDimension(typeof(TOut)) + GetDimension(typeof(TIn));
                return null;
            }

            public static void LimitToStruct<T>(GpuValue<T> theValue) where T : struct 
            {
                
            }
        }
}