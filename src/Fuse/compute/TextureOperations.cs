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
        private ShaderNode<Texture> _texture;
        private ShaderNode<TIndex> _index;
        
        public ComputeTextureGet(NodeContext nodeContext, ShaderNode<T> theDefault = null) : base( nodeContext,"getTextureValue",  theDefault)
        {
            
        }
        
        public ComputeTextureGet(NodeContext nodeContext, ShaderNode<Texture> theTexture, ShaderNode<TIndex> theIndex, ShaderNode<T> theDefault = null) : base( nodeContext,"getTextureValue",  theDefault)
        {
            _texture = theTexture;
            _index = theIndex;
            SetInputs(new List<AbstractShaderNode>{theTexture,theIndex});
        }

        public void SetInputs(ShaderNode<Texture> theTexture, ShaderNode<TIndex> theIndex)
        {
            _texture = theTexture;
            _index = theIndex;
            SetInputs(new List<AbstractShaderNode>{theTexture,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${textureName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>
            {
                {"textureName", _texture.ID},
                {"index", _index.ID}
            });
        }
    }
    
    public class TextureAttributeFunction<T> : ShaderNode<T>
    {
        private ShaderNode<T> _textureAttribute;
        private List<AbstractShaderNode> _arguments;
        private readonly string _functionName;

        public TextureAttributeFunction(
            NodeContext nodeContext, 
            string theFunction,
            ShaderNode<T> theDefault) : base(nodeContext, "textureNode", theDefault)
        {
            _functionName = theFunction;
        }

        public void SetInput(
            ShaderNode<T> theTextureAttribute,
            IEnumerable<AbstractShaderNode> theArguments)
        {
            _textureAttribute = theTextureAttribute;
            _arguments = theArguments.ToList();

            var ins = new List<AbstractShaderNode>();
            ins.Add(theTextureAttribute);
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
        private ShaderNode<T> _textureAttribute;
        private ShaderNode<TIndex> _index;
        
        public ComputeTextureAttributeGet(NodeContext nodeContext, ShaderNode<T> theDefault = null) : base( nodeContext,"getTextureValue",  theDefault)
        {
            
        }
        
        public ComputeTextureAttributeGet(NodeContext nodeContext, ShaderNode<T> theTextureAttribute, ShaderNode<TIndex> theIndex, ShaderNode<T> theDefault = null) : base( nodeContext,"getTextureValue",  theDefault)
        {
            _textureAttribute = theTextureAttribute;
            _index = theIndex;
            SetInputs(new List<AbstractShaderNode>{theTextureAttribute,theIndex});
        }

        public void SetInputs(ShaderNode<T> theTextureAttribute, ShaderNode<TIndex> theIndex)
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

    public class ComputeTextureSet<TIndex, T> : ShaderNode<GpuVoid>, IComputeVoid where T : struct
    {
        private ShaderNode<Texture> _texture;
        private AbstractShaderNode _index;
        private ShaderNode<T> _value;
    
        public ComputeTextureSet(NodeContext nodeContext) : base( nodeContext, "setTextureValue")
        {
            
        }
        
        public ComputeTextureSet(
            NodeContext nodeContext,
            ShaderNode<Texture> theTexture, 
            ShaderNode<TIndex> theIndex, 
            ShaderNode<T> theValue) : base( nodeContext, "setTextureValue")
        {
            SetInputs(theTexture, theIndex, theValue);
        }

        public void SetInputs(
            ShaderNode<Texture> theTexture, 
            ShaderNode<TIndex> theIndex, 
            ShaderNode<T> theValue)
        {
            _texture = theTexture;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{theTexture,theIndex,theValue});
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
                {"textureName", _texture.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
    
    public class ComputeTextureAbstractSet : ShaderNode<GpuVoid> 
    {
        private ShaderNode<Texture> _texture;
        private AbstractShaderNode _index;
        private AbstractShaderNode _value;
    
        public ComputeTextureAbstractSet(NodeContext nodeContext) : base( nodeContext, "setTextureValue")
        {
           
        }

        public void SetInputs(ShaderNode<Texture> theTexture, 
            AbstractShaderNode theIndex,
            AbstractShaderNode theValue)
        {
            _texture = theTexture;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{theTexture,theIndex,theValue});
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
                {"textureName", _texture.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
}