using System;
using System.Collections.Generic;
using Fuse.compute;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;


namespace Fuse.ComputeSystem
{

    public interface IComputeStage
    {
        public void BindResources(Dictionary<string, AbstractResource> theResources);
        
        public IResourceHandler ResourceHandler { get; set; }
        
        public Dictionary<string, List<IAttribute>> AttributeGroups { get;  }

        public void Build();

    }
    
    public abstract class AbstractComputeStage : Group, IComputeStage
    {
        
        protected AbstractComputeStage(NodeContext nodeContext) : base(nodeContext, "ComputeStage")
        {
        }
        
        public abstract void SetComputeInput(ShaderNode<GpuVoid> theNode);
        
        public abstract void BindResources(Dictionary<string, AbstractResource> theResources);

        public bool Enabled { get; set; }
        public virtual IResourceHandler ResourceHandler { get; set; }
        public abstract Dictionary<string, List<IAttribute>> AttributeGroups { get;  }

        public abstract void Build();
    }

    public class ComputeStage : AbstractComputeStage
    {
        private GpuResourceManager _resourceManager;

        private ShaderNode<GpuVoid> _node;

        private int _subContextId;

        private bool _writeAttributes = false;
        public bool WriteAttributes { 
            get => _writeAttributes;
            
            set
            {
                if (value == _writeAttributes) return;
                _writeAttributes = value;
                CallChangeEvent();
            }
        } 

        public ComputeStage(NodeContext nodeContext) : base(nodeContext)
        {
            _subContextId = 1;
        }

        public override Dictionary<string, List<IAttribute>> AttributeGroups
        {
            get
            {
                var result = new Dictionary<string, List<IAttribute>>();

                foreach (var attributeGroup in _node.PropertiesForTree<IAttribute>())
                {
                    if (!result.ContainsKey(attributeGroup.Key))
                    {
                        result[attributeGroup.Key] = new List<IAttribute>();
                    }

                    var referenceList = result[attributeGroup.Key];
                    foreach (var attribute in attributeGroup.Value)
                    {
                        if (!referenceList.Contains(attribute))
                        {
                            referenceList.Add(attribute);
                        }
                    }
                }

                return result;
            }
        }
        
        public override void SetComputeInput(ShaderNode<GpuVoid> theNode)
        {
            if (theNode != null && theNode.Equals(_node)) return;

            _node = theNode;
            
            CallChangeEvent();
        }

        private AbstractResourceBinder CreateBinder(AbstractResource theResource)
        {
            return ResourceHandler.CreateBinder(theResource, new Semantic<Int3>(ShaderNodesUtil.GetContext(NodeContext, _subContextId++),"DispatchThreadId"));
        }

        public override void BindResources(Dictionary<string, AbstractResource> theResources)
        {
            _resourceManager = new GpuResourceManager(NodeContext, this, theResources, CreateBinder);
        }

        public GpuResourceManager ResourceManager()
        {
            return _resourceManager;
        }

        public override void Build()
        {
            var nodes = new List<ShaderNode<GpuVoid>> {_node};
            if (WriteAttributes)
            {
                nodes.AddRange(_resourceManager.WriteAttributes());
            }
            
            SetInput(nodes, false);
        }

        public void Update()
        {
            _resourceManager.Update();
        }

    }

    public class ComputeStageGroup : Group, IComputeStage
    {
        private readonly List<AbstractComputeStage> _computeStages;

        public ComputeStageGroup(NodeContext nodeContext) : base(nodeContext)
        {
            _computeStages = new List<AbstractComputeStage>();
        }
        
        public List<AbstractComputeStage> ComputeStages
        {
            get => _computeStages;
            set
            {
                _computeStages.Clear();

                foreach (var v in value)
                {
                    if (v == null) return;
                    
                    _computeStages.Add(v);
                }
            }
        }

        public void BindResources(Dictionary<string, AbstractResource> theResources)
        {
            foreach (var stage in _computeStages)
            {
                stage.BindResources(theResources);
            }
        }

        protected IResourceHandler _resourceHandler;
        
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