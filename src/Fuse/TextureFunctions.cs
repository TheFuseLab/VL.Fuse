using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using NuGet;
using Stride.Graphics;
using VL.Core;

namespace Fuse
{
    public class TextureGetDimensions : ShaderNode<GpuVoid>
    {
        private ShaderNode<Texture> _texture;
        private ShaderNode<int> _mipLevel;

        private readonly int _dimension;

        public TextureGetDimensions(NodeContext nodeContext, int dimension) : base(nodeContext, "textureGetDimension")
        {
            _dimension = dimension;
            OptionalOutputs = new List<AbstractShaderNode>();
        }

        public void SetInput(ShaderNode<Texture> theTexture, ShaderNode<int> mipLevel)
        {
            _texture = theTexture;
            _mipLevel = mipLevel;

            OptionalOutputs.Clear();

            var myInputs = new List<AbstractShaderNode> {_texture, _mipLevel};

            for (var i = 0; i < _dimension + 1; i++)
            {
                var myDeclareValue = new DeclareValue<float>(new NodeSubContextFactory(NodeContext).NextSubContext());
                myInputs.Add(myDeclareValue);
                OptionalOutputs.Add(myDeclareValue);
            }

            SetInputs(myInputs);
        }

        protected override string SourceTemplate()
        {
            var result = new Dictionary<string, string>();
            if (_texture != null) result["texture"] = _texture.ID;

            return ShaderNodesUtil.Evaluate("${texture}.GetDimensions(${arguments});", result);
        }

        protected void Setup(IEnumerable<AbstractShaderNode> theArguments, IEnumerable<InputModifier> theModifiers,
            string theFunction)
        {
        }
    }

    public class TextureFunction<T> : ShaderNode<T>
    {
        private ShaderNode<Texture> _texture;
        private List<AbstractShaderNode> _arguments;
        private readonly string _functionName;

        public TextureFunction(
            NodeContext nodeContext, 
            string theFunction,
            ShaderNode<T> theDefault) : base(nodeContext, "textureNode", theDefault)
        {
            _functionName = theFunction;
        }

        public void SetInput(
            ShaderNode<Texture> theTexture,
            IEnumerable<AbstractShaderNode> theArguments)
        {
            _texture = theTexture;
            _arguments = theArguments.ToList();

            var ins = new List<AbstractShaderNode>();
            ins.Add(theTexture);
            ins.AddRange(_arguments);
            SetInputs(ins);
        }

        protected override string SourceTemplate()
        {
            if (ShaderNodesUtil.HasNullValue(_arguments) || _texture == null)
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
            var result = new Dictionary<string, string>
            {
                {"function", _functionName},
                {"resultName", ID},
                {"resultType", TypeName()},
                {"arguments", ShaderNodesUtil.BuildArguments(_arguments)},
                {"default", Default == null ? "" : Default.ID}
            };
            if (_texture != null) result["texture"] = _texture.ID;

            return result;
        }
    }
}