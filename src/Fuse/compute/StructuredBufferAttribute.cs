using System.Collections.Generic;
using Fuse.ComputeSystem;
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
        public string Group { get; }

        public StructuredBufferAttributeType StructuredBufferAttributeType();
    }

    public class StructuredBufferAttribute<T> : Attribute<T>, IStructureBufferAttribute
    {
       // public ShaderNode<T> Default;
        public StructuredBufferAttribute(NodeContext nodeContext, string theGroup, string theName, bool theDefineSemantic = true, ShaderNode<T> theDefault = null) : base(nodeContext, theName, AttributeType.StructuredBuffer)
        {
            var myFactory = new NodeSubContextFactory(nodeContext);
            Group = theGroup;

            Default = theDefault ?? ConstantHelper.FromFloat<T>(myFactory.NextSubContext(), 0f);
            
            SetInput(new Semantic<T>(myFactory.NextSubContext(),theName, theDefineSemantic, theName.ToUpper()));
            SetProperty("ComputeSystemAttribute", this);
        }
        
        public string Group { get; }
        public StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Attribute;
        }

        public void OverRideByInstance(ShaderNode<GpuStruct> theInstance)
        {
            if (theInstance == null)
            {
                SetProperty("ComputeSystemAttribute", this);
            }else{
                var myFactory = new NodeSubContextFactory(NodeContext,1);
                var getMember = new GetMember<GpuStruct, T>(myFactory.NextSubContext());
                getMember.SetInput(Name, theInstance);
                SetInput(getMember);
                RemoveProperty("ComputeSystemAttribute");
            }
            CallChangeEvent();
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
    
    public class StructuredBufferResource : Attribute<Buffer>, IStructureBufferAttribute, IBufferInput<GpuStruct>
    {



        public StructuredBufferResource(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.StructuredBuffer)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public string Group => Name;

        public StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Resource;
        }

        public bool Append {
            set { }
        }
    }
    
    public class StructuredBufferInstance : Attribute<GpuStruct>, IStructureBufferAttribute
    {



        public StructuredBufferInstance(NodeContext nodeContext, string theName) : base(nodeContext, theName, AttributeType.StructuredBuffer)
        {
            AddProperty("ComputeSystemAttribute", this);
        }

        public string Group => Name;

        public StructuredBufferAttributeType StructuredBufferAttributeType()
        {
            return compute.StructuredBufferAttributeType.Instance;
        }
    }
}