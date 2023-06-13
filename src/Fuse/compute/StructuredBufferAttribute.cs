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
        Resource,
        Instance,
        CreateInstance
    }

    public interface IStructureBufferAttribute : IAttribute
    {
        public Buffer Buffer { get; set; }

        public StructuredBufferAttributeType StructuredBufferAttributeType();
    }

    public abstract class AbstractStructuredBufferAttribute<T> : Attribute<T>, IStructureBufferAttribute
    {
        protected AbstractStructuredBufferAttribute(NodeContext nodeContext, string theGroup, string theName, AttributeType theType) : base(nodeContext, theGroup, theName, theType)
        {
        }

        public Buffer Buffer { get; set; }
        public abstract StructuredBufferAttributeType StructuredBufferAttributeType();
        
        public override Int3 Resolution => new(Buffer.ElementCount, 1, 1);
    }

    public class StructuredBufferAttribute<T> : AbstractStructuredBufferAttribute<T>
    {

        // public ShaderNode<T> Default;

        public StructuredBufferAttribute(NodeContext nodeContext, string theGroup, string theName, bool theDefineSemantic = true, ShaderNode<GpuStruct> theOverrideInstance = null, ShaderNode<T> theDefault = null) : base(nodeContext, theGroup, theName, AttributeType.StructuredBuffer)
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
    
    public abstract class AbstractStructuredBufferObject<T> : AbstractStructuredBufferAttribute<T>
    {
        private readonly StructuredBufferAttributeType _type;
        public AbstractStructuredBufferObject(NodeContext nodeContext, StructuredBufferAttributeType theType, string theName) : base(nodeContext, theName, theName, AttributeType.StructuredBuffer)
        {
            _type = theType;
            _group = theName;
            SetProperty("ComputeSystemAttribute", this);
        }

        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return _type;
        }
        private readonly string _group;
        public override string Group
        {
            get => _group;
            set { }
        }
    }
    
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
    
    public class StructuredBufferCreateInstance : AbstractStructuredBufferAttribute<GpuStruct>, IStructureBufferAttribute
    {

        public StructuredBufferCreateInstance(NodeContext nodeContext, string theName) : base(nodeContext, theName, theName, AttributeType.StructuredBuffer)
        {
            AddProperty("ComputeSystemAttribute", this);
            _group = theName;
        }

        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.CreateInstance;
        }

        private readonly string _group;
        public override string Group
        {
            get => _group;
            set { }
        }
    }
}