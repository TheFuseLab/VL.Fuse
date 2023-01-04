using System.Collections.Generic;
using Fuse.ComputeSystem;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.compute
{
    

    public interface ITextureAttribute : IAttribute
    {
        public string Group { get; }
        
        public bool DoubleBuffered { get; }

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
            Default = theDefault ?? ConstantHelper.FromFloat<T>( 0f);
            
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public TextureInput TextureInput{
            get;
            set;
        }

        public bool DoubleBuffered { get; }
        public string Group { get; }

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

        public override Int3 Resolution => TextureInput?.Value == null ? new Int3(1) : new Int3(TextureInput.Value.Width, TextureInput.Value.Height, TextureInput.Value.Depth);
    }

}