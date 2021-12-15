using System.Collections.Generic;
using System.Linq;
using Stride.Graphics;

namespace Fuse
{
    
    public class TextureGetDimensions<GpuVoid> : ShaderNode<GpuVoid>
    {
        private readonly GpuValue<Texture> _texture;
        
        public TextureGetDimensions(GpuValue<Texture> theTexture, GpuValue<int> mipLevel):base("textureGetDimension")
        {
            _texture = theTexture;
            
            var myInputs = new List<AbstractGpuValue>() {mipLevel};
            var myModifiers = new List<InputModifier> {InputModifier.In, InputModifier.Out, InputModifier.Out, InputModifier.Out, InputModifier.Out, InputModifier.Out, InputModifier.Out};

            for (var i = 0; i < abstractGpuValues.Count(); i++)
            {
                

                var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(abstractGpuValues[i]);
                myInputs.Add(myDeclareValue);
                var myPass = AbstractCreation.AbstractGpuValuePassThrough(myDeclareValue);
                myPass.ParentNode = this;
                OptionalOutputs.Add(myPass);
            }
            Setup(myInputs, new Dictionary<string, string> {{"function", theFunction}});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${texture}.GetDimensions(${mipLevel},${arguments});", 
                new Dictionary<string, string>
                {
                    {"texture", _texture.ID}, 
                    {"texCoords", _texCoord.ID}, 
                    {"level", _level.ID}
                });
        }
        
        protected void Setup(IEnumerable<AbstractGpuValue> theArguments, IEnumerable<InputModifier> theModifiers, string theFunction)
        {
            
        }
    }
    public class TextureNode<T> : ShaderNode<T>
    {

        private readonly GpuValue<Texture> _texture;
        private readonly List<AbstractGpuValue> _arguments;
        public TextureNode(
            GpuValue<Texture> theTexture, 
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            ConstantValue<T> theDefault
        ) : base( "textureNode", theDefault)
        {
            _texture = theTexture;
            _arguments = theArguments.ToList();

            var ins = new List<AbstractGpuValue>(_arguments) {theTexture};
            Setup(ins, new Dictionary<string, string> {{"function", theFunction}});
        }
        
        protected override string SourceTemplate()
        {
            if (ShaderNodesUtil.HasNullValue(_arguments))
            {
                return GenerateDefaultSource();
            }
            
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${texture}.${function}(${arguments});", 
                new Dictionary<string, string>
                {
                    {"texture", _texture.ID}
                });
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType", Output != null ? Output.TypeName() : TypeHelpers.GetGpuTypeForType<T>()},
                {"default", Default == null ? "": Default.ID},
                {"arguments", ShaderNodesUtil.BuildArguments(_arguments)}
            };
            if (_texture != null) result["texture"] = _texture.ID;

            return result;
        }
    }
}