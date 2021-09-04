using System;
using System.Collections.Generic;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private readonly GpuValue<T> _value;

        public DeclareValue(GpuValue<T> theValue): base("output", null,"outputParam")
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
        
        public static AbstractGpuValue AbstractDeclareValue(AbstractGpuValue theMember)
        {
            var getDeclareBaseType = typeof(DeclareValue<>);
            var dataType = new[] { theMember.GetType().GetGenericArguments()[0]};
            var getDeclareType = getDeclareBaseType.MakeGenericType(dataType);
            var getDeclareInstance = Activator.CreateInstance(getDeclareType,  new object[]{null} ) as AbstractShaderNode;
            return getDeclareInstance?.AbstractOutput();
        }
    }
}