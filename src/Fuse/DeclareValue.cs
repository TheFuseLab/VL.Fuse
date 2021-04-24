using System.Collections.Generic;
using Fuse;
using VL.Lib.Adaptive;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private readonly GpuValue<T> _value;

        public DeclareValue(GpuValue<T> theValue): base("output", null,"outputParam")
        {
            
            var inputList = new List<AbstractGpuValue>();
            if (theValue == null) theValue = ConstantHelper.FromFloat<T>(0);
            if (theValue != null)
            {
                inputList.Add(theValue);
            }
            Ins = inputList;
            Setup(Ins);
            Output = new GpuValue<T>("outputParam")
            {
                ParentNode = this
            };
            _value = theValue;
        }

        protected override string SourceTemplate()
        {
            if(_value == null)return "${resultType} ${resultName};";
            return "${resultType} ${resultName} = " + _value.ID+";";
        }
    }
}