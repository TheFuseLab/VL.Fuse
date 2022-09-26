using System;
using System.Collections.Generic;
using Fuse.compute;
using VL.Core;

namespace Fuse.ComputeSystem
{
    public class GpuResourceManager
    {
        public delegate AbstractResourceBinder CreateResourceBinding(AbstractResource theResource);

        private readonly Dictionary<string, AbstractResourceBinder> _resourceBinders;
        private readonly Dictionary<string, AbstractResource> _resources;
        
        private int _subContextId;

        private readonly NodeContext _context;

        public GpuResourceManager(
            NodeContext nodeContext,
            IComputeStage theComputeStage,
            Dictionary<string, AbstractResource> theResources,
            CreateResourceBinding theBindingCreator
            )
        {
            _context = nodeContext;
            _subContextId = 1;
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

        public Dictionary<string, AbstractResourceBinder> ResourceBinder()
        {
            return _resourceBinders;
        }

        private static void BindResource(IAttribute theAttribute, AbstractResourceBinder theAbstractResourceBinder)
        {
            theAttribute.SetInput(theAbstractResourceBinder.GetAttribute(theAttribute.Name));
        }

        public IEnumerable<ShaderNode<GpuVoid>> WriteAttributes()
        {
            var comment = new Comment<GpuVoid>(ShaderNodesUtil.GetContext(_context, _subContextId++));
            comment.SetInput("WriteBuffer", new EmptyVoid(ShaderNodesUtil.GetContext(_context, _subContextId++)));
            var result = new List<ShaderNode<GpuVoid>>{comment};

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
            var comment = new Comment<GpuVoid>(ShaderNodesUtil.GetContext(_context, _subContextId++));
            comment.SetInput("WriteBuffer", new EmptyVoid(ShaderNodesUtil.GetContext(_context, _subContextId++)));
            var result = new List<AbstractShaderNode>{comment};

            _resourceBinders.ForEach(resource =>
            {
                result.Add(resource.Value.WriteAttributes());
            });

            return result;
        }
    }
}