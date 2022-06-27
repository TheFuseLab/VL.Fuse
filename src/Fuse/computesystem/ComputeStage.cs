using System;
using System.Collections.Generic;
using System.Linq;
using Fuse.compute;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders;


namespace Fuse.ComputeSystem
{

    public interface IComputeStage : IRenderer
    {
        public void BindResources(Dictionary<string, AbstractResource> theResources);
        
        public ComputeSystem ComputeSystem { get; set; }
        
        public IResourceHandler ResourceHandler { get; set; }
        
        public int Ticket { get; }
        
        public Dictionary<string, List<IAttribute>> AttributeGroups { get;  }

        public void Build();

    }
    
    public abstract class AbstractComputeStage : IComputeStage
    {

        protected ShaderNode<GpuVoid> _node;
        
        public AbstractComputeStage(ShaderNode<GpuVoid> theNode)
        {
            _node = theNode;
        }
        
        public abstract void BindResources(Dictionary<string, AbstractResource> theResources);

        public bool Enabled { get; set; }
        public IResourceHandler ResourceHandler { get; set; }

        public ComputeSystem ComputeSystem { get; set; }

        public string Name { get; set; }

        public Dictionary<string, List<IAttribute>> AttributeGroups { get; protected set; }
        public abstract ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys);

        public int Ticket { get; }
        public abstract void Draw(RenderDrawContext theContext);
        
        public abstract void Build();
    }

    public class ComputeStage : AbstractComputeStage
    {
        private GpuResourceManager _resourceManager;

        public bool WriteAttributes { get; set; } = false;

        private Group _group;

        public ComputeStage(ShaderNode<GpuVoid> theNode) : base(theNode)
        {
            AttributeGroups = new Dictionary<string, List<IAttribute>>();
            
            if (theNode == null)
            {
                return;
            }
            
            foreach (var attributeGroup in  theNode.ResourcesForTree<IAttribute>())
            {
                if (!AttributeGroups.ContainsKey(attributeGroup.Key))
                {
                    AttributeGroups[attributeGroup.Key] = new List<IAttribute>();
                }

                var referenceList = AttributeGroups[attributeGroup.Key];
                foreach (var attribute in attributeGroup.Value)
                {
                    if (!referenceList.Contains(attribute))
                    {
                        referenceList.Add(attribute);
                    }
                }
            }
        }

        private AbstractResourceBinder CreateBinder(AbstractResource theResource)
        {
            return ResourceHandler.CreateBinder(theResource, new Semantic<Int3>("DispatchThreadID"));
        }

        public override void BindResources(Dictionary<string, AbstractResource> theResources)
        {
            _resourceManager = new GpuResourceManager(this,theResources, CreateBinder);
        }

        //TODO check draw
        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            throw new NotImplementedException();
        }

        public override void Draw(RenderDrawContext theContext)
        {
            if (!Enabled) return;
            if (ResourceHandler == null) return;
            
            ResourceHandler.GetThreadGroupInfo(out var threadGroupCount, out var threadGroupSize);
        }

        public override void Build()
        {
            var nodes = new List<AbstractShaderNode> {_node};
            if (WriteAttributes)
            {
                nodes.AddRange(_resourceManager.WriteAttributeNodes());
            }
            _group = new Group(nodes); 
        }

        public ShaderNode<GpuVoid> Node => _group;
    }

    public class ComputeStageGroup : IComputeStage
    {
        private readonly List<IComputeStage> _computeStages;

        public ComputeStageGroup()
        {
            _computeStages = new List<IComputeStage>();
        }
        
        public List<IComputeStage> ComputeStages
        {
            get => _computeStages;
            set
            {
                _computeStages.Clear();
                value.ForEach(v =>
                {
                    if (v == null) return;
                    
                    v.ComputeSystem = ComputeSystem;
                    _computeStages.Add(v);
                });
            }
        }

        public void Draw(RenderDrawContext theContext)
        {
            _computeStages.ForEach(stage => stage.Draw(theContext));
        }

        public void BindResources(Dictionary<string, AbstractResource> theResources)
        {
            foreach (var stage in _computeStages)
            {
                stage.BindResources(theResources);
            }
        }

        public ComputeSystem ComputeSystem { get; set; }

        private IResourceHandler _resourceHandler;
        
        public IResourceHandler ResourceHandler { get => _resourceHandler;
            set
            {
                _resourceHandler = value;
                foreach (var stage in _computeStages)
                {
                    stage.ResourceHandler = value;
                }
            }
        }
        public int Ticket {
            get
            {
                var ticket = 0;
                _computeStages.ForEach(stage => ticket = Math.Max(ticket, stage.Ticket));
                return ticket;
            }
        }
        public Dictionary<string, List<IAttribute>> AttributeGroups {
            get
            {
                var result = new Dictionary<string, List<IAttribute>>();

                foreach (var stage in _computeStages)
                {
                    foreach (var attributeGroup in  stage.AttributeGroups)
                    {
                        if (!result.ContainsKey(attributeGroup.Key))
                        {
                            result[attributeGroup.Key] = new List<IAttribute>();
                        }
                        result[attributeGroup.Key].AddRange(attributeGroup.Value);
                    }
                }
                
                return result;
            }
        }

        public void Build()
        {
            foreach (var stage in _computeStages)
            {
                stage.Build();
            }
        }
    }
}