using System;
using System.Collections.Generic;

namespace Fuse.ComputeSystem
{
    public class ComputeSystem
    {

        private readonly HashSet<string> _groups;

        private readonly ComputeStageGroup _stageGroup;

        private readonly IResourceHandler _resourceHandler;

        public ComputeSystem(IResourceHandler theResourceHandler)
        {
            _resourceHandler = theResourceHandler;
            Resources = new Dictionary<string, AbstractResource>();
            _groups = new HashSet<string>();

            _stageGroup = new ComputeStageGroup();

            Enabled = true;
            Name = "MySystem";
        }

        private bool NeedsRebuild()
        {
            if (_resourceHandler == null) return false;
            var ticket =  Math.Max(_resourceHandler.Ticket, _stageGroup.Ticket);
            return true;
        }

        private void AddAttribute(string theGroup, IAttribute theMember)
        {
            _groups.Add(theGroup);

            if (!Resources.ContainsKey(theGroup))
            {
                Resources.Add(theGroup, _resourceHandler.CreateResource(theGroup));
            }
            
            Resources[theGroup].AddAttribute(theMember);
        }

        private void AddAttributeGroups(Dictionary<string, List<IAttribute>> theAttributeGroups)
        {
            foreach (var attributeGroup in theAttributeGroups)
            {
                foreach (var attribute in attributeGroup.Value)
                {
                    AddAttribute(attributeGroup.Key, attribute);
                }
            }
        }

        private void CreateResources()
        {
            var groupsToRemove = new HashSet<string>();
            Resources.ForEach(kv =>
            {
                if (!_groups.Contains(kv.Key))
                {
                    groupsToRemove.Add(kv.Key);
                }
            });
            groupsToRemove.ForEach(group => Resources.Remove(group));
                
            Resources.ForEach(kv =>
            {
                kv.Value.CreateResources();
            });
        }
        
        

        private void Rebuild()
        {
            Resources.ForEach(kv => kv.Value.Reset());
            _groups.Clear();
            _stageGroup.ResourceHandler = _resourceHandler;
            AddAttributeGroups(_stageGroup.AttributeGroups);
            CreateResources();
            _stageGroup.BindResources(Resources);
            _stageGroup.Build();
        }

        public Dictionary<string, AbstractResource> Resources { get; }

        public AbstractResource GetResource(string key)
        {
            return Resources[key];
        }

        public List<IComputeStage> ComputeStages
        {
            set
            {
                _stageGroup.ComputeStages = value;

                if (!NeedsRebuild()) return;
            
                Rebuild();
            }
        }

        public bool Enabled { get; set; }

        public string Name { get; set; }
    }
}