﻿using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using Stride.Graphics;
using VL.Core;

namespace Fuse
{

    public interface ITextureInput
    {
        public string TextureID { get; }
    }

    public class TextureGetDimensions : ShaderNode<GpuVoid>
    {
        private readonly ITextureInput _texture;
        private readonly List<AbstractShaderNode> _arguments;

        public TextureGetDimensions(NodeContext nodeContext, int dimension, ITextureInput theTexture, ShaderNode<int> mipLevel) : base(nodeContext, "textureGetDimension")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
            
            _texture = theTexture as ITextureInput;

            OptionalOutputs.Clear();

            _arguments = new List<AbstractShaderNode> { mipLevel };
            var myInputs = new List<AbstractShaderNode> {_texture as AbstractShaderNode, mipLevel};
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
            if (_texture != null) result["texture"] = _texture.TextureID;

            return ShaderNodesUtil.Evaluate("${texture}.GetDimensions(${arguments});", result);
        }
    }

    public class TextureFunction<T> : ShaderNode<T>
    {
        private readonly ITextureInput _texture;
        private readonly List<AbstractShaderNode> _arguments;
        private readonly string _functionName;

        public TextureFunction(
            NodeContext nodeContext, 
            string theFunction,
            ITextureInput theTexture,
            IEnumerable<AbstractShaderNode> theArguments,
            ShaderNode<T> theDefault) : base(nodeContext, "textureNode", theDefault)
        {
            _functionName = theFunction;
            _texture = theTexture as ITextureInput;
            _arguments = theArguments.ToList();

            var ins = new List<AbstractShaderNode> { theTexture as AbstractShaderNode };
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
                    {"texture", _texture.TextureID}
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
            if (_texture != null) result["texture"] = _texture.TextureID;

            return result;
        }
    }
}