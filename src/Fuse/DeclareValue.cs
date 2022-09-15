using System.Collections.Generic;
using VL.Core;

namespace Fuse
{
    public class DeclareValue<T> : ShaderNode<T>
    {

        private ShaderNode<T> _value;

        public DeclareValue(NodeContext nodeContext): base(nodeContext, "output")
        {
        }
        
        public DeclareValue(NodeContext nodeContext, ShaderNode<T> theValue = null): this(nodeContext)
        {
            SetInput(theValue);
        }

        public void SetInput(ShaderNode<T> theValue = null)
        {
            _value = theValue ?? Default;
            SetInputs(new List<AbstractShaderNode>{_value});
        }

        protected override string SourceTemplate()
        {
            if(_value == null)return "${resultType} ${resultName};";
            return "${resultType} ${resultName} = " + _value.ID+";";
        }
    }
}