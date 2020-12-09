using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class AddNode<T> :OperatorNode<T>
    {
        public AddNode(IEnumerable<GpuValue<T>> inputs) : base(inputs, "+")
        {
        }
    }
}