using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using Stride.Graphics;

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
            
            private static readonly Dictionary<Type, string> TypeDefaults = new Dictionary<Type, string>
            {
                {typeof(float), "0.0"},
                {typeof(Vector2), "float2(0.0, 0.0)"},
                {typeof(Vector3), "float3(0.0, 0.0, 0.0)"},
                {typeof(Vector4), "float4(0.0, 0.0, 0.0, 0.0)"},
                {typeof(Color4), "float4(0.0, 0.0, 0.0, 0.0)"},
                {typeof(Matrix), "float4x4()"},
                
                {typeof(int), "0"},
                {typeof(Int2), "int2(0, 0)"},
                {typeof(Int3), "int3(0, 0, 0)"},
                {typeof(Int4), "int4(0, 0, 0, 0)"},
                {typeof(uint), "0"},
                {typeof(bool), "true"},
            };

            private static readonly Dictionary<Type, string> KnownGpuTypes = new Dictionary<Type, string>
            {
                {typeof(float), "float"},
                {typeof(Vector2), "float2"},
                {typeof(Vector3), "float3"},
                {typeof(Vector4), "float4"},
                {typeof(Color4), "float4"},
                {typeof(Matrix), "float4x4"},
                
                {typeof(int), "int"},
                {typeof(Int2), "int2"},
                {typeof(Int3), "int3"},
                {typeof(Int4), "int4"},
                {typeof(uint), "uint"},
                {typeof(bool), "bool"},
                
                {typeof(Texture), "Texture"},
                {typeof(SamplerState), "Sampler"},
                {typeof(GpuVoid), "Void"},
                {typeof(Fuse.sdf.Ray), "Ray"},
            };
            
            private static readonly Dictionary<Type, string> KnownShaderTypes = new Dictionary<Type, string>
            {
                {typeof(float), "Float"},
                {typeof(Vector2), "Float2"},
                {typeof(Vector3), "Float3"},
                {typeof(Vector4), "Float4"},
                {typeof(Color4), "Float4"},
                {typeof(Matrix), "Float4x4"},
                
                {typeof(int), "Int"},
                {typeof(Int2), "Int2"},
                {typeof(Int3), "Int3"},
                {typeof(Int4), "Int4"},
                {typeof(uint), "Unt"},
                {typeof(bool), "Bool"},
                
                {typeof(Texture), "Texture"},
                {typeof(SamplerState), "Sampler"},
                {typeof(GpuVoid), "Void"},
                {typeof(Fuse.sdf.Ray), "Ray"},
            };
            
            public static string GetGpuTypeForType<T>()
            {
                return GetGpuTypeForType(typeof(T));        
            }

            public static string GetGpuTypeForType(Type t)
            {
                if (KnownGpuTypes.TryGetValue(t, out var result))
                    return result;
                
                throw new NotImplementedException("No name defined for type: " + t.Name + " : " + t.FullName);
            }
            
            public static string GetShaderTypeForType<T>()
            {
                return GetShaderTypeForType(typeof(T));        
            }

            public static string GetShaderTypeForType(Type t)
            {
                if (KnownShaderTypes.TryGetValue(t, out var result))
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
                    case GpuValue<Fuse.sdf.Ray> _:
                        return "Ray";
                }
                throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName);
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global
            public static string GetTypeByGeneric<T>(GpuValue<T> theValue)
            {
                return GetGpuTypeForType<T>();
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