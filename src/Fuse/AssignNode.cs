using System.Collections.Generic;
using Fuse.compute;

namespace Fuse
{
    public class AssignNode<T> : ShaderNode<GpuVoid>
    {
        private GpuValue<T> _target;
        private GpuValue<T> _source;
        
        public AssignNode(GpuValue<T> theTarget, GpuValue<T> theSource) : base("Assign")
        {
            _target = theTarget;
            _source = theSource;
            Setup(new List<AbstractGpuValue>{theTarget,theSource});
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${target} = ${source};", new Dictionary<string, string>()
            {
                {"target", _target.ID}, 
                {"source", _source.ID}
            });
        }
        
        public GpuValue<T> Value()
        {
            var result = _target == null ? new GpuValue<T>(""):new GpuValuePassThrough<T>(_target);
            result.ParentNode = this;
            return result;
        }
    }
}