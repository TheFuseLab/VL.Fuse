using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float4Join : ShaderNode<Vector4>
    {
        private readonly ShaderNode<float> _x;
        private readonly ShaderNode<float> _y;
        private readonly ShaderNode<float> _z;
        private readonly ShaderNode<float> _w;

        public Float4Join(ShaderNode<float> x, ShaderNode<float> y, ShaderNode<float> z, ShaderNode<float> w) : base("Float4Join")
        {

            _x = x ?? new ConstantValue<float>(0);
            _y = y ?? new ConstantValue<float>(0);
            _z = z ?? new ConstantValue<float>(0);
            _w = w ?? new ConstantValue<float>(1);
            
            Setup( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }


        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("float4 ${resultName} = float4(${x},${y},${z},${w});", 
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