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
            var vertexId =  new Semantic<int>(NodeContext.CurrentRoot, "VertexId");
            var join = new Int3Join(nodeContext, vertexId, new ConstantValue<int>(0), new ConstantValue<int>(0));
            theReadIndex = theWriteIndex = join;
        }
    }
    
    public class DynamicIndex : PassThroughNode<Int3>
    {
        private IIndexProvider _indexProvider;
        private readonly bool _useReadIndex;

        public IIndexProvider IndexProvider
        {
            get => _indexProvider;

            set
            {
                _indexProvider = value;
                _indexProvider.Index(NodeContext, out var readIndex, out var writeIndex);

                Input = _useReadIndex ? readIndex : writeIndex;
                SetInputs(new List<AbstractShaderNode>{Input});
            }
        }

        public DynamicIndex(NodeContext nodeContext, IIndexProvider theIndexProvider, bool useReadIndex = false) : base(nodeContext, "Index")
        {
            IndexProvider = theIndexProvider ?? new DefaultIndexProvider();
            _useReadIndex = useReadIndex;
            AddProperty("DynamicIndex", this);
            IndexProvider.Index(NodeContext, out var readIndex, out var writeIndex);
            Input = useReadIndex ? readIndex : writeIndex;
            SetInputs(new List<AbstractShaderNode>{Input});
        }
    }
}