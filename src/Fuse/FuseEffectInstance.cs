using Stride.Core;
using Stride.Core.Diagnostics;
using Stride.Engine.Design;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Fuse
{
    public class FuseEffectInstance : EffectInstance
    {
        private readonly ShaderSource shaderSource;
        private readonly IDisposable subscriptions;
        private EffectSystem effectSystem;

        public FuseEffectInstance(ShaderSource shaderSource, ParameterCollection parameters, IDisposable subscriptions) : base(null, parameters)
        {
            this.shaderSource = shaderSource;
            this.subscriptions = subscriptions;
        }

        protected override void Destroy()
        {
            subscriptions.Dispose();
            base.Destroy();
        }

        /// <summary>
        /// Defines the effect parameters used when compiling this effect.
        /// </summary>
        public EffectCompilerParameters EffectCompilerParameters = EffectCompilerParameters.Default;

        public void Initialize(IServiceRegistry services)
        {
            this.effectSystem = services.GetSafeServiceAs<EffectSystem>();

            var gameSettings = services.GetSafeServiceAs<IGameSettingsService>().Settings;
            EffectCompilerParameters.ApplyCompilationMode(gameSettings.CompilationMode);

            var graphicsDevice = services.GetSafeServiceAs<IGraphicsDeviceService>().GraphicsDevice;
            EffectCompilerParameters.Platform = GraphicsDevice.Platform;
            EffectCompilerParameters.Profile = graphicsDevice.Features.RequestedProfile;
        }

        protected override void ChooseEffect(GraphicsDevice graphicsDevice)
        {
            var watch = Stopwatch.StartNew();

            // Setup compilation parameters
            var compilerParameters = new CompilerParameters
            {
                EffectParameters = EffectCompilerParameters,
            };

            foreach (var effectParameterKey in Parameters.ParameterKeyInfos)
            {
                if (effectParameterKey.Key.Type == ParameterKeyType.Permutation)
                {
                    compilerParameters.SetObject(effectParameterKey.Key, Parameters.ObjectValues[effectParameterKey.BindingSlot]);
                }
            }

            var compiler = effectSystem.Compiler;

            // Workaround for Stride effect compiler cache bug
            ShaderNodesUtil.MakeInMemoryShadersAvailableToTheSourceManager(compiler, shaderSource);

            // Compile the shader
            var compilerResult = compiler.Compile(shaderSource, compilerParameters);
            CheckResult(compilerResult);

            var bytecodeResult = compilerResult.Bytecode.WaitForResult();

            CheckResult(bytecodeResult.CompilationLog);

            if (bytecodeResult.Bytecode is null)
                throw new InvalidOperationException("EffectCompiler returned no shader and no compilation error.");

            effect = new Effect(graphicsDevice, bytecodeResult.Bytecode);

            Console.WriteLine($"Compile Time: {watch.ElapsedMilliseconds} ms for Shader {shaderSource}");
        }

        private static void CheckResult(LoggerResult compilerResult)
        {
            // Check errors
            if (compilerResult.HasErrors)
            {
                throw new InvalidOperationException("Could not compile shader. See error messages." + compilerResult.ToText());
            }
        }
    }
}
