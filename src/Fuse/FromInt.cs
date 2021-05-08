using System.Collections.Generic;

namespace Fuse
{
    public class FromInt<T> : ShaderNode<T> where T : struct
    {

        public FromInt(GpuValue<int> x) : base("fromInt")
        {
            x ??= new ConstantValue<int>(0);
            
            Setup(
                new List<AbstractGpuValue>{x},
                new Dictionary<string, string>
                {
                    {"x", x.ID},
                });
        }

        protected override string SourceTemplate()
        {
            switch (TypeHelpers.GetDimension(typeof(T)))
            {
                case 1:
                    return "int ${resultName} = ${x};";
                case 2:
                    return "int2 ${resultName} = int2(${x},${x});";
                case 3:
                    return "int3 ${resultName} = int3(${x},${x},${x});";
                case 4:
                    return "int4 ${resultName} = int4(${x},${x},${x},${x});";
            }

            return "";
        }
    }
}