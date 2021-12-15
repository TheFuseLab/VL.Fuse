using System;
using System.Collections.Generic;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private readonly GpuValue<T> _value;

        public DeclareValue(GpuValue<T> theValue = null): base("output", null,"outputParam")
        {
            
            var inputList = new List<AbstractGpuValue>();
            theValue ??= ConstantHelper.FromFloat<T>(0);
          
            inputList.Add(theValue);
            
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