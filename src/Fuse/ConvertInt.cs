using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    public class Int2Join : ShaderNode<Int2>
    {
        private ShaderNode<int> _x;
        private ShaderNode<int> _y;
        
        private readonly ShaderNode<int> _default;

        public Int2Join(NodeContext nodeContext) : base(nodeContext, "int2Join")
        {
            _default = new ConstantValue<int>(new NodeSubContextFactory(nodeContext).NextSubContext(),0);
            
            _x = _default;
            _y = _default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y});
        }

        public void SetInputs(ShaderNode<int> x, ShaderNode<int> y)
        {
            _x = x ?? _default;
            _y = y ?? _default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("int2 ${resultName} = int2(${x},${y});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID}
                });
        }
    }
    
    public class Int3Join : ShaderNode<Int3>
    {
        
        private ShaderNode<int> _x;
        private ShaderNode<int> _y;
        private ShaderNode<int> _z;
        
        private readonly ShaderNode<int> _default;

        public Int3Join(NodeContext nodeContext) : base(nodeContext, "Int3Join")
        {
            _default = new ConstantValue<int>(new NodeSubContextFactory(nodeContext).NextSubContext(),0);
            
            _x = _default;
            _y = _default;
            _z = _default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z});
        }

        public void SetInputs(ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z)
        {
            _x = x ?? _default;
            _y = y ?? _default;
            _z = z ?? _default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z});
        }
        
        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("int3 ${resultName} = int3(${x},${y},${z});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID}
                });
        }
    }
    
    public class Int4Join : ShaderNode<Int4>
    {
        private ShaderNode<int> _x;
        private ShaderNode<int> _y;
        private ShaderNode<int> _z;
        private ShaderNode<int> _w;
        
        private readonly ShaderNode<int> _default;

        public Int4Join(NodeContext nodeContext) : base(nodeContext, "Int4Join")
        {
            _default = new ConstantValue<int>(new NodeSubContextFactory(nodeContext).NextSubContext(),0);

            _x = _default;
            _y = _default;
            _z = _default;
            _w = _default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }

        public void SetInputs(ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z, ShaderNode<int> w)
        {
            _x = x ?? _default;
            _y = y ?? _default;
            _z = z ?? _default;
            _w = w ?? _default;
            
            SetInputs( new List<AbstractShaderNode>{_x,_y,_z,_w});
        }


        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("int4 ${resultName} = int4(${x},${y},${z},${w});", 
                new Dictionary<string, string>
                {
                    {"x", _x.ID},
                    {"y", _y.ID},
                    {"z", _z.ID},
                    {"w", _w.ID}
                });
        }
    }
    
    public class FromInt<T> : ShaderNode<T> where T : struct
    {
        
        private ShaderNode<int> _x;
        
        private readonly ShaderNode<int> _default;

        public FromInt(NodeContext nodeContext) : base(nodeContext, "fromInt")
        {
            _default = new ConstantValue<int>(new NodeSubContextFactory(nodeContext).NextSubContext(),0);
            
            _x = _default;
            
            SetInputs(new List<AbstractShaderNode>{_x});
        }

        public void SetInput(ShaderNode<int> x)
        {
            _x = x ?? _default;
            
            SetInputs(new List<AbstractShaderNode>{x});
        }

        protected override string SourceTemplate()
        {
            var shader = TypeHelpers.GetDimension(typeof(T)) switch
            {
                1 => "int ${resultName} = ${x};",
                2 => "int2 ${resultName} = int2(${x},${x});",
                3 => "int3 ${resultName} = int3(${x},${x},${x});",
                4 => "int4 ${resultName} = int4(${x},${x},${x},${x});",
                _ => ""
            };

            return ShaderNodesUtil.Evaluate(shader, new Dictionary<string, string> {{"x", _x.ID}});
        }
    }
}