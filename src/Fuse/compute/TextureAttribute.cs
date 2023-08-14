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

    public class TextureAttribute<TIndex,T> : Attribute<T>, ITextureInput, ITextureAttribute where T : struct
    {
       // public ShaderNode<T> Default;
        public TextureAttribute(NodeContext nodeContext, string theGroup, string theName, bool theIsDoubleBuffered, ShaderNode<T> theDefault = null) : base(nodeContext, theGroup, theName, AttributeType.Texture)
        {
            var myFactory = new NodeSubContextFactory(nodeContext);
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

        public void OverRideByIndex(ShaderNode<Texture> theInstance, ShaderNode<Int3> theIndex)
        {
            Index = theIndex;
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public void OverRideByTexture(ShaderNode<Texture> theInstance, ShaderNode<TIndex> theIndex)
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
        public string TextureID => TextureInput?.ID;
    }

}