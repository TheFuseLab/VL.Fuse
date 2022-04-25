using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Int4Join : ShaderNode<Int4>
    {
        private ShaderNode<int> _x;
        private ShaderNode<int> _y;
        private ShaderNode<int> _z;
        private ShaderNode<int> _w;

        public Int4Join(ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z, ShaderNode<int> w) : base("Int4Join")
        {

            _x = x ?? new ConstantValue<int>(0);
            _y = y ?? new ConstantValue<int>(0);
            _z = z ?? new ConstantValue<int>(0);
            _w = w ?? new ConstantValue<int>(1);
            
            Setup( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }


        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("int4 ${resultName} = int4(${x},${y},${z},${w});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID},
                    {"w", _w.ID}
                });
        }
    }
}