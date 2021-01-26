using System.Collections.Generic;
using System.Reflection;
using Stride.Rendering;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;

namespace Fuse
{
    public static class EffectSystemExtensions
    {
        

        private static readonly HashSet<string> AddedShaders = new HashSet<string>();
        
        public static void AddShaderSource(this EffectSystem effectSystem, string type, string sourceCode, string sourcePath)
        {
            if (AddedShaders.Contains(type)) return;

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

    }
}