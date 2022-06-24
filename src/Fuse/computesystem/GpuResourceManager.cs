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
            AbstractComputeStage theComputeStage,
            Dictionary<string, AbstractResource> theResources,
            CreateResourceBinding theBindingCreator
            )
        {
            _resources = new Dictionary<string, AbstractResourceBinder>();
            foreach (var kv in theResources)
            {
                _resources[kv.Key] = theBindingCreator(kv.Value);
            }

            foreach (var attributesKv in theComputeStage.Attributes)
            {
                if (!_resources.ContainsKey(attributesKv.Key)) return;
                foreach (var kv in attributesKv.Value)
                {
                    BindResource(kv, _resources[attributesKv.Key]);
                }
            }
        }

        private static void BindResource(AbstractAttribute theReference, AbstractResourceBinder theAbstractResourceBinder)
        {
            theReference.SetInput(theAbstractResourceBinder.GetAttribute(theReference.Member.Name));
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