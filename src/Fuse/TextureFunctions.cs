using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using NuGet;
using Stride.Graphics;
using VL.Core;

namespace Fuse
{

    public interface ITextureInput
    {
        
    }

    public class TextureGetDimensions : ShaderNode<GpuVoid>
    {
        private readonly ShaderNode<Texture> _texture;
        private readonly List<AbstractShaderNode> _arguments;

        public TextureGetDimensions(NodeContext nodeContext, int dimension, ShaderNode<Texture> theTexture, ShaderNode<int> mipLevel) : base(nodeContext, "textureGetDimension")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
            
            _texture = theTexture;

            OptionalOutputs.Clear();

            _arguments = new List<AbstractShaderNode> { mipLevel };
            var myInputs = new List<AbstractShaderNode> {_texture, mipLevel};
            var factory = new NodeSubContextFactory(NodeContext);
            for (var i = 0; i < dimension + 1; i++)
            {
                var myDeclareValue = new DeclareValue<float>(factory.NextSubContext());
                myInputs.Add(myDeclareValue);
                _arguments.Add(myDeclareValue);
                OptionalOutputs.Add(myDeclareValue);
            }

            SetInputs(myInputs);
        }

        protected override string SourceTemplate()
        {
            var result = new Dictionary<string, string>()
                { { "arguments", ShaderNodesUtil.BuildArguments(_arguments) } };
            if (_texture != null) result["texture"] = _texture.ID;

            return ShaderNodesUtil.Evaluate("${texture}.GetDimensions(${arguments});", result);
        }
    }

    public class TextureFunction<T> : ShaderNode<T>
    {
        private readonly ShaderNode<Texture> _texture;
        private readonly List<AbstractShaderNode> _arguments;
        private readonly string _functionName;

        public TextureFunction(
            NodeContext nodeContext, 
            string theFunction,
            ShaderNode<Texture> theTexture,
            IEnumerable<AbstractShaderNode> theArguments,
            ShaderNode<T> theDefault) : base(nodeContext, "textureNode", theDefault)
        {
            _functionName = theFunction;
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