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
            
            Setup(
                new List<AbstractGpuValue>{x},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                });
        }

        protected override string SourceTemplate()
        {
            switch (TypeHelpers.GetDimension(_x.GetType().GetGenericArguments()[0]))
            {
                case 1:
                    return "float4 ${resultName} = float4(${x},${x},${x},1.0);";
                case 2:
                    return "float4 ${resultName} = float4(${x}.xy,0.0,1.0);";
                case 3:
                    return "float4 ${resultName} = float4(${x}.xyz,1.0);";
                case 4:
                    return "float4 ${resultName} = ${x};";
            }

            return "";
        }
    }
}