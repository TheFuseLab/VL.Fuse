using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Stride.Shaders.ShaderFX;

namespace VL.ShaderFXtension
{
    public class IntrinsicFunctionNode<T> : CustomFunctionNode<T>
    {
        public IntrinsicFunctionNode(IEnumerable<AbstractGpuValue> inputs, string theFunction) : base(inputs, theFunction, new List<string>())
        {
            
        }
    }
}