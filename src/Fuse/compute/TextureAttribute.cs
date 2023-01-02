using System.Collections.Generic;
using Fuse.ComputeSystem;
using Stride.Graphics;
using VL.Core;

namespace Fuse.compute
{
    public enum TextureAttributeType
    {
        Attribute,
        Resource,
        Instance
    }

    public interface ITextureAttribute : IAttribute
    {
        public string Group { get; }
        
        public bool DoubleBuffered { get; }

        public TextureAttributeType TextureAttributeType();

        public TextureInput TextureInput{
            get;
            set;
        }
    }

    public class TextureAttribute<TIndex,T> : Attribute<T>, ITextureAttribute where T : struct
    {
       // public ShaderNode<T> Default;
        public TextureAttribute(NodeContext nodeContext, string theGroup, string theName, bool theIsDoubleBuffered, ShaderNode<T> theDefault = null) : base(nodeContext, theName, AttributeType.Texture)
        {
            var myFactory = new NodeSubContextFactory(nodeContext);
            Group = theGroup;
            DoubleBuffered = theIsDoubleBuffered;
            Default = theDefault ?? ConstantHelper.FromFloat<T>(myFactory.NextSubContext(), 0f);
            
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public TextureInput TextureInput{
            get;
            set;
        }

        public bool DoubleBuffered { get; }
        public string Group { get; }
        public TextureAttributeType TextureAttributeType()
        {
            return compute.TextureAttributeType.Attribute;
        }

        public void OverRideByTexture(ShaderNode<Texture> theInstance, ShaderNode<TIndex> theIndex)
        {
            if (theInstance == null)
            {
                SetProperty("ComputeSystemAttribute", this);
            }else{
                var myFactory = new NodeSubContextFactory(NodeContext,1);
                var textureGet = new ComputeTextureGet<TIndex, T>(myFactory.NextSubContext());
                textureGet.SetInputs(theInstance, theIndex);
                SetInput(textureGet);
                RemoveProperty("ComputeSystemAttribute");
            }
            CallChangeEvent();
        }
    }
    
    public class TextureResource : Attribute<Texture>, ITextureAttribute
    {
        public TextureResource(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.Texture)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public string Group => Name;

        public TextureAttributeType TextureAttributeType()
        {
            return compute.TextureAttributeType.Resource;
        }

        public bool DoubleBuffered => true;
        
        public TextureInput TextureInput{
            get;
            set;
        }
    }
    
    public class TextureInstance<T> : Attribute<T>, ITextureAttribute
    {



        public TextureInstance(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.Texture)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public string Group => Name;

        public TextureAttributeType TextureAttributeType()
        {
            return compute.TextureAttributeType.Instance;
        }
        
        public bool DoubleBuffered => true;
        
        public TextureInput TextureInput{
            get;
            set;
        }
    }
}