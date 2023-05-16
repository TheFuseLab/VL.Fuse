using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    
    public class Matrix2JoinVector2 : ResultNode<Matrix2>
    {
        
        private readonly ShaderNode<Vector2> _x;
        private readonly ShaderNode<Vector2> _y;

        public Matrix2JoinVector2(NodeContext nodeContext, ShaderNode<Vector2> x, ShaderNode<Vector2> y) : base(nodeContext, "Matrix2Join")
        {
            ShaderNode<Vector2> @default = new ConstantValue<Vector2>(new Vector2());
            
            _x = x ?? @default;
            _y = y ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float2x2(${x},${y})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID}
                });
        }
    }
    
    public class Matrix2Join : ResultNode<Matrix2>
    {
        
        private readonly ShaderNode<float> _00;
        private readonly ShaderNode<float> _10;
        private readonly ShaderNode<float> _01;
        private readonly ShaderNode<float> _11;

        public Matrix2Join(NodeContext nodeContext, ShaderNode<float> the00, ShaderNode<float> the10, ShaderNode<float> the01, ShaderNode<float> the11) : base(nodeContext, "Matrix2Join")
        {
            ShaderNode<float> @default = new ConstantValue<float>(0);
            
            _00 = the00 ?? @default;
            _10 = the10 ?? @default;
            _01 = the01 ?? @default;
            _11 = the11 ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_00,_10,_01,_11});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float2x2(${m00},${m10},${m01},${m11})", 
                new Dictionary<string, string>
                {
                    {"m00", _00.ID},
                    {"m10", _10.ID},
                    {"m01", _01.ID},
                    {"m11", _11.ID},
                });
        }
    }
    
    public class Matrix3Join : ResultNode<Matrix3>
    {
        
        private readonly ShaderNode<Vector3> _x;
        private readonly ShaderNode<Vector3> _y;
        private readonly ShaderNode<Vector3> _z;

        public Matrix3Join(NodeContext nodeContext, ShaderNode<Vector3> x, ShaderNode<Vector3> y, ShaderNode<Vector3> z) : base(nodeContext, "Matrix3Join")
        {
            ShaderNode<Vector3> @default = new ConstantValue<Vector3>(new Vector3());
            
            _x = x ?? @default;
            _y = y ?? @default;
            _z = z ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float3x3(${x},${y},${z})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID}
                });
        }
    }
    
    public class Matrix4Join : ResultNode<Matrix>
    {
        
        private readonly ShaderNode<Vector4> _x;
        private readonly ShaderNode<Vector4> _y;
        private readonly ShaderNode<Vector4> _z;
        private readonly ShaderNode<Vector4> _w;

        public Matrix4Join(NodeContext nodeContext, ShaderNode<Vector4> x, ShaderNode<Vector4> y, ShaderNode<Vector4> z, ShaderNode<Vector4> w) : base(nodeContext, "Matrix4Join")
        {
            ShaderNode<Vector4> @default = new ConstantValue<Vector4>(new Vector4());
            
            _x = x ?? @default;
            _y = y ?? @default;
            _z = z ?? @default;
            _w = w ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("float4x4(${x},${y},${z},${w})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID},
                    {"w", _w.ID}
                });
        }
    }
    
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