using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.ComputeSystem
{
    public interface IAttribute
    {
        public string Group { get; set; }
        public string Name { get; }

        public AttributeType AttributeType { get; set; }
        
        public AbstractShaderNode ShaderNode { get; }
        
        public void SetInput(AbstractShaderNode theNode);

        public ShaderNode<GpuVoid> WriteCall { get; set; }

        public AbstractShaderNode ReadCall { get; set; }
        
        public Int3 Resolution { get; }

        public bool IsOverridden { get; }
    }

    public enum AttributeType
    {
        Temporary,
        StructuredBuffer,
        Texture
    }
    
    public abstract class Attribute<T> : PassThroughNode<T>, IAttribute
    {
        public Attribute(NodeContext nodeContext,string theGroup,  string theName, AttributeType theType,AbstractShaderNode theValue = null) : base(nodeContext, theValue, theName)
        {
            Group = theGroup;
            AttributeType = theType;
            WriteCall = new EmptyVoid(new NodeSubContextFactory(nodeContext).NextSubContext());
            ShaderNode = new ShaderNode<T>(new NodeSubContextFactory(nodeContext).NextSubContext(),theName);
            IsOverridden = false;
        }

        public virtual string Group { get; set; }
        public AttributeType AttributeType { get; set; }
        public AbstractShaderNode ShaderNode { get; }

        public void SetInput(AbstractShaderNode theNode)
        {
            Input = theNode as ShaderNode<T>;
            SetInputs(new List<AbstractShaderNode>{Input});
        }

        public ShaderNode<GpuVoid> WriteCall { get; set; }
        
        public AbstractShaderNode ReadCall { get; set; }
        
        public abstract Int3 Resolution { get; }

        public bool IsOverridden { get; protected set; }

        public void SetAbstractInput(AbstractShaderNode theNode)
        {
            SetInput(theNode);
        }
    }

    public class TemporaryAttribute<T> : Attribute<T>
    {
        public TemporaryAttribute(NodeContext nodeContext, string theName) : base(nodeContext, "Temporary", theName, AttributeType.Temporary)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public override Int3 Resolution => new(1);
    }
/*
    public class AppendBufferAttribute : Attribute<BufferInput<int>>
    {

        private Buffer<int> _appendBuffer;
        public Buffer<int> AppendBuffer
        {
            get => _appendBuffer;
            set
            {
                _appendBuffer = value;
                AppendBufferGpu.Value =value;
            }
        }
        
        private Buffer<int> _dispatchArgsBuffer;

        public Buffer<int> DispatchArgsBuffer { get=>_dispatchArgsBuffer;
            set
            {
                _dispatchArgsBuffer = value;
                DispatchArgsBufferGpu.Value = value;
            }
        }
        
        public BufferInput<int> AppendBufferGpu { get; set; }

        public BufferInput<int> DispatchArgsBufferGpu { get; set; }
        
        public override Int3 Resolution => new Int3(_appendBuffer.ElementCount,1,1);

        public AppendBufferAttribute(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.IdAppendBuffer)
        {
            var nodeSubContextFactory = new NodeSubContextFactory(nodeContext);
            AppendBufferGpu = new BufferInput<int>(nodeSubContextFactory.NextSubContext(), new ConstantValue<int>(0))
                {
                    ForceAppendConsume = true
                };
            AppendBufferGpu.AddProperty("ComputeSystemAttribute", this);
            DispatchArgsBufferGpu = new BufferInput<int>(nodeSubContextFactory.NextSubContext(), new ConstantValue<int>(0));
            DispatchArgsBufferGpu.AddProperty("ComputeSystemAttribute", this);
        }
    }*/
}