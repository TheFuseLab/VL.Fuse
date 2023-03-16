using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    public class Float2Join : ResultNode<Vector2>
    {
        private readonly ShaderNode<float> _x;
        private readonly ShaderNode<float> _y;

        public Float2Join(NodeContext nodeContext, ShaderNode<float> x, ShaderNode<float> y) : base(nodeContext, "float2Join")
        {
            ShaderNode<float> @default = new ConstantValue<float>(0);
            
            _x = x ?? @default;
            _y = y ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float2(${x},${y})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID}
                });
        }
    }
    
    public class Float3Join : ResultNode<Vector3>
    {
        
        private readonly ShaderNode<float> _x;
        private readonly ShaderNode<float> _y;
        private readonly ShaderNode<float> _z;

        public Float3Join(NodeContext nodeContext, ShaderNode<float> x, ShaderNode<float> y, ShaderNode<float> z) : base(nodeContext, "Float3Join")
        {
            ShaderNode<float> @default = new ConstantValue<float>(0);
            
            _x = x ?? @default;
            _y = y ?? @default;
            _z = z ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float3(${x},${y},${z})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID}
                });
        }
    }
    
    public class Float4Join : ResultNode<Vector4>
    {
        private readonly ShaderNode<float> _x;
        private readonly ShaderNode<float> _y;
        private readonly ShaderNode<float> _z;
        private readonly ShaderNode<float> _w;

        public Float4Join(NodeContext nodeContext, ShaderNode<float> x, ShaderNode<float> y, ShaderNode<float> z, ShaderNode<float> w) : base(nodeContext, "Float4Join")
        {
            ShaderNode<float> @default = new ConstantValue<float>(0);

            _x = x ?? @default;
            _y = y ?? @default;
            _z = z ?? @default;
            _w = w ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }

        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float4(${x},${y},${z},${w})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID},
                    {"w", _w.ID}
                });
        }
    }
    
    public class FromFloat<T> : ResultNode<T> 
    {

        private readonly ShaderNode<float> _x;

        public FromFloat(NodeContext nodeContext, ShaderNode<float> x) : base(nodeContext, "fromFloat")
        {
            ShaderNode<float> @default = new ConstantValue<float>(0);
            
            _x = x ?? @default;
            
            SetInputs(new List<AbstractShaderNode>{x});
        }

        protected override string ImplementationTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(T)) switch
            {
                1 => "${x}",
                2 => "float2(${x},${x})",
                3 => "float3(${x},${x},${x})",
                4 => "float4(${x},${x},${x},${x})",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"x", _x.ID}
                });
        }
    }
    
    public class FloatJoinAdaptive<TIn, TExtension, TOut> : ResultNode<TOut> where TOut : struct
    {

        private readonly ShaderNode<TIn> _input;
        private readonly ShaderNode<TExtension> _extension;
        
        public FloatJoinAdaptive(NodeContext nodeContext, ShaderNode<TIn> theInput,ShaderNode<TExtension> theExtension) : base(nodeContext, "join")
        {
            _input = theInput ?? ConstantHelper.FromFloat<TIn>( 0);
            _extension = theExtension ?? ConstantHelper.FromFloat<TExtension>(0);

            SetInputs(new List<AbstractShaderNode>{_input, _extension});
        }

        protected override string ImplementationTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(TOut)) switch
            {
                2 => "float2(${input},${extension})",
                3 => "float3(${input},${extension})",
                4 => "float4(${input},${extension})",
                _ => ""
            };
           

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"input", _input.ID},
                    {"extension", _extension.ID}
                });
        }
    }
    
    public class ToFloat4 : ResultNode<Vector4>
    {

        private readonly AbstractShaderNode _x;

        public ToFloat4(NodeContext nodeContext, AbstractShaderNode x) : base(nodeContext, "ToFloat")
        {
            ShaderNode<float> @default = new ConstantValue<float>(0);
            
            _x = x ?? @default;
            
            SetInputs(new List<AbstractShaderNode>{x});
        }

        protected override string ImplementationTemplate()
        {
            var shader = _x.Dimension() switch
            {
                1 => "float4(${x},${x},${x},1.0)",
                2 => "float4(${x}.xy,0.0,1.0)",
                3 => "float4(${x}.xyz,1.0)",
                4 => "${x}",
                _ => "float4(1.0,1.0,0.0,1.0)",
            };

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"x", _x.ID}
                });
        }
    }
}