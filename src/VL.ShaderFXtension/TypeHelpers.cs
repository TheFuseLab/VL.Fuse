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

            public static void ConstrainTypesByFunction<T1, T2>(Funk1In1Out<T1, T2> funk, Var<T1> inputT1, Var<T2> inputT2)
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
            
            static Dictionary<Type, string> KnownTypes = new Dictionary<Type, string>();

            static TypeHelpers()
            {
                KnownTypes.Add(typeof(float), "Float");
                KnownTypes.Add(typeof(int), "Int");
                KnownTypes.Add(typeof(uint), "UInt");
                KnownTypes.Add(typeof(bool), "Bool");
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
            
            public static string GetType<T1>(T1 var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            
            public static string VarType<T1>(Var<T1> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            public static string VarType<T1>(IEnumerable<Var<T1>> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
            
            public static string VarType<T1>(IDictionary<string,Var<T1>> var)
            {
                return ShaderFXUtils.GetNameForType<T1>();
            }
        }
}