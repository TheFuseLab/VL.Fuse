using Fuse.ComputeSystem;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.compute
{
    

    public interface ITextureAttribute : IAttribute
    {
        
        public bool DoubleBuffered { get; }

        public ShaderNode<Int3> Index { get; set; }

        public TextureInput TextureInput{
            get;
            set;
        }
    }

    public abstract class ITextureAttributeNode<T> : Attribute<T>, ITextureInput, ITextureInputProvider
    {


        public abstract string TextureID();
        public abstract Int3 TextureSize();

        protected ITextureAttributeNode(NodeContext nodeContext, string theName, AttributeType theType, AbstractShaderNode theValue = null) : base(nodeContext, theName, theType, theValue)
        {
        }

        public ITextureInput GetTextureInput()
        {
            return this;
        }
    }

    public class TextureAttribute<TIndex,T> : ITextureAttributeNode<T>, ITextureAttribute where T : struct
    {
       // public ShaderNode<T> Default;
        public TextureAttribute(NodeContext nodeContext, string theName, bool theIsDoubleBuffered, ShaderNode<T> theDefault = null) : base(nodeContext, theName, AttributeType.Texture)
        {
            DoubleBuffered = theIsDoubleBuffered;
            Default = theDefault ?? ConstantHelper.FromFloat<T>( 0f);
            
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public TextureInput TextureInput{
            get;
            set;
        }
        
        public ShaderNode<Int3> Index{
            get;
            set;
        }

        public bool DoubleBuffered { get; }

        public void OverRideByIndex(ShaderNode<Int3> theIndex)
        {
            Index = theIndex;
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public void OverRideByTexture(ITextureInputProvider theInstance, ShaderNode<TIndex> theIndex)
        {
            if (theInstance == null) {
                SetProperty("ComputeSystemAttribute", this);
            }else{
                var myFactory = new NodeSubContextFactory(NodeContext,1);
                var textureGet = new ComputeTextureGet<TIndex, T>(myFactory.NextSubContext(),theInstance, theIndex);
                SetInput(textureGet);
                RemoveProperty("ComputeSystemAttribute");
            }
        }

        public override Int3 Resolution => TextureInput?.Value == null ? new Int3(1) : new Int3(TextureInput.Value.Width, TextureInput.Value.Height, TextureInput.Value.Depth);
       
        public override string TextureID()
        {
            return TextureInput?.ID;
        }

        public override Int3 TextureSize()
        {
            return Resolution;
        }
    }

}