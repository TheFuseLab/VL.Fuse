using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;

namespace Fuse{

    public interface IGpuInput : IShaderNode
    {
        void SetParameterValue(ParameterCollection theCollection);

        void SetParameters(ParameterCollection theCollection);
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
            Setup(new List<AbstractGpuValue>());
        }

        public void SetParameterValue(ParameterCollection theCollection)
        {
        }

        public void SetParameters(ParameterCollection theCollection)
        {
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
     
     public abstract class AbstractInput<T, TParameterKeyType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T>
     {
        
         private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";

         protected TParameterKeyType ParameterKey;

         protected ParameterCollection Parameters;
         
         public void SetParameters(ParameterCollection theCollection)
         {
             Parameters = theCollection;
         }

         public AbstractInput(string theName, T theValue = default(T)): base(theName, null,"input")
         {
             Value = theValue;
             Setup( new List<AbstractGpuValue>());
             Declaration = ShaderNodesUtil.Evaluate(
                 DeclarationTemplate,
                 new Dictionary<string, string>
                 {
                     {"inputName", Output.ID},
                     {"inputType", TypeName()}
                 });
             Declarations = new List<string>(){Declaration};
             Inputs = new List<IGpuInput>(){this};
         }

         public virtual string TypeName()
         {
             return TypeHelpers.GetGpuTypeForType<T>();
         }
         
         private T _inputValue;

         public T Value
         {
             get => _inputValue;
             set
             {
                 //if (_inputValue != null && _inputValue.Equals(value)) return;
                 _inputValue = value;
                 if (Parameters == null) return;
                 SetParameterValue(Parameters);
             }
         } 
         public abstract void SetParameterValue(ParameterCollection theCollection);
         
         public sealed override List<string> Declarations { get; }
         public sealed override List<IGpuInput> Inputs { get; }
         
         public string Declaration { get; }
         
         protected override string SourceTemplate()
         {
             return "";
         }
     }

     public class TextureInput: AbstractInput<Texture, ObjectParameterKey<Texture>>, IGpuInput 
     {
         public TextureInput(Texture theTexture) : base("TextureInput", theTexture)
         {
             ParameterKey = new ObjectParameterKey<Texture>(Output.ID);
         }

         public override string TypeName()
         {
             return "Texture2D  ";
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, Value);
         }
     }
     
     public class SamplerInput: AbstractInput<SamplerState, ObjectParameterKey<SamplerState>>, IGpuInput 
     {
         public SamplerInput(string theName, SamplerState theSampler) : base("SamplerInput", theSampler)
         {
             ParameterKey = new ObjectParameterKey<SamplerState>(Output.ID);
         }
         
         public override string TypeName()
         {
             return "SamplerState ";
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, Value);
         }
     }
     
     public class BufferInput: AbstractInput<Buffer, ObjectParameterKey<Buffer>>, IGpuInput  
     {
         public BufferInput(string theName, Buffer theBuffer = null) : base("BufferInput", theBuffer)
         {
             ParameterKey = new ObjectParameterKey<Buffer>(Output.ID);
         }
         
         public override string TypeName()
         {
             if (Value != null && (Value.Flags & BufferFlags.UnorderedAccess) == BufferFlags.UnorderedAccess)
             {
                 return "RWStructuredBuffer<float>";
             }
             return "StructuredBuffer<float>";
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             
             theCollection.Set(ParameterKey, Value);
         }
     }
     
    public class GpuInput<T> : AbstractInput<T,ValueParameterKey<T>>, IGpuInput where T : struct 
    {

        public GpuInput(): base("GPUInput")
        {
            ParameterKey = new ValueParameterKey<T>(Output.ID);
        }

        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(ParameterKey, Value);
        }
    }
}