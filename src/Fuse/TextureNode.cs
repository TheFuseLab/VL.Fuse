using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using NuGet;
using Stride.Graphics;

namespace Fuse
{
    
    public class TextureGetDimensions : ShaderNode<GpuVoid>
    {
        private readonly GpuValue<Texture> _texture;
        private readonly GpuValue<int> _mipLevel;

        public TextureGetDimensions(GpuValue<Texture> theTexture, GpuValue<int> mipLevel, int dimension):base("textureGetDimension")
        {
            _texture = theTexture;
            _mipLevel = mipLevel;
            
            OptionalOutputs = new List<AbstractGpuValue>();
            
            var myInputs = new List<AbstractGpuValue> {mipLevel};

            for (var i = 0; i < dimension + 1; i++)
            {
                var myDeclareValue = new DeclareValue<float>().Output;
                myInputs.Add(myDeclareValue);
                var myPass = new GpuValuePassThrough<float>(myDeclareValue)
                {
                    ParentNode = this
                };
                OptionalOutputs.Add(myPass);
            }
            Setup(myInputs);
        }

        protected override string SourceTemplate()
        {
            var result = new Dictionary<string, string>();
            if (_texture != null) result["texture"] = _texture.ID;
            
            return ShaderNodesUtil.Evaluate("${texture}.GetDimensions(${arguments});", result);
        }
        
        protected void Setup(IEnumerable<AbstractGpuValue> theArguments, IEnumerable<InputModifier> theModifiers, string theFunction)
        {
            
        }
    }
    public class TextureNode<T> : ShaderNode<T>
    {

        private readonly GpuValue<Texture> _texture;
        private readonly List<AbstractGpuValue> _arguments;
        private readonly string _functionName;
        public TextureNode(
            GpuValue<Texture> theTexture, 
            IEnumerable<AbstractGpuValue> theArguments, 
            string theFunction, 
            GpuValue<T> theDefault
        ) : base( "textureNode", theDefault)
        {
            _texture = theTexture;
            _arguments = theArguments.ToList();
            _functionName = theFunction;

            var ins = new List<AbstractGpuValue>(_arguments) {theTexture};
            Setup(ins);
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
                {"function", _functionName},
                {"resultName", Output.ID},
                {"resultType", Output != null ? Output.TypeName() : TypeHelpers.GetGpuTypeForType<T>()},
                {"default", Default == null ? "": Default.ID},
                {"arguments", ShaderNodesUtil.BuildArguments(_arguments)}
            };
            if (_texture != null) result["texture"] = _texture.ID;
            
            result.AddRange(base.CreateTemplateMap());

            return result;
        }
    }
}