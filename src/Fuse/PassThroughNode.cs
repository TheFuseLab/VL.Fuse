using System.Collections.Generic;

namespace Fuse
{
    public class PassThroughNode<T> : ShaderNode<T>
    {
        private ShaderNode<T> _input;
        public ShaderNode<T> Input { get => _input;
            set
            {
                _input = value ?? new ShaderNode<T>("");
                SetInputs(new List<AbstractShaderNode>{Input});
            }
        }
        
        public PassThroughNode(string theName, ShaderNode<T> theValue = null) : base(theName)
        {
            Input = theValue ?? new ShaderNode<T>("");
        }

        public void SetInput(AbstractShaderNode theValue)
        {
            Input = theValue as ShaderNode<T>;
        }
        public override string ID => _input.ID;
    }
}