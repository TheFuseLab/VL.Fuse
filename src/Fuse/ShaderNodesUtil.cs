using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using VL.Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
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
        
        public static void AddShaderSource(Game game, string type, string sourceCode, string sourcePath)
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

        public static ComputeEffectDispatcher RegisterComputeShader(Game game, ToComputeFx theComputeFX)
        {
            AddShaderSource(game, theComputeFX.ShaderName, theComputeFX.ShaderCode, "shaders\\" + theComputeFX.ShaderName + ".sdsl");
            var shaderGraph = ShaderGraph.BuildFinalShaderGraph(theComputeFX);
            var computeShader = ShaderGraph.ComposeComputeShader(game.GraphicsDevice, game.Services, shaderGraph);
            computeShader.ShaderSourceName = theComputeFX.ShaderName;
            return computeShader;
        }

        public static ToShaderFX<T> RegisterShaderFX<T>(Game game, GpuValue<T> theGpuValue) where T : struct
        {
            var toShaderFx = new ToShaderFX<T>(theGpuValue);
            AddShaderSource(game, toShaderFx.ShaderName, toShaderFx.ShaderCode, "shaders\\" + toShaderFx.ShaderName + ".sdsl");
            return toShaderFx;
        }
        
    }
}