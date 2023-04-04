using System;
using System.Collections.Generic;
using Fuse.compute;
using Fuse.function;
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
            public static void ConstrainTypeByShaderNode<T>(ShaderNode<T> input, T input2) where T : struct
            {
            }
            
             
             
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainTypesByBuffer<T>(ShaderNode<T> input, ShaderNode<Buffer<T>> input2) where T : unmanaged
            {
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global 
            public static void ConstrainType<T>(T value, ShaderNode<T> shaderNode, Buffer<T> buffer, ShaderNode<Buffer<T>> gpuBuffer) where T : unmanaged
            {
            }
            
            public static void ConstrainBuffer<T>(T value, ShaderNode<T> shaderNode, Buffer<T> buffer, ShaderNode<Buffer<T>> gpuBuffer) where T : unmanaged
            {
            }

            private static readonly Dictionary<Type, string> StructTypes = new()
            {
                {typeof(MarchRay), "Ray"},
            };

            public static bool IsStructType(Type t)
            {
                return StructTypes.ContainsKey(t);
            }
            
            public static bool IsStructType<T>()
            {
                return IsStructType(typeof(T));        
            }
            
            public static bool IsDelegate(AbstractShaderNode theValue)
            {
                return theValue != null && theValue.GetType().IsGenericType && theValue.GetType().GetGenericTypeDefinition() == typeof(Delegate<>);
            }
            
            public static bool IsVoidNode(AbstractShaderNode theValue)
            {
                return theValue switch
                {
                    ShaderNode<GpuVoid> _ => true,
                    _ => false
                };
            }
            
            public static bool IsVoid(Type t)
            {
                return t == typeof(GpuVoid);
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global
            
            #region GetTypeDefault
            
            private static readonly Dictionary<Type, string> TypeDefaults = new()
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
                
                if (typeof(T) == typeof(Color4))
                {
                    var color4 = (Color4) Convert.ChangeType(theValue, typeof(Color4));
                    return $"float4({color4.R},{color4.G},{color4.B},{color4.A})";
                }
                
                if (typeof(T) == typeof(int)) return theValue.ToString();
                
                if (typeof(T) == typeof(Int2))
                {
                    var vec2 = (Int2) Convert.ChangeType(theValue, typeof(Int2));
                    return $"int2({vec2.X},{vec2.Y})";
                }
                if (typeof(T) == typeof(Int3))
                {
                    var vec3 = (Int3) Convert.ChangeType(theValue, typeof(Int3));
                    return $"int3({vec3.X},{vec3.Y},{vec3.Z})";
                }
                if (typeof(T) == typeof(Int4))
                {
                    var vec4 = (Int4) Convert.ChangeType(theValue, typeof(Int4));
                    return $"int4({vec4.X},{vec4.Y},{vec4.Z},{vec4.W})";
                }
                
                if (typeof(T) == typeof(bool)) return theValue.ToString()?.ToLower();
         
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
            
            public static string GetDefaultForType<T>()
            {
                if (TypeDefaults.TryGetValue(typeof(T), out var result))
                    return result;

                throw new NotImplementedException("No name defined for type: " + typeof(T).Name);
            }
            
            #endregion
            
            #region GetSignature
            
            private static readonly Dictionary<Type, string> KnownSignatureTypes = new()
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
                {typeof(MarchRay), "Ray"},
            };
            
            public static string GetSignature(AbstractShaderNode theValue)
            {
                return theValue switch
                {
                    ShaderNode<float> _ => "Float",
                    ShaderNode<Vector2> _ => "Float2",
                    ShaderNode<Vector3> _ => "Float3",
                    ShaderNode<Vector4> _ => "Float4",
                    ShaderNode<Color4> _ => "Float4",
                    ShaderNode<bool> _ => "Bool",
                    ShaderNode<int> _ => "Int",
                    ShaderNode<Int2> _ => "Int2",
                    ShaderNode<Int3> _ => "Int3",
                    ShaderNode<Int4> _ => "Int4",
                    ShaderNode<GpuVoid> _ => "Void",
                    ShaderNode<MarchRay> _ => "Ray",
                    _ => throw new NotImplementedException("No name defined for type: " + theValue.GetType().FullName)
                };
            }

            public static string GetSignature(Type type)
            {
                if (KnownSignatureTypes.TryGetValue(type, out var result))
                    return result;
                
                throw new NotImplementedException("No name defined for type: " + type.Name + " : " + type.FullName);
            }
            public static string GetSignature<T>()
            {
                return GetSignature(typeof(T));        
            }
            
            public static string GetSignature<T>(ShaderNode<T> shaderNode)
            {
                return GetSignature(typeof(T));        
            }
            
            #endregion
            
            #region GetGpuType
            
            private static readonly Dictionary<Type, string> KnownGpuTypes = new()
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
                
                {typeof(GpuVoid), "void"},
                {typeof(MarchRay), "Ray"},
            };
            
            public static string GetGpuType(AbstractShaderNode abstractShaderNode)
            {
                return abstractShaderNode switch
                {
                    ShaderNode<float> _ => "float",
                    ShaderNode<Vector2> _ => "float2",
                    ShaderNode<Vector3> _ => "float3",
                    ShaderNode<Vector4> _ => "float4",
                    ShaderNode<Color4> _ => "float4",
                    ShaderNode<Matrix> _ => "float4x4",
                    ShaderNode<bool> _ => "bool",
                    ShaderNode<int> _ => "int",
                    ShaderNode<Int2> _ => "int2",
                    ShaderNode<Int3> _ => "int3",
                    ShaderNode<Int4> _ => "int4",
                    ShaderNode<GpuVoid> _ => "void",
                    ShaderNode<MarchRay> _ => "Ray",
                    _ => throw new NotImplementedException("No name defined for type: " + abstractShaderNode.GetType().FullName)
                };
            }

            public static string GetGpuType(Type type)
            {
                return KnownGpuTypes.TryGetValue(type, out var result) ? result : type.Name;
            }
            
            public static string GetGpuType<T>()
            {
                return GetGpuType(typeof(T));        
            }
            
            public static string GetGpuType<T>(ShaderNode<T> shaderNode)
            {
                return GetGpuType<T>();
            }
            
            #endregion
            
            #region GetDimension
            
            private static readonly Dictionary<Type, int> KnownDimensions = new()
            {
                {typeof(float), 1},
                {typeof(Vector2), 2},
                {typeof(Vector3), 3},
                {typeof(Vector4), 4},
                {typeof(Color4), 4},
                {typeof(Matrix), 16},
                
                {typeof(int), 1},
                {typeof(Int2), 2},
                {typeof(Int3), 3},
                {typeof(Int4), 4},
                {typeof(uint), 1},
                {typeof(bool), 1},
                
                {typeof(GpuStruct), 1},
              
                {typeof(Texture), 1},
                {typeof(SamplerState), 1},
                
                {typeof(GpuVoid), 0},
                {typeof(MarchRay), 1},
            };
            
            public static int GetDimension(AbstractShaderNode abstractShaderNode)
            {
                return abstractShaderNode switch
                {
                    ShaderNode<float> _ => 1,
                    ShaderNode<Vector2> _ => 2,
                    ShaderNode<Vector3> _ => 3,
                    ShaderNode<Vector4> _ => 4,
                    ShaderNode<Color4> _ => 4,
                    ShaderNode<bool> _ => 1,
                    ShaderNode<int> _ => 1,
                    ShaderNode<Int2> _ => 2,
                    ShaderNode<Int3> _ => 3,
                    ShaderNode<Int4> _ => 4,
                    ShaderNode<GpuVoid> _ => 0,
                    ShaderNode<MarchRay> _ => 1,
                    _ => 0
                };
            }
            
            // USED BY VL
            // ReSharper disable once UnusedMember.Global
            public static int GetDimension(Type type)
            {
                return KnownDimensions.TryGetValue(type, out var result) ? result : 0;
            }
            
            public static int GetDimension<T>(ShaderNode<T> shaderNode)
            {
                return GetDimension(typeof(T));
            }
            
            public static int GetDimension<T>()
            {
                return GetDimension(typeof(T));
            }
            
            #endregion
            
            #region GetSizeInBytes
            
            private static readonly Dictionary<Type, int> KnownByteSizes = new()
            {
                {typeof(float), 1 * 4},
                {typeof(Vector2), 2 * 4},
                {typeof(Vector3), 3 * 4},
                {typeof(Vector4), 4 * 4},
                {typeof(Color4), 4 * 4},
                {typeof(Matrix), 16 * 4},
                
                {typeof(int), 1 * 4},
                {typeof(Int2), 2 * 4},
                {typeof(Int3), 3 * 4},
                {typeof(Int4), 4 * 4},
                {typeof(uint), 1 * 4},
                {typeof(bool), 1},
                
                {typeof(GpuVoid), 0}
            };
            
            public static int GetSizeInBytes(AbstractShaderNode abstractShaderNode)
            {
                return abstractShaderNode switch
                {
                    ShaderNode<float> _ => 4,
                    ShaderNode<Vector2> _ => 2 * 4,
                    ShaderNode<Vector3> _ => 3 * 4,
                    ShaderNode<Vector4> _ => 4 * 4,
                    ShaderNode<Color4> _ => 4 * 4,
                    ShaderNode<Matrix> _ => 4 * 4 * 4,
                    ShaderNode<bool> _ => 1,
                    ShaderNode<int> _ => 4,
                    ShaderNode<Int2> _ => 2 * 4,
                    ShaderNode<Int3> _ => 3 * 4,
                    ShaderNode<Int4> _ => 4 * 4,
                    ShaderNode<GpuVoid> _ => 0,
                    ShaderNode<MarchRay> _ => 4,
                    _ => throw new NotImplementedException("No name defined for type: " + abstractShaderNode.GetType().FullName)
                };
            }

            public static int GetSizeInBytes(Type type)
            {
                return KnownByteSizes.TryGetValue(type, out var result) ? result : 0;
            }
            
            public static int GetSizeInBytes<T>(ShaderNode<T> shaderNode)
            {
                return GetSizeInBytes(typeof(T));
            }
            
            public static int GetSizeInBytes<T>()
            {
                return GetSizeInBytes(typeof(T));
            }
            
            #endregion
            
            #region GetPixelFormat
            
            private static readonly Dictionary<Type, PixelFormat> KnownPixelFormats = new()
            {
                {typeof(float), PixelFormat.R32_Float},
                {typeof(Vector2), PixelFormat.R32G32_Float},
                {typeof(Vector3), PixelFormat.R32G32B32_Float},
                {typeof(Vector4), PixelFormat.R32G32B32A32_Float},
                {typeof(Color4), PixelFormat.R32G32B32A32_Float},
                
                {typeof(int), PixelFormat.R32_SInt},
                {typeof(Int2), PixelFormat.R32G32_SInt},
                {typeof(Int3), PixelFormat.R32G32B32_SInt},
                {typeof(Int4), PixelFormat.R32G32B32A32_SInt},
                {typeof(uint), PixelFormat.R32_UInt},
                {typeof(bool), PixelFormat.R1_UNorm},
                
                {typeof(GpuVoid), 0}
            };
            
            public static PixelFormat GetPixelFormat(AbstractShaderNode abstractShaderNode)
            {
                return abstractShaderNode switch
                {
                    ShaderNode<float> _ => PixelFormat.R32_Float,
                    ShaderNode<Vector2> _ => PixelFormat.R32G32_Float,
                    ShaderNode<Vector3> _ => PixelFormat.R32G32B32_Float,
                    ShaderNode<Vector4> _ => PixelFormat.R32G32B32A32_Float,
                    ShaderNode<Color4> _ => PixelFormat.R32G32B32A32_Float,
                    ShaderNode<bool> _ => PixelFormat.R1_UNorm,
                    ShaderNode<int> _ => PixelFormat.R32_SInt,
                    ShaderNode<Int2> _ => PixelFormat.R32G32_SInt,
                    ShaderNode<Int3> _ => PixelFormat.R32G32B32_SInt,
                    ShaderNode<Int4> _ => PixelFormat.R32G32B32A32_SInt,
                    ShaderNode<uint> _ => PixelFormat.R32G32B32A32_SInt,
                    _ => throw new NotImplementedException("No name defined for type: " + abstractShaderNode.GetType().FullName)
                };
            }

            public static PixelFormat GetPixelFormat(Type type)
            {
                return KnownPixelFormats.TryGetValue(type, out var result) ? result : 0;
            }
            
            public static PixelFormat GetPixelFormat<T>(ShaderNode<T> shaderNode)
            {
                return GetPixelFormat(typeof(T));
            }
            
            public static PixelFormat GetPixelFormat<T>()
            {
                return GetPixelFormat(typeof(T));
            }
            
            #endregion

            public static List<string> GetDescription(AbstractShaderNode theValue)
            {
                var keys = new List<string>() { "x", "y", "z", "w"};
                var dimension = GetDimension(theValue);
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

            public static ShaderNode<TOut> AdaptiveSignature<TIn,TOut>(string thePrepend, ShaderNode<TIn> theValue, out string theSignature)
            {
                theSignature = thePrepend + GetDimension(typeof(TOut)) + GetDimension(typeof(TIn));
                return null;
            }

            public static void LimitToStruct<T>(ShaderNode<T> theValue) where T : struct 
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
            
            public static string BufferTypeName(Buffer theBuffer,string theBufferType, bool theAppend, bool theReadWrite, bool theForceAppendConsume)
            {
                if (theForceAppendConsume || theBuffer != null && (theBuffer.Flags & BufferFlags.StructuredAppendBuffer) == BufferFlags.StructuredAppendBuffer)
                {
                    if (theAppend)
                    {
                        return "AppendStructuredBuffer<" + theBufferType + ">";
                    }

                    return "ConsumeStructuredBuffer<" + theBufferType + ">";
                }
                
                if (theBuffer == null)
                {
                    if (theReadWrite)
                    {
                        return "RWStructuredBuffer<" + theBufferType + ">";
                    }
                    else
                    {
                        return "StructuredBuffer<" + theBufferType + ">";
                    }
                }

                if ((theBuffer.Flags & BufferFlags.UnorderedAccess) == BufferFlags.UnorderedAccess)
                {
                    return "RWStructuredBuffer<" + theBufferType + ">";
                }
                
                return "StructuredBuffer<" + theBufferType + ">";
            }

            public static string GetGenericTypeName(AbstractShaderNode node)
            {
                if (node == null) return "";
                var objectType = node.GetType();
                var genericArguments = objectType.GetGenericArguments();

                if (genericArguments.Length <= 0) return null;
                var genericType = genericArguments[0];

                if (genericType == typeof(float))
                {
                    return "Float32";
                }

                if (genericType == typeof(double))
                {
                    return "Float64";
                }

                if (genericType == typeof(Int32))
                {
                    return "Integer32";
                }

                if (genericType == typeof(Int64))
                {
                    return "Integer64";
                }

                if (genericType == typeof(Color4))
                {
                    return "RGBA";
                }

                return genericType.Name;
            }
            
            public static string ValueToString(object theObject)
            {
                if (theObject is byte || theObject is sbyte || theObject is short || theObject is ushort ||
                    theObject is int || theObject is uint || theObject is long || theObject is ulong)
                {
                    var number = Convert.ToInt64(theObject);
                    return number.ToString("#,##0").Replace(",", ".");
                }

                return theObject.ToString();
            }
        }
}