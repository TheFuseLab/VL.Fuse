using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;

namespace Fuse{

    public interface IGpuInput : IShaderNode
    {
        void SetParameterValue(ParameterCollection theCollection);

        void SetParameters(ParameterCollection theCollection);

        void UpdateParameter();
    }
    
    public class DefaultInputValue <T>: GpuValue<T>
    {
        public DefaultInputValue(string theName) : base(theName)
        {
            
        }
        
        public override string ID => Name;
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

        public void UpdateParameter()
        {
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
    
    public abstract class UnhandledResourcesAbstractInput<T, TParameterKeyType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T>
     {
        
         protected const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";


         public void SetParameters(ParameterCollection theCollection)
         {
             Parameters = theCollection;
         }

         public void UpdateParameter()
         {
             if (Parameters == null) return;
             SetParameterValue(Parameters);
         }

         public ParameterCollection Parameters { get; private set; }

         public TParameterKeyType ParameterKey { get; protected set;}

         public UnhandledResourcesAbstractInput(string theName, T theValue = default(T)): base(theName, null,"input")
         {
             Value = theValue;
             Setup( new List<AbstractGpuValue>());
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

         protected override string SourceTemplate()
         {
             return "";
         }
     }
     
     public abstract class AbstractInput<T, TParameterKeyType> : UnhandledResourcesAbstractInput<T,TParameterKeyType> where TParameterKeyType : ParameterKey<T>
     {
        
         public AbstractInput(string theName, T theValue = default(T)): base(theName, theValue)
         {
             Value = theValue;
             Setup( new List<AbstractGpuValue>());
             AddResource(Inputs, this);
             AddResource(Declarations,ShaderNodesUtil.Evaluate(
                 DeclarationTemplate,
                 new Dictionary<string, string>
                 {
                     {"inputName", Output.ID},
                     {"inputType", TypeName()}
                 }));
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
     
     public class DynamicStructBufferInput: UnhandledResourcesAbstractInput<Buffer,ObjectParameterKey<Buffer>> 
     {

         private readonly GpuValue<GpuStruct> _struct;
         public DynamicStructBufferInput(GpuValue<GpuStruct> theStruct, Buffer theBuffer = null) : base("BufferInput", theBuffer)
         {
             _struct = theStruct;
             ParameterKey = new ObjectParameterKey<Buffer>(Output.ID);
             
             AddResource(Inputs, this);
             AddResource(Declarations,ShaderNodesUtil.Evaluate(
                 DeclarationTemplate,
                 new Dictionary<string, string>
                 {
                     {"inputName", Output.ID},
                     {"inputType", TypeName()}
                 }));
         }
         
         public override string TypeName()
         {
             if (Value != null && (Value.Flags & BufferFlags.UnorderedAccess) == BufferFlags.UnorderedAccess)
             {
                 return "RWStructuredBuffer<"+_struct.TypeOverride+">";
             }
             return "StructuredBuffer<"+_struct.TypeOverride+">";
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, Value);
         }
     }
     
     public class BufferInput<T>: AbstractInput<Buffer<T>, ObjectParameterKey<Buffer<T>>>, IGpuInput where T : struct
     {
         public BufferInput(string theName, Buffer<T> theBuffer = null) : base("BufferInput", theBuffer)
         {
             ParameterKey = new ObjectParameterKey<Buffer<T>>(Output.ID);
         }
         
         public override string TypeName()
         {
             if (Value != null && (Value.Flags & BufferFlags.UnorderedAccess) == BufferFlags.UnorderedAccess)
             {
                 return "RWStructuredBuffer<"+TypeHelpers.GetGpuTypeForType<T>()+">";
             }
             return "StructuredBuffer<"+TypeHelpers.GetGpuTypeForType<T>()+">";
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