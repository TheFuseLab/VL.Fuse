using System.Collections.Generic;

namespace Fuse
{
    public class AssignNode<T> : ShaderNode<T>
    {
        public AssignNode(GpuValue<T> theTarget, GpuValue<T> theSource) : base("Assign")
        {
            
            Setup("${target} = ${source};", 
                new List<AbstractGpuValue>{theTarget,theSource},
                new Dictionary<string, string>()
                {
                    {"target", theTarget.ID}, 
                    {"source", theSource.ID}
                }
            );
        }
    }
}