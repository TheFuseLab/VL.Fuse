using System.Collections.Generic;

namespace Fuse
{
    public class AssignNode<T> : ShaderNode<T>
    {
        private GpuValue<T> _target;
        private GpuValue<T> _source;
        
        public AssignNode(GpuValue<T> theTarget, GpuValue<T> theSource) : base("Assign")
        {
            _target = theTarget;
            _source = theSource;
            Setup(new List<AbstractGpuValue>{theTarget,theSource});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${target} = ${source};", new Dictionary<string, string>()
            {
                {"target", _target.ID}, 
                {"source", _source.ID}
            });
        }
    }
}