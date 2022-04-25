using System;
using System.Collections.Generic;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private readonly ShaderNode<T> _value;

        public DeclareValue(ShaderNode<T> theValue = null): base("output", null)
        {
            
            var inputList = new List<AbstractShaderNode>();
            theValue ??= ConstantHelper.FromFloat<T>(0);
          
            inputList.Add(theValue);
            
            Ins = inputList;
            Setup(Ins);
            _value = theValue;
        }

        protected override string SourceTemplate()
        {
            if(_value == null)return "${resultType} ${resultName};";
            return "${resultType} ${resultName} = " + _value.ID+";";
        }
    }
}