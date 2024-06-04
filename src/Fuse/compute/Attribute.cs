﻿using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.ComputeSystem
{
    public interface IAttribute
    {
        public string Name { get; }

        public AttributeType AttributeType { get; set; }
        
        public AbstractShaderNode ShaderNode { get; }

        public AbstractShaderNode InputAbstract { get; set; }

        public void Sync(IAttribute theAttribute);

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
        public Attribute(NodeContext nodeContext, string theName, AttributeType theType,AbstractShaderNode theValue = null) : base(nodeContext, theValue, theName)
        {
            AttributeType = theType;
            WriteCall = new EmptyVoid(new NodeSubContextFactory(nodeContext).NextSubContext());
            ShaderNode = new ShaderNode<T>(new NodeSubContextFactory(nodeContext).NextSubContext(),theName);
            IsOverridden = false;
            HasFixedName = true;
        }
        public AttributeType AttributeType { get; set; }
        public AbstractShaderNode ShaderNode { get; }
        public AbstractShaderNode InputAbstract
        {
            get => Input;
            set => SetInput(value);
        }

        public void SetInput(AbstractShaderNode theNode)
        {
            Input = theNode as ShaderNode<T>;
            SetInputs(new List<AbstractShaderNode>{Input});
        }

        public virtual void Sync(IAttribute theAttribute)
        {
            if (theAttribute == null || theAttribute == this) return;

            InputAbstract = theAttribute.InputAbstract;
            ReadCall = theAttribute.ReadCall;
            WriteCall = theAttribute.WriteCall;
        }

        public ShaderNode<GpuVoid> WriteCall { get; set; }
        
        public AbstractShaderNode ReadCall { get; set; }
        
        public abstract Int3 Resolution { get; }

        public bool IsOverridden { get; protected set; }
    }

    public class TemporaryAttribute<T> : Attribute<T>
    {
        public TemporaryAttribute(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.Temporary)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public override Int3 Resolution => new(1);
    }

}