using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;

namespace Fuse{

    public interface IGpuInput : IShaderNode
    {
        void SetParameterValue(ParameterCollection theCollection);

        void AddParameters(ParameterCollection theCollection);

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

        public void AddParameters(ParameterCollection theCollection)
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


         public void AddParameters(ParameterCollection theCollection)
         {
             Parameters.Add(theCollection);
         }

         public void UpdateParameter()
         {
             Parameters?.ForEach(SetParameterValue);
         }

         public HashSet<ParameterCollection> Parameters { get;  }

         protected TParameterKeyType ParameterKey { get; set;}

         protected UnhandledResourcesAbstractInput(string theName, T theValue = default): base(theName, null,"input")
         {
             Value = theValue;
             Parameters = new HashSet<ParameterCollection>();
             Setup( new List<AbstractGpuValue>());
         }

         private T _inputValue;

         // ReSharper disable once MemberCanBeProtected.Global
         public T Value
         {
             get => _inputValue;
             // ReSharper disable once MemberCanBePrivate.Global
             // is accessible from VL
             set
             {
                 //if (_inputValue != null && _inputValue.Equals(value)) return;
                 _inputValue = value;
                 UpdateParameter();
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
         private readonly bool _isResource;
         
         protected AbstractInput(string theName, bool theIsResource, T theValue = default): base(theName, theValue)
         {
             _isResource = theIsResource;
             Setup( new List<AbstractGpuValue>());
             AddResource(Inputs, this);
         }

         private string BuildDeclaration(string theTypeName)
         {
             return ShaderNodesUtil.Evaluate(
                 DeclarationTemplate,
                 new Dictionary<string, string>
                 {
                     {"inputName", Output.ID},
                     {"inputType", theTypeName}
                 });
         }

         protected void SetFieldDeclaration(string theTypeName, string theComputeTypeName)
         {
             AddResource(Declarations,
                 new FieldDeclaration(
                     _isResource,
                     BuildDeclaration(theComputeTypeName),
                     BuildDeclaration(theTypeName)
                 )
             );
         }

         protected void SetFieldDeclaration(string theTypeName)
         {
             SetFieldDeclaration(theTypeName,theTypeName);
         }
     }

     public abstract class ObjectInput<T> : AbstractInput<T, ObjectParameterKey<T>>
     {
         protected ObjectInput(string theName, T theValue = default) : base(theName, true, theValue)
         {
             
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, Value);
         }

         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             base.SetHashCodes(theContext);
             Output.HashCode = theContext.GetAndIncIDCount();
             ParameterKey = theContext.GetParameterKey(new ObjectParameterKey<T>(Output.ID)) as ObjectParameterKey<T>;
         }
     }

     public class TextureInput: ObjectInput<Texture>
     {
         private bool _useRW;
         public TextureInput(Texture theTexture, bool theUseRW = false) : base("TextureInput", theTexture)
         {
             _useRW = theUseRW;
         }
         
         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             base.SetHashCodes(theContext);
             
             if (Value == null)
             {
                 SetFieldDeclaration("Texture2D");
                 return;
             }
             SetFieldDeclaration(TypeHelpers.TextureTypeName(Value, _useRW),
                 Value.Dimension switch {
                     TextureDimension.Texture1D => "Texture1D",
                     TextureDimension.Texture2D => "Texture2D",
                     TextureDimension.Texture3D => "Texture3D",
                     TextureDimension.TextureCube => "TextureCube",
                     _ => "Texture2D"
                 }
             );
         }
     }
     
     public class SamplerInput: ObjectInput<SamplerState>, IGpuInput 
     {
         public SamplerInput(SamplerState theSampler) : base("SamplerInput", theSampler)
         {
         }

         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             base.SetHashCodes(theContext);
             SetFieldDeclaration("SamplerState", "SamplerState");
         }
     }
     
      

     public class AbstractBufferInput : ObjectInput<Buffer>
     {
         
         protected AbstractBufferInput(Buffer theBuffer) : base("BufferInput", theBuffer)
         {
         }
     }

     public class DynamicStructBufferInput: ObjectInput<Buffer>
     {

         private readonly GpuValue<GpuStruct> _struct;
         private readonly bool _append;
         
         public DynamicStructBufferInput(GpuValue<GpuStruct> theStruct, Buffer theBuffer = null, bool theAppend = true) : base("DynamicBufferInput",theBuffer)
         {
             _struct = theStruct;
             _append = theAppend;
         }
         
         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             base.SetHashCodes(theContext);
             SetFieldDeclaration(TypeHelpers.BufferTypeName(Value,_struct.TypeOverride, _append));
         }
     }
     
     public class BufferInput<T>: ObjectInput<Buffer<T>> where T : struct
     {
         private readonly bool _append;

         public BufferInput(string theName, Buffer<T> theBuffer = null, bool theAppend = true) : base("BufferInput", theBuffer)
         {
             _append = theAppend;
         }
         
         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             base.SetHashCodes(theContext);
             SetFieldDeclaration(TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), _append));
         }
     }
     
    public class GpuInput<T> : AbstractInput<T,ValueParameterKey<T>> where T : struct 
    {

        public GpuInput(): base("GPUInput", false)
        {
        }

        public override void SetHashCodes(ShaderGeneratorContext theContext)
        {
            base.SetHashCodes(theContext);
            Output.HashCode = theContext.GetAndIncIDCount();
            ParameterKey = new ValueParameterKey<T>(Output.ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }
        
        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(ParameterKey, Value);
        }
    }
}