using System.Collections.Generic;
using Stride.Core.Extensions;
using Stride.Core;
using Stride.Rendering;
using Stride.Graphics;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension{

    public class GPUInput<T> : ShaderNode, IGPUInput where T : struct 
    {
        private const string DeclarationTemplate = 
@"        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";

        private ValueParameterKey<T> _valueParameterKey;

        public GPUInput(): base("Input")
        {
            Output = new GpuValue<T>("input");
            
            _valueParameterKey = new ValueParameterKey<T>(Output.ID);
            
            Setup(
                "", 
                new OrderedDictionary<string, AbstractGpuValue>(), 
                new OrderedDictionary<string, AbstractGpuValue> {{"input", Output}}
                );
            
            Declaration = ShaderTemplateEvaluator.Evaluate(
                DeclarationTemplate, 
                new Dictionary<string, string>
                {
                    {"inputName", Output.ID},
                    {"inputType",TypeHelpers.GetNameForType<T>().ToLower()}
                });
        }

        public void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(_valueParameterKey, Value);
        }

        public override string Declaration { get; }

        public GpuValue<T> Output { get; }

        public T Value { get; set; }
    }
}