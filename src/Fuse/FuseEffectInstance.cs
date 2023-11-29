using Stride.Core;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        }

        protected override void ChooseEffect(GraphicsDevice graphicsDevice)
        {
            var watch = Stopwatch.StartNew();

            // TODO: Free previous descriptor sets and layouts?

            // Looks like the effect changed, it needs a recompilation
            var compilerParameters = new CompilerParameters
            {
                EffectParameters = EffectCompilerParameters,
            };

            foreach (var effectParameterKey in Parameters.ParameterKeyInfos)
            {
                if (effectParameterKey.Key.Type == ParameterKeyType.Permutation)
                {
                    // TODO GRAPHICS REFACTOR avoid direct access, esp. since permutation values might be separated from Objects at some point
                    compilerParameters.SetObject(effectParameterKey.Key, Parameters.ObjectValues[effectParameterKey.BindingSlot]);
                }
            }

            var effectCompiler = effectSystem.Compiler;

            // Setup compilation parameters
            // GraphicsDevice might have been not valid until this point, which is why we compute platform and profile only at this point
            compilerParameters.EffectParameters.Platform = GraphicsDevice.Platform;
            compilerParameters.EffectParameters.Profile = /*graphicsDevice.ShaderProfile ?? */graphicsDevice.Features.RequestedProfile;
            // Copy optimization/debug levels
            //compilerParameters.EffectParameters.OptimizationLevel = ;
            //compilerParameters.EffectParameters.Debug = effectCompilerParameters.Debug;

            // Get the compiled result
            var compilerResult = effectCompiler.Compile(shaderSource, compilerParameters);

            // Only take the sub-effect
            var bytecodeTask = compilerResult.Bytecode;
            var byteCode = bytecodeTask.WaitForResult().Bytecode;

            effect = new Effect(graphicsDevice, byteCode);

            Console.WriteLine($"Compile Time: {watch.ElapsedMilliseconds} ms for Shader {shaderSource}");
        }
    }
}
