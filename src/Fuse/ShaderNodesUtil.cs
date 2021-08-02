using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fuse.compute;
using Fuse.ShaderFX;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using VL.Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    
    public static class DictionaryExtensions
    {
        public static void ForEach<TKey, TValue>(this Dictionary<TKey, TValue> dict, Action<KeyValuePair<TKey, TValue>> action)
        {
            foreach (var item in dict)
                action(item);
        }
    }
    
    public static class EnumerableExtensionForEach
    {
        public static void ForEach<T>(this IEnumerable<T> list, Action<T> block) {
            foreach (var item in list) {
                block(item);
            }
        }
    }
    
    public static class ShaderNodesUtil
    {
        
        public static string BuildArguments(IEnumerable<AbstractGpuValue> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }

        public static  bool HasNullValue<T>(IEnumerable<T> theSequence)
        {
            return theSequence.Any(value => value == null);
        }

        public static string IndentCode(string theCode)
        {
            var lines = theCode.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None
            );
            
            var myStringBuilder = new StringBuilder();
            lines.ForEach(line=>myStringBuilder.AppendLine("    " + line));
            return myStringBuilder.ToString();
        }

        public static string Evaluate(string theShaderTemplate, IDictionary<string,string> theKeys)
        {
            return Regex.Replace(
                theShaderTemplate, 
                @"\$\{(?<key>[^}]+)\}", 
                m => theKeys.ContainsKey(m.Groups["key"].Value) ? theKeys[m.Groups["key"].Value] : m.Value
            );
        }

        private static void AddShaderSource(Game game, string type, string sourceCode, string sourcePath)
        {
            var effectSystem = game.EffectSystem;
            var compiler = effectSystem.Compiler as EffectCompiler;
            if (compiler is null && effectSystem.Compiler is EffectCompilerCache effectCompilerCache)
                compiler = typeof(EffectCompilerChain).GetProperty("Compiler", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(effectCompilerCache) as EffectCompiler;
            
            if (compiler == null) return;
            
            var getParserMethod = typeof(EffectCompiler).GetMethod("GetMixinParser", BindingFlags.Instance | BindingFlags.NonPublic);
            if (getParserMethod == null) return;
            if (!(getParserMethod.Invoke(compiler, null) is ShaderMixinParser parser)) return;
            
            var sourceManager = parser.SourceManager;
            sourceManager.AddShaderSource(type, sourceCode, sourcePath);
        }

        public static DynamicEffectInstance RegisterDrawShader(Game game, DrawShader theDrawShader)
        {
            AddShaderSource(game, theDrawShader.ShaderName, theDrawShader.ShaderCode, "shaders\\" + theDrawShader.ShaderName + ".sdsl");
            return new DynamicEffectInstance(theDrawShader.ShaderName);
        }

        public static ComputeEffectDispatcher RegisterComputeShader(Game game, ToComputeFx theComputeFx)
        {
            AddShaderSource(game, theComputeFx.ShaderName, theComputeFx.ShaderCode, "shaders\\" + theComputeFx.ShaderName + ".sdsl");
            var shaderGraph = ShaderGraph.BuildFinalShaderGraph(theComputeFx);
            var computeShader = ShaderGraph.ComposeComputeShader(game.GraphicsDevice, game.Services, shaderGraph);
            computeShader.ShaderSourceName = theComputeFx.ShaderName;
            return computeShader;
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static ToShaderFX<T> RegisterShaderFX<T>(Game game, GpuValue<T> theGpuValue) where T : struct
        {
            var toShaderFx = new ToShaderFX<T>(theGpuValue);
            AddShaderSource(game, toShaderFx.ShaderName, toShaderFx.ShaderCode, "shaders\\" + toShaderFx.ShaderName + ".sdsl");
            return toShaderFx;
        }
        
        public static IDictionary<string, AbstractGpuValue> AbstractMembers(GpuValue<GpuStruct> theStruct, IEnumerable<AbstractGpuValue> theMembers)
        {
            var getMemberBaseType = typeof(GetMember<,>);
            
            var members = new Dictionary<string, AbstractGpuValue>();
            theMembers.ForEach(member =>
            {
                var dataType = new Type [] { typeof(GpuStruct), member.GetType().GetGenericArguments()[0]};
                var getMemberType = getMemberBaseType.MakeGenericType(dataType);
                var getMemberInstance = Activator.CreateInstance(getMemberType, theStruct, member.Name, null) as AbstractShaderNode;
                members[member.Name] = getMemberInstance?.AbstractOutput();
            });
            return members;
        }
        
        public static AbstractGpuValue AbstractGetMember(GpuValue<GpuStruct> theStruct, AbstractGpuValue theMember)
        {
            var getMemberBaseType = typeof(GetMember<,>);
            var dataType = new Type [] { typeof(GpuStruct), theMember.GetType().GetGenericArguments()[0]};
            var getMemberType = getMemberBaseType.MakeGenericType(dataType);
            var getMemberInstance = Activator.CreateInstance(getMemberType, theStruct, theMember.Name, null) as AbstractShaderNode;
            return getMemberInstance?.AbstractOutput();
        }
        
        public static AbstractGpuValue AbstractDeclareValue(AbstractGpuValue theMember)
        {
            var getDeclareBaseType = typeof(DeclareValue<>);
            var dataType = new Type [] { theMember.GetType().GetGenericArguments()[0]};
            var getDeclareType = getDeclareBaseType.MakeGenericType(dataType);
            var getDeclareInstance = Activator.CreateInstance(getDeclareType,  new object[]{null} ) as AbstractShaderNode;
            return getDeclareInstance?.AbstractOutput();
        }
        
        public static AbstractGpuValue AbstractComputeTextureGet(GpuValue<Texture> theTexture, AbstractGpuValue theIndex, AbstractGpuValue theMember)
        {
            var getComputeTextureGetBaseType = typeof(ComputeTextureGet<>);
            var dataTypes = new Type [] {theMember.GetType().GetGenericArguments()[0]};
            var getComputeTextureGetType = getComputeTextureGetBaseType.MakeGenericType(dataTypes);
            var getComputeTextureGetInstance = Activator.CreateInstance(getComputeTextureGetType,  theTexture, theIndex, null ) as AbstractShaderNode;
            return getComputeTextureGetInstance?.AbstractOutput();
        }
    }
}