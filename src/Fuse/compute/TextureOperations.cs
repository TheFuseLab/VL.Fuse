using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Graphics;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute
{

    public class ComputeTextureGet<TIndex,T> : ShaderNode<T> where T : struct
    {
        private readonly ITextureInput _texture;
        private readonly ShaderNode<TIndex> _index;
        
        
        public ComputeTextureGet(NodeContext nodeContext, ITextureInput theTexture, ShaderNode<TIndex> theIndex, ShaderNode<T> theDefault = null) : base( nodeContext,"getTextureValue",  theDefault)
        {
            _texture = theTexture as ITextureInput;
            _index = theIndex;
            SetInputs(new List<AbstractShaderNode>{theTexture as AbstractShaderNode,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${textureName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>
            {
                {"textureName", _texture.TextureID},
                {"index", _index.ID}
            });
        }
    }

    public class ComputeTextureSet<TIndex, T> : ShaderNode<GpuVoid>, IComputeVoid where T : struct
    {
        private readonly ITextureInput _texture;
        private readonly AbstractShaderNode _index;
        private readonly ShaderNode<T> _value;
    
        public ComputeTextureSet(NodeContext nodeContext) : base( nodeContext, "setTextureValue")
        {
            
        }
        
        public ComputeTextureSet(
            NodeContext nodeContext,
            ITextureInput theTexture, 
            ShaderNode<TIndex> theIndex, 
            ShaderNode<T> theValue) : base( nodeContext, "setTextureValue")
        {
            _texture = theTexture as ITextureInput;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{(AbstractShaderNode)theTexture,theIndex,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            if (_texture == null)
            {
                return GenerateDefaultSource();
            }
            
            const string shaderCode = "${textureName}[${index}] = ${value};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"textureName", _texture.TextureID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
    
    public class ComputeTextureAbstractSet : ShaderNode<GpuVoid> 
    {
        private readonly ITextureInput _texture;
        private readonly AbstractShaderNode _index;
        private readonly AbstractShaderNode _value;
    
        public ComputeTextureAbstractSet(
            NodeContext nodeContext,
            ITextureInput theTexture, 
            AbstractShaderNode theIndex,
            AbstractShaderNode theValue) : base( nodeContext, "setTextureValue")
        {
            _texture = theTexture as ITextureInput;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{(AbstractShaderNode)theTexture,theIndex,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${textureName}[${index}] = ${value};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"textureName", _texture.TextureID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
    
    /*
    public class TextureAttributeFunction<T> : ShaderNode<T>
    {
        private readonly ShaderNode<T> _textureAttribute;
        private readonly List<AbstractShaderNode> _arguments;
        private readonly string _functionName;

        public TextureAttributeFunction(
            NodeContext nodeContext, 
            string theFunction,
            ShaderNode<T> theTextureAttribute,
            IEnumerable<AbstractShaderNode> theArguments,
            ShaderNode<T> theDefault) : base(nodeContext, "textureNode", theDefault)
        {
            _functionName = theFunction;
            _textureAttribute = theTextureAttribute;
            _arguments = theArguments.ToList();

            var ins = new List<AbstractShaderNode> { theTextureAttribute };
            ins.AddRange(_arguments);
            SetInputs(ins);
        }

        protected override string SourceTemplate()
        {
            if (ShaderNodesUtil.HasNullValue(_arguments) || _textureAttribute == null)
            {
                return GenerateDefaultSource();
            }

            const string shaderCode = "${resultType} ${resultName} = ${textureName}.${function}(${arguments});";

            try
            {
                var textureAttribute = _textureAttribute as ITextureAttribute;

                return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
                {
                    {"textureName", textureAttribute.TextureInput.ID},
                    {"function", _functionName},
                    {"resultName", ID},
                    {"resultType", TypeName()},
                    {"arguments", ShaderNodesUtil.BuildArguments(_arguments)},
                    {"default", Default == null ? "" : Default.ID}
                });
            }
            catch (Exception)
            {
                return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = " + TypeHelpers.GetDefaultForType<T>()+";", new Dictionary<string, string>
                {
                    
                });
            }
        }
    }
    
    public class ComputeTextureAttributeGet<TIndex,T> : ShaderNode<T> where T : struct
    {
        private readonly ShaderNode<T> _textureAttribute;
        private readonly ShaderNode<TIndex> _index;

        public ComputeTextureAttributeGet(NodeContext nodeContext, ShaderNode<T> theTextureAttribute, ShaderNode<TIndex> theIndex, ShaderNode<T> theDefault = null) : base( nodeContext,"getTextureValue",  theDefault)
        {
            _textureAttribute = theTextureAttribute;
            _index = theIndex;
            SetInputs(new List<AbstractShaderNode>{theTextureAttribute,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${textureName}[${index}];";

            try
            {
                var textureAttribute = _textureAttribute as ITextureAttribute;

                return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
                {
                    {"textureName", textureAttribute.TextureInput.ID},
                    {"index", _index.ID}
                });
            }
            catch (Exception)
            {
                return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = " + TypeHelpers.GetDefaultForType<T>()+";", new Dictionary<string, string>
                {
                    
                });
            }
        }
    }
    
    public class TextureAttributeGetDimensions : ShaderNode<GpuVoid>
    {
        private readonly AbstractShaderNode _textureAttribute;
        private readonly List<AbstractShaderNode> _arguments;

        public TextureAttributeGetDimensions(NodeContext nodeContext, int dimension, AbstractShaderNode theTextureAttribute, ShaderNode<int> mipLevel) : base(nodeContext, "textureGetDimension")
        {
            OptionalOutputs = new List<AbstractShaderNode>();
            _textureAttribute = theTextureAttribute;

            _arguments = new List<AbstractShaderNode>{mipLevel};
            var myInputs = new List<AbstractShaderNode> {_textureAttribute, mipLevel};
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
            const string shaderCode = "${textureName}.GetDimensions(${arguments});";
            
            try
            {
                var textureAttribute = _textureAttribute as ITextureAttribute;

                return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
                {
                    {"textureName", textureAttribute.TextureInput.ID},
                    {"arguments", ShaderNodesUtil.BuildArguments(_arguments)},
                });
            }
            catch
            {
            }

            return "";
        }

        protected override string GenerateDefaultSource()
        {
            const string shaderCode = "${textureName}.GetDimensions(${arguments});";
            
            try
            {
                var textureAttribute = _textureAttribute as ITextureAttribute;

                return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>
                {
                    {"textureName", textureAttribute.TextureInput.ID},
                    {"arguments", ShaderNodesUtil.BuildArguments(_arguments)},
                });
            }
            catch
            {
            }

            return "";
        }
    }
    */
    
    
}