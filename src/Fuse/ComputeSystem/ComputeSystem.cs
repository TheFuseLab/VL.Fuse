using System;
using System.Collections.Generic;
using Stride.Rendering;

namespace Fuse.ComputeSystem
{
    public class ComputeSystem : IGraphicsRendererBase
    {
        private Dictionary<string, IResource> _resources = new Dictionary<string, IResource>();

        private IResourceHandler _resourceHandler;

        private ComputeGroup _group = new ComputeGroup();

        private int _highestTicket = 0;

        public string Name { get; set; }

        private void AddAttribute(string theGroup, IMember theMember)
        {
            if (!_resources.ContainsKey(theGroup))
            {
                _resources[theGroup] = _resourceHandler.CreateResource(theGroup);
            }
            _resources[theGroup].AddAttribute(theMember);
        }

        private void AddAttributesFromStage()
        {
            _group.SetResourceHandler(_resourceHandler);
            _group.Resources().ForEach(kv =>
            {
                kv.Value.ForEach(attribute =>
                {
                    AddAttribute(kv.Key, attribute.Member);
                });
            });
        }
        
        private void RebuildSystem()
        {
            _resources.Clear();
            AddAttributesFromStage();
            _resources.ForEach(kv => kv.Value.CreateResources());
            _group.BindResources(_resources);
            _group.UpdateGraph(true);
        }

        public void Update(IResourceHandler theResourceHandler, List<IComputeStage> theStages)
        {
            _resourceHandler = theResourceHandler;
            _group.Children = theStages;

            var currentTicket = Math.Max(_resourceHandler.Ticket, _group.Ticket);
            
            if (currentTicket <= _highestTicket) return;

            _highestTicket = currentTicket;

            RebuildSystem();
        }


        public void Draw(RenderDrawContext context)
        {
            _group.Draw(context);
        }
    }
}