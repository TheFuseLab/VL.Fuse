using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float2Join : ShaderNode<Vector2>
    {
        private ShaderNode<float> _x;
        private ShaderNode<float> _y;

        public Float2Join(ShaderNode<float> x, ShaderNode<float> y) : base("float2Join")
        {
            _x = x ?? new ConstantValue<float>(0);
            _y = y ?? new ConstantValue<float>(0);
            
            Setup( new List<AbstractShaderNode>{_x,_y});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("float2 ${resultName} = float2(${x},${y});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID}
                });
        }
    }
}