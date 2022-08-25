using System.Collections.Generic;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse{

    public interface IGpuInput : IAtomicComputeNode
    {
        
    }
    
    

    public class DefaultInput<T> : ShaderNode<T>, IGpuInput
    {
        public DefaultInput(string theName) : base(theName, null)
        {
            SetInputs(new List<AbstractShaderNode>());
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        public override string ID => Name;
    }

    public abstract class AbstractInput<T, TParameterKeyType, TParameterUpdaterType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T> where TParameterUpdaterType : ParameterUpdater<T,TParameterKeyType>
     {
         
         private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";
         
         private readonly bool _isResource;
         
         protected readonly TParameterUpdaterType Updater;// = new ValueParameterUpdater<T>();

         protected T _value;
         protected AbstractInput(string theName, bool theIsResource, T theValue = default): base(theName)
         {
             _isResource = theIsResource;
             Updater = CreateUpdater();
             Value = theValue;
             SetInputs( new List<AbstractShaderNode>());
             AddResource(Inputs, this);
         }

         protected abstract TParameterUpdaterType CreateUpdater();

        

         protected override string SourceTemplate()
         {
             return "";
         }

         protected TParameterKeyType ParameterKey { get; set;}

         public T Value
         { 
             get => Updater.Value;
             set
             {
                 _value = value;
                 Updater.Value = value;
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

     public abstract class ObjectInput<T> : AbstractInput<T, ObjectParameterKey<T>, ObjectParameterUpdater<T>> where T : class
     {
         protected ObjectInput(string theName, T theValue = default) : base(theName, true, theValue)
         {
             
         }

         protected override ObjectParameterUpdater<T> CreateUpdater()
         {
             return new ObjectParameterUpdater<T>();
         }
         
         protected override void OnPassContext(ShaderGeneratorContext theContext)
         {
             Updater.Track(theContext, ParameterKey);
             Updater.Value = _value;
         }

         public abstract void ObjectFieldDeclaration();

         protected override void OnGenerateCode(ShaderGeneratorContext theContext)
         {
             ParameterKey = new ObjectParameterKey<T>(ID);
             ObjectFieldDeclaration();
         }
     }

     public class TextureInput: ObjectInput<Texture>
     {
         private readonly bool _useRw;
         public TextureInput(Texture theTexture, bool theUseRw = false) : base("TextureInput", theTexture)
         {
             _useRw = theUseRw;
         }

         public override void ObjectFieldDeclaration()
         {
             if (Value == null)
             {
                 SetFieldDeclaration("Texture1D");
                 return;
             }
             SetFieldDeclaration(TypeHelpers.TextureTypeName(Value, _useRw),
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

         public override void ObjectFieldDeclaration()
         {
             SetFieldDeclaration("SamplerState", "SamplerState");
         }
     }

     public class BufferInput<T>: ObjectInput<Buffer>
     {

         private readonly ShaderNode<T> _type;
         private readonly bool _append;
         
         public BufferInput(ShaderNode<T> theType, Buffer theBuffer = null, bool theAppend = true) : base("DynamicBufferInput",theBuffer)
         {
             _type = theType;
             _append = theAppend;
         }

         public override void ObjectFieldDeclaration()
         {
             SetFieldDeclaration(TypeHelpers.BufferTypeName(Value,_type == null ? TypeHelpers.GetGpuTypeForType<T>() : _type.TypeName(), _append));
         }
     }
     
     public class TypedBufferInput<T>: ObjectInput<Buffer<T>> where T : struct
     {
         private readonly bool _append;

         public TypedBufferInput( Buffer<T> theBuffer = null, bool theAppend = true) : base("BufferInput", theBuffer)
         {
             _append = theAppend;
         }

         public override void ObjectFieldDeclaration()
         {
             SetFieldDeclaration(TypeHelpers.BufferTypeName(Value, TypeHelpers.GetGpuTypeForType<T>(), _append));
         }
     }
     
    public class ValueInput<T> : AbstractInput<T,ValueParameterKey<T>, ValueParameterUpdater<T>> where T : struct 
    {

        public ValueInput(): base("input", false)
        {
        }

        protected override ValueParameterUpdater<T> CreateUpdater()
        {
            return new ValueParameterUpdater<T>();
        }

        protected override void OnPassContext(ShaderGeneratorContext theContext)
        {
            Updater.Track(theContext, ParameterKey);
            Updater.Value = _value;
        }

        protected override void OnGenerateCode(ShaderGeneratorContext theContext)
        {
            ParameterKey = new ValueParameterKey<T>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }
        
        
    }
    /*
    public class CompositionInput<T> : AbstractInput<IComputeNode,PermutationParameterKey<T>, PermutationParameterUpdater<T>> where T : IComputeNode
    {

        public CompositionInput(SetVar<T> value): base("input", false, value)
        {
        }

        protected override PermutationParameterUpdater<IComputeNode> CreateUpdater()
        {
            return new PermutationParameterUpdater<IComputeNode>();
        }

        protected override void OnPassContext(ShaderGeneratorContext theContext)
        {
            Updater.Track(theContext, ParameterKey);
        }

        protected override void OnGenerateCode(ShaderGeneratorContext theContext)
        {
            ParameterKey = new PermutationParameterKey<ShaderSource>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }
        
        
    }*/
}