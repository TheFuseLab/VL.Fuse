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
        Instance
    }

    public interface IStructureBufferAttribute : IAttribute
    {
        public Buffer Buffer { get; set; }

        public StructuredBufferAttributeType StructuredBufferAttributeType();
    }

    public abstract class AbstractStructuredBufferAttribute<T> : Attribute<T>, IStructureBufferAttribute
    {
        protected AbstractStructuredBufferAttribute(NodeContext nodeContext, string theGroup, string theName, AttributeType theType) : base(nodeContext, theName, theType)
        {
            Group = theGroup;
        }

        public Buffer Buffer { get; set; }
        public abstract StructuredBufferAttributeType StructuredBufferAttributeType();
        
        public override Int3 Resolution => new(Buffer.ElementCount, 1, 1);
    }

    public class StructuredBufferAttribute<T> : AbstractStructuredBufferAttribute<T>
    {

        // public ShaderNode<T> Default;
        public StructuredBufferAttribute(NodeContext nodeContext, string theGroup, string theName, bool theDefineSemantic = true, ShaderNode<T> theDefault = null) : base(nodeContext, theGroup, theName, AttributeType.StructuredBuffer)
        {
            var myFactory = new NodeSubContextFactory(nodeContext);

            Default = theDefault ?? ConstantHelper.FromFloat<T>( 0f);
            
            SetInput(new Semantic<T>(myFactory.NextSubContext(),theName, theDefineSemantic, theName.ToUpper()));
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Attribute;
        }

        public void OverRideByInstance(ShaderNode<GpuStruct> theInstance)
        {
            if (theInstance == null)
            {
                SetProperty("ComputeSystemAttribute", this);
                IsOverridden = false;
            }else{
                var myFactory = new NodeSubContextFactory(NodeContext,1);
                var getMember = new GetMember<GpuStruct, T>(myFactory.NextSubContext(), theInstance,Name);
                SetInput(getMember);
                IsOverridden = true;
            }
        }

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
    
    public class StructuredBufferResource : AbstractStructuredBufferAttribute<Buffer>, IBufferInput<GpuStruct>
    {
        public StructuredBufferResource(NodeContext nodeContext, string theName) : base(nodeContext, theName, theName, AttributeType.StructuredBuffer)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Resource;
        }

        public bool Append {
            set { }
        }
    }
    
    public class StructuredBufferInstance : AbstractStructuredBufferAttribute<GpuStruct>, IStructureBufferAttribute
    {

        public StructuredBufferInstance(NodeContext nodeContext, string theName) : base(nodeContext, theName, theName, AttributeType.StructuredBuffer)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public override StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Instance;
        }
    }
}