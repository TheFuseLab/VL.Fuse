using System.Collections.Generic;
using Stride.Core.IO;
using Stride.Rendering;
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

        public static void RegisterShader(this EffectSystem effectSystem, string path, string shader)
        {
            var inMemoryFileProvider = effectSystem.Services.GetService<InMemoryFileProvider>();
            inMemoryFileProvider?.Register(path, shader);
        }
        
    }
}