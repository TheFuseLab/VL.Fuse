using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse.compute
{
    
    public interface IIndexProvider
    {

        public void Index(NodeContext nodeContext, out ShaderNode<Int3> theReadIndex, out ShaderNode<Int3> theWriteIndex);
    }
    
    public class DefaultIndexProvider : IIndexProvider{
        public void Index(NodeContext nodeContext, out ShaderNode<Int3> theReadIndex, out ShaderNode<Int3> theWriteIndex)
        {
            theReadIndex = theWriteIndex = new ConstantValue<Int3>(new Int3(0, 0, 0));
        }
    }
    
    public class DispatchIdIndexProvider : IIndexProvider{
        public void Index(NodeContext nodeContext, out ShaderNode<Int3> theReadIndex, out ShaderNode<Int3> theWriteIndex)
        {
            theReadIndex = theWriteIndex = new Semantic<Int3>(nodeContext, "DispatchThreadId");
        }
    }
    
    public class VertexIdIndexProvider : IIndexProvider{
        public void Index(NodeContext nodeContext, out ShaderNode<Int3> theReadIndex, out ShaderNode<Int3> theWriteIndex)
        {
            var vertexId =  new Semantic<int>(NodeContext.Default, "VertexId");
            var join = new Int3Join(nodeContext);
            join.SetInputs(vertexId, new ConstantValue<int>(0), new ConstantValue<int>(0));
            theReadIndex = theWriteIndex = join;
        }
    }
    
    public class DynamicIndex : PassThroughNode<Int3>
    {

        private  IIndexProvider _indexProvider;
        private bool _useReadIndex;
        public IIndexProvider IndexProvider
        {
            get => _indexProvider;
            set
            {
                _indexProvider = value;
                _indexProvider.Index(NodeContext, out var readIndex, out var writeIndex);
                
                SetInput(_useReadIndex? readIndex:writeIndex);
            }
        }

        public DynamicIndex(NodeContext nodeContext, bool useReadIndex = false) : base(nodeContext, "Index", false)
        {
            IndexProvider = new DefaultIndexProvider();
            _useReadIndex = useReadIndex;
            AddProperty("DynamicIndex", this);
        }
    }
}