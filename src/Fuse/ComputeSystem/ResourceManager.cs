using System.Collections.Generic;
using Stride.Core.Yaml.Tokens;

namespace Fuse.ComputeSystem
{
    public class ResourceManager
    {
        public delegate IResourceBinder CreateBinder(IResource theResource);

        private readonly Dictionary<string, IResourceBinder> _resources = new Dictionary<string, IResourceBinder>();
        
        public ResourceManager(IComputeStage _computeStage, Dictionary<string, IResource> theResources, CreateBinder theBinder)
        {
            theResources.ForEach(kv => _resources[kv.Key] = theBinder.Invoke(kv.Value));
            
            _computeStage.Resources().ForEach(kv =>
            {
                if (!_resources.ContainsKey(kv.Key)) return;

                var binder = _resources[kv.Key];
                
                kv.Value.ForEach(attribute =>
                {
                    attribute.Reference.SetAbstractInput(binder.GetAttribute(attribute.Member.Name));
                });
            });
        }
    }
}