using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class AddNode<T> :OperatorNode<T>
    {
        public AddNode(IEnumerable<GPUValue<T>> inputs) : base(inputs, "+")
        {
        }
    }
}