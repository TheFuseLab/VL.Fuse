using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Int2Join : ShaderNode<Int2>
    {
        private ShaderNode<int> _x;
        private ShaderNode<int> _y;

        public Int2Join(ShaderNode<int> x, ShaderNode<int> y) : base("int2Join")
        {
            _x = x ?? new ConstantValue<int>(0);
            _y = y ?? new ConstantValue<int>(0);
            
            Setup( new List<AbstractShaderNode>{_x,_y});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("int2 ${resultName} = int2(${x},${y});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID}
                });
        }
    }
}