using System.Collections.Generic;
using VL.Core;

namespace Fuse.ComputeSystem
{
    public interface IAttribute
    {
        public string Name { get; }
        
        public AttributeType AttributeType { get; }
        
        public AbstractShaderNode ShaderNode { get; }
        
        public void SetInput(AbstractShaderNode theNode);

        public List<string> Description { get; }
    }

    public enum AttributeType
    {
        Temporary,
        StructuredBuffer,
        PrimitiveBuffer,
        Texture,
        DoubleBufferedTexture
    }
    
    public class Attribute<T> : PassThroughNode<T>, IAttribute
    {
        
        public AttributeType AttributeType { get; }
        
        public AbstractShaderNode ShaderNode { get; }
        public List<string> Description {
            get { 
                var keys = new List<string> { "x", "y", "z", "w"};
                var dimension = TypeHelpers.GetDimension<T>();
                var result = new List<string>();
                if (dimension == 1)
                {
                    result.Add(Name);
                }else{
                    for (var i = 0; i < dimension;i++)
                    {
                        result.Add(Name +"." + keys[i]);
                    }
                }
                return result;
        } }

        public Attribute(NodeContext nodeContext, string theGroup, string theName, AttributeType theType) : base(nodeContext, theName)
        {
            AttributeType = theType;
            ShaderNode = new ShaderNode<T>(new NodeSubContextFactory(nodeContext).NextSubContext(),theName);
            
            AddProperty(theGroup, this);
        }

        public override ShaderNode<T> Input { get => _input;
            set
            {
                _input = value ?? Default;
                SetInputs(new List<AbstractShaderNode>{Input}, false);
            }
        }

        public void SetAbstractInput(AbstractShaderNode theNode)
        {
            SetInput(theNode);
        }
    }
}