using System.Collections.Generic;
using System.Reflection;
using Stride.Core.IO;
using Stride.Rendering;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using static Stride.Shaders.Compiler.EffectCompilerFactory;

namespace VL.ShaderFXtension
{
    public static class EffectSystemExtensions
    {
        public static void InstallEffectCompilerWithInMemoryPaths(this EffectSystem effectSystem)
        {
            var databaseProvider = effectSystem.Services.GetService<IDatabaseFileProviderService>();
            var inMemoryFileProvider = new InMemoryFileProvider(databaseProvider.FileProvider);
            effectSystem.Services.AddService(inMemoryFileProvider);
            effectSystem.Compiler = CreateEffectCompiler(inMemoryFileProvider, effectSystem, database: databaseProvider.FileProvider);
        }

        private static HashSet<string> addedShaders = new HashSet<string>();
        
        public static void AddShaderSource(this EffectSystem effectSystem, string type, string sourceCode, string sourcePath)
        {
            if (addedShaders.Contains(type)) return;

            var compiler = effectSystem.Compiler as EffectCompiler;
            if (compiler is null && effectSystem.Compiler is EffectCompilerCache effectCompilerCache)
                compiler = typeof(EffectCompilerChain).GetProperty("Compiler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(effectCompilerCache) as EffectCompiler;
            
            if (compiler == null) return;
            
            var getParserMethod = typeof(EffectCompiler).GetMethod("GetMixinParser", BindingFlags.Instance | BindingFlags.NonPublic);
            var parser = getParserMethod.Invoke(compiler, null) as ShaderMixinParser;
            var sourceManager = parser.SourceManager;
            sourceManager.AddShaderSource(type, sourceCode, sourcePath);
        }

        public static void RegisterShader(this EffectSystem effectSystem, string path, string shader)
        {
            var inMemoryFileProvider = effectSystem.Services.GetService<InMemoryFileProvider>();
            inMemoryFileProvider?.Register(path, shader);
        }
        
    }
}