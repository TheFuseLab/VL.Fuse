using System.Collections.Generic;
using Fuse.compute;

namespace Fuse
{
    public class AssignNode<T> : ShaderNode<GpuVoid>
    {
        private readonly ShaderNode<T> _target;
        private readonly ShaderNode<T> _source;
        
        public AssignNode(ShaderNode<T> theTarget, ShaderNode<T> theSource) : base("Assign")
        {
            _target = theTarget;
            _source = theSource;
            Setup(new List<AbstractShaderNode>{theTarget,theSource});
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
        
        public ShaderNode<T> Value()
        {
            return _target;
        }
    }
}