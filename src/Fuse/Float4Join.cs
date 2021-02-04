using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float4Join : ShaderNode<Vector4>
    {
        private GpuValue<float> _x;
        private GpuValue<float> _y;
        private GpuValue<float> _z;
        private GpuValue<float> _w;

        public Float4Join(GpuValue<float> x, GpuValue<float> y, GpuValue<float> z, GpuValue<float> w) : base("Float4Join")
        {

            _x = x ?? new ConstantValue<float>(0);
            _y = y ?? new ConstantValue<float>(0);
            _z = z ?? new ConstantValue<float>(0);
            _w = w ?? new ConstantValue<float>(1);
            
            Setup( new List<AbstractGpuValue>{_x,_y,_z,_w});
        }


        protected override string SourceTemplate()
        {
            return ShaderTemplateEvaluator.Evaluate("float4 ${resultName} = float4(${x},${y},${z},${w});", 
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