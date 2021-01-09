﻿using System.Collections.Generic;
 using Stride.Core.Mathematics;
 using Stride.Graphics;
 using Stride.Rendering;

 namespace VL.ShaderFXtension{

     public abstract class AbstractInput<T, ParameterKeyType> : ShaderNode<T>, IGPUInput where ParameterKeyType : ParameterKey<T>
     {
        
         private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";

         protected ParameterKeyType _parameterKey;

         public AbstractInput(string theName): base(theName, null,"input")
         {
            
             Setup("", new OrderedDictionary<string, AbstractGpuValue>());
            
             Declaration = ShaderTemplateEvaluator.Evaluate(
                 DeclarationTemplate, 
                 new Dictionary<string, string>
                 {
                     {"inputName", Output.ID},
                     {"inputType",TypeHelpers.GetNameForType<T>().ToLower()}
                 });
         }

         public abstract void SetParameterValue(ParameterCollection theCollection);

         public override string Declaration { get; }

         public T Value { get; set; }
     }

     public class TextureInput: AbstractInput<Texture, ObjectParameterKey<Texture>>, IGPUInput 
     {
         public TextureInput() : base("TextureInput")
         {
             _parameterKey = new ObjectParameterKey<Texture>(Output.ID);
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(_parameterKey, Value);
         }
     }
     
     public class SamplerInput: AbstractInput<SamplerState, ObjectParameterKey<SamplerState>>, IGPUInput 
     {
         public SamplerInput(string theName) : base("SamplerInput")
         {
             _parameterKey = new ObjectParameterKey<SamplerState>(Output.ID);
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(_parameterKey, Value);
         }
     }
     
    public class GPUInput<T> : AbstractInput<T,ValueParameterKey<T>>, IGPUInput where T : struct 
    {

        public GPUInput(): base("GPUInput")
        {
            _parameterKey = new ValueParameterKey<T>(Output.ID);
        }

        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(_parameterKey, Value);
        }
    }
}