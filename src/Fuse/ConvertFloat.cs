using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Float2Join : ShaderNode<Vector2>
    {
        private readonly ShaderNode<float> _x;
        private readonly ShaderNode<float> _y;

        public Float2Join(ShaderNode<float> x, ShaderNode<float> y) : base("float2Join")
        {
            _x = x ?? new ConstantValue<float>(0);
            _y = y ?? new ConstantValue<float>(0);
            
            SetInputs( new List<AbstractShaderNode>{_x,_y});
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
    
    public class Float3Join : ShaderNode<Vector3>
    {
        
        private readonly ShaderNode<float> _x;
        private readonly ShaderNode<float> _y;
        private readonly ShaderNode<float> _z;

        public Float3Join(ShaderNode<float> x, ShaderNode<float> y, ShaderNode<float> z) : base("Float3Join")
        {
            
            _x = x ?? new ConstantValue<float>(0);
            _y = y ?? new ConstantValue<float>(0);
            _z = z ?? new ConstantValue<float>(0);
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z});
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
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z,_w});
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
    
    public class FromFloat<T> : ShaderNode<T> where T : struct
    {

        private readonly ShaderNode<float> _x;
        
        public FromFloat(ShaderNode<float> x) : base("fromFloat")
        {
            _x = x ?? new ConstantValue<float>(0);
            
            SetInputs(new List<AbstractShaderNode>{x});
        }

        protected override string SourceTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(T)) switch
            {
                1 => "float ${resultName} = ${x};",
                2 => "float2 ${resultName} = float2(${x},${x});",
                3 => "float3 ${resultName} = float3(${x},${x},${x});",
                4 => "float4 ${resultName} = float4(${x},${x},${x},${x});",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"x", _x.ID}
                });
        }
    }
    
    public class ToFloat4 : ShaderNode<Vector4>
    {

        private readonly AbstractShaderNode _x;

        public ToFloat4(AbstractShaderNode x) : base("ToFloat")
        {
            _x = x ?? new ConstantValue<float>(0);
            
            SetInputs(new List<AbstractShaderNode>{x});
        }

        protected override string SourceTemplate()
        {
            var shader = _x.Dimension() switch
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