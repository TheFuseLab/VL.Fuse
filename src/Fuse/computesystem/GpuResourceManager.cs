using System;
using System.Collections.Generic;
using Fuse.compute;

namespace Fuse.ComputeSystem
{
    public class GpuResourceManager
    {
        public delegate AbstractResourceBinder CreateResourceBinding(AbstractResource theResource);

        private readonly Dictionary<string, AbstractResourceBinder> _resources;

        public GpuResourceManager(
            IComputeStage theComputeStage,
            Dictionary<string, AbstractResource> theResources,
            CreateResourceBinding theBindingCreator
            )
        {
            _resources = new Dictionary<string, AbstractResourceBinder>();
            foreach (var kv in theResources)
            {
                _resources[kv.Key] = theBindingCreator(kv.Value);
            }

            foreach (var attributeGroup in theComputeStage.AttributeGroups)
            {
                if (!_resources.ContainsKey(attributeGroup.Key)) return;
                foreach (var attribute in attributeGroup.Value)
                {
                    BindResource(attribute, _resources[attributeGroup.Key]);
                }
            }
        }

        private static void BindResource(IAttribute theAttribute, AbstractResourceBinder theAbstractResourceBinder)
        {
            theAttribute.SetInput(theAbstractResourceBinder.GetAttribute(theAttribute.Name));
        }

        public List<AbstractShaderNode> WriteAttributeNodes()
        {
            var result = new List<AbstractShaderNode>{new Comment<GpuVoid>(new EmptyVoid(), "WriteBuffer")};

            foreach (var resource in _resources)
            {
                result.Add(resource.Value.WriteAttributes());
            }
            

            return result;
        }
        
        public List<AbstractShaderNode> WriteToIndexNodes(ShaderNode<int> theIndex)
        {
            var result = new List<AbstractShaderNode>{new Comment<GpuVoid>(new EmptyVoid(), "WriteBuffer")};

            _resources.ForEach(resource =>
            {
                result.Add(resource.Value.WriteAttributes());
            });

            return result;
        }
    }
}