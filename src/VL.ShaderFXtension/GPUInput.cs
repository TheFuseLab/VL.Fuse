using System.Collections.Generic;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension{

    public class GPUInput<T> : ShaderNode
    {
        private const string DeclarationTemplate = 
@"    [Link(""${inputName}"")]
    stage ${inputType} ${inputName};";

        public GPUInput()
        {
            Output = new GPUValue<T>("input");
            
            Setup(
                "", 
                new Dictionary<string, AbstractGPUValue>(), 
                new Dictionary<string, AbstractGPUValue> {{"input", Output}}
                );
            
            Declaration = ShaderTemplateEvaluator.Evaluate(
                DeclarationTemplate, 
                new Dictionary<string, string>
                {
                    {"inputName", Output.ID},
                    {"inputType",TypeHelpers.GetNameForType<T>().ToLower()}
                });
        }

        public override string Declaration { get; }

        public GPUValue<T> Output { get; }

        public T Value { get; set; }
    }
}