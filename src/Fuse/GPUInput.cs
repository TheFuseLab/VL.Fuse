using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;

namespace Fuse{

    public interface IGpuInput : IShaderNode
    {
        void SetParameterValue(ParameterCollection theCollection);
    }
    
    public class DefaultInputValue <T>: GpuValue<T>
    {
        public DefaultInputValue(string theName) : base(theName)
        {
        }
        
        public override string ID => name;
    }

    public class DefaultInput<T> : ShaderNode<T>, IGpuInput
    {
        public DefaultInput(string theName) : base(theName, null, "defaultInput")
        {
            Output = new DefaultInputValue<T>(theName);
            Setup("", new List<AbstractGpuValue>());
        }

        public void SetParameterValue(ParameterCollection theCollection)
        {
        }
    }
     
     public abstract class AbstractInput<T, TParameterKeyType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T>
     {
        
         private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";

         protected TParameterKeyType _parameterKey;

         public AbstractInput(string theName): base(theName, null,"input")
         {
            
             Setup("", new List<AbstractGpuValue>());
            
             Declaration = ShaderTemplateEvaluator.Evaluate(
                 DeclarationTemplate, 
                 new Dictionary<string, string>
                 {
                     {"inputName", Output.ID},
                     {"inputType",TypeName()}
                 });
         }


         public override string Declaration { get; }

         public virtual string TypeName()
         {
             return TypeHelpers.GetNameForType<T>().ToLower();
         }

         public T Value { get; set; }
         public abstract void SetParameterValue(ParameterCollection theCollection);
     }

     public class TextureInput: AbstractInput<Texture, ObjectParameterKey<Texture>>, IGpuInput 
     {
         public TextureInput() : base("TextureInput")
         {
             _parameterKey = new ObjectParameterKey<Texture>(Output.ID);
         }

         public override string TypeName()
         {
             return "Texture2D  ";
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(_parameterKey, Value);
         }
     }
     
     public class SamplerInput: AbstractInput<SamplerState, ObjectParameterKey<SamplerState>>, IGpuInput 
     {
         public SamplerInput(string theName) : base("SamplerInput")
         {
             _parameterKey = new ObjectParameterKey<SamplerState>(Output.ID);
         }
         
         public override string TypeName()
         {
             return "SamplerState ";
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(_parameterKey, Value);
         }
     }
     
    public class GPUInput<T> : AbstractInput<T,ValueParameterKey<T>>, IGpuInput where T : struct 
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