using Stride.Core.Mathematics;
using Fuse.compute;
using Stride.Rendering.Shaders;
using Stride.Rendering.Shaders.ShaderNode;
using Stride.Rendering.Shaders.ShaderTypes;

namespace Fuse.ComputeSystem
{
    /// <summary>
    /// Interface for dispatch information in the compute system.
    /// </summary>
    public interface IDispatchInfo
    {
        // Empty interface for now, to be implemented as needed
    }

    /// <summary>
    /// Interface for compute resources in the Fuse system.
    /// </summary>
    public interface IComputeResource
    {
        /// <summary>
        /// Gets the name of the resource.
        /// </summary>
        string GetName();
        
        /// <summary>
        /// Gets the size of the resource.
        /// </summary>
        Int3 GetSize();
        
        /// <summary>
        /// Gets the type of attribute this compute resource represents.
        /// </summary>
        AttributeType GetAttributeType();
        
        /// <summary>
        /// Gets the dispatch information for this resource.
        /// </summary>
        IDispatchInfo GetDispatchInfo();
        
        /// <summary>
        /// Handles an attribute.
        /// </summary>
        /// <param name="attribute">The attribute to handle.</param>
        void HandleAttribute(IAttribute attribute);
        
        /// <summary>
        /// Gets the attribute map for this resource.
        /// </summary>
        AttributeMap GetAttributeMap();
        
        /// <summary>
        /// Binds the compute stage with the given attribute map.
        /// </summary>
        /// <param name="attributeMap">The attribute map to bind.</param>
        void BindComputeStage(AttributeMap attributeMap);
        
        /// <summary>
        /// Lists the read shader graph.
        /// </summary>
        /// <returns>The read shader graph.</returns>
        ShaderNode<GpuVoid> ListIrenderer();
        
        /// <summary>
        /// Gets the write shader graph.
        /// </summary>
        /// <returns>The write shader graph.</returns>
        ShaderNode<GpuVoid> Write();
        
        /// <summary>
        /// Prepares the resource for processing.
        /// </summary>
        void Prepare();
        
        /// <summary>
        /// Finishes the resource processing.
        /// </summary>
        void Finish();
    }
}
