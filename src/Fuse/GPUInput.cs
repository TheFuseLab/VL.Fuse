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

     public class ObjectInput<T> : AbstractInput<T, ObjectParameterKey<T>>
     {
         protected ObjectInput(string theName, T theValue = default) : base(theName, true, theValue)
         {
             ParameterKey = new ObjectParameterKey<T>(Output.ID);
             /*
             ShaderGeneratorContext context = null;
             ParameterKey = context.GetParameterKey(new ObjectParameterKey<T>(Output.ID)) as ObjectParameterKey<T>;
            */
         }

         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, Value);
         }
     }

     public class TextureInput: ObjectInput<Texture> 
     {
         public TextureInput(Texture theTexture, bool theUseRW = false) : base("TextureInput", theTexture)
         {
             if (theTexture == null)
             {
                 SetFieldDeclaration("Texture2D");
                 return;
             }
             SetFieldDeclaration(TypeHelpers.TextureTypeName(theTexture, theUseRW),
                 theTexture.Dimension switch {
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
         
         public DynamicStructBufferInput(GpuValue<GpuStruct> theStruct, Buffer theBuffer = null, bool theAppend = true) : base("DynamicBufferInput",theBuffer)
         {
             SetFieldDeclaration(
                 TypeHelpers.BufferTypeName(Value,theStruct.TypeOverride, theAppend)
              //   TypeHelpers.BufferTypeName(Value,theStruct.TypeOverride, theAppend)
                 );
         }
     }
     
     public class UntypedBufferInput<T>: ObjectInput<Buffer<T>> where T : struct
     {
 
         private readonly Buffer _buffer;
         private readonly ObjectParameterKey<Buffer> _parameterKey;

         public UntypedBufferInput(T theType, Buffer theBuffer = null, bool theAppend = true) : base("BufferInput")
         {
             _buffer = theBuffer;
             _parameterKey = new ObjectParameterKey<Buffer>(Output.ID);
             SetFieldDeclaration(
                 TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), theAppend)
                 //  TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), theAppend)
             );
         }
         
         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(_parameterKey, _buffer);
         }
     }
     
     public class BufferInput<T>: ObjectInput<Buffer<T>> where T : struct
     {

         public BufferInput(string theName, Buffer<T> theBuffer = null, bool theAppend = true) : base("BufferInput", theBuffer)
         {
             SetFieldDeclaration(
                 TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), theAppend)
               //  TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), theAppend)
                 );
         }
     }
     
    public class GpuInput<T> : AbstractInput<T,ValueParameterKey<T>> where T : struct 
    {

        public GpuInput(): base("GPUInput", false)
        {
            ParameterKey = new ValueParameterKey<T>(Output.ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }

        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(ParameterKey, Value);
        }
    }
}