using Stride.Core.Mathematics;
using Fuse.compute;
using System.Collections.Generic;
using VL.Core;

namespace Fuse.ComputeSystem
{
    /// <summary>
    /// Implementation of a structured buffer resource for the compute system.
    /// </summary>
    public class StructuredBufferResource : IComputeResource
    {
        private readonly string _name;
        private readonly Int3 _size;
        private readonly AttributeMap _attributeMap = new AttributeMap();
        private DynamicStruct<GpuStruct> _struct;
        private NodeContext _nodeContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="StructuredBufferResource"/> class.
        /// </summary>
        /// <param name="name">The name of the resource.</param>
        /// <param name="size">The size of the resource.</param>
        public StructuredBufferResource(string name, Int3 size, NodeContext nodeContext = null)
        {
            _name = name;
            _size = size;
            _nodeContext = nodeContext;
        }

        /// <summary>
        /// Gets the name of the resource.
        /// </summary>
        public string GetName()
        {
            return _name;
        }

        /// <summary>
        /// Gets the size of the resource.
        /// </summary>
        public Int3 GetSize()
        {
            return _size;
        }

        /// <summary>
        /// Gets the type of attribute this compute resource represents.
        /// Always returns AttributeType.StructuredBuffer for this implementation.
        /// </summary>
        public AttributeType GetAttributeType()
        {
            return AttributeType.StructuredBuffer;
        }

        /// <summary>
        /// Gets the dispatch information for this resource.
        /// </summary>
        public IDispatchInfo GetDispatchInfo()
        {
            // Empty implementation
            return null;
        }

        /// <summary>
        /// Handles an attribute.
        /// </summary>
        /// <param name="attribute">The attribute to handle.</param>
        public void HandleAttribute(IAttribute attribute)
        {
            // Empty implementation
            _attributeMap.HandleAttribute(attribute);
        }

        /// <summary>
        /// Gets the attribute map for this resource.
        /// </summary>
        public AttributeMap GetAttributeMap()
        {
            return _attributeMap;
        }

        /// <summary>
        /// Binds the compute stage with the given attribute map.
        /// </summary>
        /// <param name="attributeMap">The attribute map to bind.</param>
        public void BindComputeStage(AttributeMap attributeMap, List<IRenderer> postGraphRenderer, out ShaderGraph<GpuVoid> read, out ShaderGraph<GpuVoid> write)
        {
            // Empty implementation
        }

        /// <summary>
        /// Prepares the resource for processing.
        /// </summary>
        public void Prepare()
        {
            // Empty implementation
            _attributeMap.Prepare();
        }

        /// <summary>
        /// Finishes the resource processing.
        /// </summary>
        public void Finish()
        {
            // Empty implementation
            bool changed = _attributeMap.Finish();
            
            if (changed)
            {
                UpdateStruct();
            }
        }
        
        /// <summary>
        /// Updates the internal DynamicStruct based on the AttributeMap's attribute set.
        /// </summary>
        private void UpdateStruct()
        {
            // Get all attributes from the attribute map
            var attributeNames = _attributeMap.GetAttributeNames();
            var attributeNodes = new Dictionary<string, AbstractShaderNode>();
            
            // Collect all attribute instances
            foreach (var attributeName in attributeNames)
            {
                var instances = _attributeMap.GetAttributeInstances(attributeName);
                if (instances.Count > 0)
                {
                    // Use the first instance of each attribute
                    var attribute = instances[0];
                    if (attribute.ShaderNode != null)
                    {
                        attributeNodes[attributeName] = attribute.ShaderNode;
                    }
                }
            }
            
            // Create a new DynamicStruct with the collected attributes
            if (attributeNodes.Count > 0)
            {
                _struct = new DynamicStruct<GpuStruct>(_nodeContext, attributeNodes, null);
            }
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is IComputeResource other)
            {
                return GetAttributeType() == other.GetAttributeType() && 
                       GetName() == other.GetName();
            }
            return false;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 23 + GetAttributeType().GetHashCode();
            hash = hash * 23 + (GetName()?.GetHashCode() ?? 0);
            return hash;
        }
    }
}
