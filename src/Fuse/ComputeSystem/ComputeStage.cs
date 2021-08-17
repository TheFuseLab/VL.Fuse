using System;
using System.Collections.Generic;
using System.Linq;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;

namespace Fuse.ComputeSystem
{
    public interface IComputeStage : IGraphicsRendererBase, ITicketHolder
    {
        public void BindResources(Dictionary<string,IResource> theResources);

        public void SetResourceHandler(IResourceHandler theResourceHandler);

        public void UpdateGraph(bool theUpdateGraph);

        public string Name { get; }

        public Dictionary<string, List<AttributeReference>> Resources();

        public Entity Helper { get; }

        public bool HelperEnabled { get; }
    }

    public class ComputeStage :IComputeStage
    {
        private IResourceHandler _resourceHandler;

        public bool WriteAttributes { get; set; }

        private ResourceManager _resourceManager;

        public void Draw(RenderDrawContext context)
        {
            if (_resourceHandler == null) return;

            var theThreadGroupCount = new Int3();
            var theThreadGroupSize = new Int3();
            
            _resourceHandler.GetThreadGroupInfo(ref theThreadGroupCount, ref theThreadGroupSize);
        }

        public int Ticket => Tickets.Next;
        public void BindResources(Dictionary<string, IResource> theResources)
        {
            throw new NotImplementedException();
        }

        public void SetResourceHandler(IResourceHandler theResourceHandler)
        {
            throw new NotImplementedException();
        }

        public void UpdateGraph(bool theUpdateGraph)
        {
            throw new NotImplementedException();
        }

        public string Name { get; }
        public Dictionary<string, List<AttributeReference>> Resources()
        {
            throw new NotImplementedException();
        }

        public Entity Helper { get; }
        public bool HelperEnabled { get; }
    }

    public class ComputeGroup : IComputeStage
    {
        private List<IComputeStage> _group = new List<IComputeStage>();

        public List<IComputeStage> Children{
            get => _group;

            set
            {
                _group = new List<IComputeStage>(value.Where(computeStage => computeStage != null));
            }
        }

        public void Draw(RenderDrawContext context)
        {
            _group.ForEach(computeStage => computeStage.Draw(context));
        }

        public void BindResources(Dictionary<string, IResource> theResources)
        {
            _group.ForEach(computeStage => computeStage.BindResources(theResources));
        }

        public void SetResourceHandler(IResourceHandler theResourceHandler)
        {
            _group.ForEach(computeStage => computeStage.SetResourceHandler(theResourceHandler));
        }

        public void UpdateGraph(bool theUpdateGraph)
        {
            _group.ForEach(computeStage => computeStage.UpdateGraph(theUpdateGraph));
        }

        public string Name
        {
            get;
            set;
        }

        public Dictionary<string, List<AttributeReference>> Resources()
        {
            var result = new Dictionary<string, List<AttributeReference>>();
            _group.ForEach(computeStage =>
            {
                computeStage.Resources().ForEach(kv =>
                {
                    if (!result.ContainsKey(kv.Key))
                    {
                        result[kv.Key] = new List<AttributeReference>();
                    }
                    result[kv.Key].AddRange(kv.Value);
                });
            });
            return result;
        }
        
        public int Ticket => _group.Max(computeStage => computeStage.Ticket);

        public Entity Helper { get; set; }
        public bool HelperEnabled { get; set; }
    }
}