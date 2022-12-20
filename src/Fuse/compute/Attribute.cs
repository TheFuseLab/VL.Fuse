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
        public Attribute(NodeContext nodeContext, string theName, AttributeType theType) : base(nodeContext, theName, false)
        {
            AttributeType = theType;
            WriteCall = new EmptyVoid(new NodeSubContextFactory(nodeContext).NextSubContext());
            ShaderNode = new ShaderNode<T>(new NodeSubContextFactory(nodeContext).NextSubContext(),theName);
            
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
    
    

    public class TemporaryAttribute<T> : Attribute<T>
    {
        public TemporaryAttribute(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.Temporary)
        {
            AddProperty("ComputeSystemAttribute", this);
        }
    }

    public class AppendBufferAttribute : Attribute<BufferInput<int>>
    {

        private Buffer<int> _appendBuffer;
        public Buffer<int> AppendBuffer
        {
            get => _appendBuffer;
            set
            {
                _appendBuffer = value;
                AppendBufferGpu.SetInput(value,null);
            }
        }
        
        private Buffer<int> _dispatchArgsBuffer;

        public Buffer<int> DispatchArgsBuffer { get=>_dispatchArgsBuffer;
            set
            {
                _dispatchArgsBuffer = value;
                DispatchArgsBufferGpu.SetInput(value,null);
            }
        }
        
        public BufferInput<int> AppendBufferGpu { get; set; }

        public BufferInput<int> DispatchArgsBufferGpu { get; set; }

        public AppendBufferAttribute(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.IdAppendBuffer)
        {
            var nodeSubContextFactory = new NodeSubContextFactory(nodeContext);
            AppendBufferGpu = new BufferInput<int>(nodeSubContextFactory.NextSubContext(), new ConstantValue<int>(nodeSubContextFactory.NextSubContext(),0))
                {
                    ForceAppendConsume = true
                };
            AppendBufferGpu.AddProperty("ComputeSystemAttribute", this);
            DispatchArgsBufferGpu = new BufferInput<int>(nodeSubContextFactory.NextSubContext(), new ConstantValue<int>(nodeSubContextFactory.NextSubContext(),0));
            DispatchArgsBufferGpu.AddProperty("ComputeSystemAttribute", this);
        }
    }
}