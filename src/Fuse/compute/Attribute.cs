using System.Collections.Generic;
using Fuse.compute;
using Stride.Graphics;
using VL.Core;

namespace Fuse.ComputeSystem
{
    public interface IAttribute
    {
        public string Name { get; }

        public AttributeType AttributeType { get; }
        
        public AbstractShaderNode ShaderNode { get; }
        
        public void SetInput(AbstractShaderNode theNode);

        public ShaderNode<GpuVoid> WriteCall { get; set; }

        public AbstractShaderNode ReadCall { get; set; }
    }

    public enum AttributeType
    {
        Temporary,
        StructuredBuffer,
        PrimitiveBuffer,
        Texture,
        DoubleBufferedTexture,
        IdAppendBuffer
    }
    
    public class Attribute<T> : PassThroughNode<T>, IAttribute
    {
        public Attribute(NodeContext nodeContext, string theGroup, string theName, AttributeType theType) : base(nodeContext, theName, false)
        {
            AttributeType = theType;
            WriteCall = new EmptyVoid(new NodeSubContextFactory(nodeContext).NextSubContext());
            ShaderNode = new ShaderNode<T>(new NodeSubContextFactory(nodeContext).NextSubContext(),theName);
            
            AddProperty("ComputeSystemAttribute", this);
        }
        public AttributeType AttributeType { get; }

        public AbstractShaderNode ShaderNode { get; }

        public ShaderNode<GpuVoid> WriteCall { get; set; }
        
        public AbstractShaderNode ReadCall { get; set; }

        public void SetAbstractInput(AbstractShaderNode theNode)
        {
            SetInput(theNode);
        }
    }

    public interface IStructureBufferAttribute : IAttribute
    {
        public string Group { get; }
    }

    public class StructuredBufferAttribute<T> : Attribute<T>, IStructureBufferAttribute
    {
        public StructuredBufferAttribute(NodeContext nodeContext, string theGroup, string theName) : base(nodeContext, "", theName, AttributeType.StructuredBuffer)
        {
            Group = theGroup;
        }
        
        public string Group { get; }
        
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
    }

    public class TempoaryAttribute<T> : Attribute<T>
    {
        public TempoaryAttribute(NodeContext nodeContext, string theName) : base(nodeContext, "", theName, AttributeType.Temporary)
        {
        }
    }

    public class AppendBufferAttribute : Attribute<BufferInput<int>>
    {

        public Buffer<int> AppendBuffer { get; set; }

        public Buffer<int> DispatchArgsBuffer { get; set; }
        
        public BufferInput<int> AppendBufferGPU { get; set; }

        public BufferInput<int> DispatchArgsBufferGPU { get; set; }

        public AppendBufferAttribute(NodeContext nodeContext, string theName) : base(nodeContext, "", theName, AttributeType.IdAppendBuffer)
        {
            
        }
    }
}