using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class AssignNode<T> : ShaderNode<T>
    {
        public AssignNode(GpuValue<T> theTarget, GpuValue<T> theSource) : base("Assign")
        {
            const string shaderCode = "${target} = ${source};";
            var inputs = new OrderedDictionary<string, AbstractGpuValue>()
            {
                {"target", theTarget}, 
                {"source", theSource}
            };
            Setup(shaderCode, inputs);
        }
    }
}