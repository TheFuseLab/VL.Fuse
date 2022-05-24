using System.Collections.Generic;
using Stride.Core.Mathematics;

namespace Fuse
{
    public class Int2Join : ShaderNode<Int2>
    {
        private readonly ShaderNode<int> _x;
        private readonly ShaderNode<int> _y;

        public Int2Join(ShaderNode<int> x, ShaderNode<int> y) : base("int2Join")
        {
            _x = x ?? new ConstantValue<int>(0);
            _y = y ?? new ConstantValue<int>(0);
            
            Setup( new List<AbstractShaderNode>{_x,_y});
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
        
        private readonly ShaderNode<int> _x;
        private readonly ShaderNode<int> _y;
        private readonly ShaderNode<int> _z;

        public Int3Join(ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z) : base("Int3Join")
        {
            
            _x = x ?? new ConstantValue<int>(0);
            _y = y ?? new ConstantValue<int>(0);
            _z = z ?? new ConstantValue<int>(0);
            
            Setup( new List<AbstractShaderNode>{_x,_y,_z});
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
        private readonly ShaderNode<int> _x;
        private readonly ShaderNode<int> _y;
        private readonly ShaderNode<int> _z;
        private readonly ShaderNode<int> _w;

        public Int4Join(ShaderNode<int> x, ShaderNode<int> y, ShaderNode<int> z, ShaderNode<int> w) : base("Int4Join")
        {

            _x = x ?? new ConstantValue<int>(0);
            _y = y ?? new ConstantValue<int>(0);
            _z = z ?? new ConstantValue<int>(0);
            _w = w ?? new ConstantValue<int>(1);
            
            Setup( new List<AbstractShaderNode>{_x,_y,_z,_w});
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
        
        private readonly ShaderNode<int> _x;

        public FromInt(ShaderNode<int> x) : base("fromInt")
        {
            _x = x ?? new ConstantValue<int>(0);
            
            Setup(new List<AbstractShaderNode>{x});
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