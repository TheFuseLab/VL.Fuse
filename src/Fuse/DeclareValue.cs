﻿using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private ShaderNode<T> _value;
        
        public DeclareValue(NodeContext nodeContext, ShaderNode<T> theValue = null): base(nodeContext, "output")
        {
            _value = theValue ?? Default;
            SetInputs(new List<AbstractShaderNode>{_value});
        }
        
        public void SetInputAbstract(AbstractShaderNode theValue)
        {
            _value = theValue as ShaderNode<T>;
            SetInputs(new List<AbstractShaderNode>{theValue});
        }

        protected override string SourceTemplate()
        {
            if(_value == null)return "${resultType} ${resultName};";
            return "${resultType} ${resultName} = " + _value.ID+";";
        }
    }
}