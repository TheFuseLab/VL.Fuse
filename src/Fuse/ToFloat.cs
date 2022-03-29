using System;
using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class ToFloat4 : ShaderNode<Vector4>
    {

        private readonly AbstractGpuValue _x;

        public ToFloat4(AbstractGpuValue x) : base("ToFloat")
        {
            _x = x ?? new ConstantValue<float>(0);
            
            Setup(new List<AbstractGpuValue>{x});
        }

        protected override string SourceTemplate()
        {
            var shader = TypeHelpers.GetDimension(_x.GetType().GetGenericArguments()[0]) switch
            {
                1 => "float4 ${resultName} = float4(${x},${x},${x},1.0);",
                2 => "float4 ${resultName} = float4(${x}.xy,0.0,1.0);",
                3 => "float4 ${resultName} = float4(${x}.xyz,1.0);",
                4 => "float4 ${resultName} = ${x};",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"x", _x.ID}
                });
        }
    }
}