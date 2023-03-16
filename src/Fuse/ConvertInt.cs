using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    public class Int2Join : ResultNode<Int2>
    {
        private readonly ShaderNode<int> _x;
        private readonly ShaderNode<int> _y;

        public Int2Join(NodeContext nodeContext, ShaderNode<int> x, ShaderNode<int> y) : base(nodeContext, "int2Join")
        {
            ShaderNode<int> @default = new ConstantValue<int>(0);
            
            _x = x ?? @default;
            _y = y ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y});
        }
        
        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("int2(${x},${y})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID}
                });
        }
    }
    
    public class Int3Join : ResultNode<Int3>
    {
        
        private readonly ShaderNode<int> _x;
        private readonly ShaderNode<int> _y;
        private readonly ShaderNode<int> _z;

        public Int3Join(NodeContext nodeContext, ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z) : base(nodeContext, "Int3Join")
        {
            ShaderNode<int> @default = new ConstantValue<int>(0);
            
            _x = x ?? @default;
            _y = y ?? @default;
            _z = z ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z});
        }

        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("int3(${x},${y},${z})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID}
                });
        }
    }
    
    public class Int4Join : ResultNode<Int4>
    {
        private readonly ShaderNode<int> _x;
        private readonly ShaderNode<int> _y;
        private readonly ShaderNode<int> _z;
        private readonly ShaderNode<int> _w;

        public Int4Join(NodeContext nodeContext, ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z, ShaderNode<int> w) : base(nodeContext, "Int4Join")
        {
            ShaderNode<int> @default = new ConstantValue<int>(0);

            _x = x ?? @default;
            _y = y ?? @default;
            _z = z ?? @default;
            _w = w ?? @default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }

        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("int4(${x},${y},${z},${w})", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID},
                    {"w", _w.ID}
                });
        }
    }
    
    public class IntJoinAdaptive<TIn, TExtension, TOut> : ResultNode<TOut> where TOut : struct
    {

        private readonly ShaderNode<TIn> _input;
        private readonly ShaderNode<TExtension> _extension;
        
        public IntJoinAdaptive(NodeContext nodeContext, ShaderNode<TIn> theInput,ShaderNode<TExtension> theExtension) : base(nodeContext, "join")
        {
            _input = theInput ?? ConstantHelper.FromFloat<TIn>( 0);
            _extension = theExtension ?? ConstantHelper.FromFloat<TExtension>(0);

            SetInputs(new List<AbstractShaderNode>{_input, _extension});
        }

        protected override string ImplementationTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(TOut)) switch
            {
                2 => "int2(${input},${extension})",
                3 => "int3(${input},${extension})",
                4 => "int4(${input},${extension})",
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
    
    public class FromInt<T> : ResultNode<T> where T : struct
    {
        
        private readonly ShaderNode<int> _x;

        public FromInt(NodeContext nodeContext, ShaderNode<int> x) : base(nodeContext, "fromInt")
        {
            ShaderNode<int> @default = new ConstantValue<int>(0);
            
            _x = x ?? @default;
            
            SetInputs(new List<AbstractShaderNode>{_x});
        }

        protected override string ImplementationTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(T)) switch
            {
                1 => "${x}",
                2 => "int2(${x},${x})",
                3 => "int3(${x},${x},${x})",
                4 => "int4(${x},${x},${x},${x})",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, new Dictionary<string, string> {{"x", _x.ID}});
        }
    }
}