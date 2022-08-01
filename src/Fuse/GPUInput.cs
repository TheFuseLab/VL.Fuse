using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;

namespace Fuse{

    public interface IGpuInput : IAtomicComputeNode
    {
        void SetParameterValue(ParameterCollection theCollection);

        void AddParameters(ParameterCollection theCollection);

        void UpdateParameter();
    }
    
    

    public class DefaultInput<T> : ShaderNode<T>, IGpuInput
    {
        public DefaultInput(string theName) : base(theName, null)
        {
            SetInputs(new List<AbstractShaderNode>());
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
        
        public override string ID => Name;
    }

    public abstract class AbstractInput<T, TParameterKeyType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T>
     {
         private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";
         
         private readonly bool _isResource;
         
         protected AbstractInput(string theName, bool theIsResource, T theValue = default): base(theName)
         {
             _isResource = theIsResource;
             Value = theValue;
             Parameters = new HashSet<ParameterCollection>();
             SetInputs( new List<AbstractShaderNode>());
             AddResource(Inputs, this);
         }

         private HashSet<ParameterCollection> Parameters { get;  }
         
         public void AddParameters(ParameterCollection theCollection)
         {
             Parameters.Add(theCollection);
         }

         public void UpdateParameter()
         {
             Parameters?.ForEach(SetParameterValue);
         }
         
         public abstract void SetParameterValue(ParameterCollection theCollection);

         protected override string SourceTemplate()
         {
             return "";
         }

         protected TParameterKeyType ParameterKey { get; set;}
         
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

         private string BuildDeclaration(string theTypeName)
         {
             return ShaderNodesUtil.Evaluate(
                 DeclarationTemplate,
                 new Dictionary<string, string>
                 {
                     {"inputName", ID},
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
             if (HashCode >= 0) return;
             base.SetHashCodes(theContext);
             ParameterKey = theContext.GetParameterKey(new ObjectParameterKey<T>(ID)) as ObjectParameterKey<T>;
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
             if (HashCode >= 0) return;
             
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
             if (HashCode >= 0) return;
             
             base.SetHashCodes(theContext);
             SetFieldDeclaration("SamplerState", "SamplerState");
         }
     }

     public class BufferInput<T>: ObjectInput<Buffer>
     {

         private readonly ShaderNode<T> _struct;
         private readonly bool _append;
         
         public BufferInput(ShaderNode<T> theStruct, Buffer theBuffer = null, bool theAppend = true) : base("DynamicBufferInput",theBuffer)
         {
             _struct = theStruct;
             _append = theAppend;
         }
         
         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             if (HashCode >= 0) return;
             
             base.SetHashCodes(theContext);
             SetFieldDeclaration(TypeHelpers.BufferTypeName(Value,_struct == null?TypeHelpers.GetGpuTypeForType<T>():_struct.TypeName(), _append));
         }
     }
     
     public class TypedBufferInput<T>: ObjectInput<Buffer<T>> where T : struct
     {
         private readonly bool _append;

         public TypedBufferInput( Buffer<T> theBuffer = null, bool theAppend = true) : base("BufferInput", theBuffer)
         {
             _append = theAppend;
         }
         
         public override void SetHashCodes(ShaderGeneratorContext theContext)
         {
             if (HashCode >= 0) return;
             
             base.SetHashCodes(theContext);
             SetFieldDeclaration(TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), _append));
         }
     }
     
    public class GpuInput<T> : AbstractInput<T,ValueParameterKey<T>> where T : struct 
    {

        public GpuInput(): base("input", false)
        {
        }
        
        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(ParameterKey, Value);
        }

        public override void OnGenerateCode(ShaderGeneratorContext theContext)
        {
            ParameterKey = new ValueParameterKey<T>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }
        
        
    }
}