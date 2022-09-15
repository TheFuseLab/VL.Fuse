using System;
using System.Collections.Generic;
using VL.Core;

namespace Fuse.ComputeSystem
{
    
    public class ComputeSystemChangeListener : IChangeGraph
    {

        private readonly ComputeSystem _system;

        public ComputeSystemChangeListener(ComputeSystem theSystem)
        {
            _system = theSystem;
        }
        public void ChangeGraph(AbstractShaderNode theNode)
        {
            _system.Rebuild();
        }
    }
    
    public class ComputeSystem : ComputeStageGroup
    {

        private readonly HashSet<string> _groups;

        public ComputeSystem(NodeContext nodeContext) : base(nodeContext)
        {
            Resources = new Dictionary<string, AbstractResource>();
            _groups = new HashSet<string>();

            Enabled = true;
            Name = "MySystem";
            
            ChangeGraphListener.Add(new ComputeSystemChangeListener(this));
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
        
        public void Rebuild()
        {
            Resources.ForEach(kv => kv.Value.Reset());
            _groups.Clear();
            AddAttributeGroups(AttributeGroups);
            CreateResources();
            BindResources(Resources);
            Build();
        }

        public Dictionary<string, AbstractResource> Resources { get; }

        public AbstractResource GetResource(string key)
        {
            return Resources[key];
        }

        public bool Enabled { get; set; }

        public void SetInput(IResourceHandler theResourceHandler, List<AbstractComputeStage> theComputeStages)
        {
            ComputeStages = theComputeStages;
            ResourceHandler = theResourceHandler;
            SetInputs(theComputeStages);
        }
    }
}