using System.Collections.Generic;
using Fuse;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private GpuValue<T> _value;

        public DeclareValue(GpuValue<T> theValue): base("output", null,"outputParam")
        {
            Output = new GpuValue<T>("outputParam")
            {
                ParentNode = this
            };
            Ins = new List<AbstractGpuValue>();
            _value = theValue;
        }

        protected override string SourceTemplate()
        {
            if(_value == null)return "${resultType} ${resultName};";
            return "${resultType} ${resultName} = " + _value.ID+";";
        }
    }
}