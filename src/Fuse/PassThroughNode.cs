using System.Collections.Generic;

namespace Fuse
{
    public class PassThroughNode<T> : ShaderNode<T>
    {
       
        private ShaderNode<T> _value;
        
        public PassThroughNode(ShaderNode<T> theValue) : base(theValue == null ? "": theValue.Name)
        {
            _value = theValue ?? new ShaderNode<T>("");
            SetInputs(new List<AbstractShaderNode>{_value});
        }

        public void SetInput(ShaderNode<T> theValue)
        {
            
            _value = theValue ?? new ShaderNode<T>("");
        }
        public override string ID => _value.ID;
    }
}