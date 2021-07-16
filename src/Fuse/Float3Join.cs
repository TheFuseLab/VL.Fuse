using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float3Join : ShaderNode<Vector3>
    {
        
        private readonly GpuValue<float> _x;
        private readonly GpuValue<float> _y;
        private readonly GpuValue<float> _z;

        public Float3Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z) : base("Float3Join")
        {
            
            _x = x ?? new ConstantValue<float>(0);
            _y = y ?? new ConstantValue<float>(0);
            _z = z ?? new ConstantValue<float>(0);
            
            Setup( new List<AbstractGpuValue>{_x,_y,_z});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("float3 ${resultName} = float3(${x},${y},${z});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID}
                });
        }
    }
}