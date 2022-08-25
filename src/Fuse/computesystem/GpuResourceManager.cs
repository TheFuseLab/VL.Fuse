using System;
using System.Collections.Generic;
using Fuse.compute;

namespace Fuse.ComputeSystem
{
    public class GpuResourceManager
    {
        public delegate AbstractResourceBinder CreateResourceBinding(AbstractResource theResource);

        private readonly Dictionary<string, AbstractResourceBinder> _resourceBinders;
        private readonly Dictionary<string, AbstractResource> _resources;
        
        //private 

        public GpuResourceManager(
            IComputeStage theComputeStage,
            Dictionary<string, AbstractResource> theResources,
            CreateResourceBinding theBindingCreator
            )
        {
            _resources = theResources;
            _resourceBinders = new Dictionary<string, AbstractResourceBinder>();
            foreach (var kv in theResources)
            {
                _resourceBinders[kv.Key] = theBindingCreator(kv.Value);
            }

            foreach (var attributeGroup in theComputeStage.AttributeGroups)
            {
                if (!_resourceBinders.ContainsKey(attributeGroup.Key)) return;
                foreach (var attribute in attributeGroup.Value)
                {
                    BindResource(attribute, _resourceBinders[attributeGroup.Key]);
                }
            }
        }

        private static void BindResource(IAttribute theAttribute, AbstractResourceBinder theAbstractResourceBinder)
        {
            theAttribute.SetInput(theAbstractResourceBinder.GetAttribute(theAttribute.Name));
        }

        public List<ShaderNode<GpuVoid>> WriteAttributes()
        {
            var result = new List<ShaderNode<GpuVoid>>{new Comment<GpuVoid>(new EmptyVoid(), "WriteBuffer")};

            foreach (var resource in _resourceBinders)
            {
                result.Add(resource.Value.WriteAttributes());
            }
            

            return result;
        }

        public void Update()
        {
            foreach (var kv in _resourceBinders)
            {
                kv.Value.UpdateResource(_resources[kv.Key]);
            }
        }
        
        public List<AbstractShaderNode> WriteToIndexNodes(ShaderNode<int> theIndex)
        {
            var result = new List<AbstractShaderNode>{new Comment<GpuVoid>(new EmptyVoid(), "WriteBuffer")};

            _resourceBinders.ForEach(resource =>
            {
                result.Add(resource.Value.WriteAttributes());
            });

            return result;
        }
    }
}