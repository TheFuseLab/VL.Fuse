using System;
using System.Collections.Generic;

namespace Fuse.ComputeSystem
{
    public class ComputeSystem
    {
        private Dictionary<string, AbstractResource> _resources;

        private int _highestTicket;

        private HashSet<string> _groups;

        private ComputeStageGroup _stageGroup;

        public ComputeSystem()
        {
            _resources = new Dictionary<string, AbstractResource>();
            _highestTicket = -1;
            _groups = new HashSet<string>();

            _stageGroup = new ComputeStageGroup();

            Enabled = true;
            Name = "MySystem";
        }

        private bool NeedsRebuild()
        {
            var ticket =  Math.Max(ResourceHandler.Ticket, _stageGroup.Ticket);
            if (ticket <= _highestTicket) return false;
            _highestTicket = ticket;
            return true;
        }

        private void AddAttribute(string theGroup, IMember theMember)
        {
            _groups.Add(theGroup);

            if (!_resources.ContainsKey(theGroup))
            {
                _resources.Add(theGroup, ResourceHandler.CreateResource(theGroup));
            }
            
            _resources[theGroup].AddAttribute(theMember);
        }

        private void AddAttributes(Dictionary<string, List<AbstractAttributeReference>> theAttributes)
        {
            theAttributes.ForEach(kv =>
            {
                kv.Value.ForEach(attribute =>
                {
                    AddAttribute(kv.Key, attribute.Member);
                });
            });
        }

        private void CreateResources()
        {
            var groupsToRemove = new HashSet<string>();
            _resources.ForEach(kv =>
            {
                if (!_groups.Contains(kv.Key))
                {
                    groupsToRemove.Add(kv.Key);
                }
            });
            groupsToRemove.ForEach(group => _resources.Remove(group));
                
            _resources.ForEach(kv =>
            {
                kv.Value.CreateResources();
            });
        }
        
        

        private void Rebuild()
        {
            _resources.ForEach(kv => kv.Value.Reset());
            _groups.Clear();
            _stageGroup.ResourceHandler = ResourceHandler;
            AddAttributes(_stageGroup.Attributes);
            CreateResources();
            _stageGroup.BindResources(_resources);
            _stageGroup.Build();
        }

        public IResourceHandler ResourceHandler { get; set; }

        public void Update(
            IResourceHandler theResourceHandler,
            List<IComputeStage> theStages
            )
        {
            ResourceHandler = theResourceHandler;
            _stageGroup.ComputeStages = theStages;

            if (!NeedsRebuild()) return;
            
            Rebuild();
        }

        public bool Enabled { get; set; }

        public string Name { get; set; }
    }
}