using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.compute
{
    
    public interface IIndexProvider
    {

        public ShaderNode<Int3> Index(NodeContext nodeContext);
    }
    
    public class DefaultIndexProvider : IIndexProvider{
        public ShaderNode<Int3> Index(NodeContext nodeContext)
        {
            return new ConstantValue<Int3>(nodeContext, new Int3(0, 0, 0));
        }
    }
    
    public class DispatchIdIndexProvider : IIndexProvider{
        public ShaderNode<Int3> Index(NodeContext nodeContext)
        {
            return new Semantic<Int3>(nodeContext, "DispatchThreadId");
        }
    }
    
    public class VertexIdIndexProvider : IIndexProvider{
        public ShaderNode<Int3> Index(NodeContext nodeContext)
        {
            var vertexId =  new Semantic<int>(NodeContext.Default, "VertexId");
            var join = new Int3Join(nodeContext);
            join.SetInputs(vertexId, new ConstantValue<int>(NodeContext.Default,0), new ConstantValue<int>(NodeContext.Default,0));
            return join;
        }
    }
    
    public class DynamicIndex : PassThroughNode<Int3>
    {

        private  IIndexProvider _indexProvider;
        public IIndexProvider IndexProvider
        {
            get => _indexProvider;
            set
            {
                _indexProvider = value;
                SetInput(_indexProvider.Index(NodeContext));
            }
        }

        public DynamicIndex(NodeContext nodeContext) : base(nodeContext, "Index", false)
        {
            IndexProvider = new DefaultIndexProvider();
            
            AddProperty("DynamicIndex", this);
        }
    }
}