using System.Collections.Generic;
using Fuse.ComputeSystem;
using Stride.Core.Mathematics;
using Stride.Graphics;
using VL.Core;

namespace Fuse.compute
{
    public enum StructuredBufferAttributeType
    {
        Attribute,
        Object
    }

    public interface IStructureBufferAttribute : IAttribute
    {
        public Buffer Buffer { get; set; }

        public StructuredBufferAttributeType StructuredBufferAttributeType();
    }

    public abstract class AbstractStructuredBufferAttribute<T> : Attribute<T>, IStructureBufferAttribute
    {
        protected AbstractStructuredBufferAttribute(NodeContext nodeContext, string theName, AttributeType theType) : base(nodeContext, theName, theType)
        {
        }

        public Buffer Buffer { get; set; }
        public abstract StructuredBufferAttributeType StructuredBufferAttributeType();
        
        public override Int3 Resolution => new(Buffer.ElementCount, 1, 1);
    }

    public class StructuredBufferAttribute<T> : AbstractStructuredBufferAttribute<T>
    {

        // public ShaderNode<T> Default;

        public StructuredBufferAttribute(NodeContext nodeContext, string theName, bool theDefineSemantic = true, ShaderNode<GpuStruct> theOverrideInstance = null, ShaderNode<T> theDefault = null) : base(nodeContext, theName, AttributeType.StructuredBuffer)
        {
            var myFactory = new NodeSubContextFactory(nodeContext);

            Default = theDefault ?? ConstantHelper.FromFloat<T>( 0f);
            
            if (theOverrideInstance != null)
            {
                var getMember = new GetMember<GpuStruct, T>(myFactory.NextSubContext(), theOverrideInstance, theName);
                SetInput(getMember);
                IsOverridden = true;
            }
            else
            {
                SetInput(new Semantic<T>(myFactory.NextSubContext(),theName, theDefineSemantic, theName.ToUpper()));
                SetProperty("ComputeSystemAttribute", this);
                SetViewerID("ComputeAttribute");
            }
            
        }
        
        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Attribute;
        }
    }
    
    public class StructuredBufferObject : AbstractStructuredBufferAttribute<Buffer>, IBufferInput<GpuStruct>
    {
        public StructuredBufferObject(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.StructuredBuffer)
        {
            _group = theName;
            SetProperty("ComputeSystemAttribute", this);
        }

        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Object;
        }
        private readonly string _group;
        public string Resource
        {
            get => _group;
            set { }
        }
        
        public BufferType BufferType {
            set { }
            get => BufferType.Normal;
        }
    }
    /*
    public class StructuredBufferResource : AbstractStructuredBufferObject<Buffer>, IBufferInput<GpuStruct>
    {
        public StructuredBufferResource(NodeContext nodeContext, string theName) : base(nodeContext, compute.StructuredBufferAttributeType.Resource, theName)
        {
        }

        public BufferType BufferType {
            set { }
            get => BufferType.Normal;
        }
    }
    
    public class StructuredBufferInstance : AbstractStructuredBufferObject<GpuStruct>, IStructureBufferAttribute
    {

        public StructuredBufferInstance(NodeContext nodeContext, string theName) : base(nodeContext, compute.StructuredBufferAttributeType.Instance, theName)
        {
        }
    }
    
    public class StructuredBufferCreateInstance : AbstractStructuredBufferObject<GpuStruct>, IStructureBufferAttribute
    {

        public StructuredBufferCreateInstance(NodeContext nodeContext, string theName) : base(nodeContext, compute.StructuredBufferAttributeType.CreateInstance, theName)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.CreateInstance;
        }

       
    }*/
}