using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Int3Join : ShaderNode<Int3>
    {
        
        private ShaderNode<int> _x;
        private ShaderNode<int> _y;
        private ShaderNode<int> _z;

        public Int3Join(ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z) : base("Int3Join")
        {
            
            _x = x ?? new ConstantValue<int>(0);
            _y = y ?? new ConstantValue<int>(0);
            _z = z ?? new ConstantValue<int>(0);
            
            Setup( new List<AbstractShaderNode>{_x,_y,_z});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("int3 ${resultName} = int3(${x},${y},${z});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID}
                });
        }
    }
}