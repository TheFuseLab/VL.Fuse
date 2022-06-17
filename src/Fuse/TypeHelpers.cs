﻿using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Buffer = Stride.Graphics.Buffer;

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
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainTypeByGpuValue<T>(GpuValue<T> input, T input2) where T : struct
            {
            }
            
             
             
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainTypesByBuffer<T>(GpuValue<T> input, GpuValue<Buffer<T>> input2) where T : struct
            {
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainType<T>(T value, GpuValue<T> gpuValue, Buffer<T> buffer, GpuValue<Buffer<T>> gpuBuffer) where T : struct
            {
            }
            
            public static void ConstrainBuffer<T>(T value, GpuValue<T> gpuValue, Buffer<T> buffer, GpuValue<Buffer<T>> gpuBuffer) where T : struct
            {
            }


            public static string GetDefaultForType<T>(T theValue)
            {
                
                if (typeof(T) == typeof(bool)) return theValue.ToString().ToLower();
                
                if (typeof(T) == typeof(float)) return theValue.ToString();
                
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
                
                if (typeof(T) == typeof(int)) return theValue.ToString();
                
                if (typeof(T) == typeof(Int2))
                {
                    var int2 = (Int2) Convert.ChangeType(theValue, typeof(Int2));
                    return $"float2({int2.X},{int2.Y})";
                }
                if (typeof(T) == typeof(Int3))
                {
                    var int3 = (Int3) Convert.ChangeType(theValue, typeof(Int3));
                    return $"float3({int3.X},{int3.Y},{int3.Z})";
                }
                if (typeof(T) == typeof(Int4))
                {
                    var int4 = (Int4) Convert.ChangeType(theValue, typeof(Int4));
                    return $"float4({int4.X},{int4.Y},{int4.Z},{int4.W})";
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
                
                
                {typeof(GpuStruct), "struct"},
              
                {typeof(Texture), "Texture"},
                {typeof(SamplerState), "Sampler"},
                
                {typeof(GpuVoid), "Void"},
                {typeof(Fuse.sdf.Ray), "Ray"},
            };
            
            private static readonly Dictionary<Type, string> KnownSignatureTypes = new Dictionary<Type, string>
            {
                {typeof(float), "Float"},
                {typeof(Vector2), "Float2"},
                {typeof(Vector3), "Float3"},
                {typeof(Vector4), "Float4"},
                {typeof(Color4), "Float4"},
                {typeof(Matrix), "Matrix"},
                
                {typeof(int), "Int"},
                {typeof(Int2), "Int2"},
                {typeof(Int3), "Int3"},
                {typeof(Int4), "Int4"},
                {typeof(uint), "UInt"},
                {typeof(bool), "Bool"},
                
                {typeof(GpuStruct), "Struct"},
                
                {typeof(Texture), "Texture"},
                {typeof(SamplerState), "Sampler"},
                
                {typeof(GpuVoid), "Void"},
                {typeof(Fuse.sdf.Ray), "Ray"},
            };
            
            private static readonly Dictionary<Type, PixelFormat> KnownPixelFormats = new Dictionary<Type, PixelFormat>
            {
                {typeof(float), PixelFormat.R32_Float},
                {typeof(Vector2), PixelFormat.R32G32_Float},
                {typeof(Vector3), PixelFormat.R32G32B32_Float},
                {typeof(Vector4), PixelFormat.R32G32B32A32_Float},
                {typeof(Color4), PixelFormat.R32G32B32A32_Float},
                {typeof(Matrix), PixelFormat.None},
                
                {typeof(int), PixelFormat.R32_SInt},
                {typeof(Int2), PixelFormat.R32G32_SInt},
                {typeof(Int3), PixelFormat.R32G32B32_SInt},
                {typeof(Int4), PixelFormat.R32G32B32A32_SInt},
                {typeof(uint), PixelFormat.R32_UInt},
                {typeof(bool), PixelFormat.None},
                
                {typeof(GpuStruct), PixelFormat.None},
                
                {typeof(Texture), PixelFormat.None},
                {typeof(SamplerState), PixelFormat.None},
                
                {typeof(GpuVoid), PixelFormat.None},
                {typeof(Fuse.sdf.Ray), PixelFormat.None},
            };
            
            private static readonly Dictionary<Type, string> StructTypes = new Dictionary<Type, string>
            {
                {typeof(Fuse.sdf.Ray), "Ray"},
            };
            
            private static readonly Dictionary<Type, int> KnownTypeSize = new Dictionary<Type, int>
            {
                {typeof(float), 4},
                {typeof(Vector2), 2 * 4},
                {typeof(Vector3), 3 * 4},
                {typeof(Vector4), 4 * 4},
                {typeof(Color4), 4 * 4},
                {typeof(Matrix), 16 * 4},
                
                {typeof(int), 4},
                {typeof(Int2), 2 * 4},
                {typeof(Int3), 3 * 4},
                {typeof(Int4), 4 * 4},
                {typeof(uint), 4},
                {typeof(bool), 1},
                
                {typeof(GpuStruct), 0},
               
                {typeof(Texture), 0},
                {typeof(SamplerState), 0},
                
                {typeof(GpuVoid), 0},
                {typeof(Fuse.sdf.Ray), 0},
            };
            
            public static PixelFormat GetPixelFormatForType<T>()
            {
                return GetPixelFormatForType(typeof(T));        
            }

            public static PixelFormat GetPixelFormatForType(Type t)
            {
                return KnownPixelFormats.TryGetValue(t, out var result) ? result : PixelFormat.None;
            }
            
            public static string GetGpuTypeForType<T>()
            {
                return GetGpuTypeForType(typeof(T));        
            }

            public static string GetGpuTypeForType(Type t)
            {
                return KnownGpuTypes.TryGetValue(t, out var result) ? result : t.Name;
            }
            
            public static string GetSignatureTypeForType<T>()
            {
                return GetSignatureTypeForType(typeof(T));        
            }

            public static string GetSignatureTypeForType(Type t)
            {
                if (KnownSignatureTypes.TryGetValue(t, out var result))
                    return result;
                
                throw new NotImplementedException("No name defined for type: " + t.Name + " : " + t.FullName);
            }
            
            public static bool IsStructType<T>()
            {
                return IsStructType(typeof(T));        
            }

            public static bool IsStructType(Type t)
            {
                return StructTypes.ContainsKey(t);
            }
            
            
            public static int GetGpuTypeSizeForType<T>()
            {
                return GetGpuTypeSizeForType(typeof(T));        
            }

            public static int GetGpuTypeSizeForType(Type t)
            {
                if (KnownTypeSize.TryGetValue(t, out var result))
                    return result;
                
                throw new NotImplementedException("No size defined for type: " + t.Name + " : " + t.FullName);
            }

            public static string GetGpuTypeByValue(AbstractGpuValue theValue)
            {
                return theValue switch
                {
                    GpuValue<float> _ => "float",
                    GpuValue<Vector2> _ => "float2",
                    GpuValue<Vector3> _ => "float3",
                    GpuValue<Vector4> _ => "float4",
                    GpuValue<Color4> _ => "float4",
                    GpuValue<Matrix> _ => "float4x4",
                    GpuValue<bool> _ => "bool",
                    GpuValue<int> _ => "int",
                    GpuValue<Int2> _ => "int2",
                    GpuValue<Int3> _ => "int3",
                    GpuValue<Int4> _ => "int4",
                    GpuValue<GpuVoid> _ => "void",
                    GpuValue<Fuse.sdf.Ray> _ => "Ray",
                    _ => throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName)
                };
            }
            
            public static int GetSizeInBytes(AbstractGpuValue theValue)
            {
                return theValue switch
                {
                    GpuValue<float> _ => 4,
                    GpuValue<Vector2> _ => 2 * 4,
                    GpuValue<Vector3> _ => 3 * 4,
                    GpuValue<Vector4> _ => 4 * 4,
                    GpuValue<Color4> _ => 4 * 4,
                    GpuValue<Matrix> _ => 4 * 4 * 4,
                    GpuValue<bool> _ => 1,
                    GpuValue<int> _ => 4,
                    GpuValue<Int2> _ => 2 * 4,
                    GpuValue<Int3> _ => 3 * 4,
                    GpuValue<Int4> _ => 4 * 4,
                    GpuValue<GpuVoid> _ => 0,
                    GpuValue<Fuse.sdf.Ray> _ => 4,
                    _ => throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName)
                };
            }
            
            public static string GetSignatureTypeByValue(AbstractGpuValue theValue)
            {
                return theValue switch
                {
                    GpuValue<float> _ => "Float",
                    GpuValue<Vector2> _ => "Float2",
                    GpuValue<Vector3> _ => "Float3",
                    GpuValue<Vector4> _ => "Float4",
                    GpuValue<Color4> _ => "Float4",
                    GpuValue<bool> _ => "Bool",
                    GpuValue<int> _ => "Int",
                    GpuValue<Int2> _ => "Int2",
                    GpuValue<Int3> _ => "Int3",
                    GpuValue<Int4> _ => "Int4",
                    GpuValue<GpuVoid> _ => "Void",
                    GpuValue<Fuse.sdf.Ray> _ => "Ray",
                    _ => throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName)
                };
            }
            
            public static int GetDimensionByValue(AbstractGpuValue theValue)
            {
                return theValue switch
                {
                    GpuValue<float> _ => 1,
                    GpuValue<Vector2> _ => 2,
                    GpuValue<Vector3> _ => 3,
                    GpuValue<Vector4> _ => 4,
                    GpuValue<Color4> _ => 4,
                    GpuValue<bool> _ => 1,
                    GpuValue<int> _ => 1,
                    GpuValue<Int2> _ => 2,
                    GpuValue<Int3> _ => 3,
                    GpuValue<Int4> _ => 4,
                    GpuValue<GpuVoid> _ => 0,
                    GpuValue<Fuse.sdf.Ray> _ => 1,
                    _ => throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName)
                };
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
                if (theType == typeof(bool)) return 1;

                return 0;
            }

            public static List<string> GetDescription(AbstractGpuValue theValue)
            {
                var keys = new List<string>() { "x", "y", "z", "w"};
                var dimension = GetDimensionByValue(theValue);
                var result = new List<string>();
                if (dimension == 1)
                {
                    result.Add(theValue.Name);
                }else{
                    for (var i = 0; i < dimension;i++)
                    {
                        result.Add(theValue.Name +"." + keys[i]);
                    }
                }
                return result;
            }

            public static GpuValue<TOut> AdaptiveSignature<TIn,TOut>(string thePrepend, GpuValue<TIn> theValue, out string theSignature)
            {
                theSignature = thePrepend + GetDimension(typeof(TOut)) + GetDimension(typeof(TIn));
                return null;
            }

            public static void LimitToStruct<T>(GpuValue<T> theValue) where T : struct 
            {
                
            }
            
            public static string TextureTypeName(Texture theTexture, bool theUseRW)
            {
                if(theTexture == null)return "Texture2D<float4>";

                var textureType = theTexture.Dimension switch
                {
                    TextureDimension.Texture1D => "Texture1D",
                    TextureDimension.Texture2D => "Texture2D",
                    TextureDimension.Texture3D => "Texture3D",
                    TextureDimension.TextureCube => "TextureCube",
                    _ => "Texture2D"
                };

                var textureDataType = theTexture.Format switch
                {
                    PixelFormat.R16_Float => "half",
                    PixelFormat.R16G16_Float => "half2",
                    PixelFormat.R16G16B16A16_Float => "half4",
                    PixelFormat.R32_Float => "float",
                    PixelFormat.R32G32_Float => "float2",
                    PixelFormat.R32G32B32A32_Float => "float4",
                    _ => "float4"
                };
                
                if ((theTexture.Flags & TextureFlags.UnorderedAccess) == TextureFlags.UnorderedAccess && theUseRW)
                {
                    return "RW"+textureType+"<" + textureDataType + ">";
                }

                return textureType + "<" + textureDataType + ">";
            }
            
            public static string BufferTypeName(Buffer theBuffer,string theBufferType, bool theAppend, bool comnpute = true)
            {
                if(theBuffer == null)return "StructuredBuffer<" + theBufferType + ">";

                if ((theBuffer.Flags & BufferFlags.StructuredAppendBuffer) == BufferFlags.StructuredAppendBuffer)
                {
                    if (theAppend)
                    {
                        return "AppendStructuredBuffer<" + theBufferType + ">";
                    }

                    return "ConsumeStructuredBuffer<" + theBufferType + ">";
                }
             
                if ((theBuffer.Flags & BufferFlags.UnorderedAccess) == BufferFlags.UnorderedAccess && comnpute)
                {
                    return "RWStructuredBuffer<" + theBufferType + ">";
                }
                
                return "StructuredBuffer<" + theBufferType + ">";
            }
        }
}