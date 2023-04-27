using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;
using Buffer = Stride.Graphics.Buffer;

namespace Fuse{

    public interface IGpuInput : IComputeNode
    {
        void SetParameterValue(ParameterCollection theCollection);
        
        public abstract void OnUpdateName();
    }
    
    

    public class DefaultInput<T> : ShaderNode<T>, IGpuInput
    {
        public DefaultInput(NodeContext nodeContext, string theName) : base(nodeContext, theName, null)
        {
            SetInputs(new List<AbstractShaderNode>());
        }

        public void SetParameterValue(ParameterCollection theCollection)
        {
        }

        public void OnUpdateName() {}

        public bool CheckDeclaration()
        {
            return false;
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        public override string ID => Name;
    }
    
    public class FieldDeclaration
    {
        
        private const string DeclarationTemplate = @"
        [Link(""${inputName}"")]
        stage ${inputType} ${inputName};";
        
        private string _computeShaderDeclaration;
        private string _declaration;
        
        public string TypeName { get; private set; }

        public FieldDeclaration(bool isResource, string computeShaderDeclaration, string declaration, string theID)
        {
            IsResource = isResource;
            Set(computeShaderDeclaration, declaration, theID);
        }

        public FieldDeclaration(string declaration)
        {
            _computeShaderDeclaration = declaration;
            _declaration = declaration;
        }
        
        private string BuildDeclaration(string theTypeName, string theID)
        {
            return ShaderNodesUtil.Evaluate(
                DeclarationTemplate,
                new Dictionary<string, string>
                {
                    {"inputName", theID},
                    {"inputType", theTypeName}
                });
        }

        public void Set(string computeShaderTypeName, string typeName, string theID)
        {
            TypeName = typeName;
            _computeShaderDeclaration = BuildDeclaration(computeShaderTypeName, theID);
            _declaration = BuildDeclaration(typeName, theID);
        }

        public string GetDeclaration(bool theIsComputeShader)
        {
            return theIsComputeShader && _computeShaderDeclaration != null ? _computeShaderDeclaration : _declaration;
        }

        public override string ToString()
        {
            return _declaration;
        }

        public bool IsResource { get; }
    }

    public abstract class AbstractInput<T, TParameterKeyType, TParameterUpdaterType> : ShaderNode<T>, IGpuInput where TParameterKeyType : ParameterKey<T> where TParameterUpdaterType : ParameterUpdater<T,TParameterKeyType>
     {
         
        

         protected readonly TParameterUpdaterType Updater;// = new ValueParameterUpdater<T>();

         protected T _value;

         private readonly FieldDeclaration _fieldDeclaration;
         
         protected AbstractInput(NodeContext nodeContext, string theName, bool theIsResource): base(nodeContext, theName)
         {
             Updater = CreateUpdater();
             SetInputs( new List<AbstractShaderNode>());
             AddProperty(Inputs, this);

             _fieldDeclaration = new FieldDeclaration(theIsResource, "", "", ID);
             AddProperty(Declarations,_fieldDeclaration);
         }

         protected abstract TParameterUpdaterType CreateUpdater();

        

         protected override string SourceTemplate()
         {
             return "";
         }
         
         protected override string GenerateDefaultSource()
         {
             return "";
         }

         protected TParameterKeyType ParameterKey { get; set;}

         public virtual T Value
         { 
             get => Updater.Value;
             set
             {
                 _value = value;
                 Updater.Value = value;
             }
         }

         
         
         

         protected void SetFieldDeclaration(string theTypeName, string theComputeTypeName)
         {
             _fieldDeclaration.Set(theComputeTypeName,theTypeName, ID);
         }

         protected void SetFieldDeclaration(string theTypeName)
         {
             SetFieldDeclaration(theTypeName,theTypeName);
         }
         
         public abstract void SetParameterValue(ParameterCollection theCollection);
         public abstract void OnUpdateName();
     }

     public abstract class ObjectInput<T> : AbstractInput<T, ObjectParameterKey<T>, ObjectParameterUpdater<T>> where T : class
     {
         protected ObjectInput(NodeContext nodeContext, string theName) : base(nodeContext, theName, true)
         {
             ParameterKey = new ObjectParameterKey<T>(ID);
         }

         protected override ObjectParameterUpdater<T> CreateUpdater()
         {
             return new ObjectParameterUpdater<T>();
         }

         public override void OnPassContext(ShaderGeneratorContext nodeContext)
         {
             Updater.Track(nodeContext, ParameterKey);
             Updater.Value = _value;
         }
         
         public override void SetParameterValue(ParameterCollection theCollection)
         {
             theCollection.Set(ParameterKey, _value);
         }

     }

     public abstract class GpuTypeTracker<T>
     {
         protected GpuTypeTracker()
         {
             ComputeGpuType = "";
             GpuType = "";
         }

         public string ComputeGpuType { get; private set; }

         public string GpuType { get; private set; }

         protected abstract string DefineGpuType(T value);
         protected abstract string DefineComputeGpuType(T value);
         
         public bool CheckDeclaration(T value)
         {
             var gpuType = DefineGpuType(value);
             var computeGpuType = DefineComputeGpuType(value);
             
             if (computeGpuType.Equals(ComputeGpuType) &&
                 gpuType.Equals(GpuType)) return false;

             ComputeGpuType = computeGpuType;
             GpuType = gpuType;
             return true;
         }
     }

     public abstract class ChangeableObjectInput<T> : ObjectInput<T> where T : class
     {

         public GpuTypeTracker<T> TypeTracker { get; }
         
         protected ChangeableObjectInput(NodeContext nodeContext, GpuTypeTracker<T> theTypeTracker, string theName) : base(nodeContext, theName)
         {
             TypeTracker = theTypeTracker;
             SetFieldDeclaration(TypeTracker.ComputeGpuType, TypeTracker.GpuType);
         }

         public override void OnUpdateName()
         {
             SetFieldDeclaration(TypeTracker.ComputeGpuType, TypeTracker.GpuType);
         }
     }
     
     public class TextureTypeTracker:GpuTypeTracker<Texture>{
         private bool _useRw;
         public TextureTypeTracker(bool theUseRw)
         {
             _useRw = theUseRw;
         }

         public void SetUseRw(bool theUseRw)
         {
             _useRw = theUseRw;
         }

         protected override string DefineGpuType(Texture value)
         {
             if (value == null)
             {
                 return "Texture1D";
             }

             return
                 value.Dimension switch
                 {
                     TextureDimension.Texture1D => "Texture1D",
                     TextureDimension.Texture2D => "Texture2D",
                     TextureDimension.Texture3D => "Texture3D",
                     TextureDimension.TextureCube => "TextureCube",
                     _ => "Texture2D"
                 };
         }

         protected override string DefineComputeGpuType(Texture value)
         {
             return value == null ? "Texture1D" : TypeHelpers.TextureTypeName(value, _useRw);
         }
     }

     public class TextureInput : ChangeableObjectInput<Texture>
     {
         public TextureTypeTracker TextureTypeTracker { get; }
         
         public TextureInput(NodeContext nodeContext, TextureTypeTracker theTypeTracker) : base(nodeContext, theTypeTracker, "TextureInput")
         {
             TextureTypeTracker = theTypeTracker;
         }
     }
     
     public class SamplerInput: ObjectInput<SamplerState>, IGpuInput 
     {
         public SamplerInput(NodeContext nodeContext) : base(nodeContext, "SamplerInput")
         {
             SetFieldDeclaration("SamplerState", "SamplerState");
         }

         public override void OnUpdateName()
         {
             SetFieldDeclaration("SamplerState", "SamplerState");
         }
     }

     public class BufferTypeTracker<T> : GpuTypeTracker<Buffer>
     {
         public bool Append { get; set; }

         private readonly bool _forceAppendConsume;
         private readonly ShaderNode<T> _type;

         public BufferTypeTracker(ShaderNode<T> theType, bool theAppend, bool theForceAppend)
         {
             _type = theType;
             Append = theAppend;
             _forceAppendConsume = theForceAppend;
         }

         protected override string DefineGpuType(Buffer value)
         {
             return TypeHelpers.BufferTypeName(value, _type == null ? TypeHelpers.GetGpuType<T>() : _type.TypeName(), Append, false, _forceAppendConsume);
         }

         protected override string DefineComputeGpuType(Buffer value)
         {
             return DefineGpuType(value);
         }
     }

     public class BufferInput<T>: ChangeableObjectInput<Buffer>, IBufferInput<T>
     {
         
         
         public ShaderNode<T> Type { get;  }

         private readonly BufferTypeTracker<T> _typeTracker;

         public bool Append
         {
             get => _typeTracker.Append;
             set
             {
                 _typeTracker.Append = value;
                 SetFieldDeclaration(_typeTracker.ComputeGpuType, _typeTracker.GpuType);
             }
         }

         public override string TypeName()
         {
             return Type == null ? TypeHelpers.GetGpuType<T>() : Type.TypeName();
         }

         public BufferInput(NodeContext nodeContext, BufferTypeTracker<T> theTypeTracker, ShaderNode<T> theType) : base(nodeContext, theTypeTracker, "DynamicBufferInput")
         {
             _typeTracker = theTypeTracker; 
             Value = null;
             Type = theType;
             
             SetInputs( new List<AbstractShaderNode>{Type});
         }

     }
     
    public class ValueInput<T> : AbstractInput<T,ValueParameterKey<T>, ValueParameterUpdater<T>> where T : struct 
    {

        public ValueInput(NodeContext nodeContext): base(nodeContext, "input", false)
        {
            ParameterKey = new ValueParameterKey<T>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuType<T>());
        }

        protected override ValueParameterUpdater<T> CreateUpdater()
        {
            return new ValueParameterUpdater<T>();
        }

        public override void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            try
            {
                Updater.Track(nodeContext, ParameterKey);
                Updater.Value = _value;
            }
            catch (Exception)
            {
               // Console.WriteLine(e);
            }
            
        }
        
        public override void SetParameterValue(ParameterCollection theCollection)
        {
            theCollection.Set(ParameterKey, Value);
        }
        
        public override void OnUpdateName()
        {
            ParameterKey = new ValueParameterKey<T>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuType<T>());
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

        protected override void OnPassContext(ShaderGeneratorContext nodeContext)
        {
            Updater.Track(nodeContext, ParameterKey);
        }

        protected override void OnGenerateCode(ShaderGeneratorContext nodeContext)
        {
            ParameterKey = new PermutationParameterKey<ShaderSource>(ID);
            SetFieldDeclaration(TypeHelpers.GetGpuTypeForType<T>());
        }
        
        
    }*/
}